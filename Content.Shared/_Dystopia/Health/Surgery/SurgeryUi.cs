// SPDX-License-Identifier: AGPL-3.0-or-later
// Хирургия Dystopia: окно операции.

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Surgery;

[Serializable, NetSerializable]
public enum SurgeryUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SurgeryStepState(string id, string name, string tools, bool done, bool current)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Tools = tools;
    public readonly bool Done = done;
    public readonly bool Current = current;
}

[Serializable, NetSerializable]
public sealed class SurgeryEntryState(string id, string name, List<SurgeryStepState> steps)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly List<SurgeryStepState> Steps = steps;
}

[Serializable, NetSerializable]
public sealed class SurgeryOrganState(NetEntity organ, string name)
{
    public readonly NetEntity Organ = organ;
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class SurgeryPartState(NetEntity part, string name, string status, List<SurgeryEntryState> surgeries,
    bool cavityOpen, List<SurgeryOrganState> organs, List<string> missing, bool open, List<string> missingLimbs)
{
    /// <summary>Часть тела вскрыта (к ней можно пришить конечность).</summary>
    public readonly bool Open = open;
    /// <summary>Каких конечностей не хватает у этой части тела.</summary>
    public readonly List<string> MissingLimbs = missingLimbs;

    public readonly NetEntity Part = part;
    public readonly string Name = name;
    public readonly string Status = status;
    public readonly List<SurgeryEntryState> Surgeries = surgeries;
    public readonly bool CavityOpen = cavityOpen;
    public readonly List<SurgeryOrganState> Organs = organs;
    /// <summary>Категории органов, которых не хватает (их можно вставить).</summary>
    public readonly List<string> Missing = missing;
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiState(string patient, string conditions, string held, List<SurgeryPartState> parts) : BoundUserInterfaceState
{
    public readonly string Patient = patient;
    public readonly string Conditions = conditions;
    public readonly string Held = held;
    public readonly List<SurgeryPartState> Parts = parts;
}

/// <summary>Выполнить шаг операции инструментом из руки.</summary>
[Serializable, NetSerializable]
public sealed class SurgeryStepMessage(NetEntity part, string surgery, string step) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly string Surgery = surgery;
    public readonly string Step = step;
}

/// <summary>Достать орган (гемостатом).</summary>
[Serializable, NetSerializable]
public sealed class SurgeryRemoveOrganMessage(NetEntity part, NetEntity organ) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly NetEntity Organ = organ;
}

/// <summary>Вставить орган из руки в часть тела.</summary>
[Serializable, NetSerializable]
public sealed class SurgeryInsertOrganMessage(NetEntity part) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
}

/// <summary>Пришить конечность из руки (отрезанную или протез) к вскрытой части тела.</summary>
[Serializable, NetSerializable]
public sealed class SurgeryAttachLimbMessage(NetEntity part) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
}

[Serializable, NetSerializable]
public enum SurgeryActionType : byte
{
    Step,
    RemoveOrgan,
    InsertOrgan,
    AttachLimb,
}

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : DoAfterEvent
{
    [DataField]
    public SurgeryActionType Action;

    [DataField]
    public NetEntity Part;

    [DataField]
    public string Surgery = string.Empty;

    [DataField]
    public string Step = string.Empty;

    [DataField]
    public NetEntity Organ;

    private SurgeryDoAfterEvent()
    {
    }

    public SurgeryDoAfterEvent(SurgeryActionType action, NetEntity part, string surgery = "", string step = "", NetEntity organ = default)
    {
        Action = action;
        Part = part;
        Surgery = surgery;
        Step = step;
        Organ = organ;
    }

    public override DoAfterEvent Clone() => this;
}
