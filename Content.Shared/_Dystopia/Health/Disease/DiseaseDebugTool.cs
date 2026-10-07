// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: отладочный инструмент «Болезни» — быстро проверить заражение, течение болезни, иммунитет,
// мутации и передачу. Щелчок по цели (или в руке — по себе) открывает окно.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Disease;

[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseDebugToolComponent : Component
{
    /// <summary>Кого сейчас лечим/заражаем.</summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>Радиус «чиха» — принудительной передачи болезней окружающим.</summary>
    [DataField]
    public float SpreadRange = 3f;
}

[Serializable, NetSerializable]
public enum DiseaseDebugUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DiseaseDebugEffectEntry(string name, float severity)
{
    public readonly string Name = name;
    public readonly float Severity = severity;
}

[Serializable, NetSerializable]
public sealed class DiseaseDebugEntry(
    NetEntity disease,
    string name,
    string type,
    int genotype,
    float infection,
    float immunity,
    float infectionRate,
    float mutationRate,
    float complexity,
    List<DiseaseDebugEffectEntry> effects)
{
    public readonly NetEntity Disease = disease;
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly int Genotype = genotype;
    public readonly float Infection = infection;
    public readonly float Immunity = immunity;
    public readonly float InfectionRate = infectionRate;
    public readonly float MutationRate = mutationRate;
    public readonly float Complexity = complexity;
    public readonly List<DiseaseDebugEffectEntry> Effects = effects;
}

[Serializable, NetSerializable]
public sealed class DiseaseDebugPreset(string id, string name)
{
    public readonly string Id = id;
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class DiseaseDebugBuiState(
    string targetName,
    bool hasTarget,
    bool canCarry,
    List<DiseaseDebugEntry> diseases,
    List<DiseaseDebugPreset> presets,
    List<int> immuneTo) : BoundUserInterfaceState
{
    public readonly string TargetName = targetName;
    public readonly bool HasTarget = hasTarget;
    public readonly bool CanCarry = canCarry;
    public readonly List<DiseaseDebugEntry> Diseases = diseases;
    public readonly List<DiseaseDebugPreset> Presets = presets;
    public readonly List<int> ImmuneTo = immuneTo;
}

/// <summary>Заразить болезнью-прототипом (force — даже при иммунитете).</summary>
[Serializable, NetSerializable]
public sealed class DiseaseDebugInfectMessage(string proto, bool force) : BoundUserInterfaceMessage
{
    public readonly string Proto = proto;
    public readonly bool Force = force;
}

/// <summary>Заразить случайной болезнью этой сложности.</summary>
[Serializable, NetSerializable]
public sealed class DiseaseDebugInfectRandomMessage(float complexity) : BoundUserInterfaceMessage
{
    public readonly float Complexity = complexity;
}

[Serializable, NetSerializable]
public sealed class DiseaseDebugCureMessage(NetEntity disease) : BoundUserInterfaceMessage
{
    public readonly NetEntity Disease = disease;
}

[Serializable, NetSerializable]
public sealed class DiseaseDebugCureAllMessage : BoundUserInterfaceMessage;

/// <summary>Сдвинуть прогресс инфекции или иммунитета.</summary>
[Serializable, NetSerializable]
public sealed class DiseaseDebugProgressMessage(NetEntity disease, float infection, float immunity) : BoundUserInterfaceMessage
{
    public readonly NetEntity Disease = disease;
    public readonly float Infection = infection;
    public readonly float Immunity = immunity;
}

[Serializable, NetSerializable]
public sealed class DiseaseDebugMutateMessage(NetEntity disease) : BoundUserInterfaceMessage
{
    public readonly NetEntity Disease = disease;
}

/// <summary>Забыть все приобретённые иммунитеты (переболел, вакцина).</summary>
[Serializable, NetSerializable]
public sealed class DiseaseDebugClearImmunityMessage : BoundUserInterfaceMessage;

/// <summary>Принудительно попытаться передать болезни всем рядом (с учётом масок и костюмов).</summary>
[Serializable, NetSerializable]
public sealed class DiseaseDebugSpreadMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiseaseDebugRefreshMessage : BoundUserInterfaceMessage;
