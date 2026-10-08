// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: броня защищает только те части тела, которые закрывает.

using System.Linq;
using Content.Shared._Dystopia.Health.Armor.Penetration;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared._Dystopia.Health.Targeting;
using Content.Shared.Armor;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Shared._Dystopia.Health.Armor;

public sealed partial class ArmorCoverageSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ArmorPenetrationSystem _penetration = default!;

    private static readonly BodyPartType[] Torso = { BodyPartType.Chest, BodyPartType.Groin, BodyPartType.Arm, BodyPartType.Leg };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArmorComponent, ArmorExamineEvent>(OnArmorExamine);
    }

    /// <summary>
    /// Применять ли броню к этому урону. Броня работает только если удар пришёлся в закрытую часть тела.
    /// Урон без прицела (взрыв, огонь, турель) броня гасит полностью, как раньше.
    /// </summary>
    public bool ShouldApply(EntityUid armor, DamageModifyEvent ev)
    {
        if (!_container.TryGetContainingContainer((armor, null, null), out var container))
            return true;

        var wearer = container.Owner;
        if (!HasComp<TargetingComponent>(wearer) || ev.Origin is not { } origin || !HasComp<TargetingComponent>(origin))
            return true;

        var coverage = GetCoverage(armor);
        if (coverage.Count == 0)
            return true;

        var (type, _) = _body.ConvertTargetBodyPart(_wounds.PeekTargetPart(wearer, origin));
        return coverage.Contains(type);
    }

    public IReadOnlyList<BodyPartType> GetCoverage(EntityUid armor)
    {
        if (TryComp<ArmorCoverageComponent>(armor, out var explicitCoverage))
            return explicitCoverage.Coverage;

        if (!TryComp<ClothingComponent>(armor, out var clothing))
            return Array.Empty<BodyPartType>();

        var slots = clothing.InSlotFlag ?? clothing.Slots;
        var result = new List<BodyPartType>();
        if ((slots & (SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES | SlotFlags.EARS)) != 0)
            result.Add(BodyPartType.Head);
        if ((slots & (SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING)) != 0)
            result.AddRange(Torso);
        if ((slots & (SlotFlags.NECK | SlotFlags.BACK)) != 0)
            result.Add(BodyPartType.Chest);
        if ((slots & SlotFlags.BELT) != 0)
            result.Add(BodyPartType.Groin);
        if ((slots & SlotFlags.GLOVES) != 0)
            result.Add(BodyPartType.Hand);
        if ((slots & SlotFlags.FEET) != 0)
            result.Add(BodyPartType.Foot);

        return result;
    }

    /// <summary>Надетая броня, которая закрывает эту часть тела (первая найденная), или null.</summary>
    public EntityUid? GetCoveringArmor(EntityUid body, BodyPartType part)
    {
        if (!_inventory.TryGetContainerSlotEnumerator(body, out var slots))
            return null;

        while (slots.NextItem(out var item))
        {
            if (HasComp<ArmorComponent>(item) && GetCoverage(item).Contains(part))
                return item;
        }

        return null;
    }

    /// <summary>
    /// Насколько броня на части гасит этот вид урона: 0 — никак, 0.9 — почти полностью.
    /// Берётся из коэффициентов самой брони (бронежилет с ушибами 0.6 — защита 0.4). Несколько слоёв складываются.
    /// Используется для шанса травм (перелом, органы, вены, нервы) и отрыва.
    /// Острый, тупой урон и жар броня уже погасила пробитием (ArmorPenetrationSystem) — для них 0,
    /// иначе броня считалась бы дважды.
    /// </summary>
    public float GetPartProtection(EntityUid body, BodyPartType part, string damageType)
    {
        if (_penetration.IsHandledType(damageType) || !_inventory.TryGetContainerSlotEnumerator(body, out var slots))
            return 0f;

        var passThrough = 1f;
        while (slots.NextItem(out var item))
        {
            if (!HasComp<ArmorComponent>(item) || !GetCoverage(item).Contains(part))
                continue;

            // Коэффициенты брони читаем через её же запрос (поле брони закрыто для чужих систем)
            var query = new InventoryRelayedEvent<CoefficientQueryEvent>(new CoefficientQueryEvent(SlotFlags.All), body);
            RaiseLocalEvent(item, query);
            if (query.Args.DamageModifiers.Coefficients.TryGetValue(damageType, out var coefficient))
                passThrough *= Math.Clamp(coefficient, 0f, 1f);
        }

        return Math.Clamp(1f - passThrough, 0f, 0.9f);
    }

    private void OnArmorExamine(Entity<ArmorComponent> ent, ref ArmorExamineEvent args)
    {
        _penetration.AddArmorExamine(ent, args.Msg);

        foreach (var type in GetCoverage(ent))
        {
            args.Msg.PushNewline();
            args.Msg.AddMarkupOrThrow(Loc.GetString("armor-coverage-value",
                ("type", Loc.GetString($"armor-coverage-type-{type.ToString().ToLower()}"))));
        }
    }
}
