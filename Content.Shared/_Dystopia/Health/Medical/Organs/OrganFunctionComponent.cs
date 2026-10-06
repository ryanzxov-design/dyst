// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Organs;

/// <summary>
/// Работа органов тела: повреждённый орган работает хуже (доля от целостности) и даёт последствия.
/// Органы понемногу восстанавливаются сами, лекарства ускоряют. Всё настраивается в YAML.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganFunctionComponent : Component
{
    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public Dictionary<string, TimeSpan> NextWarning = new();

    [ViewVariables]
    public bool EyesImpaired;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Урон за проверку от органа, который не справляется, по категории органа.
    /// Умножается на долю «неработы» (орган на 40% → 0.6 урона от указанного).
    /// </summary>
    [DataField]
    public Dictionary<string, DamageSpecifier> FailureDamage = new()
    {
        { "Lungs", new DamageSpecifier { DamageDict = { { "Asphyxiation", 4 } } } },
        { "Heart", new DamageSpecifier { DamageDict = { { "Asphyxiation", 3 } } } },
        { "Liver", new DamageSpecifier { DamageDict = { { "Poison", 1.5 } } } },
        { "Kidneys", new DamageSpecifier { DamageDict = { { "Poison", 1 } } } },
    };

    /// <summary>Повреждённое сердце усиливает кровотечение: до такого множителя при полностью нерабочем сердце.</summary>
    [DataField]
    public float HeartBleedMultiplier = 1.6f;

    /// <summary>Органы, разрушение которых убивает (мозг).</summary>
    [DataField]
    public List<string> FatalOrgans = new() { "Brain" };

    /// <summary>Повреждённые глаза: столько урона глазам (размытие) держится, пока глаза не восстановятся.</summary>
    [DataField]
    public int ImpairedEyeDamage = 3;

    /// <summary>С какой доли работы органа человек чувствует, что что-то не так (сообщения).</summary>
    [DataField]
    public float WarningEfficiency = 0.75f;

    [DataField]
    public TimeSpan WarningInterval = TimeSpan.FromSeconds(40);

    /// <summary>Сколько целостности органа восстанавливается в секунду само по себе (у живого).</summary>
    [DataField]
    public float RecoveryPerSecond = 0.15f;

    /// <summary>Ускорение восстановления органов: прибавка к множителю за единицу вещества в крови.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> RecoveryBoost = new()
    {
        { "Omnizine", 0.25f },
        { "Tricordrazine", 0.05f },
        { "Cryoxadone", 0.3f },
        { "Doxarubixadone", 0.3f },
    };

    [DataField]
    public float MaxRecoveryMultiplier = 6f;

    /// <summary>Точечные лекарства органов: категория органа → вещество → прибавка к множителю за единицу.</summary>
    [DataField]
    public Dictionary<string, Dictionary<ProtoId<ReagentPrototype>, float>> OrganRecoveryBoost = new()
    {
        { "Liver", new() { { "Ademetionine", 0.4f } } },
        { "Lungs", new() { { "Salbutamol", 0.4f } } },
        { "Heart", new() { { "Digoxin", 0.4f } } },
        { "Brain", new() { { "Piracetam", 0.4f } } },
        { "Eyes", new() { { "Oculine", 0.4f } } },
    };

    /// <summary>Вещества, которые в большой дозе бьют по органу: сколько целостности снимают за проверку.</summary>
    [DataField]
    public List<OrganToxin> OrganToxins = new()
    {
        new OrganToxin { Reagent = "Paracetamol", Organ = "Liver", Threshold = 25f, Damage = 6f },
        new OrganToxin { Reagent = "Digoxin", Organ = "Heart", Threshold = 15f, Damage = 8f },
        new OrganToxin { Reagent = "Ethanol", Organ = "Liver", Threshold = 40f, Damage = 2f },
    };

    /// <summary>Лекарства костей: прибавка к скорости сращивания под шиной за единицу вещества.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> BoneHealBoost = new()
    {
        { "Calcitonin", 0.15f },
    };

    /// <summary>Предел ускорения сращивания лекарствами.</summary>
    [DataField]
    public float MaxBoneHealMultiplier = 3f;

    /// <summary>
    /// С лекарством костей кость срастается и без шины, но медленно (целостность в секунду),
    /// если вещества в крови не меньше BoneHealMinimum.
    /// </summary>
    [DataField]
    public float BoneHealWithoutSplint = 0.03f;

    [DataField]
    public float BoneHealMinimum = 5f;

    /// <summary>Лекарства нервов: шанс за проверку на единицу вещества снять одно повреждение нервов.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> NerveRepair = new()
    {
        { "Ipidacrine", 0.02f },
    };

    [DataField]
    public List<string> ReagentSolutions = new() { "bloodstream", "chemicals" };
}

[DataDefinition]
public sealed partial class OrganToxin
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>Категория органа (Liver, Heart...).</summary>
    [DataField(required: true)]
    public string Organ = string.Empty;

    /// <summary>С какого количества в крови начинается вред.</summary>
    [DataField]
    public float Threshold = 20f;

    /// <summary>Сколько целостности органа снимается за проверку.</summary>
    [DataField]
    public float Damage = 5f;
}
