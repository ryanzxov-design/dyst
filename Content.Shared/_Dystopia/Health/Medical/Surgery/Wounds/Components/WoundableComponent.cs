// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;

/// <summary>
/// Часть тела, на которой бывают раны. Целостность (integrity) — «здоровье» части: падает от тяжести ран,
/// по порогам определяется состояние части (цела, легко, средне, тяжело, критично, изуродована, отделена).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundableComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? ParentWoundable;

    [ViewVariables, AutoNetworkedField]
    public EntityUid RootWoundable;

    [ViewVariables, AutoNetworkedField]
    public HashSet<EntityUid> ChildWoundables = [];

    [DataField, AutoNetworkedField]
    public bool AllowWounds = true;

    /// <summary>
    /// Шанс оторвать уже разрушенную часть одним ударом, по виду урона (при ударе в 15 единиц и сильнее;
    /// слабее — пропорционально меньше). Режущее отрубает, тупое отрывает, пули почти никогда.
    /// </summary>
    /// <summary>Часть считается разрушенной, когда целостности осталось меньше этой доли.</summary>
    [DataField]
    public float DestroyedFraction = 0.02f;

    /// <summary>Удар такой силы и выше даёт полный шанс отрыва; слабее — пропорционально меньше.</summary>
    [DataField]
    public float DismemberFullDamage = 25f;

    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> DismemberChances = new()
    {
        { "Slash", 0.1f },
        { "Blunt", 0.05f },
        { "Piercing", 0.003f },
        { "Heat", 0.02f },
    };

    [DataField]
    public bool RedirectOverflowDamage;

    [DataField("damageContainer")]
    public ProtoId<DamageContainerPrototype>? DamageContainerID;

    /// <summary>Кость части: появляется при создании части (переломы).</summary>
    [DataField]
    public EntProtoId BoneEntity = "HealthBone";

    [DataField, AutoNetworkedField]
    public FixedPoint2 IntegrityCap;

    [DataField, AutoNetworkedField]
    public FixedPoint2 DodgeChance = 0.1;

    [DataField("integrity"), AutoNetworkedField]
    public FixedPoint2 WoundableIntegrity;

    [DataField(required: true)]
    public Dictionary<WoundableSeverity, FixedPoint2> Thresholds = new();

    public KeyValuePair<WoundableSeverity, FixedPoint2>[]? SortedThresholds;

    /// <summary>Сколько тяжести ран снимается за тик естественного заживления.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 HealAbility = 0.03;

    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 Bleeds = FixedPoint2.Zero;

    [ViewVariables, DataField]
    public FixedPoint2 BleedingTreatmentAbility = 0.01f;

    [ViewVariables, DataField]
    public FixedPoint2 BleedsThreshold = 3.5f;

    /// <summary>Ниже этой целостности часть не заживает сама — нужна медицина.</summary>
    [DataField]
    public FixedPoint2 DamageThreshold = 45;

    [ViewVariables]
    public bool CanHealDamage => WoundableIntegrity > DamageThreshold && WoundableIntegrity < IntegrityCap;

    [ViewVariables]
    public bool CanHealBleeds => Bleeds > 0 && Bleeds < BleedsThreshold;

    public Dictionary<EntityUid, WoundableSeverityMultiplier> SeverityMultipliers = new();

    public Dictionary<EntityUid, WoundableHealingMultiplier> HealingMultipliers = new();

    [DataField]
    public SoundSpecifier WoundableDestroyedSound = new SoundCollectionSpecifier("WoundableDestroyed");

    [DataField]
    public SoundSpecifier WoundableDelimbedSound = new SoundCollectionSpecifier("WoundableDelimbed");

    [DataField, AutoNetworkedField]
    public WoundableSeverity WoundableSeverity;

    [ViewVariables]
    public Container Wounds = default!;

    [ViewVariables]
    public Container Bone = default!;

    [DataField]
    public bool CanRemove = true;

    [DataField]
    public bool CanBleed = true;

    [DataField]
    public bool IsBoneExposed;

    /// <summary>Вычеты брони для травм (Ф4). Ключ — тип травмы.</summary>
    [DataField]
    public Dictionary<string, FixedPoint2> TraumaDeductions = new()
    {
        { "Dismemberment", 0.3f },
    };
}
