// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Health.Armor;

/// <summary>
/// Какие части тела закрывает броня. Если компонента нет — части определяются по слоту, в котором надета вещь
/// (шлем — голова, перчатки — кисти, ботинки — стопы, костюм и комбинезон — туловище, руки и ноги).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ArmorCoverageComponent : Component
{
    [DataField(required: true)]
    public List<BodyPartType> Coverage = new();
}
