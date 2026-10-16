// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: раскалённое берётся только клещами.

using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Industry;

public sealed partial class DystopiaHotItemSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaHotItemComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<DystopiaHotItemComponent, InteractHandEvent>(OnInteractHand,
            before: new[] { typeof(SharedItemSystem) });
    }

    /// <summary>Держит ли существо клещи в какой-нибудь руке.</summary>
    public bool HasTongs(EntityUid user)
    {
        if (!HasComp<HandsComponent>(user))
            return false;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<DystopiaTongsComponent>(held))
                return true;
        }

        return false;
    }

    public void SetHot(Entity<DystopiaHotItemComponent?> ent, bool hot)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Hot == hot)
            return;

        ent.Comp.Hot = hot;
        Dirty(ent);
    }

    private void OnPickupAttempt(Entity<DystopiaHotItemComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.Hot || HasTongs(args.User))
            return;

        args.Cancel();
        if (args.ShowPopup)
            _popup.PopupClient(Loc.GetString("dystopia-hot-item-need-tongs", ("item", ent.Owner)), ent, args.User);
    }

    // Голой рукой за раскалённое — ожог (до того, как предмет попробуют поднять).
    private void OnInteractHand(Entity<DystopiaHotItemComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.Hot || HasTongs(args.User))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("dystopia-hot-item-burn", ("item", ent.Owner)), ent, args.User,
            PopupType.SmallCaution);

        if (_net.IsServer)
            _damageable.TryChangeDamage(args.User, ent.Comp.BurnDamage, origin: ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        // Раскалённое в руке без клещей (клещи выпустили) — ожог и предмет падает.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaHotItemComponent>();
        while (query.MoveNext(out var uid, out var hot))
        {
            if (!hot.Hot || now < hot.NextCheck)
                continue;

            hot.NextCheck = now + TimeSpan.FromSeconds(hot.CheckInterval);

            if (!_container.TryGetContainingContainer(uid, out var container) ||
                !HasComp<HandsComponent>(container.Owner) ||
                !_hands.IsHolding(container.Owner, uid))
            {
                continue;
            }

            var holder = container.Owner;
            if (HasTongs(holder))
                continue;

            _damageable.TryChangeDamage(holder, hot.BurnDamage, origin: uid);
            _hands.TryDrop(holder, uid, checkActionBlocker: false);
            _popup.PopupEntity(Loc.GetString("dystopia-hot-item-burn", ("item", uid)), uid, holder,
                PopupType.SmallCaution);
        }
    }
}
