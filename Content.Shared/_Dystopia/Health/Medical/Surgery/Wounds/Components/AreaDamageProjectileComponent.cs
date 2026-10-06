// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;

/// <summary>
/// Снаряд бьёт по всему телу, а не в часть под прицелом (струя огнемёта, облако, картечь пламени).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AreaDamageProjectileComponent : Component;
