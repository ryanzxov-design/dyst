// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;

/// <summary>Вид травмы: перелом, повреждение органа, вен, нервов, отрыв конечности.</summary>
[Prototype]
public sealed partial class TraumaTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Травма возможна, только если рана выросла за раз хотя бы на столько.</summary>
    [DataField]
    public FixedPoint2 SeverityGate = 10;

    /// <summary>Пока травма есть, рана не заживает ниже определённой тяжести.</summary>
    [DataField]
    public bool BlocksHealing;

    [DataField]
    public TraumaTarget Target = TraumaTarget.Woundable;

    [DataField(required: true)]
    public EntProtoId TraumaEntity;

    [DataField]
    public FixedPoint2 BaseChance;

    [DataField]
    public List<TraumaCause>? Causes;
}

[Serializable, NetSerializable]
public enum TraumaTarget : byte
{
    Woundable,
    Bone,
    Organ,
    ParentWoundable,
}
