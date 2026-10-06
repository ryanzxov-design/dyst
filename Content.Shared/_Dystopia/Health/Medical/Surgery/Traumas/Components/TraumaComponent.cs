// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;

/// <summary>Травма: перелом, повреждение органа, вен, нервов или отрыв. Живёт в ране, которая её вызвала.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TraumaComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? HoldingWoundable;

    /// <summary>Что повреждено: кость, орган или сама часть.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? TraumaTarget;

    /// <summary>Для отрыва: какая часть оторвана.</summary>
    [ViewVariables, AutoNetworkedField]
    public BodyPartType? TargetPartType;

    [ViewVariables, AutoNetworkedField]
    public BodyPartSymmetry? TargetSymmetry;

    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 TraumaSeverity;

    [DataField, AutoNetworkedField]
    public ProtoId<TraumaTypePrototype> TraumaType;
}
