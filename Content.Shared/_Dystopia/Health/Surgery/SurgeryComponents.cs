// SPDX-License-Identifier: AGPL-3.0-or-later
// Хирургия Dystopia под новую систему тела (органы — сущности в контейнере тела, связи родитель/ребёнок).

using Robust.Shared.Audio;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Surgery;

/// <summary>Вид хирургического инструмента.</summary>
[Serializable, NetSerializable]
public enum SurgeryToolKind : byte
{
    Scalpel,
    Retractor,
    Hemostat,
    Saw,
    Cautery,
    BoneSetter,
    BoneGel,
}

/// <summary>Лечебный эффект шага операции (выполняется, когда шаг сделан).</summary>
[Serializable, NetSerializable]
public enum SurgeryEffect : byte
{
    None,
    /// <summary>Срастить кость части полностью (снимает перелом, шину).</summary>
    MendBone,
    /// <summary>Восстановить повреждённые органы части (и разрушенный мозг).</summary>
    RepairOrgans,
    /// <summary>Обработать раны части: снять тяжесть ран (мимо блокировок травм), остановить кровь.</summary>
    TendWounds,
    /// <summary>Сшить сосуды: снять травму вен, остановить кровотечение части.</summary>
    RepairVessels,
    /// <summary>Сшить нервы: снять травму нервов.</summary>
    RepairNerves,
}

/// <summary>
/// Когда показывать лечебную операцию: только если на части есть что лечить этим эффектом
/// (или операция уже начата).
/// </summary>
[RegisterComponent]
public sealed partial class SurgeryConditionComponent : Component
{
    [DataField(required: true)]
    public SurgeryEffect Need;
}

/// <summary>Кость вправлена, ждёт костного геля.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBoneSetComponent : Component;

/// <summary>Сосуды пережаты, можно сшивать.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryVesselsClampedComponent : Component;

/// <summary>Операция (прототип-сущность, не спавнится): список шагов и требование предыдущей операции.</summary>
[RegisterComponent]
public sealed partial class SurgeryComponent : Component
{
    /// <summary>Порядок в списке (меньше — выше).</summary>
    [DataField]
    public int Priority;

    /// <summary>Операция, которая должна быть полностью выполнена до этой.</summary>
    [DataField]
    public EntProtoId? Requirement;

    [DataField(required: true)]
    public List<EntProtoId> Steps = new();
}

/// <summary>Шаг операции: каким инструментом, сколько секунд, что добавить/убрать на части тела.</summary>
[RegisterComponent]
public sealed partial class SurgeryStepComponent : Component
{
    /// <summary>Подходит любой из этих инструментов.</summary>
    [DataField(required: true)]
    public List<SurgeryToolKind> Tools = new();

    [DataField]
    public float Duration = 3f;

    /// <summary>Состояния, которые появятся на части тела (шаг выполнен, когда все они есть).</summary>
    [DataField]
    public ComponentRegistry Add = new();

    /// <summary>Состояния, которые исчезнут с части тела (шаг выполнен, когда ни одного нет).</summary>
    [DataField]
    public ComponentRegistry Remove = new();

    /// <summary>Изменение кровотечения пациента после шага (+ — открыть, − — остановить).</summary>
    [DataField]
    public float Bleed;

    /// <summary>Шаг отделяет эту часть тела от тела (ампутация).</summary>
    [DataField]
    public bool Amputate;

    /// <summary>Лечебный эффект, когда шаг сделан.</summary>
    [DataField]
    public SurgeryEffect Effect = SurgeryEffect.None;

    /// <summary>Сила эффекта (для «Обработать раны» — сколько тяжести ран снимается за раз).</summary>
    [DataField]
    public float EffectAmount = 40f;
}

/// <summary>Для каких частей тела доступна операция.</summary>
[RegisterComponent]
public sealed partial class SurgeryPartConditionComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<OrganCategoryPrototype>> Parts = new();
}

/// <summary>Хирургический инструмент.</summary>
[RegisterComponent]
public sealed partial class SurgeryToolComponent : Component
{
    [DataField(required: true)]
    public List<SurgeryToolKind> Kinds = new();

    /// <summary>Множитель скорости (больше — быстрее).</summary>
    [DataField]
    public float Speed = 1f;

    /// <summary>Собственный шанс ошибки инструмента.</summary>
    [DataField]
    public float FailChance = 0.03f;
}

/// <summary>Операционный стол: быстрее и меньше ошибок.</summary>
[RegisterComponent]
public sealed partial class SurgeryOperatingTableComponent : Component
{
    [DataField]
    public float SpeedMultiplier = 1f;
}

/// <summary>Пациент, у которого открыто окно операции (служебный).</summary>
[RegisterComponent]
public sealed partial class SurgeryPatientComponent : Component
{
    [ViewVariables]
    public TimeSpan NextRefresh;
}

// --- Состояния части тела во время операции (вешаются на сущность части тела: торс, голова, рука, нога) ---

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryIncisionOpenComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBleedersClampedComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySkinRetractedComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBonesSawedComponent : Component;

/// <summary>Кости вскрыты: органы части тела доступны (достать / вставить).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBonesOpenComponent : Component;

/// <summary>Деталь протеза: ходьба медленнее (множитель скорости для ноги или стопы).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryProstheticComponent : Component
{
    [DataField]
    public float SpeedMultiplier = 1f;
}

/// <summary>
/// Тело, у которого менялись конечности: скорость зависит от ног и стоп (нет ноги — ползёт, протез — медленнее).
/// Есть у всех видов (BaseSpeciesMob), так что работает при любой потере ноги, не только после операции.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryLimbLossComponent : Component
{
    /// <summary>Скорость стороны без стопы (хромота).</summary>
    [DataField]
    public float NoFootSpeed = 0.7f;

    /// <summary>Нижний предел скорости от конечностей и переломов.</summary>
    [DataField]
    public float MinimumSpeed = 0.15f;

    /// <summary>Ниже этой доли скорости (из-за переломов ног) не устоять на ногах.</summary>
    [DataField]
    public float CrippledSpeed = 1f / 3.4f;

    /// <summary>Доля, которую несёт нога с повреждённой костью (нет в списке — 1).</summary>
    [DataField]
    public Dictionary<BoneSeverity, float> LegBoneSpeed = new()
    {
        { BoneSeverity.Damaged, 0.625f },
        { BoneSeverity.Cracked, 0.5f },
        { BoneSeverity.Broken, 0f },
    };

    /// <summary>Множитель стопы с повреждённой костью.</summary>
    [DataField]
    public Dictionary<BoneSeverity, float> FootBoneSpeed = new()
    {
        { BoneSeverity.Damaged, 0.77f },
        { BoneSeverity.Cracked, 0.66f },
        { BoneSeverity.Broken, 0.55f },
    };

    /// <summary>Шанс, что рука дрогнет при ударе или выстреле, по худшей кости рук и кистей.</summary>
    [DataField]
    public Dictionary<BoneSeverity, float> FumbleChance = new()
    {
        { BoneSeverity.Cracked, 0.10f },
        { BoneSeverity.Broken, 0.25f },
    };

    [DataField]
    public SoundSpecifier FumbleSound = new SoundPathSpecifier("/Audio/Effects/slip.ogg");
}
