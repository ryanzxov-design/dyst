// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: энергетика, шаг 1 — паровой котёл с топкой и паровая турбина (оба — фиксированные многоблоки).
//
// Котёл 3×3: топка (уголь, кокс, доски — руками или вставщиком; жидкое топливо — мазутной форсункой вплотную),
// вода — жидкостным экспортером у крана, пар — в газовую трубу из патрубка. Режим (температура пара): горячее —
// больше энергии в каждом моле пара и больше расход топлива. Пару некуда уйти — давление в барабане растёт: тревога,
// урон, взрыв. Стравить пар — вручную (в воздух вокруг). Без воды топка жжёт трубы.
//
// Турбина 3×5: свежий пар из патрубка с одного торца, отработанный (~100 °C) — в патрубок на другом торце (дальше —
// в градирню, вода — обратно в котёл). Мощность ∝ расход пара × перепад температур → высоковольтный кабель под клеммой
// генератора. Масло расходуется, без него — сильный износ; слишком горячий пар жжёт лопатки.
//
// Патрубки и клеммы — отдельные сущности-узлы на краевых клетках (DystopiaMachinePortsComponent), поворачиваются
// вместе с машиной.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

// =====================================================================================================================
// Патрубки и клеммы многоклеточных машин

/// <summary>
/// Машина ставит на своих краевых клетках сущности-узлы (патрубок газовой трубы, клемма кабеля). Смещение и сторона —
/// в системе машины без поворота; при повороте машины поворачиваются и они. Удаляются вместе с машиной.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaMachinePortsComponent : Component
{
    [DataField(required: true)]
    public List<DystopiaMachinePortSpec> Ports = new();

    /// <summary>Можно развернуть машину на 180° (действие) — торцы меняются местами.</summary>
    [DataField]
    public bool Flippable;

    /// <summary>Сколько секунд разворачивать.</summary>
    [DataField]
    public float FlipDelay = 6f;

    /// <summary>Поставленные узлы по ключу (сохраняются с картой).</summary>
    [DataField]
    public Dictionary<string, EntityUid> Spawned = new();
}

[DataDefinition]
public sealed partial class DystopiaMachinePortSpec
{
    /// <summary>Как система машины находит узел (steamIn, steamOut, power...).</summary>
    [DataField(required: true)]
    public string Key = string.Empty;

    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>Клетка от центра машины (без поворота).</summary>
    [DataField(required: true)]
    public Vector2i Offset;

    /// <summary>Куда смотрит узел (без поворота машины): туда подводят трубу.</summary>
    [DataField]
    public Direction Direction = Direction.South;
}

[Serializable, NetSerializable]
public sealed partial class DystopiaMachineFlipDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Узел-патрубок или клемма: принадлежит машине.</summary>
[RegisterComponent]
public sealed partial class DystopiaMachinePortComponent : Component
{
    [DataField]
    public EntityUid? Machine;

    [DataField]
    public string Key = string.Empty;
}

/// <summary>
/// Жидкостный экспортер наливает в эту машину только перечисленное и только в указанный бак (котёл — воду, турбина —
/// масло). Остальное в сети не трогает.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaLiquidInputFilterComponent : Component
{
    [DataField(required: true)]
    public string Solution = string.Empty;

    [DataField(required: true)]
    public List<ProtoId<ReagentPrototype>> Reagents = new();
}

// =====================================================================================================================
// Котёл

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DystopiaBoilerMode
{
    /// <summary>Температура пара, °C.</summary>
    [DataField(required: true)]
    public float Temperature;

    /// <summary>Пара в секунду при полном прогреве, моль.</summary>
    [DataField(required: true)]
    public float Steam;

    /// <summary>Топлива в секунду, секунд горения (как у горна).</summary>
    [DataField(required: true)]
    public float Fuel;
}

[RegisterComponent]
public sealed partial class DystopiaBoilerComponent : Component
{
    // ------------------------------------------------------------ топка

    /// <summary>Топливо по материалу: секунд горения на 100 единиц материала (как у горна).</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<MaterialPrototype>, float> FuelMaterials = new();

    /// <summary>Сколько секунд горения влезает в топку.</summary>
    [DataField]
    public float MaxFuel = 1800f;

    [DataField]
    public float Fuel;

    [DataField]
    public bool Lit;

    /// <summary>Режимы: температура пара, пар и топливо в секунду.</summary>
    [DataField(required: true)]
    public List<DystopiaBoilerMode> Modes = new();

    [DataField]
    public int Mode;

    /// <summary>Прогрев котла 0…1: растёт, пока горит топка, падает, когда погасла.</summary>
    [DataField]
    public float Heat;

    /// <summary>Сколько секунд прогреваться от холодного до полного.</summary>
    [DataField]
    public float WarmupTime = 60f;

    /// <summary>Сколько секунд остывать от полного до холодного.</summary>
    [DataField]
    public float CooldownTime = 180f;

    /// <summary>С какого прогрева идёт пар (дальше — линейно до полного).</summary>
    [DataField]
    public float SteamFromHeat = 0.4f;

    // ------------------------------------------------------------ вода и пар

    [DataField]
    public string WaterSolution = "water";

    [DataField]
    public ProtoId<ReagentPrototype> Water = "Water";

    /// <summary>Пара (моль) из единицы воды (как у двигателя и градирни: 2 ед. на моль).</summary>
    [DataField]
    public float SteamPerWaterUnit = 0.5f;

    /// <summary>Пар в барабане, моль.</summary>
    [DataField]
    public float SteamMoles;

    /// <summary>Объём барабана, л: по нему давление (P = nRT/V).</summary>
    [DataField]
    public float DrumVolume = 500f;

    /// <summary>Узел-патрубок пара (ключ в DystopiaMachinePortsComponent).</summary>
    [DataField]
    public string SteamPort = "steam";

    [DataField]
    public string SteamNode = "pipe";

    /// <summary>Выше этого давления в трубе пар из барабана не уходит, кПа.</summary>
    [DataField]
    public float OutletPressure = 4500f;

    // ------------------------------------------------------------ давление

    [DataField]
    public float WarnPressure = 6000f;

    /// <summary>Выше — барабан трещит: урон за каждые 100 кПа сверх в секунду.</summary>
    [DataField]
    public float DamagePressure = 8000f;

    [DataField]
    public DamageSpecifier PressureDamage = new();

    /// <summary>Урон в секунду за каждые 100 кПа выше DamagePressure.</summary>
    [DataField]
    public float PressureDamageRate = 1f;

    /// <summary>Сухая топка (горит, а воды нет): урон в секунду.</summary>
    [DataField]
    public DamageSpecifier DryDamage = new();

    [DataField]
    public float DryDamageRate = 2f;

    /// <summary>Стравливание: моль в секунду в воздух вокруг котла.</summary>
    [DataField]
    public float VentRate = 12f;

    [DataField]
    public bool Venting;

    [DataField]
    public SoundSpecifier? AlarmSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");

    [DataField]
    public SoundSpecifier? CreakSound = new SoundPathSpecifier("/Audio/Effects/metal_scrape1.ogg");

    [DataField]
    public float Interval = 1f;

    // ------------------------------------------------------------ состояние

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan NextCreak;

    [ViewVariables]
    public bool Warned;

    [ViewVariables]
    public float PendingDamage;

    [ViewVariables]
    public float PendingDry;

    /// <summary>Сколько пара ушло в трубу за последний такт, моль/с.</summary>
    [ViewVariables]
    public float SteamRate;

    [ViewVariables]
    public float ProducedRate;

    [ViewVariables]
    public float WaterRate;

    [ViewVariables]
    public float FuelRate;

    [ViewVariables]
    public bool Dry;
}

[Serializable, NetSerializable]
public enum DystopiaBoilerVisuals : byte
{
    Lit,
    Venting,
    Alarm,
}

[Serializable, NetSerializable]
public enum DystopiaBoilerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DystopiaBoilerStatus : byte
{
    Cold,
    Warming,
    Producing,
    NoWater,
    Danger,
}

[Serializable, NetSerializable]
public sealed class DystopiaBoilerUiState(
    DystopiaBoilerStatus status,
    bool lit,
    float fuel,
    float maxFuel,
    List<DystopiaBoilerMode> modes,
    int mode,
    float heat,
    float pressure,
    float warnPressure,
    float damagePressure,
    float steam,
    float steamRate,
    float producedRate,
    bool steamConnected,
    float pipePressure,
    float water,
    float waterMax,
    float waterRate,
    float fuelRate,
    bool venting,
    float wear) : BoundUserInterfaceState
{
    public readonly DystopiaBoilerStatus Status = status;
    public readonly bool Lit = lit;
    public readonly float Fuel = fuel;
    public readonly float MaxFuel = maxFuel;
    public readonly List<DystopiaBoilerMode> Modes = modes;
    public readonly int Mode = mode;
    public readonly float Heat = heat;
    public readonly float Pressure = pressure;
    public readonly float WarnPressure = warnPressure;
    public readonly float DamagePressure = damagePressure;
    public readonly float Steam = steam;
    public readonly float SteamRate = steamRate;
    public readonly float ProducedRate = producedRate;
    public readonly bool SteamConnected = steamConnected;
    public readonly float PipePressure = pipePressure;
    public readonly float Water = water;
    public readonly float WaterMax = waterMax;
    public readonly float WaterRate = waterRate;
    public readonly float FuelRate = fuelRate;
    public readonly bool Venting = venting;

    /// <summary>Повреждение котла, доля до взрыва (0…1).</summary>
    public readonly float Wear = wear;
}

[Serializable, NetSerializable]
public sealed class DystopiaBoilerSetModeMessage(int mode) : BoundUserInterfaceMessage
{
    public readonly int Mode = mode;
}

[Serializable, NetSerializable]
public sealed class DystopiaBoilerToggleVentMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DystopiaBoilerExtinguishMessage : BoundUserInterfaceMessage;

// =====================================================================================================================
// Паровая турбина

[RegisterComponent]
public sealed partial class DystopiaSteamTurbineComponent : Component
{
    /// <summary>Патрубок свежего пара, патрубок выхлопа и клемма генератора (ключи в DystopiaMachinePortsComponent).</summary>
    [DataField]
    public string InletPort = "steamIn";

    [DataField]
    public string OutletPort = "steamOut";

    [DataField]
    public string PowerPort = "power";

    [DataField]
    public string PipeNode = "pipe";

    /// <summary>Сколько пара в секунду проходит через турбину при открытом клапане, моль.</summary>
    [DataField]
    public float MaxFlow = 6f;

    /// <summary>Открытие клапана 0…1.</summary>
    [DataField]
    public float Valve = 1f;

    [DataField]
    public bool On;

    /// <summary>Температура отработанного пара, K (100 °C).</summary>
    [DataField]
    public float ExhaustTemperature = 373.15f;

    /// <summary>Мощность на моль/с пара на каждый градус перепада, Вт.</summary>
    [DataField]
    public float PowerPerMoleKelvin = 400f;

    /// <summary>Наибольший перепад, который лопатки берут, K. Пар горячее — жжёт лопатки, лишний жар уходит в выхлоп.</summary>
    [DataField]
    public float MaxDelta = 400f;

    /// <summary>Выше этой температуры на входе — урон жаром (за каждый градус сверху в секунду), K.</summary>
    [DataField]
    public float MaxInletTemperature = 773.15f;

    [DataField]
    public DamageSpecifier OverheatDamage = new();

    [DataField]
    public float OverheatRate = 0.2f;

    /// <summary>Выше этого давления на выхлопе турбина встаёт (пару некуда уйти), кПа.</summary>
    [DataField]
    public float MaxExhaustPressure = 4500f;

    // ------------------------------------------------------------ ротор

    /// <summary>Обороты 0…1 (доля номинала).</summary>
    [DataField]
    public float Speed;

    /// <summary>Раскрутка, доля в секунду.</summary>
    [DataField]
    public float SpinUp = 0.08f;

    /// <summary>Выбег, доля в секунду.</summary>
    [DataField]
    public float SpinDown = 0.04f;

    // ------------------------------------------------------------ масло и износ

    [DataField]
    public string OilSolution = "oil";

    [DataField]
    public ProtoId<ReagentPrototype> Lubricant = "DystopiaLubricatingOil";

    /// <summary>Расход масла на полных оборотах, ед./с.</summary>
    [DataField]
    public float OilRate = 0.02f;

    [DataField]
    public DamageSpecifier WearDamage = new();

    /// <summary>Износ в секунду на полных оборотах.</summary>
    [DataField]
    public float WearRate = 0.01f;

    /// <summary>Без масла износ во столько раз выше.</summary>
    [DataField]
    public float DryWear = 25f;

    /// <summary>С какого урона ротор клинит (не крутится, пока не починят сваркой).</summary>
    [DataField]
    public FixedPoint2 SeizeDamage = 400;

    [DataField]
    public float Interval = 1f;

    // ------------------------------------------------------------ состояние

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public float PendingWear;

    [ViewVariables]
    public float PendingHeat;

    [ViewVariables]
    public bool Running;

    [ViewVariables]
    public float Power;

    [ViewVariables]
    public float Flow;

    [ViewVariables]
    public float InletTemperature;

    [ViewVariables]
    public float InletPressure;

    [ViewVariables]
    public float ExhaustPressure;

    [ViewVariables]
    public float OilUse;

    [ViewVariables]
    public bool Dry;

    [ViewVariables]
    public bool Blocked;
}

[Serializable, NetSerializable]
public enum DystopiaSteamTurbineUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DystopiaSteamTurbineStatus : byte
{
    Off,
    Running,
    NoSteam,
    Blocked,
    Seized,
}

[Serializable, NetSerializable]
public sealed class DystopiaSteamTurbineUiState(
    DystopiaSteamTurbineStatus status,
    bool on,
    float valve,
    float speed,
    float power,
    float maxPower,
    float flow,
    float maxFlow,
    float inletTemperature,
    float inletPressure,
    float exhaustTemperature,
    float exhaustPressure,
    float maxExhaustPressure,
    float maxInletTemperature,
    bool inletConnected,
    bool outletConnected,
    bool powerConnected,
    float oil,
    float oilMax,
    float oilRate,
    float wear) : BoundUserInterfaceState
{
    public readonly DystopiaSteamTurbineStatus Status = status;
    public readonly bool On = on;
    public readonly float Valve = valve;
    public readonly float Speed = speed;
    public readonly float Power = power;
    public readonly float MaxPower = maxPower;
    public readonly float Flow = flow;
    public readonly float MaxFlow = maxFlow;
    public readonly float InletTemperature = inletTemperature;
    public readonly float InletPressure = inletPressure;
    public readonly float ExhaustTemperature = exhaustTemperature;
    public readonly float ExhaustPressure = exhaustPressure;
    public readonly float MaxExhaustPressure = maxExhaustPressure;
    public readonly float MaxInletTemperature = maxInletTemperature;
    public readonly bool InletConnected = inletConnected;
    public readonly bool OutletConnected = outletConnected;
    public readonly bool PowerConnected = powerConnected;
    public readonly float Oil = oil;
    public readonly float OilMax = oilMax;
    public readonly float OilRate = oilRate;

    /// <summary>Износ, доля до заклинивания (0…1).</summary>
    public readonly float Wear = wear;
}

[Serializable, NetSerializable]
public sealed class DystopiaSteamTurbineToggleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DystopiaSteamTurbineSetValveMessage(float valve) : BoundUserInterfaceMessage
{
    public readonly float Valve = valve;
}
