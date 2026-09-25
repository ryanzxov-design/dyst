using System.Linq;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._Dystopia.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Городские автоматы: продажа за марки (выручка в казну) и автомат пайков
/// (бесплатно, раз в N минут на счёт, только при доступе рабочего места).
/// </summary>
public sealed partial class CityVendorSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier VendSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");
    private static readonly SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityVendorComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CityVendorComponent, CityVendorBuyMessage>(OnBuy);
        SubscribeLocalEvent<CityRationDispenserComponent, ActivateInWorldEvent>(OnRationActivate);
    }

    #region Автомат за марки

    private void OnOpened(Entity<CityVendorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<CityVendorComponent> ent)
    {
        var items = ent.Comp.Inventory
            .Select((entry, index) => new CityVendorUiEntry(index, ItemName(entry), entry.Price, entry.Amount))
            .ToList();

        _ui.SetUiState(ent.Owner, CityVendorUiKey.Key, new CityVendorUiState(items));
    }

    private string ItemName(CityVendorEntry entry)
    {
        return ProtoMan.TryIndex(entry.Item, out var proto) ? proto.Name : entry.Item.Id;
    }

    private void OnBuy(Entity<CityVendorComponent> ent, ref CityVendorBuyMessage args)
    {
        if (args.Index < 0 || args.Index >= ent.Comp.Inventory.Count)
            return;

        var entry = ent.Comp.Inventory[args.Index];
        if (entry.Amount == 0)
        {
            Deny(ent.Owner, args.Actor, Loc.GetString("dystopia-vendor-out-of-stock"));
            return;
        }

        if (!_bank.TryGetActiveAccount(args.Actor, out var bank, out var account))
        {
            Deny(ent.Owner, args.Actor, Loc.GetString("dystopia-payment-terminal-no-card"));
            return;
        }

        var name = ItemName(entry);
        var result = _bank.BuyFromCity(bank, account, entry.Price, name);
        switch (result)
        {
            case CityBankSystem.TransferResult.Success:
                break;
            case CityBankSystem.TransferResult.Frozen:
                Deny(ent.Owner, args.Actor, Loc.GetString("dystopia-bank-transfer-frozen"));
                return;
            case CityBankSystem.TransferResult.NotEnoughMoney:
                Deny(ent.Owner, args.Actor, Loc.GetString("dystopia-bank-transfer-no-money"));
                return;
            default:
                return;
        }

        if (entry.Amount > 0)
            entry.Amount--;

        var item = Spawn(entry.Item, Transform(ent.Owner).Coordinates);
        _hands.PickupOrDrop(args.Actor, item);
        _audio.PlayPvs(VendSound, ent.Owner);
        UpdateUi(ent);
    }

    #endregion

    #region Автомат пайков

    private void OnRationActivate(Entity<CityRationDispenserComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        var user = args.User;

        if (!_power.IsPowered(ent.Owner))
            return;

        if (!_access.IsAllowed(user, ent.Owner))
        {
            Deny(ent.Owner, user, Loc.GetString("dystopia-ration-no-assignment"));
            return;
        }

        if (!_bank.TryGetActiveAccount(user, out _, out var account))
        {
            Deny(ent.Owner, user, Loc.GetString("dystopia-payment-terminal-no-card"));
            return;
        }

        var now = _timing.CurTime;
        if (ent.Comp.NextRation.TryGetValue(account.Id, out var next) && now < next)
        {
            var minutes = (int) Math.Ceiling((next - now).TotalMinutes);
            Deny(ent.Owner, user, Loc.GetString("dystopia-ration-cooldown", ("minutes", minutes)));
            return;
        }

        ent.Comp.NextRation[account.Id] = now + TimeSpan.FromMinutes(ent.Comp.CooldownMinutes);

        foreach (var proto in ent.Comp.Items)
        {
            var item = Spawn(proto, Transform(ent.Owner).Coordinates);
            _hands.PickupOrDrop(user, item);
        }

        _audio.PlayPvs(VendSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("dystopia-ration-issued", ("minutes", (int) ent.Comp.CooldownMinutes)), ent.Owner, user);
    }

    #endregion

    private void Deny(EntityUid uid, EntityUid user, string message)
    {
        _popup.PopupEntity(message, uid, user);
        _audio.PlayPvs(DeniedSound, uid);
    }
}
