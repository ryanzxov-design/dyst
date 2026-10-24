// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: промышленный двигатель внутреннего сгорания. Многоблочный, как ДАМ, но строго рядом: пульт, за ним
// (с его задней стороны) цилиндры в линию, в конце ряда — радиаторы. Мощность — по числу цилиндров и подаче; топливо
// любое из списка, у каждого своя отдача (хуже топливо — больше расход) и износ. Масло расходуется, без него износ
// резко растёт. Охлаждение: радиаторы (пассивно) и вода — она выкипает в пар, пар уходит в газовую трубу. Износ и
// перегрев — урон пульту (чинят сваркой, как окна); сильно изношенный двигатель клинит, разбитый — взрывается.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

[RegisterComponent]
public sealed partial class DystopiaEngineControllerComponent : Component
{
    // ------------------------------------------------------------ баки

    [DataField]
    public string FuelTank = "fuel";

    [DataField]
    public string OilTank = "oil";

    [DataField]
    public string WaterTank = "water";

    /// <summary>Топливо → отдача (энергия на единицу относительно дизеля). Хуже отдача — больше расход.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, float> Fuels = new();

    /// <summary>Топливо → множитель износа (бензин стучит, сварочное топливо — ацетилен — жжёт клапаны).</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> FuelWear = new();

    [DataField]
    public ProtoId<ReagentPrototype> Lubricant = "DystopiaLubricatingOil";

    [DataField]
    public ProtoId<ReagentPrototype> Coolant = "Water";

    // ------------------------------------------------------------ ряд

    [DataField]
    public int MinCylinders = 2;

    [DataField]
    public int MaxCylinders = 12;

    [DataField]
    public int MaxRadiators = 3;

    // ------------------------------------------------------------ работа (на один цилиндр при подаче 100%)

    /// <summary>Мощность цилиндра, Вт.</summary>
    [DataField]
    public float PowerPerCylinder = 20000f;

    /// <summary>Расход топлива с отдачей 1 (дизель), ед./с.</summary>
    [DataField]
    public float FuelPerCylinder = 0.04f;

    /// <summary>Расход масла, ед./с.</summary>
    [DataField]
    public float OilPerCylinder = 0.004f;

    /// <summary>Нагрев, °C/с.</summary>
    [DataField]
    public float HeatPerCylinder = 0.6f;

    /// <summary>Подача (доля от номинала): 0…MaxThrottle. Больше 1 — перегрузка: мощнее, но сильно греет и изнашивает.</summary>
    [DataField]
    public float Throttle = 1f;

    [DataField]
    public float MaxThrottle = 1.5f;

    [DataField]
    public bool On;

    // ------------------------------------------------------------ тепло

    [DataField]
    public float Temperature = 20f;

    [DataField]
    public float AmbientTemperature = 20f;

    /// <summary>Остывание само по себе: доля разницы с окружающей средой в секунду.</summary>
    [DataField]
    public float PassiveCooling = 0.004f;

    /// <summary>Радиатор: °C/с при разнице с окружающей средой в 70 °C (пропорционально разнице).</summary>
    [DataField]
    public float RadiatorCooling = 1.5f;

    /// <summary>С какой температуры подключается водяное охлаждение (вода выкипает).</summary>
    [DataField]
    public float WaterCoolingFrom = 90f;

    /// <summary>Сколько градусов снимает единица выкипевшей воды.</summary>
    [DataField]
    public float CoolingPerWaterUnit = 1.5f;

    /// <summary>Сколько воды в секунду может выкипеть на цилиндр.</summary>
    [DataField]
    public float WaterPerCylinder = 0.5f;

    /// <summary>Выше — износ вдвое быстрее.</summary>
    [DataField]
    public float WarnTemperature = 115f;

    /// <summary>Выше — двигатель горит: урон жаром за каждый градус сверху в секунду.</summary>
    [DataField]
    public float OverheatTemperature = 130f;

    [DataField]
    public float MaxTemperature = 250f;

    // ------------------------------------------------------------ пар

    /// <summary>Узел газовой трубы, куда уходит пар.</summary>
    [DataField]
    public string SteamNode = "steam";

    /// <summary>Узел высоковольтного кабеля.</summary>
    [DataField]
    public string PowerNode = "output";

    /// <summary>Пара (моль) из единицы воды.</summary>
    [DataField]
    public float SteamPerWaterUnit = 0.5f;

    /// <summary>Буфер пара, моль. Если полон и в трубу некуда — водяное охлаждение встаёт.</summary>
    [DataField]
    public float SteamCapacity = 200f;

    [DataField]
    public float SteamMoles;

    /// <summary>Температура пара, К.</summary>
    [DataField]
    public float SteamTemperature = 423.15f;

    /// <summary>Выше этого давления в трубе пар не выпускается, кПа.</summary>
    [DataField]
    public float MaxSteamPressure = 4500f;

    /// <summary>Выхлоп: углекислого газа на единицу топлива, моль (в воздух клетки).</summary>
    [DataField]
    public float ExhaustPerFuelUnit = 0.5f;

    // ------------------------------------------------------------ износ

    /// <summary>Износ в секунду на цилиндр при подаче 100% (урон пульту).</summary>
    [DataField]
    public DamageSpecifier WearDamage = new();

    /// <summary>Урон жаром за каждый градус выше OverheatTemperature в секунду.</summary>
    [DataField]
    public DamageSpecifier OverheatDamage = new();

    /// <summary>Без масла износ во столько раз выше.</summary>
    [DataField]
    public float DryWear = 20f;

    /// <summary>Перегрузка: износ ×(1 + (подача − 1) × OverloadWear).</summary>
    [DataField]
    public float OverloadWear = 6f;

    /// <summary>С какого урона двигатель клинит (не запускается, пока не починят).</summary>
    [DataField]
    public FixedPoint2 SeizeDamage = 300;

    [DataField]
    public float Interval = 1f;

    // ------------------------------------------------------------ состояние (для меню)

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public bool Running;

    [ViewVariables]
    public int Cylinders;

    [ViewVariables]
    public int Radiators;

    [ViewVariables]
    public float Power;

    [ViewVariables]
    public float FuelRate;

    [ViewVariables]
    public float OilRate;

    [ViewVariables]
    public float WaterRate;

    [ViewVariables]
    public List<EntityUid> Row = new();
}

/// <summary>Блок цилиндров: ставится в ряд за пультом.</summary>
[RegisterComponent]
public sealed partial class DystopiaEngineCylinderComponent : Component;

/// <summary>Радиатор: в конце ряда цилиндров.</summary>
[RegisterComponent]
public sealed partial class DystopiaEngineRadiatorComponent : Component;

[Serializable, NetSerializable]
public enum DystopiaEngineUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DystopiaEngineStatus : byte
{
    Off,
    Running,
    NoFuel,
    ShortRow,
    Seized,
}

[Serializable, NetSerializable]
public sealed class DystopiaEngineTankInfo(string id, float amount)
{
    public readonly string Id = id;
    public readonly float Amount = amount;
}

[Serializable, NetSerializable]
public sealed class DystopiaEngineUiState(
    DystopiaEngineStatus status,
    bool on,
    int cylinders,
    int minCylinders,
    int maxCylinders,
    int radiators,
    float throttle,
    float maxThrottle,
    float power,
    float maxPower,
    float temperature,
    float warnTemperature,
    float overheatTemperature,
    float wear,
    List<DystopiaEngineTankInfo> fuel,
    float fuelMax,
    float oil,
    float oilMax,
    float water,
    float waterMax,
    float steam,
    float steamMax,
    float steamPressure,
    bool steamConnected,
    float fuelRate,
    float oilRate,
    float waterRate) : BoundUserInterfaceState
{
    public readonly DystopiaEngineStatus Status = status;
    public readonly bool On = on;
    public readonly int Cylinders = cylinders;
    public readonly int MinCylinders = minCylinders;
    public readonly int MaxCylinders = maxCylinders;
    public readonly int Radiators = radiators;
    public readonly float Throttle = throttle;
    public readonly float MaxThrottle = maxThrottle;
    public readonly float Power = power;
    public readonly float MaxPower = maxPower;
    public readonly float Temperature = temperature;
    public readonly float WarnTemperature = warnTemperature;
    public readonly float OverheatTemperature = overheatTemperature;

    /// <summary>Износ, доля до заклинивания (0…1).</summary>
    public readonly float Wear = wear;

    public readonly List<DystopiaEngineTankInfo> Fuel = fuel;
    public readonly float FuelMax = fuelMax;
    public readonly float Oil = oil;
    public readonly float OilMax = oilMax;
    public readonly float Water = water;
    public readonly float WaterMax = waterMax;
    public readonly float Steam = steam;
    public readonly float SteamMax = steamMax;
    public readonly float SteamPressure = steamPressure;
    public readonly bool SteamConnected = steamConnected;
    public readonly float FuelRate = fuelRate;
    public readonly float OilRate = oilRate;
    public readonly float WaterRate = waterRate;
}

[Serializable, NetSerializable]
public sealed class DystopiaEngineToggleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DystopiaEngineSetThrottleMessage(float throttle) : BoundUserInterfaceMessage
{
    public readonly float Throttle = throttle;
}
