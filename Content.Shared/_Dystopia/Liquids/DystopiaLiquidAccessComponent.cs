// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: точки доступа жидкостных устройств к многоклеточным машинам (градирня, цистерна). Экспортер или импортер
// работает с такой машиной, только если стоит вплотную к отмеченной клетке с нужной стороны — там на спрайте кран.
// Точки поворачиваются вместе с машиной. Anywhere — с любой стороны любой краевой клетки (цистерна на бетонном
// основании с коллектором по периметру).

namespace Content.Shared._Dystopia.Liquids;

[RegisterComponent]
public sealed partial class DystopiaLiquidAccessComponent : Component
{
    [DataField]
    public List<DystopiaLiquidAccessPoint> Points = new();

    [DataField]
    public bool Anywhere;
}

[DataDefinition]
public sealed partial class DystopiaLiquidAccessPoint
{
    /// <summary>Клетка крана от центра машины.</summary>
    [DataField(required: true)]
    public Vector2i Offset;

    /// <summary>С какой стороны этой клетки ставят устройство.</summary>
    [DataField(required: true)]
    public Direction Side;
}
