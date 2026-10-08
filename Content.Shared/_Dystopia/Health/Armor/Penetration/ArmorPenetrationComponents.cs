// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Armor.Penetration;

/// <summary>
/// Пробитие брони: у пули, у оружия ближнего боя, у существа (удар без оружия).
/// Если компонента нет — пробитие считается из урона по настройкам <see cref="ArmorPenetrationSettingsPrototype"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ArmorPenetrationComponent : Component
{
    /// <summary>
    /// Острое пробитие, мм.
    /// </summary>
    [DataField]
    public float Sharp;

    /// <summary>
    /// Тупое пробитие, МПа: давит на тупую броню и решает, сколько урона даст рикошет.
    /// </summary>
    [DataField]
    public float Blunt;
}

/// <summary>
/// Рейтинг брони. На одежде — броня закрытых ею частей тела, на самом теле — естественная броня
/// (последний слой). Обычно задаётся классом защиты; не задано — для одежды считается из её обычных коэффициентов.
/// </summary>
[RegisterComponent]
public sealed partial class ArmorRatingComponent : Component
{
    /// <summary>
    /// Класс защиты (Бр1–Бр6): острая и тупая броня берутся из него. Sharp/Blunt, если заданы, важнее класса.
    /// </summary>
    [DataField]
    public ProtoId<ArmorClassPrototype>? ArmorClass;

    /// <summary>
    /// Острая броня, мм.
    /// </summary>
    [DataField]
    public float? Sharp;

    /// <summary>
    /// Тупая броня, МПа.
    /// </summary>
    [DataField]
    public float? Blunt;

    /// <summary>
    /// Множители брони по частям тела: шея у жилета, лицо у шлема и т.п. Нет в списке — 1.
    /// </summary>
    [DataField]
    public Dictionary<BodyPartType, float> PartMultipliers = new();
}
