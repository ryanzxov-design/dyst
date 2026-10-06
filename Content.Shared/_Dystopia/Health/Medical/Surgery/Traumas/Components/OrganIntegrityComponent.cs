// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;

/// <summary>
/// Целостность внутреннего органа. Травмы органа снижают её; при нуле орган разрушается и пропадает
/// (кроме неразрушимых). Добавляется органу при первом повреждении.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganIntegrityComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 IntegrityCap = 120;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Integrity = 120;

    [ViewVariables, AutoNetworkedField]
    public OrganSeverity Severity = OrganSeverity.Normal;

    /// <summary>Источники повреждения: (идентификатор, травма) → урон.</summary>
    public Dictionary<(string, EntityUid), FixedPoint2> Modifiers = new();

    [DataField]
    public Dictionary<OrganSeverity, FixedPoint2> Thresholds = new()
    {
        { OrganSeverity.Normal, 120 },
        { OrganSeverity.Damaged, 60 },
        { OrganSeverity.Destroyed, 0 },
    };

    [DataField]
    public Dictionary<OrganSeverity, FixedPoint2> HealSeverityFloor = new()
    {
        { OrganSeverity.Normal, 0 },
        { OrganSeverity.Damaged, 10 },
        { OrganSeverity.Destroyed, 30 },
    };

    [DataField]
    public bool Indestructible;

    /// <summary>Сколько целостности органа снимает единица тяжести травмы (удар, выросший на 10, при 2.5 — минус 25).</summary>
    [DataField]
    public FixedPoint2 DamageMultiplier = 2.5;

    /// <summary>Насколько разрушение органа сбивает с ног.</summary>
    [DataField]
    public TimeSpan DestroyedKnockdown = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier DestroyedSound = new SoundPathSpecifier("/Audio/Effects/gib1.ogg");
}
