// SPDX-License-Identifier: AGPL-3.0-or-later
// Хирургия Dystopia — перенос хирургии Shitmed из Goob-Station (space-syndicate/Goob-Station, AGPL-3.0),
// адаптированный под нашу систему тела (органы — сущности с категориями) и наши раны, травмы и боль.

using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Surgery;

/// <summary>Операция: прототип-сущность со списком шагов и операцией, которую нужно сделать раньше.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[EntityCategory("Surgeries")]
public sealed partial class SurgeryComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Priority;

    [DataField, AutoNetworkedField]
    public EntProtoId? Requirement;

    [DataField(required: true), AutoNetworkedField]
    public List<EntProtoId> Steps = new();
}

/// <summary>Можно оперировать (у пациента) и можно быть хирургом (у оператора).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTargetComponent : Component
{
    [DataField]
    public bool CanOperate = true;

    /// <summary>Нестерильная операция не вызывает заражения.</summary>
    [DataField]
    public bool SepsisImmune;
}

/// <summary>Операционный стол: ускоряет операции.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OperatingTableComponent : Component
{
    [DataField]
    public float SpeedModifier = 1f;
}

/// <summary>Стерильная вещь: в руках или надетая, спасает пациента от заражения.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SanitizedComponent : Component
{
    [DataField]
    public bool WorksInHands;
}

/// <summary>Хирург работает быстрее (опыт, особые перчатки).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySpeedModifierComponent : Component
{
    [DataField]
    public float SpeedModifier = 1.5f;
}

/// <summary>Можно оперировать, не снимая одежду.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryIgnoreClothingComponent : Component;

[Serializable, NetSerializable]
public enum SurgeryUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiState(Dictionary<NetEntity, List<EntProtoId>> choices) : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, List<EntProtoId>> Choices = choices;
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SurgeryStepChosenBuiMsg(NetEntity part, EntProtoId surgery, EntProtoId step) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly EntProtoId Step = step;
}

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : SimpleDoAfterEvent
{
    public readonly EntProtoId Surgery;
    public readonly EntProtoId Step;
    public readonly bool ToolUsed;

    public SurgeryDoAfterEvent(EntProtoId surgery, EntProtoId step, bool toolUsed)
    {
        Surgery = surgery;
        Step = step;
        ToolUsed = toolUsed;
    }
}

/// <summary>Шаг сделан. Вызывается на сущности шага и на хирурге.</summary>
[ByRefEvent]
public record struct SurgeryStepEvent(EntityUid User, EntityUid Body, EntityUid Part, EntityUid Tool, EntityUid Surgery, EntityUid Step, bool Complete);

/// <summary>Шаг не удался (прерван).</summary>
[ByRefEvent]
public record struct SurgeryStepFailedEvent(EntityUid User, EntityUid Body, EntProtoId SurgeryId, EntProtoId StepId);

/// <summary>Урон или лечение части от шага (вызывается на пациенте, применяет сервер).</summary>
[ByRefEvent]
public record struct SurgeryStepDamageEvent(EntityUid User, EntityUid Body, EntityUid Part, EntityUid Surgery, Content.Shared.Damage.DamageSpecifier Damage);

/// <summary>Стерильность: Handled — заражения не будет.</summary>
public sealed class SurgerySanitizationEvent : HandledEntityEventArgs;

/// <summary>Хирург может пропускать предыдущие шаги (отладка).</summary>
public sealed class SurgeryIgnorePreviousStepsEvent : HandledEntityEventArgs;

public enum StepInvalidReason
{
    None,
    NeedsOperatingTable,
    Armor,
    MissingTool,
    SurgeryInvalid,
    MissingPreviousSteps,
    StepCompleted,
    ToolInvalid,
    DoAfterFailed,
}
