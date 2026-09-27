using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.FleshCult;

/// <summary>
/// Плоть, которая горит: загорается от струи огнемёта, пролетевшей над клеткой, и от пожара на клетке.
/// Горящая плоть получает урон каждую секунду. На соседние клетки огонь сам не переходит.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaFleshBurnableComponent : Component
{
    /// <summary>Урон огнём в секунду, пока горит (до модификаторов плоти).</summary>
    [DataField]
    public DamageSpecifier BurnDamage = new();

    /// <summary>Сколько секунд горит после поджога (повторный поджог продлевает).</summary>
    [DataField]
    public float BurnTime = 8f;

    /// <summary>Эффект огня (частицы и свет), пока горит.</summary>
    [DataField]
    public EntProtoId FireEffect = "DystopiaFleshFireEffect";
}

/// <summary>Плоть горит прямо сейчас.</summary>
[RegisterComponent]
public sealed partial class DystopiaFleshBurningComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public EntityUid? Effect;
}

/// <summary>
/// Поджигает плоть на клетках, над которыми пролетает (струя огнемёта).
/// Через WideAfter секунд полёта облако огня шире — поджигает и соседние клетки.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaFleshIgniterComponent : Component
{
    [DataField]
    public float WideAfter = 0.3f;

    [ViewVariables]
    public TimeSpan SpawnTime;
}
