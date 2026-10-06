// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    /// <summary>Лечение по всему телу: каждый тип лечения делится между частями с ранами этого типа.</summary>
    public bool TryHealWoundsOnOwner(EntityUid body, DamageSpecifier healing, bool ignoreBlockers = false)
    {
        var healedAny = false;
        var woundables = _body.GetBodyChildrenWithComponent<WoundableComponent>(body).ToList();

        foreach (var (type, value) in healing.DamageDict)
        {
            if (value >= 0)
                continue;

            var targets = woundables.Where(w => GetWoundableWounds(w.Id, w.Component).Any(x => x.Comp.DamageType.Id == type)).ToList();
            if (targets.Count == 0)
                continue;

            var perPart = -value / targets.Count;
            foreach (var target in targets)
            {
                if (HealWoundsCore(target.Id, perPart, type, out _, target.Component, ignoreBlockers: ignoreBlockers))
                    healedAny = true;
            }
        }

        foreach (var target in woundables)
        {
            UpdateWoundableIntegrity(target.Id, target.Component);
            CheckWoundableSeverityThresholds(target.Id, target.Component);
        }

        return healedAny;
    }

    private bool HealWoundsCore(EntityUid woundable, FixedPoint2 healAmount, string damageType, out FixedPoint2 healed,
        WoundableComponent component, bool ignoreMultipliers = false, bool ignoreBlockers = false)
    {
        healed = 0;
        var woundsToHeal = new List<(Entity<WoundComponent> Wound, FixedPoint2 Floor)>();
        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (CanHealWound(wound, out var floor, wound, ignoreBlockers) && damageType == wound.Comp.DamageType.Id)
                woundsToHeal.Add((wound, floor));
        }

        if (woundsToHeal.Count == 0)
            return false;

        var perWound = healAmount / woundsToHeal.Count;
        var actualHeal = FixedPoint2.Zero;
        foreach (var (wound, floor) in woundsToHeal)
        {
            var heal = ignoreMultipliers ? -perWound : ApplyHealingRateMultipliers(wound, woundable, -perWound, component);
            heal = ClampHealToFloor(wound.Comp, heal, floor);
            if (heal >= 0)
                continue;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        healed = actualHeal;
        return actualHeal > 0;
    }

    public FixedPoint2 ApplyHealingRateMultipliers(EntityUid wound, EntityUid woundable, FixedPoint2 severity,
        WoundableComponent? component = null, WoundComponent? woundComp = null)
    {
        if (!Resolve(woundable, ref component) || !Resolve(wound, ref woundComp))
            return severity;

        var multiplier = FixedPoint2.New(woundComp.SelfHealMultiplier);
        if (component.HealingMultipliers.Count == 0)
            return severity * multiplier;

        var sum = FixedPoint2.Zero;
        foreach (var m in component.HealingMultipliers)
        {
            sum += m.Value.Change;
        }

        return severity * (sum / component.HealingMultipliers.Count) * multiplier;
    }

    public bool TryAddHealingRateMultiplier(EntityUid owner, EntityUid woundable, string identifier, FixedPoint2 change,
        WoundableComponent? component = null)
    {
        return Resolve(woundable, ref component) &&
               component.HealingMultipliers.TryAdd(owner, new WoundableHealingMultiplier(change, identifier));
    }

    public bool TryRemoveHealingRateMultiplier(EntityUid owner, EntityUid woundable, WoundableComponent? component = null)
    {
        return Resolve(woundable, ref component) && component.HealingMultipliers.Remove(owner);
    }

    public bool CanHealWound(EntityUid wound, WoundComponent? comp = null, bool ignoreBlockers = false)
        => CanHealWound(wound, out _, comp, ignoreBlockers);

    public bool CanHealWound(EntityUid wound, out FixedPoint2 severityFloor, WoundComponent? comp = null, bool ignoreBlockers = false)
    {
        severityFloor = FixedPoint2.Zero;
        if (!Resolve(wound, ref comp, false))
            return false;

        if (!ignoreBlockers && !comp.CanBeHealed)
            return false;

        var holding = comp.HoldingWoundable;
        if (!TryComp<WoundableComponent>(holding, out var holdingComp))
            return false;

        var ev = new WoundHealAttemptOnWoundableEvent((wound, comp));
        RaiseLocalEvent(holding, ref ev);
        if (ev.Cancelled)
            return false;

        var ev1 = new WoundHealAttemptEvent((holding, holdingComp), ignoreBlockers);
        RaiseLocalEvent(wound, ref ev1);
        severityFloor = ev1.SeverityFloor;
        return !ev1.Cancelled;
    }

    private static FixedPoint2 ClampHealToFloor(WoundComponent wound, FixedPoint2 heal, FixedPoint2 floor)
    {
        if (floor <= 0 || heal >= 0)
            return heal;

        var allowedReduction = wound.WoundSeverityPoint - floor;
        if (allowedReduction <= 0)
            return FixedPoint2.Zero;

        return -heal > allowedReduction ? -allowedReduction : heal;
    }
}
