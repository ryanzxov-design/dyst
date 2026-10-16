// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: форма отливки (слиток, лист, прут...) и что из какого расплава в ней получается.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Industry;

[Prototype]
public sealed partial class DystopiaMouldShapePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Название формы для осмотра («слиток», «лист»...).</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Сколько расплава уходит на одну отливку.</summary>
    [DataField]
    public FixedPoint2 Volume = 10;

    /// <summary>Какая отливка получается из какого расплава.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, EntProtoId> Products = new();

    /// <summary>Что получается из смеси, шлака или недолива.</summary>
    [DataField(required: true)]
    public EntProtoId Defect;

    /// <summary>Минимальная доля основного металла в расплаве, иначе брак.</summary>
    [DataField]
    public float MinPurity = 0.95f;
}
