// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос Shitmed (Goob-Station, AGPL-3.0): шаги операций и состояния части тела во время операции.

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared._Dystopia.Health.Surgery.Tools;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Surgery.Steps;

/// <summary>
/// Шаг операции: каким инструментом, сколько длится, какие состояния появляются (add) и исчезают (remove)
/// у части тела. Шаг выполнен, когда все add есть и ни одного remove нет (плюс проверки особых шагов).
/// </summary>
[RegisterComponent, NetworkedComponent]
[EntityCategory("SurgerySteps")]
public sealed partial class SurgeryStepComponent : Component
{
    [DataField]
    public ComponentRegistry? Tool;

    [DataField]
    public ComponentRegistry? Add;

    [DataField]
    public ComponentRegistry? BodyAdd;

    [DataField]
    public ComponentRegistry? Remove;

    [DataField]
    public ComponentRegistry? BodyRemove;

    [DataField]
    public float Duration = 2f;
}

/// <summary>Шаг повторяется сам, пока не будет выполнен (зажимать сосуды, сращивать кость...).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryRepeatableStepComponent : Component;

/// <summary>Вставить часть тела (конечность или протез из рук хирурга).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryAddPartStepComponent : Component;

/// <summary>Закрепить вставленную часть тела.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryAffixPartStepComponent : Component;

/// <summary>Отделить часть тела (ампутация).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryRemovePartStepComponent : Component;

/// <summary>Вставить орган из рук хирурга.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryAddOrganStepComponent : Component;

/// <summary>Закрепить вставленный орган.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryAffixOrganStepComponent : Component;

/// <summary>Извлечь орган.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryRemoveOrganStepComponent : Component;

/// <summary>Лечение травмы части: кость, органы, мозг, вены, нервы, культя.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTraumaTreatmentStepComponent : Component
{
    [DataField]
    public ProtoId<TraumaTypePrototype> TraumaType = "BoneDamage";

    /// <summary>Сколько прочности кости или органа восстанавливает шаг.</summary>
    [DataField]
    public FixedPoint2 Amount = 5;

    /// <summary>Только органы этих категорий (пусто — все органы части).</summary>
    [DataField]
    public List<string> Organs = new();
}

/// <summary>Остановить кровотечение ран части.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBleedsTreatmentStepComponent : Component;

/// <summary>Шаг причиняет боль (под наркозом — меньше).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepPainInflicterComponent : Component
{
    [DataField]
    public float Amount = 5f;

    /// <summary>Доля боли, если пациент спит (наркоз).</summary>
    [DataField]
    public float SleepModifier = 1f;

    [DataField]
    public TimeSpan PainDuration = TimeSpan.FromSeconds(10);
}

/// <summary>Можно ли выполнить шаг (инструмент, одежда, стол). Вызывается на шаге, затем на пациенте.</summary>
[ByRefEvent]
public record struct SurgeryCanPerformStepEvent(
    EntityUid User,
    EntityUid Body,
    EntityUid Part,
    EntityUid Tool,
    SlotFlags TargetSlots,
    string? Popup = null,
    StepInvalidReason Invalid = StepInvalidReason.None,
    ISurgeryToolComponent? ValidTool = null)
{
    public bool IsValid => Invalid == StepInvalidReason.None;
    public bool IsInvalid => !IsValid;
}

/// <summary>Выполнен ли шаг. Cancelled — ещё нет.</summary>
[ByRefEvent]
public record struct SurgeryStepCompleteCheckEvent(EntityUid Body, EntityUid Part, EntityUid Surgery, bool Cancelled = false);
