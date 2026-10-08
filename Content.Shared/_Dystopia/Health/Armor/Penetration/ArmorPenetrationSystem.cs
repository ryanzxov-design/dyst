// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: пробитие брони по образцу Combat Extended (RimWorld, ArmorUtilityCE).
//
// Удар идёт по слоям: одежда, закрывающая поражённую часть, от внешней к внутренней, затем естественная броня тела.
// Острый урон на каждом слое: броня больше пробития — рикошет (острый урон становится тупым ударом, который
// встречает тот же слой ещё раз и дальше идёт внутрь). Иначе урон уменьшается на долю потерянного пробития,
// а «недопробитая» часть даёт отдельный тупой удар по всем слоям. Тупой урон давит на тупую броню без рикошета.
// «Средовой» урон (жар, лазеры) — процентом, как огонь. Остальные виды — обычная броня по коэффициентам.
// Без износа брони, без падения пробития с расстоянием.

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Armor;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Dystopia.Health.Armor.Penetration;

public sealed partial class ArmorPenetrationSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ArmorCoverageSystem _coverage = default!;
    [Dependency] private WoundSystem _wounds = default!;

    private static readonly ProtoId<ArmorPenetrationSettingsPrototype> SettingsId = "Default";

    /// <summary>
    /// Чем наносится текущий урон: снаряд или оружие ближнего боя (сам атакующий — удар без оружия).
    /// Источник урона в событии — стрелок, поэтому пулю передаём отдельно, на время нанесения урона.
    /// </summary>
    private EntityUid? _source;

    private readonly List<(int Order, Layer Layer)> _layerBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, DamageModifyEvent>(OnBodyDamageModify);
    }

    /// <summary>
    /// Задать снаряд или оружие для урона, который сейчас будет нанесён. Возвращает прежнее значение —
    /// его нужно вернуть после нанесения урона.
    /// </summary>
    public EntityUid? SetSource(EntityUid? source)
    {
        var previous = _source;
        _source = source;
        return previous;
    }

    private ArmorPenetrationSettingsPrototype? Settings => _proto.TryIndex(SettingsId, out var settings) ? settings : null;

    /// <summary>
    /// Этот вид урона по живому телу считает эта система (обычные коэффициенты брони к нему не применяются).
    /// </summary>
    public bool IsHandledType(ProtoId<DamageTypePrototype> type)
    {
        return Settings is { } settings && IsHandledType(settings, type);
    }

    /// <summary>
    /// Острый или тупой урон: броня держит его в мм/МПа, а не процентом.
    /// </summary>
    public bool IsRatedType(ProtoId<DamageTypePrototype> type)
    {
        return Settings is { } settings && (settings.SharpTypes.Contains(type) || settings.BluntTypes.Contains(type));
    }

    private static bool IsHandledType(ArmorPenetrationSettingsPrototype settings, ProtoId<DamageTypePrototype> type)
    {
        return settings.SharpTypes.Contains(type) || settings.BluntTypes.Contains(type) || settings.AmbientTypes.ContainsKey(type);
    }

    /// <summary>
    /// Обычная броня уже посчитала урон по своим коэффициентам: вернуть виды, которые считает эта система,
    /// к значениям до неё. Только для тех, у кого есть тело.
    /// </summary>
    public void RestoreHandled(EntityUid wearer, DamageSpecifier before, DamageSpecifier after)
    {
        if (!HasComp<BodyComponent>(wearer) || Settings is not { } settings)
            return;

        foreach (var (type, value) in before.DamageDict)
        {
            if (IsHandledType(settings, type))
                after.DamageDict[type] = value;
        }
    }

    // ===================== Урон =====================

    private void OnBodyDamageModify(EntityUid body, BodyComponent component, DamageModifyEvent args)
    {
        // Источник относится ровно к этому урону: вложенный урон (например, от разрушения) его не наследует
        var source = _source;
        _source = null;

        if (Settings is not { } settings)
            return;

        var damage = args.Damage;
        var sharpTotal = 0f;
        var any = false;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (value <= 0 || !IsHandledType(settings, type))
                continue;

            any = true;
            if (settings.SharpTypes.Contains(type))
                sharpTotal += value.Float();
        }

        if (!any)
            return;

        var part = _wounds.PredictHitPartType(body, args.Origin, damage) ?? settings.AreaPart;
        var layers = GetLayers(body, part, settings);
        var (sharpPen, bluntPen) = GetPenetration(source, damage, settings);

        var result = new DamageSpecifier(damage);
        var extraBlunt = 0f;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (value <= 0)
                continue;

            var amount = value.Float();
            if (settings.SharpTypes.Contains(type))
            {
                // Несколько видов острого урона в одном ударе делят между собой один рикошет
                var share = sharpTotal > 0f ? amount / sharpTotal : 1f;
                var (left, blunt) = ApplySharp(amount, sharpPen, bluntPen, share, layers, settings);
                SetOrRemove(result, type, left);
                extraBlunt += blunt;
            }
            else if (settings.BluntTypes.Contains(type))
            {
                SetOrRemove(result, type, ApplyBlunt(amount, bluntPen, layers, 0));
            }
            else if (settings.AmbientTypes.TryGetValue(type, out var ambientPen))
            {
                SetOrRemove(result, type, amount * GetAmbientMultiplier(type, ambientPen, layers));
            }
        }

        if (extraBlunt > 0f)
        {
            result.DamageDict[settings.DeflectType] =
                result.DamageDict.GetValueOrDefault(settings.DeflectType) + FixedPoint2.New(extraBlunt);
        }

        args.Damage = result;
    }

    /// <summary>
    /// Полностью погашенный вид урона убираем: удар, который броня целиком остановила, не считается нанесённым.
    /// </summary>
    private static void SetOrRemove(DamageSpecifier damage, ProtoId<DamageTypePrototype> type, float value)
    {
        var amount = FixedPoint2.New(value);
        if (amount > FixedPoint2.Zero)
            damage.DamageDict[type] = amount;
        else
            damage.DamageDict.Remove(type);
    }

    /// <summary>
    /// Острый удар сквозь слои. Возвращает острый урон, который дошёл до тела, и тупой урон
    /// от рикошета или частичного пробития (уже прошедший броню).
    /// </summary>
    private (float Sharp, float Blunt) ApplySharp(float damage, float pen, float sourceBluntPen, float share,
        List<Layer> layers, ArmorPenetrationSettingsPrototype settings)
    {
        var originalDamage = damage;
        var originalPen = pen;

        for (var i = 0; i < layers.Count; i++)
        {
            if (TryPenetrate(true, layers[i].Sharp, ref pen, ref damage))
            {
                if (damage <= 0f)
                    return (0f, 0f);

                continue;
            }

            // Рикошет: тупой удар с долей тупого пробития по оставшемуся острому, тот же слой ещё раз и внутрь
            var penMulti = originalPen > 0f ? pen / originalPen : 0f;
            var deflectPen = sourceBluntPen * penMulti;
            var deflectDamage = GetDeflectDamage(deflectPen, settings) * share;
            return (0f, ApplyBlunt(deflectDamage, deflectPen, layers, i));
        }

        // Частичное пробитие: потерянное на броне превращается в тупой удар по всем слоям
        var partial = 0f;
        if (originalDamage > damage && originalPen > 0f && originalDamage > 0f)
        {
            var penMulti = (originalPen - pen) * (originalDamage - damage) / originalDamage / originalPen;
            var partialPen = sourceBluntPen * penMulti;
            partial = ApplyBlunt(GetDeflectDamage(partialPen, settings) * share, partialPen, layers, 0);
        }

        return (damage, partial);
    }

    private static float ApplyBlunt(float damage, float pen, List<Layer> layers, int start)
    {
        for (var i = start; i < layers.Count; i++)
        {
            if (!TryPenetrate(false, layers[i].Blunt, ref pen, ref damage) || damage <= 0f)
                return 0f;
        }

        return damage;
    }

    /// <summary>
    /// Один слой брони (CE TryPenetrateArmor). false — удар не прошёл: острый срикошетил, тупой погас полностью.
    /// При рикошете острого урон и пробитие не меняются — из них считается тупой удар.
    /// </summary>
    private static bool TryPenetrate(bool sharp, float armor, ref float pen, ref float damage)
    {
        if (armor <= 0f)
            return true;

        var deflected = sharp && armor > pen;
        var newPen = pen - armor;
        // В CE при нулевом пробитии урон проходит целиком; у нас нулевое пробитие против брони не проходит
        var multiplier = pen > 0f ? Math.Clamp(newPen / pen, 0f, 1f) : 0f;
        deflected |= multiplier <= 0f;

        if (!deflected || !sharp)
        {
            damage = Math.Max(0f, damage * multiplier);
            pen = Math.Max(0f, newPen);
        }

        return !deflected;
    }

    private static float GetDeflectDamage(float bluntPen, ArmorPenetrationSettingsPrototype settings)
    {
        if (bluntPen <= 0f || settings.DeflectDivisor <= 0f)
            return 0f;

        return MathF.Pow(bluntPen * settings.DeflectScale, settings.DeflectPower) / settings.DeflectDivisor
               * settings.DeflectDamageMultiplier;
    }

    /// <summary>
    /// Огонь и подобное (CE GetAmbientPostArmorDamage): множитель = 1 + пробитие − сумма защиты слоёв, от 0 до 1.
    /// </summary>
    private static float GetAmbientMultiplier(ProtoId<DamageTypePrototype> type, float pen, List<Layer> layers)
    {
        var multiplier = 1f + Math.Max(0f, pen);
        foreach (var layer in layers)
        {
            if (layer.Armor is not { } armor)
                continue;

            // Поле брони закрыто для чужих систем: читаем словарь в локальную переменную (только чтение)
            var coefficients = armor.Modifiers.Coefficients;
            if (!coefficients.TryGetValue(type, out var coefficient))
                continue;

            multiplier -= Math.Max(0f, 1f - coefficient);
            if (multiplier <= 0f)
                return 0f;
        }

        return Math.Min(1f, multiplier);
    }

    // ===================== Слои брони =====================

    private readonly record struct Layer(float Sharp, float Blunt, ArmorComponent? Armor);

    /// <summary>
    /// Слои брони на этой части тела: одежда от внешней к внутренней, последней — естественная броня тела.
    /// </summary>
    private List<Layer> GetLayers(EntityUid body, BodyPartType part, ArmorPenetrationSettingsPrototype settings)
    {
        _layerBuffer.Clear();
        if (_inventory.TryGetContainerSlotEnumerator(body, out var slots))
        {
            var index = 0;
            while (slots.NextItem(out var item, out var slot))
            {
                index++;
                if ((slot.SlotFlags & SlotFlags.POCKET) != 0)
                    continue;

                TryComp<ArmorComponent>(item, out var armor);
                TryComp<ArmorRatingComponent>(item, out var rating);
                if (armor == null && rating == null)
                    continue;

                // Поднятая маска не защищает (как у обычной брони)
                if (TryComp<MaskComponent>(item, out var mask) && mask.IsToggled)
                    continue;

                if (!_coverage.GetCoverage(item).Contains(part))
                    continue;

                var (sharp, blunt) = GetRating(armor, rating, part, settings);
                var order = settings.LayerOrder.IndexOf(slot.Name);
                if (order < 0)
                    order = settings.LayerOrder.Count + index;

                _layerBuffer.Add((order, new Layer(sharp, blunt, armor)));
            }
        }

        var layers = _layerBuffer.OrderBy(l => l.Order).Select(l => l.Layer).ToList();
        if (TryComp<ArmorRatingComponent>(body, out var natural))
        {
            var multiplier = natural.PartMultipliers.GetValueOrDefault(part, 1f);
            layers.Add(new Layer((natural.Sharp ?? 0f) * multiplier, (natural.Blunt ?? 0f) * multiplier, null));
        }

        return layers;
    }

    /// <summary>
    /// Рейтинг вещи на этой части: заданный или посчитанный из коэффициентов обычной брони.
    /// </summary>
    public (float Sharp, float Blunt) GetRating(EntityUid item, BodyPartType? part = null)
    {
        if (Settings is not { } settings)
            return (0f, 0f);

        TryComp<ArmorComponent>(item, out var armor);
        TryComp<ArmorRatingComponent>(item, out var rating);
        return GetRating(armor, rating, part, settings);
    }

    private static (float Sharp, float Blunt) GetRating(ArmorComponent? armor, ArmorRatingComponent? rating, BodyPartType? part,
        ArmorPenetrationSettingsPrototype settings)
    {
        var sharp = rating?.Sharp ?? DeriveRating(armor, settings.SharpRating);
        var blunt = rating?.Blunt ?? DeriveRating(armor, settings.BluntRating);
        var multiplier = 1f;
        if (part is { } partType && rating != null)
            multiplier = rating.PartMultipliers.GetValueOrDefault(partType, 1f);

        return (Math.Max(0f, sharp * multiplier), Math.Max(0f, blunt * multiplier));
    }

    private static float DeriveRating(ArmorComponent? armor, RatingDerivation derivation)
    {
        if (armor == null || derivation.DamageType is not { } type)
            return 0f;

        var coefficients = armor.Modifiers.Coefficients;
        if (!coefficients.TryGetValue(type, out var coefficient))
            return 0f;

        return Math.Max(0f, 1f - coefficient) * derivation.PerProtection;
    }

    // ===================== Пробитие =====================

    private (float Sharp, float Blunt) GetPenetration(EntityUid? source, DamageSpecifier damage, ArmorPenetrationSettingsPrototype settings)
    {
        if (source is { } uid && TryComp<ArmorPenetrationComponent>(uid, out var penetration))
            return (penetration.Sharp, penetration.Blunt);

        var derivation = source is not { } src ? settings.Fallback
            : HasComp<ProjectileComponent>(src) ? settings.Projectile
            : settings.Melee;
        return Derive(damage, derivation, settings);
    }

    private static (float Sharp, float Blunt) Derive(DamageSpecifier damage, PenetrationDerivation derivation,
        ArmorPenetrationSettingsPrototype settings)
    {
        var sharp = 0f;
        var blunt = 0f;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (value <= 0)
                continue;

            if (settings.SharpTypes.Contains(type))
                sharp += value.Float();
            else if (settings.BluntTypes.Contains(type))
                blunt += value.Float();
        }

        return (sharp * derivation.SharpPerSharpDamage,
            blunt * derivation.BluntPerBluntDamage + sharp * derivation.BluntPerSharpDamage);
    }

    // ===================== Осмотр =====================

    /// <summary>
    /// Пробитие оружия ближнего боя (или удара без оружия) с этим уроном.
    /// </summary>
    public void AddMeleeExamine(FormattedMessage message, EntityUid weapon, DamageSpecifier damage)
    {
        if (Settings is not { } settings)
            return;

        var (sharp, blunt) = TryComp<ArmorPenetrationComponent>(weapon, out var penetration)
            ? (penetration.Sharp, penetration.Blunt)
            : Derive(damage, settings.Melee, settings);
        AddPenetrationExamine(message, sharp, blunt);
    }

    /// <summary>
    /// Пробитие пули по её прототипу (осмотр патрона).
    /// </summary>
    public void AddProjectileExamine(FormattedMessage message, EntProtoId projectile)
    {
        if (Settings is not { } settings || !_proto.TryIndex(projectile, out var proto))
            return;

        float sharp, blunt;
        if (proto.TryComp<ArmorPenetrationComponent>(out var penetration, EntityManager.ComponentFactory))
            (sharp, blunt) = (penetration.Sharp, penetration.Blunt);
        else if (proto.TryComp<ProjectileComponent>(out var projectileComp, EntityManager.ComponentFactory))
            (sharp, blunt) = Derive(projectileComp.Damage, settings.Projectile, settings);
        else
            return;

        AddPenetrationExamine(message, sharp, blunt);
    }

    private void AddPenetrationExamine(FormattedMessage message, float sharp, float blunt)
    {
        if (sharp <= 0f && blunt <= 0f)
            return;

        if (!message.IsEmpty)
            message.PushNewline();

        message.AddMarkupOrThrow(Loc.GetString("armor-penetration-examine",
            ("sharp", MathF.Round(sharp, 1)),
            ("blunt", MathF.Round(blunt, 1))));
    }

    /// <summary>
    /// Рейтинг брони при осмотре: мм и МПа, с оговоркой для частей, где броня слабее или сильнее.
    /// </summary>
    public void AddArmorExamine(EntityUid item, FormattedMessage message)
    {
        if (Settings is not { } settings)
            return;

        TryComp<ArmorComponent>(item, out var armor);
        TryComp<ArmorRatingComponent>(item, out var rating);
        var (sharp, blunt) = GetRating(armor, rating, null, settings);
        if (sharp <= 0f && blunt <= 0f)
            return;

        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("armor-rating-examine",
            ("sharp", MathF.Round(sharp, 1)),
            ("blunt", MathF.Round(blunt, 1))));

        if (rating == null)
            return;

        foreach (var (part, multiplier) in rating.PartMultipliers)
        {
            message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString("armor-rating-part-examine",
                ("type", Loc.GetString($"armor-coverage-type-{part.ToString().ToLowerInvariant()}")),
                ("percent", MathF.Round(multiplier * 100f))));
        }
    }
}
