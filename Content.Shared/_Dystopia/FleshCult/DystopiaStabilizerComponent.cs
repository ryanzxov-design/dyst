using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.FleshCult;

/// <summary>
/// Стабилизатор Города: глушит Плоть вокруг себя.
///  - в радиусе не приживается Семя и не появляются кисты;
///  - кисты рядом с зоной растят наросты медленнее;
///  - работает от сети, при отключении — от внутреннего аккумулятора (~20 минут);
///  - при сильных повреждениях ломается; чинится сваркой.
/// Омега-Стабилизатор (IsOmega) неуязвим, пока работает достаточно обычных стабилизаторов;
/// его разрушение — победа Культа (этап К6).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaStabilizerComponent : Component
{
    /// <summary>Радиус зоны, клетки: здесь не приживаются Семена и не появляются кисты.</summary>
    [DataField]
    public float Radius = 20f;

    /// <summary>Во сколько раз медленнее растут наросты у кист рядом с зоной.</summary>
    [DataField]
    public float GrowthSlowdown = 3f;

    /// <summary>Насколько клеток за радиусом ещё действует замедление роста (досягаемость наростов кисты).</summary>
    [DataField]
    public float SlowdownReach = 8f;

    /// <summary>Запас аккумулятора, секунды работы без сети.</summary>
    [DataField]
    public float BatteryCapacity = 1200f;

    /// <summary>Сколько секунд заряда восстанавливается за секунду работы от сети.</summary>
    [DataField]
    public float RechargeRate = 2f;

    /// <summary>При таком общем уроне стабилизатор ломается (обычный) или разрушается (Омега).</summary>
    [DataField]
    public float BreakDamage = 200f;

    [DataField]
    public bool IsOmega;

    /// <summary>Омега неуязвим, пока работает не меньше стольких обычных стабилизаторов.</summary>
    [DataField]
    public int OmegaMinWorking = 3;

    // --- Рабочее состояние ---

    [ViewVariables]
    public float Battery = -1f;

    [ViewVariables]
    public bool Powered;

    [ViewVariables]
    public bool Working;

    [ViewVariables]
    public bool Broken;

    /// <summary>Омега: сейчас под защитой (неуязвим).</summary>
    [ViewVariables]
    public bool Shielded;

    /// <summary>Омега: защита уже вычислялась (первое вычисление не объявляется).</summary>
    [ViewVariables]
    public bool ShieldKnown;

    [ViewVariables]
    public TimeSpan NextUpdate;
}

[Serializable, NetSerializable]
public enum DystopiaStabilizerVisuals : byte
{
    State,
    Shielded,
}

/// <summary>Разрушен Омега-Стабилизатор (для правила раунда, этап К6).</summary>
[ByRefEvent]
public readonly record struct DystopiaOmegaStabilizerDestroyedEvent(EntityUid Omega);
