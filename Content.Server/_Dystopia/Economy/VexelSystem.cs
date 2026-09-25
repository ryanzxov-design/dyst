using Content.Server.Popups;
using Content.Shared._Dystopia.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Вексельный принтер Гражданского Инспектора и векселя на предъявителя.
/// Печать — из фонда (по умолчанию Фонд поощрений), обналичивание — использованием векселя в руке:
/// сумма зачисляется на активную карту того, кто держит вексель (с налогом его профессии).
/// </summary>
public sealed partial class VexelSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public const string VexelPrototype = "DystopiaVexel";
    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private const int MaxReasonLength = 80;

    private float _refreshAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VexelPrinterComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<VexelPrinterComponent, VexelPrinterPrintMessage>(OnPrint);
        SubscribeLocalEvent<VexelComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<VexelComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Раз в 2 секунды обновляем открытые принтеры: Консул мог пополнить фонд.
        _refreshAccumulator += frameTime;
        if (_refreshAccumulator < 2f)
            return;
        _refreshAccumulator = 0f;

        var query = EntityQueryEnumerator<VexelPrinterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, VexelPrinterUiKey.Key))
                UpdateUi((uid, comp));
        }
    }

    private void OnOpened(Entity<VexelPrinterComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<VexelPrinterComponent> ent)
    {
        var name = ent.Comp.Fund;
        var balance = 0;
        if (_bank.TryGetBank(out var bank) && _bank.GetFund(bank, ent.Comp.Fund) is { } fund)
        {
            name = fund.Name;
            balance = fund.Balance;
        }

        _ui.SetUiState(ent.Owner, VexelPrinterUiKey.Key, new VexelPrinterUiState(name, balance, ent.Comp.MaxAmount));
    }

    private void OnPrint(Entity<VexelPrinterComponent> ent, ref VexelPrinterPrintMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent.Owner))
        {
            Deny(ent.Owner, args.Actor, Loc.GetString("dystopia-city-console-access-denied"));
            return;
        }

        if (args.Amount <= 0 || args.Amount > ent.Comp.MaxAmount)
            return;

        if (!_bank.TryGetBank(out var bank) || _bank.GetFund(bank, ent.Comp.Fund) is not { } fund)
            return;

        if (!_bank.TrySpendFund(fund, args.Amount))
        {
            Deny(ent.Owner, args.Actor, Loc.GetString("dystopia-city-console-fund-not-enough"));
            return;
        }

        var reason = args.Reason.Trim();
        if (reason.Length > MaxReasonLength)
            reason = reason[..MaxReasonLength];
        if (reason.Length == 0)
            reason = Loc.GetString("dystopia-city-console-no-reason");

        var serial = $"{_random.Next(100000, 1000000)}";
        var issuer = Name(args.Actor);

        var vexelUid = Spawn(VexelPrototype, Transform(ent.Owner).Coordinates);
        var vexel = EnsureComp<VexelComponent>(vexelUid);
        vexel.Amount = args.Amount;
        vexel.Serial = serial;
        vexel.Reason = reason;
        vexel.IssuedBy = issuer;
        _meta.SetEntityName(vexelUid, Loc.GetString("dystopia-vexel-name", ("amount", args.Amount)));

        _bank.AddLedger(bank, Loc.GetString("dystopia-bank-ledger-vexel-printed",
            ("serial", serial), ("amount", args.Amount), ("fund", fund.Name), ("issuer", issuer), ("reason", reason)), null, null);

        _audio.PlayPvs(PrintSound, ent.Owner);
        UpdateUi(ent);
    }

    private void OnUseInHand(Entity<VexelComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Amount <= 0)
            return;

        if (!_bank.TryGetActiveAccount(args.User, out var bank, out var account))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-vexel-no-card"), ent.Owner, args.User);
            return;
        }

        var amount = ent.Comp.Amount;
        ent.Comp.Amount = 0; // защита от двойного обналичивания в один тик
        var (net, tax, _) = _bank.RedeemVexel(bank, account, amount, ent.Comp.Serial, ent.Comp.Reason);

        _popup.PopupEntity(Loc.GetString("dystopia-vexel-redeemed", ("net", net), ("tax", tax), ("id", account.Id)), args.User, args.User);
        QueueDel(ent.Owner);
    }

    private void OnExamined(Entity<VexelComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Amount <= 0)
            return;

        args.PushMarkup(Loc.GetString("dystopia-vexel-examine",
            ("amount", ent.Comp.Amount), ("serial", ent.Comp.Serial),
            ("reason", ent.Comp.Reason), ("issuer", ent.Comp.IssuedBy)));
    }

    private void Deny(EntityUid uid, EntityUid user, string message)
    {
        _popup.PopupEntity(message, uid, user);
        _audio.PlayPvs(DeniedSound, uid);
    }
}
