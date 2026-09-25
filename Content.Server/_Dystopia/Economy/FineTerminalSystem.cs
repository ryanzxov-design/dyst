using System.Linq;
using Content.Server._Dystopia.Laws;
using Content.Server.Popups;
using Content.Shared._Dystopia.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Штрафной терминал Стражи: подготовка штрафа по статье Свода законов и выписка штрафа нарушителю.
/// Штраф — с активной карты нарушителя (в руке или в слоте ID), остаток — в долг, деньги — в казну.
/// </summary>
public sealed partial class FineTerminalSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private CityLawsSystem _laws = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private AudioSystem _audio = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private const int MaxReasonLength = 80;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FineTerminalComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<FineTerminalComponent, FineTerminalPrepareMessage>(OnPrepare);
        SubscribeLocalEvent<FineTerminalComponent, FineTerminalClearMessage>(OnClear);
        SubscribeLocalEvent<FineTerminalComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FineTerminalComponent, FineTerminalDoAfterEvent>(OnDoAfter);
    }

    private void OnOpened(Entity<FineTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<FineTerminalComponent> ent)
    {
        var laws = new List<FineTerminalLawEntry>();
        if (_laws.TryGetLaws(out var codex))
        {
            laws = _laws.GetSortedLaws(codex.Comp)
                .Select(l => new FineTerminalLawEntry(l.Id, l.Number, l.Title, l.Sanction))
                .ToList();
        }

        _ui.SetUiState(ent.Owner, FineTerminalUiKey.Key,
            new FineTerminalUiState(laws, ent.Comp.PendingAmount, ent.Comp.PendingArticle, ent.Comp.PendingReason));
    }

    private void Deny(EntityUid terminal, EntityUid user, string message)
    {
        _popup.PopupEntity(message, terminal, user);
        _audio.PlayPvs(DeniedSound, terminal);
    }

    private void OnPrepare(Entity<FineTerminalComponent> ent, ref FineTerminalPrepareMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent.Owner))
        {
            Deny(ent.Owner, args.Actor, Loc.GetString("dystopia-city-console-access-denied"));
            return;
        }

        if (args.Amount <= 0 || args.Amount > ent.Comp.MaxAmount)
            return;

        var article = string.Empty;
        var lawId = args.LawId; // ref-параметр нельзя использовать внутри лямбды
        if (lawId >= 0 && _laws.TryGetLaws(out var codex))
        {
            var law = codex.Comp.Laws.FirstOrDefault(l => l.Id == lawId);
            if (law != null)
                article = Loc.GetString("dystopia-laws-article-header", ("number", law.Number), ("title", law.Title));
        }

        var reason = args.Reason.Trim();
        if (reason.Length > MaxReasonLength)
            reason = reason[..MaxReasonLength];

        ent.Comp.PendingAmount = args.Amount;
        ent.Comp.PendingArticle = article;
        ent.Comp.PendingReason = reason;
        UpdateUi(ent);
    }

    private void OnClear(Entity<FineTerminalComponent> ent, ref FineTerminalClearMessage args)
    {
        ClearPending(ent);
        UpdateUi(ent);
    }

    private static void ClearPending(Entity<FineTerminalComponent> ent)
    {
        ent.Comp.PendingAmount = 0;
        ent.Comp.PendingArticle = string.Empty;
        ent.Comp.PendingReason = string.Empty;
    }

    private void OnAfterInteract(Entity<FineTerminalComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        // Штрафуем только тех, у кого есть карта Банка Города (живые люди с документами).
        if (!_bank.TryGetActiveAccount(target, out _, out _))
            return;

        args.Handled = true;

        if (!_access.IsAllowed(args.User, ent.Owner))
        {
            Deny(ent.Owner, args.User, Loc.GetString("dystopia-city-console-access-denied"));
            return;
        }

        if (ent.Comp.PendingAmount <= 0)
        {
            Deny(ent.Owner, args.User, Loc.GetString("dystopia-fine-terminal-not-prepared"));
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.FineDelay,
            new FineTerminalDoAfterEvent(), ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("dystopia-fine-terminal-writing-others",
            ("officer", IdentityName(args.User)), ("target", IdentityName(target))), args.User,
            Filter.PvsExcept(args.User), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("dystopia-fine-terminal-writing-target"), target, target, PopupType.MediumCaution);
    }

    private string IdentityName(EntityUid uid)
    {
        return Identity.Name(uid, EntityManager);
    }

    private void OnDoAfter(Entity<FineTerminalComponent> ent, ref FineTerminalDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (ent.Comp.PendingAmount <= 0)
            return;

        if (!_bank.TryGetActiveAccount(target, out var bank, out var account))
        {
            Deny(ent.Owner, args.User, Loc.GetString("dystopia-fine-terminal-no-card"));
            return;
        }

        var amount = ent.Comp.PendingAmount;
        var reason = ent.Comp.PendingArticle;
        if (ent.Comp.PendingReason.Length > 0)
            reason = reason.Length > 0 ? $"{reason}. {ent.Comp.PendingReason}" : ent.Comp.PendingReason;
        if (reason.Length == 0)
            reason = Loc.GetString("dystopia-city-console-no-reason");

        var (taken, toDebt) = _bank.Fine(bank, account, amount, Loc.GetString("dystopia-fine-terminal-reason",
            ("reason", reason), ("officer", Name(args.User))));

        ClearPending(ent);
        _audio.PlayPvs(PrintSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("dystopia-fine-terminal-done",
            ("amount", amount), ("taken", taken), ("debt", toDebt), ("id", account.Id)), ent.Owner, args.User);
        UpdateUi(ent);
    }
}
