using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Managers;
using Content.Shared._Dystopia.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Банк Города: счета жителей, зарплаты, налоги, долги, переводы, банковский реестр.
/// Деньги лежат на счёте в реестре Города, ID-карта — ключ к счёту.
/// Кто держит карту (активная карта: в руке или в слоте ID), тот и распоряжается счётом.
/// Сообщения о деньгах приходят уведомлением на КПК, в котором лежит карта счёта.
/// </summary>
public sealed partial class CityBankSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<CityBankCardComponent, ExaminedEvent>(OnCardExamined);
        SubscribeLocalEvent<PdaComponent, ExaminedEvent>(OnPdaExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CityBankComponent>();
        while (query.MoveNext(out var uid, out var bank))
        {
            var interval = TimeSpan.FromMinutes(bank.PayIntervalMinutes);

            if (bank.NextPayday == TimeSpan.Zero)
            {
                bank.NextPayday = _timing.CurTime + interval;
                continue;
            }

            if (_timing.CurTime < bank.NextPayday)
                continue;

            bank.NextPayday = _timing.CurTime + interval;
            Payday((uid, bank));
        }
    }

    #region Счета

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!TryComp<CityBankComponent>(ev.Station, out var bank))
            return;

        ProtoId<JobPrototype>? job = null;
        if (ev.JobId != null)
            job = new ProtoId<JobPrototype>(ev.JobId);

        var account = OpenAccount((ev.Station, bank), Name(ev.Mob), job, ev.Mob);

        if (_idCard.TryFindIdCard(ev.Mob, out var card))
        {
            var bankCard = EnsureComp<CityBankCardComponent>(card.Owner);
            bankCard.AccountId = account.Id;
            bankCard.Bank = ev.Station;
        }

        _chat.DispatchServerMessage(ev.Player,
            Loc.GetString("dystopia-bank-account-opened", ("id", account.Id), ("balance", account.Balance)));
    }

    /// <summary>Открывает новый счёт в реестре. Стартовый баланс — несколько зарплат профессии.</summary>
    public CityBankAccount OpenAccount(Entity<CityBankComponent> bank, string name, ProtoId<JobPrototype>? job, EntityUid? owner)
    {
        int id;
        do
        {
            id = _random.Next(100000, 1000000);
        } while (bank.Comp.Accounts.ContainsKey(id));

        var salary = job != null ? bank.Comp.Salaries.GetValueOrDefault(job.Value) : 0;

        var account = new CityBankAccount
        {
            Id = id,
            Name = name,
            Job = job,
            Owner = owner,
            Balance = Math.Max(0, salary * bank.Comp.StartingSalaries),
        };

        bank.Comp.Accounts[id] = account;
        AddHistory(bank, account, Loc.GetString("dystopia-bank-history-opened", ("amount", account.Balance)));
        AddLedger(bank, Loc.GetString("dystopia-bank-ledger-opened",
            ("id", id), ("name", name), ("amount", account.Balance)), null, id);
        return account;
    }

    /// <summary>Первый Банк Города на сервере (у нас одна станция).</summary>
    public bool TryGetBank(out Entity<CityBankComponent> bank)
    {
        var query = EntityQueryEnumerator<CityBankComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            bank = (uid, comp);
            return true;
        }

        bank = default;
        return false;
    }

    /// <summary>Счёт, к которому привязана карта (или КПК, в котором лежит карта).</summary>
    public bool TryGetAccount(EntityUid card, out Entity<CityBankComponent> bank, [NotNullWhen(true)] out CityBankAccount? account)
    {
        bank = default;
        account = null;

        // КПК — берём карту, которая в нём лежит.
        if (TryComp<PdaComponent>(card, out var pda) && pda.ContainedId is { } contained)
            card = contained;

        if (!TryComp<CityBankCardComponent>(card, out var bankCard) ||
            bankCard.AccountId is not { } id ||
            bankCard.Bank is not { } bankUid ||
            !TryComp<CityBankComponent>(bankUid, out var bankComp) ||
            !bankComp.Accounts.TryGetValue(id, out account))
        {
            return false;
        }

        bank = (bankUid, bankComp);
        return true;
    }

    /// <summary>
    /// Счёт активной карты существа: сначала карта в руке, потом в слоте ID (в том числе в КПК).
    /// Кто держит карту — тот и распоряжается счётом.
    /// </summary>
    public bool TryGetActiveAccount(EntityUid user, out Entity<CityBankComponent> bank, [NotNullWhen(true)] out CityBankAccount? account)
    {
        bank = default;
        account = null;

        return _idCard.TryFindIdCard(user, out var card) && TryGetAccount(card.Owner, out bank, out account);
    }

    /// <summary>Зачисляет (или списывает, если сумма отрицательная) деньги на счёт без налога. Для админ-команд.</summary>
    public void Deposit(CityBankAccount account, int amount)
    {
        account.Balance = Math.Max(0, account.Balance + amount);
    }

    /// <summary>Списывает деньги, если их хватает и счёт не заморожен.</summary>
    public bool TryWithdraw(CityBankAccount account, int amount)
    {
        if (amount < 0 || account.Frozen || account.Balance < amount)
            return false;

        account.Balance -= amount;
        return true;
    }

    /// <summary>Налог профессии в процентах (0–100).</summary>
    public int GetTaxRate(Entity<CityBankComponent> bank, CityBankAccount account)
    {
        if (account.Job is not { } job)
            return 0;

        return Math.Clamp(bank.Comp.TaxRates.GetValueOrDefault(job), 0, 100);
    }

    /// <summary>
    /// Доход на счёт с удержанием налога и погашением долга. Налог и погашение долга остаются в казне
    /// (если деньги пришли из казны) или поступают в неё (если деньги пришли извне — от покупателя, по векселю).
    /// Возвращает (зачислено на счёт, налог, погашено долга).
    /// </summary>
    public (int Net, int Tax, int DebtPaid) ApplyIncome(Entity<CityBankComponent> bank, CityBankAccount account, int gross)
    {
        if (gross <= 0)
            return (0, 0, 0);

        var tax = gross * GetTaxRate(bank, account) / 100;
        var afterTax = gross - tax;
        var debtPaid = Math.Min(account.Debt, afterTax);
        var net = afterTax - debtPaid;

        account.Debt -= debtPaid;
        account.Balance += net;
        return (net, tax, debtPaid);
    }

    #endregion

    #region Переводы и штрафы

    public enum TransferResult : byte
    {
        Success,
        Frozen,
        RecipientFrozen,
        NotEnoughMoney,
        NoRecipient,
        SameAccount,
        BadAmount,
    }

    /// <summary>Перевод между счетами. Налогом не облагается. С замороженного и на замороженный счёт переводить нельзя.</summary>
    public TransferResult Transfer(Entity<CityBankComponent> bank, CityBankAccount from, int toId, int amount, string comment)
    {
        if (amount <= 0)
            return TransferResult.BadAmount;

        if (!bank.Comp.Accounts.TryGetValue(toId, out var to))
            return TransferResult.NoRecipient;

        if (to.Id == from.Id)
            return TransferResult.SameAccount;

        if (from.Frozen)
            return TransferResult.Frozen;

        if (to.Frozen)
            return TransferResult.RecipientFrozen;

        if (from.Balance < amount)
            return TransferResult.NotEnoughMoney;

        from.Balance -= amount;
        to.Balance += amount;

        AddHistory(bank, from, Loc.GetString("dystopia-bank-history-transfer-out",
            ("amount", amount), ("id", to.Id), ("name", to.Name), ("comment", comment)));
        AddHistory(bank, to, Loc.GetString("dystopia-bank-history-transfer-in",
            ("amount", amount), ("id", from.Id), ("name", from.Name), ("comment", comment)));
        AddLedger(bank, Loc.GetString("dystopia-bank-ledger-transfer",
            ("from", from.Id), ("to", to.Id), ("amount", amount), ("comment", comment)), from.Id, to.Id);

        NotifyAccount(to, Loc.GetString("dystopia-bank-transfer-received",
            ("amount", amount), ("id", from.Id), ("name", from.Name), ("comment", comment)));

        return TransferResult.Success;
    }

    /// <summary>
    /// Оплата покупки: с плательщика уходит полная сумма, продавцу — доход за вычетом налога его профессии
    /// (и погашения долга); налог и погашение идут в казну. Замороженные счета не участвуют.
    /// </summary>
    public TransferResult PayMerchant(Entity<CityBankComponent> bank, CityBankAccount payer, CityBankAccount merchant, int amount, string description)
    {
        if (amount <= 0)
            return TransferResult.BadAmount;

        if (payer.Id == merchant.Id)
            return TransferResult.SameAccount;

        if (payer.Frozen)
            return TransferResult.Frozen;

        if (merchant.Frozen)
            return TransferResult.RecipientFrozen;

        if (payer.Balance < amount)
            return TransferResult.NotEnoughMoney;

        payer.Balance -= amount;
        var (net, tax, debtPaid) = ApplyIncome(bank, merchant, amount);
        bank.Comp.Treasury += tax + debtPaid;

        AddHistory(bank, payer, Loc.GetString("dystopia-bank-history-purchase",
            ("amount", amount), ("id", merchant.Id), ("name", merchant.Name), ("description", description)));
        AddHistory(bank, merchant, Loc.GetString("dystopia-bank-history-sale",
            ("net", net), ("tax", tax), ("id", payer.Id), ("description", description)));
        AddLedger(bank, Loc.GetString("dystopia-bank-ledger-purchase",
            ("from", payer.Id), ("to", merchant.Id), ("amount", amount), ("tax", tax), ("description", description)),
            payer.Id, merchant.Id);

        NotifyAccount(payer, Loc.GetString("dystopia-bank-purchase-notify",
            ("amount", amount), ("name", merchant.Name), ("description", description)));
        NotifyAccount(merchant, Loc.GetString("dystopia-bank-sale-notify",
            ("net", net), ("tax", tax), ("description", description)));

        return TransferResult.Success;
    }

    /// <summary>
    /// Штраф в казну. Списывается всё, что есть на счёте (даже замороженном), остаток становится долгом.
    /// Возвращает (списано, добавлено в долг).
    /// </summary>
    public (int Taken, int ToDebt) Fine(Entity<CityBankComponent> bank, CityBankAccount account, int amount, string reason)
    {
        if (amount <= 0)
            return (0, 0);

        var taken = Math.Min(amount, account.Balance);
        var toDebt = amount - taken;

        account.Balance -= taken;
        account.Debt += toDebt;
        bank.Comp.Treasury += taken;

        AddHistory(bank, account, Loc.GetString("dystopia-bank-history-fine",
            ("amount", amount), ("debt", toDebt), ("reason", reason)));
        AddLedger(bank, Loc.GetString("dystopia-bank-ledger-fine",
            ("id", account.Id), ("name", account.Name), ("amount", amount), ("debt", toDebt), ("reason", reason)),
            account.Id, null);
        NotifyAccount(account, Loc.GetString("dystopia-bank-fined", ("amount", amount), ("debt", toDebt), ("reason", reason)));

        return (taken, toDebt);
    }

    #endregion

    #region Зарплаты

    /// <summary>
    /// Выплата зарплат всем живым владельцам незамороженных счетов.
    /// Налог и погашение долга удерживаются сразу: из казны уходит только то, что зачислено на счёт.
    /// Если в казне не хватает денег — зарплата не выплачивается.
    /// </summary>
    public (int Paid, int Unpaid) Payday(Entity<CityBankComponent> bank)
    {
        var paid = 0;
        var unpaid = 0;

        foreach (var account in bank.Comp.Accounts.Values)
        {
            if (account.Frozen || account.Job is not { } job)
                continue;

            var salary = bank.Comp.Salaries.GetValueOrDefault(job);
            if (salary <= 0)
                continue;

            if (account.Owner is not { } owner || TerminatingOrDeleted(owner) || _mobState.IsDead(owner))
                continue;

            // Сколько реально уйдёт из казны: зарплата минус налог (налог и долг остаются в казне).
            var tax = salary * GetTaxRate(bank, account) / 100;
            var afterTax = salary - tax;
            var debtPaid = Math.Min(account.Debt, afterTax);
            var net = afterTax - debtPaid;

            if (bank.Comp.Treasury < net)
            {
                unpaid++;
                NotifyAccount(account, Loc.GetString("dystopia-bank-salary-unpaid"));
                continue;
            }

            bank.Comp.Treasury -= net;
            account.Debt -= debtPaid;
            account.Balance += net;
            paid++;

            AddHistory(bank, account, Loc.GetString("dystopia-bank-history-salary",
                ("net", net), ("tax", tax), ("debt", debtPaid)));
            AddLedger(bank, Loc.GetString("dystopia-bank-ledger-salary",
                ("id", account.Id), ("name", account.Name), ("net", net), ("tax", tax), ("debt", debtPaid)),
                null, account.Id);

            NotifyAccount(account, Loc.GetString("dystopia-bank-salary-paid",
                ("net", net), ("tax", tax), ("balance", account.Balance)));
            if (debtPaid > 0)
                NotifyAccount(account, Loc.GetString("dystopia-bank-debt-withheld", ("amount", debtPaid), ("debt", account.Debt)));
        }

        AddLedger(bank, Loc.GetString("dystopia-bank-log-payday",
            ("paid", paid), ("unpaid", unpaid), ("treasury", bank.Comp.Treasury)), null, null);
        return (paid, unpaid);
    }

    /// <summary>Личное сообщение в чат (только для открытия счёта и админских нужд).</summary>
    public void Notify(EntityUid owner, string message)
    {
        if (TryComp<ActorComponent>(owner, out var actor))
            _chat.DispatchServerMessage(actor.PlayerSession, message);
    }

    /// <summary>
    /// Уведомление на КПК, в котором лежит карта этого счёта (звонок КПК + сообщение тому, кто его носит).
    /// Если карта не в КПК — уведомления нет: деньги видны в программе «Банк».
    /// </summary>
    public void NotifyAccount(CityBankAccount account, string message)
    {
        var header = Loc.GetString("dystopia-bank-notification-header");
        var query = EntityQueryEnumerator<CityBankCardComponent>();
        while (query.MoveNext(out var card, out var bankCard))
        {
            if (bankCard.AccountId != account.Id)
                continue;

            if (!_container.TryGetContainingContainer((card, null, null), out var container) ||
                !HasComp<CartridgeLoaderComponent>(container.Owner))
            {
                continue;
            }

            _cartridgeLoader.SendNotification(container.Owner, header, message);
        }
    }

    #endregion

    #region Премии и изъятия Консула

    /// <summary>
    /// Премия из казны. С премии удерживается налог профессии (и долг), из казны уходит только зачисленное.
    /// </summary>
    public bool TryPayBonus(Entity<CityBankComponent> bank, CityBankAccount account, int amount, string reason, out int net, out int tax)
    {
        tax = amount * GetTaxRate(bank, account) / 100;
        var afterTax = amount - tax;
        var debtPaid = Math.Min(account.Debt, Math.Max(0, afterTax));
        net = afterTax - debtPaid;

        if (amount <= 0 || bank.Comp.Treasury < net)
            return false;

        bank.Comp.Treasury -= net;
        account.Debt -= debtPaid;
        account.Balance += net;

        AddHistory(bank, account, Loc.GetString("dystopia-bank-history-bonus", ("net", net), ("tax", tax), ("reason", reason)));
        AddLedger(bank, Loc.GetString("dystopia-bank-ledger-bonus",
            ("id", account.Id), ("name", account.Name), ("net", net), ("tax", tax), ("reason", reason)), null, account.Id);

        NotifyAccount(account, Loc.GetString("dystopia-bank-bonus-received", ("net", net), ("tax", tax), ("reason", reason)));

        return true;
    }

    /// <summary>Изъятие со счёта в казну. Изымается не больше, чем есть на счету. Возвращает изъятую сумму.</summary>
    public int Seize(Entity<CityBankComponent> bank, CityBankAccount account, int amount, string reason)
    {
        var taken = Math.Clamp(amount, 0, account.Balance);
        if (taken <= 0)
            return 0;

        account.Balance -= taken;
        bank.Comp.Treasury += taken;

        AddHistory(bank, account, Loc.GetString("dystopia-bank-history-seized", ("amount", taken), ("reason", reason)));
        AddLedger(bank, Loc.GetString("dystopia-bank-ledger-seized",
            ("id", account.Id), ("name", account.Name), ("amount", taken), ("reason", reason)), account.Id, null);

        NotifyAccount(account, Loc.GetString("dystopia-bank-seized", ("amount", taken), ("reason", reason)));

        return taken;
    }

    #endregion

    #region Автоматы

    /// <summary>Покупка в городском автомате: деньги с активной карты покупателя — в казну.</summary>
    public TransferResult BuyFromCity(Entity<CityBankComponent> bank, CityBankAccount buyer, int price, string item)
    {
        if (price < 0)
            return TransferResult.BadAmount;

        if (buyer.Frozen)
            return TransferResult.Frozen;

        if (buyer.Balance < price)
            return TransferResult.NotEnoughMoney;

        buyer.Balance -= price;
        bank.Comp.Treasury += price;

        AddHistory(bank, buyer, Loc.GetString("dystopia-bank-history-vendor", ("amount", price), ("item", item)));
        AddLedger(bank, Loc.GetString("dystopia-bank-ledger-vendor",
            ("id", buyer.Id), ("name", buyer.Name), ("amount", price), ("item", item)), buyer.Id, null);
        return TransferResult.Success;
    }

    #endregion

    #region Фонды

    public CityFund? GetFund(Entity<CityBankComponent> bank, string fundId)
    {
        foreach (var fund in bank.Comp.Funds)
        {
            if (fund.Id == fundId)
                return fund;
        }

        return null;
    }

    /// <summary>Перевод между казной и фондом. toFund — из казны в фонд, иначе из фонда в казну.</summary>
    public bool TryMoveFundMoney(Entity<CityBankComponent> bank, CityFund fund, int amount, bool toFund)
    {
        if (amount <= 0)
            return false;

        if (toFund)
        {
            if (bank.Comp.Treasury < amount)
                return false;

            bank.Comp.Treasury -= amount;
            fund.Balance += amount;
        }
        else
        {
            if (fund.Balance < amount)
                return false;

            fund.Balance -= amount;
            bank.Comp.Treasury += amount;
        }

        AddLedger(bank, Loc.GetString(toFund ? "dystopia-bank-ledger-fund-in" : "dystopia-bank-ledger-fund-out",
            ("fund", fund.Name), ("amount", amount), ("balance", fund.Balance)), null, null);
        return true;
    }

    /// <summary>Списать деньги из фонда (например, на печать векселя).</summary>
    public bool TrySpendFund(CityFund fund, int amount)
    {
        if (amount <= 0 || fund.Balance < amount)
            return false;

        fund.Balance -= amount;
        return true;
    }

    /// <summary>
    /// Обналичивание векселя: деньги уже вышли из фонда при печати, на счёт зачисляется доход
    /// за вычетом налога и долга; налог и погашение долга уходят в казну.
    /// </summary>
    public (int Net, int Tax, int DebtPaid) RedeemVexel(Entity<CityBankComponent> bank, CityBankAccount account, int amount, string serial, string reason)
    {
        var (net, tax, debtPaid) = ApplyIncome(bank, account, amount);
        bank.Comp.Treasury += tax + debtPaid;

        AddHistory(bank, account, Loc.GetString("dystopia-bank-history-vexel",
            ("net", net), ("tax", tax), ("serial", serial), ("reason", reason)));
        AddLedger(bank, Loc.GetString("dystopia-bank-ledger-vexel",
            ("id", account.Id), ("name", account.Name), ("amount", amount), ("net", net), ("tax", tax), ("serial", serial)),
            null, account.Id);
        NotifyAccount(account, Loc.GetString("dystopia-bank-vexel-notify", ("net", net), ("tax", tax), ("serial", serial)));

        return (net, tax, debtPaid);
    }

    #endregion

    #region Журналы

    private string Stamp()
    {
        var time = _ticker.RoundDuration();
        return $"[{(int) time.TotalHours:00}:{time.Minutes:00}]";
    }

    /// <summary>Журнал решений Консула (только решения Консула).</summary>
    public void AddLog(Entity<CityBankComponent> bank, string text)
    {
        bank.Comp.Log.Add($"{Stamp()} {text}");

        var excess = bank.Comp.Log.Count - bank.Comp.MaxLogEntries;
        if (excess > 0)
            bank.Comp.Log.RemoveRange(0, excess);
    }

    /// <summary>Банковский реестр: все движения денег.</summary>
    public void AddLedger(Entity<CityBankComponent> bank, string text, int? from, int? to)
    {
        bank.Comp.Ledger.Add(new CityBankLedgerEntry($"{Stamp()} {text}", from, to));

        var excess = bank.Comp.Ledger.Count - bank.Comp.MaxLedgerEntries;
        if (excess > 0)
            bank.Comp.Ledger.RemoveRange(0, excess);
    }

    /// <summary>История операций конкретного счёта.</summary>
    public void AddHistory(Entity<CityBankComponent> bank, CityBankAccount account, string text)
    {
        account.History.Add($"{Stamp()} {text}");

        var excess = account.History.Count - bank.Comp.MaxHistoryEntries;
        if (excess > 0)
            account.History.RemoveRange(0, excess);
    }

    #endregion

    #region Осмотр

    private void OnCardExamined(Entity<CityBankCardComponent> ent, ref ExaminedEvent args)
    {
        PushAccountInfo(ent.Owner, ref args);
    }

    // Карта обычно лежит в КПК — показываем счёт и при осмотре КПК.
    private void OnPdaExamined(Entity<PdaComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.ContainedId is { } card)
            PushAccountInfo(card, ref args);
    }

    private void PushAccountInfo(EntityUid card, ref ExaminedEvent args)
    {
        if (!TryGetAccount(card, out _, out var account))
            return;

        args.PushMarkup(Loc.GetString("dystopia-bank-card-examine-account", ("id", account.Id)));

        // Баланс видит только владелец счёта.
        if (args.Examiner == account.Owner)
            args.PushMarkup(Loc.GetString("dystopia-bank-card-examine-balance", ("balance", account.Balance)));
    }

    #endregion
}
