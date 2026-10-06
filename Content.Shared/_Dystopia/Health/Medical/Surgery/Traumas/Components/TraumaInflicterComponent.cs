// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;

/// <summary>Рана, которая может вызывать травмы. Травмы — сущности в её контейнере «Traumas».</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TraumaInflicterComponent : Component
{
    /// <summary>Травма возможна, только если рана за раз выросла хотя бы на столько.</summary>
    [DataField]
    public FixedPoint2 SeverityThreshold = 9f;

    [ViewVariables]
    public Container TraumaContainer = default!;

    [DataField]
    public List<ProtoId<TraumaTypePrototype>> AllowArmourDeduction = new();

    /// <summary>
    /// Насколько защита брони части снижает шанс травм из AllowArmourDeduction
    /// (1 — броня с защитой 0.4 отнимает 0.4 шанса).
    /// </summary>
    [DataField]
    public FixedPoint2 ArmourDeductionFactor = 1;

    [DataField]
    public Dictionary<ProtoId<TraumaTypePrototype>, EntProtoId> TraumaPrototypes = new();

    /// <summary>Добавка к шансу травмы для этого вида раны.</summary>
    [DataField]
    public Dictionary<ProtoId<TraumaTypePrototype>, FixedPoint2> TraumasChances = new();

    /// <summary>Травмы, которые гарантированно случаются, когда рана изуродовала часть.</summary>
    [DataField]
    public Dictionary<ProtoId<TraumaTypePrototype>, FixedPoint2>? MangledMultipliers;
}
