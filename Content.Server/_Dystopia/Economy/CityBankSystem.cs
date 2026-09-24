using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Managers;
using Content.Shared._Dystopia.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Банк Города: открывает счета новым жителям, платит зарплаты из казны, удерживает налоги.
/// Публичные методы (TryGetBank, TryGetAccount, Deposit, TryWithdraw, Payday) — для консоли Консула,
/// терминалов и автоматов, которые появятся позже.
/// </summary>
public sealed partial class CityBankSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private GameTicker _ticker = default!;

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

    /// <summary>Счёт, к которому привязана карта.</summary>
    public bool TryGetAccount(EntityUid card, out Entity<CityBankComponent> bank, [NotNullWhen(true)] out CityBankAccount? account)
    {
        bank = default;
        account = null;

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

    /// <summary>Зачисляет деньги на счёт (отрицательная сумма — списание, баланс не уходит ниже нуля).</summary>
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

    #endregion

    #region Зарплаты

    /// <summary>
    /// Выплата зарплат всем живым владельцам незамороженных счетов.
    /// Налог удерживается сразу: из казны уходит только зарплата за вычетом налога.
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

            var tax = salary * GetTaxRate(bank, account) / 100;
            var net = salary - tax;

            if (bank.Comp.Treasury < net)
            {
                unpaid++;
                Notify(owner, Loc.GetString("dystopia-bank-salary-unpaid"));
                continue;
            }

            bank.Comp.Treasury -= net;
            account.Balance += net;
            paid++;

            Notify(owner, Loc.GetString("dystopia-bank-salary-paid",
                ("net", net), ("tax", tax), ("balance", account.Balance)));
        }

        AddLog(bank, Loc.GetString("dystopia-bank-log-payday", ("paid", paid), ("unpaid", unpaid), ("treasury", bank.Comp.Treasury)));
        return (paid, unpaid);
    }

    /// <summary>Личное сообщение в чат владельцу счёта (если он в игре).</summary>
    public void Notify(EntityUid owner, string message)
    {
        if (TryComp<ActorComponent>(owner, out var actor))
            _chat.DispatchServerMessage(actor.PlayerSession, message);
    }

    #endregion

    #region Премии, изъятия, журнал

    /// <summary>
    /// Премия из казны. С премии удерживается налог профессии, из казны уходит сумма за вычетом налога.
    /// </summary>
    public bool TryPayBonus(Entity<CityBankComponent> bank, CityBankAccount account, int amount, string reason, out int net, out int tax)
    {
        tax = amount * GetTaxRate(bank, account) / 100;
        net = amount - tax;

        if (amount <= 0 || bank.Comp.Treasury < net)
            return false;

        bank.Comp.Treasury -= net;
        account.Balance += net;

        if (account.Owner is { } owner)
            Notify(owner, Loc.GetString("dystopia-bank-bonus-received", ("net", net), ("tax", tax), ("reason", reason)));

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

        if (account.Owner is { } owner)
            Notify(owner, Loc.GetString("dystopia-bank-seized", ("amount", taken), ("reason", reason)));

        return taken;
    }

    /// <summary>Запись в журнал казны с временем раунда.</summary>
    public void AddLog(Entity<CityBankComponent> bank, string text)
    {
        var time = _ticker.RoundDuration();
        bank.Comp.Log.Add($"[{(int) time.TotalHours:00}:{time.Minutes:00}] {text}");

        var excess = bank.Comp.Log.Count - bank.Comp.MaxLogEntries;
        if (excess > 0)
            bank.Comp.Log.RemoveRange(0, excess);
    }

    #endregion

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
}
