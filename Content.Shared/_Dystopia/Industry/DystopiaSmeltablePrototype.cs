// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: что получается при плавке материала в тигле.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Industry;

/// <summary>
/// Плавка одного материала. ID прототипа = ID материала (<see cref="MaterialPrototype"/>): тигель смотрит на
/// PhysicalComposition брошенного предмета, поэтому плавится и руда, и слитки, и любой металлический лом.
/// Выход указан на 100 единиц материала (один кусок руды, один слиток, один лист).
/// </summary>
[Prototype]
public sealed partial class DystopiaSmeltablePrototype : IPrototype
{
    /// <summary>ID материала.</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Реагенты на 100 единиц материала: расплав металла, шлак и т.п.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Products = new();

    /// <summary>
    /// Температура плавки, K. Если не задана — наибольшая точка плавления (°C) среди продуктов,
    /// у которых она указана в прототипе реагента.
    /// </summary>
    [DataField]
    public float? MeltTemperature;
}
