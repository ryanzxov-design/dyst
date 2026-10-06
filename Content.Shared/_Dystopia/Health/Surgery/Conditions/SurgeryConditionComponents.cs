// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос Shitmed (Goob-Station, AGPL-3.0): условия, при которых операция доступна для части тела.
// Dystopia: органы и слоты — по категориям органов нашей системы тела (Heart, Lungs, ArmLeft...).

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Surgery.Conditions;

/// <summary>Проверка операции для части. Cancelled — операция недоступна.</summary>
[ByRefEvent]
public record struct SurgeryValidEvent(EntityUid Body, EntityUid Part, bool Cancelled = false);

/// <summary>Только для этих типов частей (и стороны).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPartConditionComponent : Component
{
    [DataField]
    public HashSet<BodyPartType> Parts = new();

    [DataField]
    public BodyPartSymmetry? Symmetry;

    [DataField]
    public bool Inverse;
}

/// <summary>Часть на месте (в теле).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPartPresentConditionComponent : Component;

/// <summary>Часть прикреплена к телу.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryHasBodyConditionComponent : Component;

/// <summary>
/// Отсутствует дочерняя часть категории Connection (или только что пришита и шов не закрыт):
/// можно пришить новую.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPartRemovedConditionComponent : Component
{
    /// <summary>Категория органа-части, которой не хватает (ArmLeft, HandRight, LegLeft...).</summary>
    [DataField(required: true)]
    public string Connection = string.Empty;
}

/// <summary>
/// Орган этих категорий есть в части (для извлечения). Inverse — органа нет (для вставки);
/// Reattaching — вставленный, но не закреплённый орган тоже считается «нет».
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganConditionComponent : Component
{
    [DataField(required: true)]
    public List<string> Organs = new();

    [DataField]
    public bool Inverse;

    [DataField]
    public bool Reattaching;
}

/// <summary>Часть по устройству тела должна содержать орган этой категории (есть «место» для него).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganSlotConditionComponent : Component
{
    [DataField(required: true)]
    public string OrganSlot = string.Empty;
}

/// <summary>На части есть травма этого типа (Inverted — нет).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTraumaPresentConditionComponent : Component
{
    [DataField("trauma")]
    public ProtoId<TraumaTypePrototype> TraumaType = "BoneDamage";

    [DataField]
    public bool Inverted;
}

/// <summary>Кость части повреждена.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBoneDamagedConditionComponent : Component;

/// <summary>Органы части повреждены (только эти категории, если указаны).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganDamagedConditionComponent : Component
{
    [DataField]
    public List<string> Organs = new();
}

/// <summary>Часть кровоточит (Inverted — не кровоточит или сосуды пережаты).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBleedsPresentConditionComponent : Component
{
    [DataField]
    public bool Inverted;
}

/// <summary>На части есть раны этой группы урона.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryWoundedConditionComponent : Component
{
    [DataField]
    public ProtoId<DamageGroupPrototype> DamageGroup = "Brute";
}

/// <summary>Шаги можно делать только на операционном столе.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOperatingTableConditionComponent : Component;

/// <summary>Есть (или нет — Inverse) компоненты на части.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPartComponentConditionComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public bool Inverse;
}
