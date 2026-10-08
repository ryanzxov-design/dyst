// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Armor.Penetration;

/// <summary>
/// Класс защиты брони (Бр1–Бр6): острая броня в мм и тупая в МПа.
/// Броня ссылается на класс через <see cref="ArmorRatingComponent.ArmorClass"/>, числа меняются в одном месте.
/// </summary>
[Prototype]
public sealed partial class ArmorClassPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Название класса («Бр4»).
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Что класс держит по стандарту (испытательный патрон).
    /// </summary>
    [DataField]
    public LocId? Threat;

    /// <summary>
    /// Острая броня, мм.
    /// </summary>
    [DataField(required: true)]
    public float Sharp;

    /// <summary>
    /// Тупая броня, МПа.
    /// </summary>
    [DataField(required: true)]
    public float Blunt;
}
