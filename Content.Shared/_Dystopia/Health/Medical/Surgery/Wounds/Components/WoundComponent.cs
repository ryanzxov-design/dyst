// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;

/// <summary>Рана: сущность внутри контейнера «Wounds» части тела. Тяжесть — в очках, порог тяжести — по процентам прочности части.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundComponent : Component
{
    /// <summary>Часть тела, на которой рана.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid HoldingWoundable;

    public FixedPoint2 WoundIntegrityDamage => WoundSeverityPoint;

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public FixedPoint2 WoundSeverityPoint;

    [DataField, AutoNetworkedField]
    public WoundType WoundType = WoundType.External;

    [DataField, AutoNetworkedField]
    public ProtoId<DamageGroupPrototype>? DamageGroup;

    [DataField(required: true), AutoNetworkedField]
    public ProtoId<DamageTypePrototype> DamageType;

    /// <summary>Во что превращается рана, когда заживёт (шрам).</summary>
    [DataField]
    public EntProtoId? ScarWound;

    [DataField, AutoNetworkedField]
    public bool IsScar;

    [DataField, AutoNetworkedField]
    public WoundSeverity WoundSeverity;

    [DataField, AutoNetworkedField]
    public WoundVisibility WoundVisibility = WoundVisibility.Always;

    [DataField, AutoNetworkedField]
    public bool CanBeHealed = true;

    /// <summary>С какой тяжести раны часть тела считается изуродованной (травмы — Ф4).</summary>
    [DataField]
    public WoundSeverity? MangleSeverity;

    [DataField]
    public string TextString = "wound";

    [DataField, AutoNetworkedField]
    public float SelfHealMultiplier = 1.0f;
}
