// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: энергетика, шаг 3б — отработавшее топливо и авария реактора.
//
// Облучённый ТВЭЛ (активность в DystopiaFuelRodComponent) горячий и светит; его носят захватом (клещи-захват,
// DystopiaTongsComponent) и выдерживают в бассейне: ячейки 1×1, соседние — один бассейн с общей водой; вода гасит
// излучение и снимает тепло (испаряется). Кориум — расплав топлива из разрушенного горячего канала: жжёт и светит,
// застывает в глыбу, глыбу разбивают на куски, куски собирают захватом. Отладка: размещение реактора по шаблону и
// пульт отладки (температуры, ксенон, стержни, вода, давление, ускорение, аварии).

using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

/// <summary>Ячейка бассейна выдержки: одна ячейка — один ТВЭЛ.</summary>
[RegisterComponent]
public sealed partial class DystopiaSpentFuelPoolCellComponent : Component
{
    [DataField]
    public string WaterSolution = "water";

    [DataField]
    public string Water = "Water";

    [DataField]
    public string Slot = "dystopia_pool_rod";

    /// <summary>Сколько рад/с гасит полная ячейка (вода над ТВЭЛом); пустая — ничего.</summary>
    [DataField]
    public float ShieldResistance = 30f;

    /// <summary>Сколько воды испаряет 1 кВт остаточного тепла за секунду, ед.</summary>
    [DataField]
    public float EvaporationPerKilowatt = 0.01f;

    /// <summary>Ниже этой доли воды ТВЭЛ уже не охлаждается (греется и сыплет пыль, как на воздухе).</summary>
    [DataField]
    public float LowWater = 0.25f;

    /// <summary>Урон ячейке в секунду, если в сухой ячейке стоит горячий ТВЭЛ.</summary>
    [DataField]
    public DamageSpecifier DryDamage = new();

    [ViewVariables]
    public float PendingDamage;
}

/// <summary>Жидкий кориум: через SolidifyTime застывает в глыбу.</summary>
[RegisterComponent]
public sealed partial class DystopiaCoriumComponent : Component
{
    [DataField]
    public float SolidifyTime = 300f;

    [DataField]
    public EntProtoId Solid = "DystopiaCoriumSolid";

    [DataField]
    public TimeSpan SolidifyAt;
}

/// <summary>Отладка: тычок в клетку — вокруг неё строится реактор по выбранному шаблону.</summary>
[RegisterComponent]
public sealed partial class DystopiaReactorPlacerComponent : Component
{
    [DataField(required: true)]
    public List<DystopiaReactorLayout> Layouts = new();

    /// <summary>Знак шаблона → что ставить. «.» и пробел — пусто.</summary>
    [DataField(required: true)]
    public Dictionary<string, EntProtoId> Legend = new();

    /// <summary>Знаки, на которые после постройки ставится ТВЭЛ шаблона.</summary>
    [DataField]
    public string FuelKeys = "F";

    [DataField]
    public int Index;
}

[DataDefinition]
public sealed partial class DystopiaReactorLayout
{
    /// <summary>Ключ локали названия.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>Строки сверху вниз, знаки через пробел или подряд.</summary>
    [DataField(required: true)]
    public List<string> Rows = new();

    [DataField]
    public EntProtoId? Rod;
}

/// <summary>Отладка: пульт отладки реактора (тычок в колонну, барабан или пульт — привязать и открыть).</summary>
[RegisterComponent]
public sealed partial class DystopiaReactorDebugToolComponent : Component
{
    [ViewVariables]
    public EntityUid? Target;
}

[Serializable, NetSerializable]
public enum DystopiaReactorDebugUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DystopiaReactorDebugUiState(bool linked, string summary, int speed) : BoundUserInterfaceState
{
    public readonly bool Linked = linked;
    public readonly string Summary = summary;
    public readonly int Speed = speed;
}

/// <summary>Действие отладки (<see cref="DystopiaReactorDebugActions"/>) с числом.</summary>
[Serializable, NetSerializable]
public sealed class DystopiaReactorDebugActionMessage(string action, float value) : BoundUserInterfaceMessage
{
    public readonly string Action = action;
    public readonly float Value = value;
}

public static class DystopiaReactorDebugActions
{
    public const string Temperature = "temperature";
    public const string ClearXenon = "clear-xenon";
    public const string MaxXenon = "max-xenon";
    public const string Rods = "rods";
    public const string Burnup = "burnup";
    public const string Activity = "activity";
    public const string FillWater = "fill-water";
    public const string DrainWater = "drain-water";
    public const string Pressure = "pressure";
    public const string Kick = "kick";
    public const string Speed = "speed";
    public const string Explosion = "explosion";
    public const string Meltdown = "meltdown";
    public const string Corium = "corium";
    public const string GraphiteFire = "graphite-fire";
    public const string Contamination = "contamination";
    public const string BreakChannel = "break-channel";
    public const string ResetScram = "reset-scram";
    public const string Runaway = "runaway";
}

[Serializable, NetSerializable]
public enum DystopiaPoolVisuals : byte
{
    Water,
    Rod,
    Glow,
}
