// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: жидкостные трубы и устройства. Трубы держат общий раствор сети; на трубе — отсечки цвета смеси.
// Устройства: порт (выравнивает сеть с цистерной на своей клетке), насос (из сети в сеть), экспортер (из сети в объект
// впереди) и импортер (из объекта позади — в сеть). Качалка (Extractor) — из природного источника на своей клетке
// (нефтяной выход) в сеть. Фильтр (Filter) — выбранное вещество в боковую трубу, остальное прямо.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Liquids;

/// <summary>Жидкостная труба: осмотр и отсечки цвета смеси.</summary>
[RegisterComponent]
public sealed partial class DystopiaLiquidPipeComponent : Component
{
    /// <summary>Узел трубы в NodeContainer.</summary>
    [DataField]
    public string Node = "pipe";

    /// <summary>Как часто обновлять цвет отсечек, секунды.</summary>
    [DataField]
    public float ColorInterval = 2f;

    /// <summary>Шаг цвета (0–255): цвет меняется ступенями, мелкие колебания смеси не шлются клиенту.</summary>
    [DataField]
    public int ColorStep = 16;

    /// <summary>Цвет отсечек пустой трубы.</summary>
    [DataField]
    public Color EmptyColor = Color.FromHex("#5A5A5A");

    [ViewVariables]
    public Color? LastColor;

    [ViewVariables]
    public TimeSpan NextColorUpdate;
}

[Serializable, NetSerializable]
public enum DystopiaLiquidDeviceMode : byte
{
    /// <summary>Выравнивает заполненность сети и цистерны на своей клетке.</summary>
    Port,

    /// <summary>Из сети входа в сеть выхода.</summary>
    Pump,

    /// <summary>Из сети в объект на клетке <see cref="DystopiaLiquidDeviceComponent.TargetSide"/>.</summary>
    Exporter,

    /// <summary>Из объекта на клетке <see cref="DystopiaLiquidDeviceComponent.TargetSide"/> в сеть.</summary>
    Importer,

    /// <summary>Из природного источника (<see cref="DystopiaLiquidSourceComponent"/>) на своей клетке в сеть.</summary>
    Extractor,

    /// <summary>
    /// Из сети входа: выбранное вещество (<see cref="DystopiaLiquidDeviceComponent.FilterReagent"/>) — в сеть
    /// <see cref="DystopiaLiquidDeviceComponent.FilterNode"/> (сбоку), всё остальное — в сеть выхода (прямо).
    /// </summary>
    Filter,
}

[RegisterComponent]
public sealed partial class DystopiaLiquidDeviceComponent : Component
{
    [DataField(required: true)]
    public DystopiaLiquidDeviceMode Mode;

    /// <summary>Узел, из которого берётся жидкость (насос) или единственный узел (остальные).</summary>
    [DataField]
    public string Inlet = "pipe";

    /// <summary>Узел, куда насос гонит жидкость.</summary>
    [DataField]
    public string Outlet = "outlet";

    /// <summary>
    /// Где объект, с которым работает экспортер или импортер, — относительно поворота устройства
    /// (South — перед ним для устройства с поворотом 0).
    /// </summary>
    [DataField]
    public Direction TargetSide = Direction.South;

    /// <summary>Сколько единиц в секунду перекачивает.</summary>
    [DataField]
    public float Rate = 20f;

    /// <summary>Как часто работает, секунды.</summary>
    [DataField]
    public float Interval = 1f;

    /// <summary>Нужна ли сеть питания.</summary>
    [DataField]
    public bool RequirePower;

    /// <summary>Включается и выключается через ПКМ.</summary>
    [DataField]
    public bool Toggleable;

    [DataField]
    public bool On = true;

    /// <summary>Фильтр: боковой узел, куда уходит выбранное вещество.</summary>
    [DataField]
    public string FilterNode = "filter";

    /// <summary>Фильтр: какое вещество отводить вбок (выбирается через ПКМ; пусто — всё идёт прямо).</summary>
    [DataField]
    public ProtoId<ReagentPrototype>? FilterReagent;

    [ViewVariables]
    public TimeSpan NextUpdate;
}

/// <summary>
/// Природный источник жидкости (нефтяной выход): запас одного реагента, который выкачивает качалка, стоящая на нём.
/// Запас конечен; иссякший источник остаётся на месте, но больше ничего не даёт.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaLiquidSourceComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>Начальный запас — случайно между Min и Max (при появлении в мире).</summary>
    [DataField]
    public float MinAmount = 3000f;

    [DataField]
    public float MaxAmount = 6000f;

    /// <summary>Сколько осталось.</summary>
    [DataField]
    public FixedPoint2 Amount;

    /// <summary>Начальный запас — для оценки при осмотре.</summary>
    [DataField]
    public FixedPoint2 Initial;

    /// <summary>Пороги оценки запаса при осмотре (доля начального): выше первого — богатый, выше второго — средний.</summary>
    [DataField]
    public float RichFraction = 0.66f;

    [DataField]
    public float FairFraction = 0.33f;
}

[Serializable, NetSerializable]
public enum DystopiaLiquidSourceVisuals : byte
{
    /// <summary>Источник иссяк.</summary>
    Depleted,
}

[Serializable, NetSerializable]
public enum DystopiaLiquidVisuals : byte
{
    /// <summary>Цвет отсечек трубы.</summary>
    Color,

    /// <summary>Устройство работает.</summary>
    Working,
}

[Serializable, NetSerializable]
public enum DystopiaLiquidVisualLayers : byte
{
    Marks,
}
