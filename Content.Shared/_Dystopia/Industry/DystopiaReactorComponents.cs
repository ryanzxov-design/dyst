// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: энергетика, шаг 3 — реактор РБМК, собираемый по клеткам.
//
// Колонны 1×1 (топливный канал, канал регулирующего стержня, графитовая кладка, пароводяной канал): соседние по
// стороне — один реактор, любого размера. Нейтроны из ТВЭЛа идут по четырём направлениям: графит замедляет (без
// замедления топливо делится плохо), стержень поглощает по глубине погружения, вода в пароводяном канале немного
// поглощает (пустой канал — меньше поглощения: положительный паровой эффект). Тепло: от делений, между соседями,
// снимается водой (кипение → пар). Йодная яма, выгорание. Барабан-сепаратор вплотную к реактору: вода — экспортером,
// пар — в газовую трубу. Пульт — вплотную к колонне или по кабелю управления к любой колонне; несколько пультов на
// один реактор — можно. Ручной сброс стержня — на самом канале стержня.

using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

[Serializable, NetSerializable]
public enum DystopiaReactorColumnKind : byte
{
    Fuel,
    Control,
    Graphite,
    Coolant,
}

/// <summary>Колонна реактора. Состояние — здесь же (реактор пересобирается из колонн в любой момент).</summary>
[RegisterComponent]
public sealed partial class DystopiaReactorColumnComponent : Component
{
    [DataField(required: true)]
    public DystopiaReactorColumnKind Kind;

    /// <summary>Слот ТВЭЛа (у топливного канала).</summary>
    [DataField]
    public string FuelSlot = "dystopia_fuel_rod";

    [DataField]
    public float Temperature = 20f;

    /// <summary>Нейтроны, пришедшие в канал за прошлый такт (у топливного).</summary>
    [DataField]
    public float Flux;

    /// <summary>Тепловая мощность канала за последний такт, кВт.</summary>
    [DataField]
    public float Power;

    [DataField]
    public float Iodine;

    [DataField]
    public float Xenon;

    /// <summary>Глубина погружения стержня 0…1 и куда он идёт.</summary>
    [DataField]
    public float Insertion = 1f;

    [DataField]
    public float Target = 1f;

    /// <summary>Стержень сброшен аварийной защитой — идёт вниз быстро.</summary>
    [DataField]
    public bool Scrammed;

    /// <summary>Группа стержней на пульте (1…4).</summary>
    [DataField]
    public int Group = 1;

    /// <summary>Снятое водой тепло за последний такт, кВт.</summary>
    [ViewVariables]
    public float Cooling;

    [ViewVariables]
    public float PendingDamage;

    [ViewVariables]
    public int HeatLevel;
}

/// <summary>ТВЭЛ: сколько даёт нейтронов на пришедший (до выгорания) и сколько тепла в нём, кДж.</summary>
[RegisterComponent]
public sealed partial class DystopiaFuelRodComponent : Component
{
    [DataField(required: true)]
    public float Reactivity = 1f;

    [DataField(required: true)]
    public float Energy = 1_000_000f;

    /// <summary>Выгорание 0…1.</summary>
    [DataField]
    public float Burnup;

    /// <summary>
    /// Короткоживущие продукты деления (шаг 3б): копятся, пока ТВЭЛ работает, и распадаются везде. От них остаточное
    /// тепло (ТВЭЛ раскалён — только захватом), излучение и, на воздухе, разрушение оболочки (радиоактивная пыль).
    /// </summary>
    [DataField]
    public float Activity;

    /// <summary>Излучение свежего ТВЭЛа, рад/с (к нему добавляются выгорание и активность).</summary>
    [DataField]
    public float BaseRadiation = 0.1f;

    [ViewVariables]
    public float LastIntensity = -1f;

    [ViewVariables]
    public TimeSpan NextShed;
}

/// <summary>Барабан-сепаратор: вода от экспортера, пар реактора — в газовую трубу (выравнивает давление с ней).</summary>
[RegisterComponent]
public sealed partial class DystopiaReactorSeparatorComponent : Component
{
    [DataField]
    public string WaterSolution = "water";

    [DataField]
    public string Water = "Water";

    [DataField]
    public string SteamNode = "steam";

    [DataField]
    public float SteamMoles;

    [DataField]
    public float DrumVolume = 1000f;

    [DataField]
    public float WarnPressure = 6500f;

    [DataField]
    public float DamagePressure = 8000f;

    [DataField]
    public DamageSpecifier PressureDamage = new();

    [DataField]
    public float PressureDamageRate = 1f;

    [ViewVariables]
    public float PendingDamage;

    [ViewVariables]
    public float SteamRate;
}

/// <summary>Пульт реактора: связь (вплотную или кабелем управления), авторегулятор, автоматическая защита.</summary>
[RegisterComponent]
public sealed partial class DystopiaReactorConsoleComponent : Component
{
    [DataField]
    public string ControlNode = "control";

    /// <summary>Авторегулятор: держит тепловую мощность группой 1.</summary>
    [DataField]
    public bool AutoRegulator;

    /// <summary>Уставка авторегулятора, кВт тепловых.</summary>
    [DataField]
    public float Setpoint = 2000f;

    /// <summary>Автоматическая аварийная защита: выше этой температуры любой колонны — сброс всех стержней.</summary>
    [DataField]
    public bool AutoScram = true;

    [DataField]
    public float AutoScramTemperature = 900f;

    [DataField]
    public SoundSpecifier? AlarmSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");

    [ViewVariables]
    public TimeSpan NextAlarm;

    /// <summary>Мощность на прошлом такте — авторегулятору для скорости роста.</summary>
    [ViewVariables]
    public float LastPower;
}

/// <summary>Числа физики реактора (прототип Default).</summary>
[Prototype("dystopiaReactor")]
public sealed partial class DystopiaReactorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Нейтронов от самопроизвольного деления в загруженном канале за такт.</summary>
    [DataField] public float Source = 0.5f;

    /// <summary>Сколько клеток летят нейтроны.</summary>
    [DataField] public int Travel = 4;

    [DataField] public float GraphitePass = 0.92f;

    /// <summary>Доля нейтронов, которую колонна на краю реактора (графитовый блок) возвращает в испустивший канал.</summary>
    [DataField] public float Reflector = 0.7f;

    /// <summary>Какая доля разницы потока доходит за такт (запаздывающие нейтроны: разгон плавный, а не взрывом).</summary>
    [DataField] public float FluxRelax = 0.2f;
    [DataField] public float RodAbsorb = 0.95f;
    [DataField] public float ControlPass = 0.95f;
    [DataField] public float WaterPass = 0.85f;
    [DataField] public float DryPass = 0.97f;

    /// <summary>Доля деления быстрыми (не замедленными графитом) нейтронами.</summary>
    [DataField] public float FastFactor = 0.3f;

    /// <summary>кВт на единицу потока.</summary>
    [DataField] public float PowerPerFlux = 3f;

    [DataField] public float MaxFlux = 100000f;

    /// <summary>Номинальная мощность канала (для йода и ксенона), кВт.</summary>
    [DataField] public float NominalPower = 500f;

    // тепло
    [DataField] public float HeatCapacity = 100f;          // кДж/K на колонну
    [DataField] public float Conduction = 20f;             // кВт/K между соседями
    [DataField] public float BoilTemperature = 285f;       // °C
    [DataField] public float CoolingPerKelvin = 10f;       // кВт/K выше кипения
    [DataField] public float FuelCoolingCap = 600f;        // кВт — топливный канал
    [DataField] public float CoolantCoolingCap = 800f;     // кВт — пароводяной канал
    [DataField] public float PassiveCooling = 0.0002f;     // доля разницы с 20 °C в секунду
    [DataField] public float Doppler = 0.0003f;            // −реактивность на °C выше кипения
    [DataField] public float VoidBoost = 1.08f;            // без воды — реактивнее
    [DataField] public float SteamEnergy = 240f;           // кДж на моль пара
    [DataField] public float WaterPerMole = 2f;            // ед. воды на моль пара
    [DataField] public float SteamTemperature = 573.15f;   // K

    // йод и ксенон
    [DataField] public float IodineProduction = 0.01f;
    [DataField] public float IodineHalfLife = 300f;
    [DataField] public float XenonHalfLife = 450f;
    [DataField] public float XenonBurn = 0.02f;
    [DataField] public float XenonWorth = 0.08f;
    [DataField] public float MaxXenonPoison = 0.25f;

    // стержни
    [DataField] public float RodSpeed = 0.05f;
    [DataField] public float ScramSpeed = 0.2f;

    // авторегулятор: шаг уставки стержней группы 1 за такт = усиление × отклонение + демпфер × скорость роста мощности
    [DataField] public float RegulatorGain = 0.02f;
    [DataField] public float RegulatorDamping = 0.5f;
    [DataField] public float RegulatorDeadband = 0.02f;
    [DataField] public float RegulatorMaxStep = 0.02f;

    /// <summary>Мощность выше уставки на эту долю — стержни вниз на полном шаге.</summary>
    [DataField] public float RegulatorOverpower = 0.2f;

    /// <summary>Пока самая горячая колонна горячее этого, авторегулятор стержни не вынимает, °C.</summary>
    [DataField] public float RegulatorHotLimit = 450f;

    // перегрев
    [DataField] public float DamageTemperature = 1200f;
    [DataField] public float DamagePerKelvin = 0.05f;
    [DataField] public float HotLevel1 = 400f;
    [DataField] public float HotLevel2 = 800f;

    // излучение: рад/с в центре реактора на МВт тепловых и за каждый загруженный ТВЭЛ
    [DataField] public float RadsPerMegawatt = 0.4f;
    [DataField] public float RadsPerRod = 0.05f;

    // облучённое топливо (шаг 3б)
    [DataField] public float ActivityGain = 0.0385f;       // активности в секунду при номинальной мощности канала
    [DataField] public float ActivityHalfLife = 180f;      // с
    [DataField] public float RadsPerActivity = 0.4f;       // рад/с на единицу активности
    [DataField] public float SpentRads = 0.5f;             // рад/с при полном выгорании (долгоживущие)
    [DataField] public float HotActivity = 1f;             // выше — ТВЭЛ раскалён: голой рукой не взять
    [DataField] public float DecayHeatPerActivity = 3f;    // кВт остаточного тепла на единицу активности
    [DataField] public float AirShedActivity = 5f;         // выше — на воздухе оболочка трескается и сыплет пыль
    [DataField] public float AirShedInterval = 15f;        // с
    [DataField] public float AirShedUnits = 3f;            // ед. радиоактивной пыли за раз
    [DataField] public string ShedReagent = "DystopiaRadioactiveDust";

    // авария (шаг 3б)
    [DataField] public EntProtoId Corium = "DystopiaCorium";
    [DataField] public EntProtoId GraphiteDebris = "DystopiaGraphiteDebris";
    [DataField] public float ExplosionMinPower = 1000f;    // кВт: ниже разрыв канала без парового взрыва
    [DataField] public float ExplosionPerMegawatt = 500f;  // полная сила взрыва на МВт тепловых
    [DataField] public float ExplosionMinIntensity = 800f;
    [DataField] public float ExplosionMaxIntensity = 8000f;
    [DataField] public float ExplosionSlope = 2f;
    [DataField] public float ExplosionMaxTile = 40f;
    [DataField] public float ExplosionCooldown = 60f;      // с: один взрыв на одну аварию
    [DataField] public int DebrisPerGraphite = 1;          // горящих обломков на каждую графитовую колонну
    [DataField] public float DebrisRadius = 8f;            // клеток
    [DataField] public int ContaminationPuddles = 80;
    [DataField] public float ContaminationRadius = 25f;    // клеток
    [DataField] public float ContaminationMaxUnits = 25f;  // у эпицентра; к краю — до ContaminationMinUnits
    [DataField] public float ContaminationMinUnits = 3f;
    [DataField] public int CoriumSpread = 2;               // сколько соседних клеток ещё заливает кориум
}

[Serializable, NetSerializable]
public enum DystopiaReactorVisuals : byte
{
    Heat,
    Rod,
    Insertion,
}

[Serializable, NetSerializable]
public enum DystopiaReactorConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DystopiaReactorCell(
    NetEntity entity,
    int x,
    int y,
    DystopiaReactorColumnKind kind,
    float temperature,
    float power,
    float flux,
    float burnup,
    int rodGrade,
    float insertion,
    float target,
    int group,
    float xenon,
    bool scrammed,
    float health)
{
    public readonly NetEntity Entity = entity;
    public readonly int X = x;
    public readonly int Y = y;
    public readonly DystopiaReactorColumnKind Kind = kind;
    public readonly float Temperature = temperature;
    public readonly float Power = power;
    public readonly float Flux = flux;
    public readonly float Burnup = burnup;

    /// <summary>Сорт ТВЭЛа: 0 — пусто, иначе реактивность ×10 (для цвета на карте).</summary>
    public readonly int RodGrade = rodGrade;

    public readonly float Insertion = insertion;
    public readonly float Target = target;
    public readonly int Group = group;
    public readonly float Xenon = xenon;
    public readonly bool Scrammed = scrammed;

    /// <summary>Целость колонны 0…1.</summary>
    public readonly float Health = health;
}

[Serializable, NetSerializable]
public sealed class DystopiaReactorConsoleUiState(
    bool connected,
    List<DystopiaReactorCell> cells,
    int width,
    int height,
    float thermalPower,
    float steamRate,
    float water,
    float waterMax,
    float maxTemperature,
    float averageXenon,
    int separators,
    float separatorPressure,
    bool autoRegulator,
    float setpoint,
    bool autoScram,
    float autoScramTemperature,
    bool alarm,
    float powerScale,
    float alarmTemperature,
    float damageTemperature,
    float warnPressure,
    float damagePressure) : BoundUserInterfaceState
{
    public readonly bool Connected = connected;
    public readonly List<DystopiaReactorCell> Cells = cells;
    public readonly int Width = width;
    public readonly int Height = height;
    public readonly float ThermalPower = thermalPower;
    public readonly float SteamRate = steamRate;
    public readonly float Water = water;
    public readonly float WaterMax = waterMax;
    public readonly float MaxTemperature = maxTemperature;
    public readonly float AverageXenon = averageXenon;
    public readonly int Separators = separators;
    public readonly float SeparatorPressure = separatorPressure;
    public readonly bool AutoRegulator = autoRegulator;
    public readonly float Setpoint = setpoint;
    public readonly bool AutoScram = autoScram;
    public readonly float AutoScramTemperature = autoScramTemperature;
    public readonly bool Alarm = alarm;

    /// <summary>Верх шкалы мощности (сколько могут снять все каналы), кВт.</summary>
    public readonly float PowerScale = powerScale;
    public readonly float AlarmTemperature = alarmTemperature;
    public readonly float DamageTemperature = damageTemperature;
    public readonly float WarnPressure = warnPressure;
    public readonly float DamagePressure = damagePressure;
}

/// <summary>Цель одного стержня (0…1).</summary>
[Serializable, NetSerializable]
public sealed class DystopiaReactorSetRodMessage(NetEntity rod, float target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Rod = rod;
    public readonly float Target = target;
}

/// <summary>Цель всей группы (0…1); группа 0 — все стержни.</summary>
[Serializable, NetSerializable]
public sealed class DystopiaReactorSetGroupMessage(int group, float target) : BoundUserInterfaceMessage
{
    public readonly int Group = group;
    public readonly float Target = target;
}

/// <summary>Сдвинуть группу на delta; группа 0 — все стержни.</summary>
[Serializable, NetSerializable]
public sealed class DystopiaReactorStepGroupMessage(int group, float delta) : BoundUserInterfaceMessage
{
    public readonly int Group = group;
    public readonly float Delta = delta;
}

[Serializable, NetSerializable]
public sealed class DystopiaReactorAssignGroupMessage(NetEntity rod, int group) : BoundUserInterfaceMessage
{
    public readonly NetEntity Rod = rod;
    public readonly int Group = group;
}

[Serializable, NetSerializable]
public sealed class DystopiaReactorScramMessage : BoundUserInterfaceMessage;

/// <summary>Снять АЗ: стержни снова слушаются уставок.</summary>
[Serializable, NetSerializable]
public sealed class DystopiaReactorResetScramMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DystopiaReactorAutoRegulatorMessage(bool on, float setpoint) : BoundUserInterfaceMessage
{
    public readonly bool On = on;
    public readonly float Setpoint = setpoint;
}

[Serializable, NetSerializable]
public sealed class DystopiaReactorAutoScramMessage(bool on) : BoundUserInterfaceMessage
{
    public readonly bool On = on;
}
