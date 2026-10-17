// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: раскалённое берётся только клещами; ожог — по той руке, которой трогают или держат.

using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
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
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

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
        // Жар виден всем: частицы дымка и искр (у литейного стола и печей DystopiaHotItem нет — им только это).
        _appearance.SetData(ent.Owner, DystopiaHeatVisuals.Hot, hot);

        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Hot == hot)
            return;

        ent.Comp.Hot = hot;
        Dirty(ent);
    }

    /// <summary>
    /// Обжечь руку handId (кисть, если нет — плечо этой стороны). Без руки или без тела — урон по телу.
    /// </summary>
    public void BurnHand(EntityUid user, string? handId, DamageSpecifier damage, EntityUid origin)
    {
        if (_net.IsClient)
            return;

        if (handId != null && HasComp<BodyComponent>(user) && _hands.TryGetHand(user, handId, out var hand))
        {
            var symmetry = hand.Value.Location switch
            {
                HandLocation.Left => BodyPartSymmetry.Left,
                HandLocation.Right => BodyPartSymmetry.Right,
                _ => BodyPartSymmetry.None,
            };

            if ((FindPart(user, BodyPartType.Hand, symmetry) ?? FindPart(user, BodyPartType.Arm, symmetry)) is { } part)
            {
                _wounds.DamagePart(user, part, damage, origin);
                return;
            }
        }

        _damageable.TryChangeDamage(user, damage, origin: origin);
    }

    private EntityUid? FindPart(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        foreach (var part in _body.GetBodyChildrenOfType(body, type, symmetry: symmetry))
        {
            if (HasComp<WoundableComponent>(part.Id))
                return part.Id;
        }

        return null;
    }

    private void OnPickupAttempt(Entity<DystopiaHotItemComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.Hot)
            return;

        args.Cancel();
        if (args.ShowPopup)
            _popup.PopupEntity(Loc.GetString("dystopia-hot-item-need-tongs", ("item", ent.Owner)), ent, args.User);
    }

    // Голой рукой за раскалённое — ожог активной руки (до того, как предмет попробуют поднять).
    private void OnInteractHand(Entity<DystopiaHotItemComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.Hot)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("dystopia-hot-item-burn", ("item", ent.Owner)), ent, args.User,
            PopupType.SmallCaution);

        BurnHand(args.User, _hands.GetActiveHand(args.User), ent.Comp.BurnDamage, ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        // Раскалилось прямо в руке (налили в изложницу, которую держат) — ожог этой руки и предмет падает.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaHotItemComponent>();
        while (query.MoveNext(out var uid, out var hot))
        {
            if (!hot.Hot || now < hot.NextCheck)
                continue;

            hot.NextCheck = now + TimeSpan.FromSeconds(hot.CheckInterval);

            if (!_container.TryGetContainingContainer(uid, out var container) ||
                !HasComp<HandsComponent>(container.Owner) ||
                !_hands.IsHolding(container.Owner, uid, out var handId))
            {
                continue;
            }

            var holder = container.Owner;
            BurnHand(holder, handId, hot.BurnDamage, uid);
            _hands.TryDrop(holder, uid, checkActionBlocker: false);
            _popup.PopupEntity(Loc.GetString("dystopia-hot-item-burn", ("item", uid)), uid, holder,
                PopupType.SmallCaution);
        }
    }
}
