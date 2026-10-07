// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: пути передачи болезней сверх переноса из Goob (там только кашель/чих и укусы).
// - контакт: прикосновения, объятия, таскание, удары, поднятые предметы (перчатки защищают);
// - кровь: общие иглы шприцев;
// - еда: сырое мясо, человечина, мозги;
// - животные: лишай от кошек и собак.
// Логика — Content.Server/_Dystopia/Health/Disease/DiseaseTransmissionSystem.cs

using Content.Shared._Goobstation.Disease;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Disease;

/// <summary>
/// Симптом-«метка»: болезнь передаётся при касании — человеку напрямую или через тронутые предметы.
/// Шанс растёт с тяжестью симптома и прогрессом болезни.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseContactSpreadEffectComponent : Component
{
    [DataField]
    public DiseaseSpreadSpecifier SpreadParams = new(0.3f, 1f, "Contact");

    /// <summary>
    /// Шанс оставить заразу на тронутом предмете.
    /// </summary>
    [DataField]
    public float SurfaceChance = 0.5f;

    /// <summary>
    /// Сколько зараза живёт на предмете.
    /// </summary>
    [DataField]
    public TimeSpan SurfaceLifetime = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Симптом-«метка»: болезнь передаётся через кровь — игла шприца, которой кололи больного, заражает следующего.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseBloodborneEffectComponent : Component
{
    [DataField]
    public DiseaseSpreadSpecifier SpreadParams = new(0.3f, 1f, "Blood");

    /// <summary>
    /// Сколько зараза держится на игле.
    /// </summary>
    [DataField]
    public TimeSpan NeedleLifetime = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Еда, от которой можно заразиться (сырое мясо — цепень, человечина и мозги — куру).
/// Проверяется на каждый укус.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseOnIngestComponent : Component
{
    [DataField(required: true)]
    public List<DiseaseSourceEntry> Diseases = new();
}

/// <summary>
/// Существо, от которого можно заразиться, погладив или потрогав его (кошки, собаки — лишай).
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseOnTouchComponent : Component
{
    [DataField(required: true)]
    public List<DiseaseSourceEntry> Diseases = new();
}

[DataDefinition]
public sealed partial class DiseaseSourceEntry
{
    [DataField(required: true)]
    public EntProtoId Disease;

    [DataField]
    public DiseaseSpreadSpecifier SpreadParams = new(0.1f, 1f, "Contact");
}

/// <summary>
/// Инжектор с иглой (шприц): уносит на игле болезни, передающиеся через кровь. У гипоспреев иглы нет.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseNeedleComponent : Component
{
    /// <summary>
    /// Сколько игла считается нестерильной после укола (видно при осмотре).
    /// </summary>
    [DataField]
    public TimeSpan NonSterileTime = TimeSpan.FromMinutes(10);

    [ViewVariables, AutoNetworkedField]
    public TimeSpan NonSterileUntil;
}

/// <summary>
/// Шприц или другой инжектор коснулся крови существа (укол или забор крови). Поднимается на инжекторе.
/// </summary>
[ByRefEvent]
public readonly record struct DiseaseInjectorContactEvent(EntityUid User, EntityUid Target);
