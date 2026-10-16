// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: жидкостные трубы и устройства. Трубы держат общий раствор сети; на трубе — отсечки цвета смеси.
// Устройства: порт (выравнивает сеть с цистерной на своей клетке), насос (из сети в сеть), экспортер (из сети в объект
// впереди) и импортер (из объекта позади — в сеть).

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

    [ViewVariables]
    public TimeSpan NextUpdate;
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
