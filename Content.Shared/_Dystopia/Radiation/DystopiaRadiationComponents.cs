// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: дополнения к ванильной радиации.
//
// Заражение: радиоактивные вещества в лужах (пыль после аварии, пролитый радий...) светят, пока лужа лежит. Лужу
// собирают шваброй (грязная вода в ведре уже не светит, но вылитая на пол — снова лужа) или нейтрализуют
// распылителем с космическим очистителем. Какие вещества и насколько светят — прототип dystopiaContamination.
//
// Лучевая болезнь: каждый, кого облучает (с учётом защиты костюма), набирает дозу. Выше порога — болезнь
// (DiseaseRadiationSickness): чем больше доза, тем она тяжелее. Доза медленно спадает сама, быстрее — с
// противорадиационными препаратами в крови; йодид калия часть облучения не пускает.

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Radiation;

/// <summary>Что светит в лужах и насколько.</summary>
[Prototype("dystopiaContamination")]
public sealed partial class DystopiaContaminationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Вещество → рад/с на единицу в луже.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, float> Reagents = new();

    /// <summary>Слабее — лужа не светит (не плодим источники из пылинок).</summary>
    [DataField]
    public float MinIntensity = 0.05f;

    /// <summary>Как часто пересчитывать лужи, секунды.</summary>
    [DataField]
    public float Interval = 2f;
}

/// <summary>Лужа светит из-за радиоактивного вещества (источник поставлен системой заражения, она же его снимет).</summary>
[RegisterComponent]
public sealed partial class DystopiaContaminatedComponent : Component;

/// <summary>Лучевая болезнь: пороги дозы, спад, препараты (прототип Default).</summary>
[Prototype("dystopiaRadiationSickness")]
public sealed partial class DystopiaRadiationSicknessPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>С какой дозы начинается болезнь, рад.</summary>
    [DataField]
    public float Threshold = 40f;

    /// <summary>При какой дозе болезнь в полной силе, рад.</summary>
    [DataField]
    public float Severe = 400f;

    /// <summary>Больше этой дозы не набирается (лечение не растягивается на часы), рад.</summary>
    [DataField]
    public float MaxDose = 800f;

    /// <summary>Спад дозы: рад/с плюс доля от дозы в секунду.</summary>
    [DataField]
    public float DecayFlat = 0.02f;

    [DataField]
    public float DecayFraction = 0.0005f;

    /// <summary>Препарат в крови → во сколько раз быстрее спадает доза.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Medicines = new();

    /// <summary>Препарат в крови → какую долю облучения не пускает (берётся самый сильный).</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Protectors = new();

    [DataField(required: true)]
    public EntProtoId Disease;

    /// <summary>Как часто пересчитывать дозу и болезнь, секунды.</summary>
    [DataField]
    public float Interval = 2f;
}

/// <summary>Набранная доза облучения (ставится при первом облучении). Видна анализатору здоровья.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DystopiaRadiationDoseComponent : Component
{
    /// <summary>Доза, рад (с учётом защиты).</summary>
    [DataField, AutoNetworkedField]
    public float Dose;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public EntityUid? DiseaseUid;
}
