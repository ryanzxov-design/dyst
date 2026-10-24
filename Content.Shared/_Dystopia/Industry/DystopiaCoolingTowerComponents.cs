// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: градирня — пар из газовой трубы (патрубки по бокам) конденсирует в воду (бак, из него забирает жидкостный
// импортер у крана спереди). Часть пара уходит в небо облаком. С питанием вентилятор гонит воздух — работает в
// полную силу, без питания — на тяге, медленно.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

[RegisterComponent]
public sealed partial class DystopiaCoolingTowerComponent : Component
{
    /// <summary>Патрубки пара: клетка от центра и сторона, куда смотрит патрубок (туда подводят трубу).</summary>
    [DataField(required: true)]
    public List<DystopiaCoolingTowerInletSpec> Inlets = new();

    /// <summary>Сущность-патрубок (узел газовой трубы на краевой клетке).</summary>
    [DataField]
    public EntProtoId InletPrototype = "DystopiaCoolingTowerInlet";

    /// <summary>Узел трубы у патрубка.</summary>
    [DataField]
    public string InletNode = "inlet";

    /// <summary>Бак воды.</summary>
    [DataField]
    public string WaterSolution = "water";

    [DataField]
    public string Water = "Water";

    /// <summary>Сколько пара (моль/с) берёт с вентилятором.</summary>
    [DataField]
    public float SteamPerSecond = 1.5f;

    /// <summary>Доля производительности без питания (на естественной тяге).</summary>
    [DataField]
    public float UnpoweredFactor = 0.3f;

    /// <summary>Воды из моля пара (обратно к двигателю: 0,5 моль пара на единицу воды).</summary>
    [DataField]
    public float WaterPerMole = 2f;

    /// <summary>Доля, что уходит в небо облаком.</summary>
    [DataField]
    public float Loss = 0.05f;

    [DataField]
    public float Interval = 1f;

    /// <summary>Сколько держать «работает» после последнего пара (чтобы облако не мигало).</summary>
    [DataField]
    public float WorkingHold = 3f;

    // --- состояние
    [DataField]
    public List<EntityUid> InletEntities = new();

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan LastWorked;

    [ViewVariables]
    public float SteamRate;

    [ViewVariables]
    public float WaterRate;
}

[DataDefinition]
public sealed partial class DystopiaCoolingTowerInletSpec
{
    [DataField(required: true)]
    public Vector2i Offset;

    [DataField(required: true)]
    public Direction Direction;
}

/// <summary>Патрубок градирни: узел газовой трубы на краевой клетке, принадлежит градирне.</summary>
[RegisterComponent]
public sealed partial class DystopiaCoolingTowerInletComponent : Component
{
    [DataField]
    public EntityUid? Tower;
}

[Serializable, NetSerializable]
public enum DystopiaCoolingTowerVisuals : byte
{
    Working,
}
