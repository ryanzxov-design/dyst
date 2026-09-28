using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.FleshCult;

/// <summary>Может сеять Семя Плоти (способность). Апостол и Проповедник.</summary>
[RegisterComponent]
public sealed partial class DystopiaFleshSeederComponent : Component
{
    [DataField]
    public EntProtoId ActionProto = "ActionDystopiaFleshPlantSeed";

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>Сколько секунд сажать семя.</summary>
    [DataField]
    public float PlantDelay = 4f;

    [DataField]
    public EntProtoId Seedling = "DystopiaFleshSeedling";
}

/// <summary>Росток из Семени Плоти: через время превращается в Кисту.</summary>
[RegisterComponent]
public sealed partial class DystopiaFleshSeedlingComponent : Component
{
    [DataField]
    public float GrowTime = 60f;

    [DataField]
    public EntProtoId Cyst = "DystopiaFleshCyst";

    [ViewVariables]
    public TimeSpan GrowAt;
}

/// <summary>
/// Киста Плоти: распространяет наросты по соседним клеткам (до MaxGrowths), затем одна из крайних клеток
/// становится новой кистой. Вокруг кисты — ядовитая зона (урон всем, кроме культистов и тех, кто в противогазе).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaFleshCystComponent : Component
{
    [DataField]
    public int MaxGrowths = 30;

    /// <summary>Секунд между появлением новых наростов.</summary>
    [DataField]
    public float SpreadInterval = 4f;

    [DataField]
    public EntProtoId Growth = "DystopiaFleshGrowth";

    [DataField]
    public EntProtoId Cyst = "DystopiaFleshCyst";

    /// <summary>Радиус ядовитой зоны, клетки.</summary>
    [DataField]
    public float ZoneRadius = 2.5f;

    /// <summary>Урон в секунду в ядовитой зоне.</summary>
    [DataField]
    public DamageSpecifier ZoneDamage = new();

    [ViewVariables]
    public HashSet<EntityUid> Growths = new();

    /// <summary>Киста уже породила свою дочернюю кисту.</summary>
    [ViewVariables]
    public bool ChildSpawned;

    [ViewVariables]
    public TimeSpan NextSpread;

    [ViewVariables]
    public TimeSpan NextZoneTick;
}

/// <summary>Клетка нароста. Если её киста уничтожена — нарост со временем отмирает.</summary>
[RegisterComponent]
public sealed partial class DystopiaFleshGrowthComponent : Component
{
    [ViewVariables]
    public EntityUid? Cyst;

    /// <summary>Сколько секунд нарост переваривает оставленный на нём труп.</summary>
    [DataField]
    public float DissolveTime = 30f;

    [ViewVariables]
    public TimeSpan? WitherAt;

    [ViewVariables]
    public TimeSpan NextCheck;

    // --- Стадии нароста ---

    /// <summary>Текущая стадия (1..MaxStage).</summary>
    [ViewVariables]
    public int Stage = 1;

    [DataField]
    public int MaxStage = 4;

    /// <summary>Через сколько секунд нарост переходит в следующую стадию.</summary>
    [DataField]
    public float StageTime = 60f;

    /// <summary>Во сколько раз прочность каждой следующей стадии больше предыдущей.</summary>
    [DataField]
    public float StageHealthMultiplier = 1.4f;

    /// <summary>Во сколько раз урон каждой следующей стадии больше предыдущей.</summary>
    [DataField]
    public float StageDamageMultiplier = 1.3f;

    /// <summary>Вариант рисунка 1–3 (выбирается случайно при появлении).</summary>
    [ViewVariables]
    public int Variant = 1;

    [ViewVariables]
    public TimeSpan NextStageAt;

    /// <summary>Урон нароста на первой стадии (запоминается из DamageContacts при появлении).</summary>
    [ViewVariables]
    public DamageSpecifier? BaseContactDamage;
}

/// <summary>Труп, который сейчас переваривается наростом.</summary>
[RegisterComponent]
public sealed partial class DystopiaFleshDissolvingComponent : Component
{
    [ViewVariables]
    public EntityUid Growth;

    [ViewVariables]
    public TimeSpan EndTime;
}

/// <summary>Способность «Посеять Семя Плоти».</summary>
public sealed partial class DystopiaFleshPlantSeedActionEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class DystopiaFleshPlantSeedDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
/// Можно ли посадить семя в этой точке. Стабилизаторы Города (этап К3) отменяют посадку в своей зоне.
/// </summary>
[ByRefEvent]
public record struct DystopiaFleshSeedAttemptEvent(EntityCoordinates Coordinates, bool Cancelled = false, string? Reason = null);

/// <summary>Во сколько раз медленнее растут наросты у этой кисты (стабилизаторы, этап К3). 1 — без замедления.</summary>
[ByRefEvent]
public record struct DystopiaFleshGrowthSpeedEvent(EntityUid Cyst, float Multiplier = 1f);

/// <summary>Внешний вид нароста: состояние спрайта kudzu_{стадия}{вариант}.</summary>
[Serializable, NetSerializable]
public enum DystopiaFleshGrowthVisuals : byte
{
    State,
}
