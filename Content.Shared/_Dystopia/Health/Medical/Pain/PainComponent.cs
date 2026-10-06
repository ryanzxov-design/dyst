// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Medical.Pain;

[Serializable, NetSerializable]
public enum PainLevel : byte
{
    None = 0,
    Mild = 1,
    Moderate = 2,
    Severe = 3,
    Shock = 4,
}

/// <summary>
/// Боль тела: складывается из ран, переломов, повреждённых органов и жгутов; глушится обезболивающими.
/// Сильная боль замедляет, болевой шок валит с ног. Все числа настраиваются в YAML.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PainComponent : Component
{
    /// <summary>Текущая ощущаемая боль (после обезболивания).</summary>
    [ViewVariables, AutoNetworkedField]
    public float Pain;

    /// <summary>Боль без обезболивания (для анализатора).</summary>
    [ViewVariables, AutoNetworkedField]
    public float RawPain;

    /// <summary>Сколько боли сейчас глушат лекарства.</summary>
    [ViewVariables, AutoNetworkedField]
    public float Suppression;

    [ViewVariables, AutoNetworkedField]
    public PainLevel Level = PainLevel.None;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan NextShock;

    [ViewVariables]
    public TimeSpan NextComplaint;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>Боль на единицу тяжести ран, по типу части (голова и кисти чувствительнее).</summary>
    [DataField]
    public Dictionary<BodyPartType, float> PainPerSeverity = new()
    {
        { BodyPartType.Head, 1.2f },
        { BodyPartType.Chest, 0.8f },
        { BodyPartType.Groin, 0.9f },
        { BodyPartType.Arm, 0.7f },
        { BodyPartType.Hand, 0.9f },
        { BodyPartType.Leg, 0.7f },
        { BodyPartType.Foot, 0.9f },
    };

    /// <summary>Добавочная боль от кости части по стадии.</summary>
    [DataField]
    public Dictionary<BoneSeverity, float> BonePain = new()
    {
        { BoneSeverity.Damaged, 4f },
        { BoneSeverity.Cracked, 10f },
        { BoneSeverity.Broken, 25f },
    };

    /// <summary>Добавочная боль от повреждённого органа.</summary>
    [DataField]
    public Dictionary<OrganSeverity, float> OrganPain = new()
    {
        { OrganSeverity.Damaged, 10f },
        { OrganSeverity.Destroyed, 25f },
    };

    /// <summary>Боль от конечности, которая отмирает под жгутом.</summary>
    [DataField]
    public float NecrosisPain = 15f;

    /// <summary>Повреждённые нервы части усиливают её боль.</summary>
    [DataField]
    public float NerveDamageMultiplier = 1.5f;

    /// <summary>Под шиной сломанная кость болит меньше.</summary>
    [DataField]
    public float SplintPainMultiplier = 0.5f;

    /// <summary>Обезболивание: сколько боли глушит единица вещества в крови.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Painkillers = new()
    {
        { "Epinephrine", 1.5f },
        { "Inaprovaline", 0.6f },
        { "Tricordrazine", 0.4f },
        { "Omnizine", 2f },
        { "Synaptizine", 1f },
        { "Ethanol", 0.3f },
        { "SpaceDrugs", 1f },
        { "Ketamine", 3f },
        { "Paracetamol", 0.8f },
        { "Tramadol", 2f },
        { "Morphine", 4f },
        { "Promedol", 5f },
        { "Propofol", 1.5f },
        { "Midazolam", 0.5f },
    };

    [DataField]
    public float MaxSuppression = 90f;

    /// <summary>Растворы тела, в которых ищем обезболивающее.</summary>
    [DataField]
    public List<string> PainkillerSolutions = new() { "bloodstream", "chemicals" };

    /// <summary>Как быстро ощущаемая боль догоняет настоящую (в секунду).</summary>
    [DataField]
    public float AdjustRate = 8f;

    /// <summary>Пороги уровней боли.</summary>
    [DataField]
    public Dictionary<PainLevel, float> Thresholds = new()
    {
        { PainLevel.Mild, 15f },
        { PainLevel.Moderate, 35f },
        { PainLevel.Severe, 60f },
        { PainLevel.Shock, 90f },
    };

    /// <summary>Замедление по уровню боли.</summary>
    [DataField]
    public Dictionary<PainLevel, float> SpeedModifier = new()
    {
        { PainLevel.Moderate, 0.92f },
        { PainLevel.Severe, 0.8f },
        { PainLevel.Shock, 0.7f },
    };

    [DataField]
    public TimeSpan ShockDuration = TimeSpan.FromSeconds(8);

    /// <summary>Не чаще одного болевого шока за это время.</summary>
    [DataField]
    public TimeSpan ShockCooldown = TimeSpan.FromSeconds(40);

    /// <summary>Как часто человек жалуется на боль (сообщение себе).</summary>
    [DataField]
    public TimeSpan ComplaintInterval = TimeSpan.FromSeconds(25);
}
