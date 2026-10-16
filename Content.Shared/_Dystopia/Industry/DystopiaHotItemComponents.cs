// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: раскалённые предметы (тигель, изложница) берутся только клещами.

using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

/// <summary>
/// Предмет может раскалиться. Раскалённый (Hot) голой рукой не взять: обжигает руку.
/// Держать можно, только если в другой руке клещи (<see cref="DystopiaTongsComponent"/>);
/// выпустил клещи — обжёгся и уронил. Hot выставляет система, которая греет предмет (тигель, изложница).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DystopiaHotItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Hot;

    /// <summary>Ожог за попытку взять голой рукой и за каждую проверку, пока держишь без клещей.</summary>
    [DataField]
    public DamageSpecifier BurnDamage = new();

    /// <summary>Как часто проверять, что раскалённый предмет держат клещами, секунды.</summary>
    [DataField]
    public float CheckInterval = 1f;

    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>Кузнечные клещи: в руке — можно брать и держать раскалённые предметы.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DystopiaTongsComponent : Component
{
}

[Serializable, NetSerializable]
public enum DystopiaForgeVisuals : byte
{
    Lit,
    Bellows,
}

[Serializable, NetSerializable]
public sealed partial class DystopiaForgeBellowsDoAfterEvent : SimpleDoAfterEvent
{
}
