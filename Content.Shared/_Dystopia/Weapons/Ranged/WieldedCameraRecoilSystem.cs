// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: см. WieldedCameraRecoilComponent.

using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Dystopia.Weapons.Ranged;

public sealed class WieldedCameraRecoilSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WieldedCameraRecoilComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
        SubscribeLocalEvent<WieldedCameraRecoilComponent, ItemWieldedEvent>(OnWielded);
        SubscribeLocalEvent<WieldedCameraRecoilComponent, ItemUnwieldedEvent>(OnUnwielded);
    }

    private void OnRefreshModifiers(Entity<WieldedCameraRecoilComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (TryComp<WieldableComponent>(ent, out var wield) && wield.Wielded)
            args.CameraRecoilScalar = MathF.Max(0f, args.CameraRecoilScalar + ent.Comp.CameraRecoilScalar);
    }

    private void OnWielded(Entity<WieldedCameraRecoilComponent> ent, ref ItemWieldedEvent args)
    {
        _gun.RefreshModifiers(ent.Owner);
    }

    private void OnUnwielded(Entity<WieldedCameraRecoilComponent> ent, ref ItemUnwieldedEvent args)
    {
        _gun.RefreshModifiers(ent.Owner);
    }
}
