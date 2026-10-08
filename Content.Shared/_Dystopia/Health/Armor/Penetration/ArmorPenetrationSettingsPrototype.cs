// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: настройки пробития брони по образцу Combat Extended (RimWorld). Все числа — здесь, в прототипе.

using Content.Shared.Body.Part;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Armor.Penetration;

/// <summary>
/// Как броня останавливает урон.
/// <list type="bullet">
/// <item>Острый урон (пули, клинки): пробитие в мм против брони в мм. Пробитие больше брони — урон проходит,
/// уменьшенный на долю потерянного пробития. Меньше или равно — рикошет: острый урон превращается в тупой удар.</item>
/// <item>Тупой урон: давление в МПа против тупой брони в МПа, урон уменьшается так же, без рикошета.</item>
/// <item>«Средовой» урон (жар, лазеры): процент, как огонь — защита всех слоёв складывается.</item>
/// </list>
/// </summary>
[Prototype]
public sealed partial class ArmorPenetrationSettingsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Острый урон: пробивает броню в мм и рикошетит.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> SharpTypes = new();

    /// <summary>
    /// Тупой урон: давит на броню в МПа.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> BluntTypes = new();

    /// <summary>
    /// «Средовой» урон и его пробитие (доля, 0 — обычный огонь). Броня гасит его процентом:
    /// множитель = 1 + пробитие − сумма защиты слоёв, защита слоя = 1 − коэффициент брони для этого вида.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> AmbientTypes = new();

    /// <summary>
    /// Во что превращается острый урон при рикошете и частичном пробитии.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DamageTypePrototype> DeflectType;

    /// <summary>
    /// Урон рикошета = (тупое пробитие × DeflectScale) ^ DeflectPower / DeflectDivisor × DeflectDamageMultiplier.
    /// </summary>
    [DataField]
    public float DeflectScale = 10000f;

    [DataField]
    public float DeflectPower = 1f / 3f;

    [DataField]
    public float DeflectDivisor = 10f;

    /// <summary>
    /// Перевод урона рикошета из единиц RimWorld в единицы нашего урона.
    /// </summary>
    [DataField]
    public float DeflectDamageMultiplier = 1f;

    /// <summary>
    /// Куда считать удар, который пришёлся по всему телу (взрыв, пожар, падение): броня какой части его встречает.
    /// </summary>
    [DataField]
    public BodyPartType AreaPart = BodyPartType.Chest;

    /// <summary>
    /// Порядок слоёв одежды от внешнего к внутреннему (по имени слота). Слоты не из списка — после них.
    /// </summary>
    [DataField]
    public List<string> LayerOrder = new();

    /// <summary>
    /// Броня без <see cref="ArmorRatingComponent"/>: рейтинг считается из её обычных коэффициентов.
    /// </summary>
    [DataField]
    public RatingDerivation SharpRating = new();

    [DataField]
    public RatingDerivation BluntRating = new();

    /// <summary>
    /// Пробитие без <see cref="ArmorPenetrationComponent"/> — из урона. Для оружия ближнего боя, ударов без оружия
    /// и брошенных предметов.
    /// </summary>
    [DataField]
    public PenetrationDerivation Melee = new();

    /// <summary>
    /// Для снарядов.
    /// </summary>
    [DataField]
    public PenetrationDerivation Projectile = new();

    /// <summary>
    /// Для урона, источник которого неизвестен (взрыв, хитскан, падение).
    /// </summary>
    [DataField]
    public PenetrationDerivation Fallback = new();
}

/// <summary>
/// Рейтинг брони из коэффициента: (1 − коэффициент вида DamageType) × PerProtection.
/// </summary>
[DataDefinition]
public sealed partial class RatingDerivation
{
    [DataField]
    public ProtoId<DamageTypePrototype>? DamageType;

    [DataField]
    public float PerProtection;
}

/// <summary>
/// Пробитие из урона: острое (мм) = острый урон × SharpPerSharpDamage,
/// тупое (МПа) = тупой урон × BluntPerBluntDamage + острый урон × BluntPerSharpDamage.
/// </summary>
[DataDefinition]
public sealed partial class PenetrationDerivation
{
    [DataField]
    public float SharpPerSharpDamage;

    [DataField]
    public float BluntPerBluntDamage;

    [DataField]
    public float BluntPerSharpDamage;
}
