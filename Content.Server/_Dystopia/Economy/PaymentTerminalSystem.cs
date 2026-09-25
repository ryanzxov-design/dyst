using Content.Server.Popups;
using Content.Shared._Dystopia.Economy;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Платёжный терминал Лавки.
/// Привязка и выставление счёта — только у того, кто держит карту привязанного счёта (или у любого, пока терминал не привязан).
/// Оплата — покупатель прикладывает к терминалу ID-карту или КПК.
/// </summary>
public sealed partial class PaymentTerminalSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private AudioSystem _audio = default!;

    private static readonly SoundSpecifier PaidSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");
    private static readonly SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private const int MaxDescriptionLength = 60;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PaymentTerminalComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<PaymentTerminalComponent, PaymentTerminalSetBillMessage>(OnSetBill);
        SubscribeLocalEvent<PaymentTerminalComponent, PaymentTerminalCancelBillMessage>(OnCancelBill);
        SubscribeLocalEvent<PaymentTerminalComponent, PaymentTerminalLinkMessage>(OnLink);
        SubscribeLocalEvent<PaymentTerminalComponent, PaymentTerminalUnlinkMessage>(OnUnlink);
        SubscribeLocalEvent<PaymentTerminalComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PaymentTerminalComponent, PaymentTerminalDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PaymentTerminalComponent, ExaminedEvent>(OnExamined);
    }

    private void OnOpened(Entity<PaymentTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<PaymentTerminalComponent> ent)
    {
        var name = string.Empty;
        var taxRate = 0;
        if (ent.Comp.LinkedAccount is { } linked &&
            _bank.TryGetBank(out var bank) &&
            bank.Comp.Accounts.TryGetValue(linked, out var account))
        {
            name = account.Name;
            taxRate = _bank.GetTaxRate(bank, account);
        }

        _ui.SetUiState(ent.Owner, PaymentTerminalUiKey.Key,
            new PaymentTerminalUiState(ent.Comp.LinkedAccount, name, ent.Comp.PendingAmount, ent.Comp.PendingDescription, taxRate));
    }

    /// <summary>Держит ли пользователь карту привязанного счёта. Непривязанным терминалом может управлять любой.</summary>
    private bool CanManage(Entity<PaymentTerminalComponent> ent, EntityUid user)
    {
        if (ent.Comp.LinkedAccount is not { } linked)
            return true;

        return _bank.TryGetActiveAccount(user, out _, out var account) && account.Id == linked;
    }

    private void Deny(Entity<PaymentTerminalComponent> ent, EntityUid user, string loc)
    {
        _popup.PopupEntity(Loc.GetString(loc), ent.Owner, user);
        _audio.PlayPvs(DeniedSound, ent.Owner);
    }

    private void OnLink(Entity<PaymentTerminalComponent> ent, ref PaymentTerminalLinkMessage args)
    {
        if (!CanManage(ent, args.Actor))
        {
            Deny(ent, args.Actor, "dystopia-payment-terminal-not-owner");
            return;
        }

        if (!_bank.TryGetActiveAccount(args.Actor, out _, out var account))
        {
            Deny(ent, args.Actor, "dystopia-payment-terminal-no-card");
            return;
        }

        ent.Comp.LinkedAccount = account.Id;
        ClearBill(ent);
        _popup.PopupEntity(Loc.GetString("dystopia-payment-terminal-linked", ("id", account.Id)), ent.Owner, args.Actor);
        UpdateUi(ent);
    }

    private void OnUnlink(Entity<PaymentTerminalComponent> ent, ref PaymentTerminalUnlinkMessage args)
    {
        if (!CanManage(ent, args.Actor))
        {
            Deny(ent, args.Actor, "dystopia-payment-terminal-not-owner");
            return;
        }

        ent.Comp.LinkedAccount = null;
        ClearBill(ent);
        UpdateUi(ent);
    }

    private void OnSetBill(Entity<PaymentTerminalComponent> ent, ref PaymentTerminalSetBillMessage args)
    {
        if (ent.Comp.LinkedAccount == null)
        {
            Deny(ent, args.Actor, "dystopia-payment-terminal-not-linked");
            return;
        }

        if (!CanManage(ent, args.Actor))
        {
            Deny(ent, args.Actor, "dystopia-payment-terminal-not-owner");
            return;
        }

        if (args.Amount <= 0 || args.Amount > ent.Comp.MaxAmount)
            return;

        var description = args.Description.Trim();
        if (description.Length > MaxDescriptionLength)
            description = description[..MaxDescriptionLength];

        ent.Comp.PendingAmount = args.Amount;
        ent.Comp.PendingDescription = description;
        ent.Comp.BillId++;
        UpdateUi(ent);
    }

    private void OnCancelBill(Entity<PaymentTerminalComponent> ent, ref PaymentTerminalCancelBillMessage args)
    {
        if (!CanManage(ent, args.Actor))
        {
            Deny(ent, args.Actor, "dystopia-payment-terminal-not-owner");
            return;
        }

        ClearBill(ent);
        UpdateUi(ent);
    }

    private static void ClearBill(Entity<PaymentTerminalComponent> ent)
    {
        ent.Comp.PendingAmount = 0;
        ent.Comp.PendingDescription = string.Empty;
        ent.Comp.BillId++;
    }

    private void OnInteractUsing(Entity<PaymentTerminalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Реагируем только на карту или КПК со счётом Банка Города.
        if (!_bank.TryGetAccount(args.Used, out _, out _))
            return;

        args.Handled = true;

        if (ent.Comp.PendingAmount <= 0 || ent.Comp.LinkedAccount == null)
        {
            Deny(ent, args.User, "dystopia-payment-terminal-no-bill");
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.PayDelay,
            new PaymentTerminalDoAfterEvent { BillId = ent.Comp.BillId }, ent.Owner, target: ent.Owner, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<PaymentTerminalComponent> ent, ref PaymentTerminalDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } used)
            return;

        args.Handled = true;

        // Счёт могли отменить или заменить, пока прикладывали карту.
        if (args.BillId != ent.Comp.BillId || ent.Comp.PendingAmount <= 0 || ent.Comp.LinkedAccount is not { } linked)
        {
            Deny(ent, args.User, "dystopia-payment-terminal-bill-changed");
            return;
        }

        if (!_bank.TryGetAccount(used, out var bank, out var payer) ||
            !bank.Comp.Accounts.TryGetValue(linked, out var merchant))
        {
            Deny(ent, args.User, "dystopia-payment-terminal-no-card");
            return;
        }

        var amount = ent.Comp.PendingAmount;
        var description = ent.Comp.PendingDescription;
        var result = _bank.PayMerchant(bank, payer, merchant, amount, description);

        switch (result)
        {
            case CityBankSystem.TransferResult.Success:
                ClearBill(ent);
                _popup.PopupEntity(Loc.GetString("dystopia-payment-terminal-paid", ("amount", amount)), ent.Owner);
                _audio.PlayPvs(PaidSound, ent.Owner);
                break;
            case CityBankSystem.TransferResult.Frozen:
                Deny(ent, args.User, "dystopia-bank-transfer-frozen");
                break;
            case CityBankSystem.TransferResult.RecipientFrozen:
                Deny(ent, args.User, "dystopia-payment-terminal-merchant-frozen");
                break;
            case CityBankSystem.TransferResult.NotEnoughMoney:
                Deny(ent, args.User, "dystopia-bank-transfer-no-money");
                break;
            case CityBankSystem.TransferResult.SameAccount:
                Deny(ent, args.User, "dystopia-payment-terminal-same");
                break;
            default:
                Deny(ent, args.User, "dystopia-bank-transfer-bad-amount");
                break;
        }

        UpdateUi(ent);
    }

    private void OnExamined(Entity<PaymentTerminalComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.LinkedAccount is not { } linked)
        {
            args.PushMarkup(Loc.GetString("dystopia-payment-terminal-examine-unlinked"));
            return;
        }

        args.PushMarkup(Loc.GetString("dystopia-payment-terminal-examine-linked", ("id", linked)));
        if (ent.Comp.PendingAmount > 0)
        {
            args.PushMarkup(Loc.GetString("dystopia-payment-terminal-examine-bill",
                ("amount", ent.Comp.PendingAmount), ("description", ent.Comp.PendingDescription)));
        }
    }
}
