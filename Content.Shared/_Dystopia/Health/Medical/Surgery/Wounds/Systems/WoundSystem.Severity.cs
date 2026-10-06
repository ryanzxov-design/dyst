// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Random;
using Content.Shared.Damage;
using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Targeting;
using Content.Shared._Dystopia.Health.Targeting.Events;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    /// <summary>Пороги тяжести раны, в процентах прочности части.</summary>
    private static readonly KeyValuePair<WoundSeverity, FixedPoint2>[] WoundThresholds =
    [
        new(WoundSeverity.Loss, 100),
        new(WoundSeverity.Critical, 80),
        new(WoundSeverity.Severe, 50),
        new(WoundSeverity.Moderate, 25),
        new(WoundSeverity.Minor, 1),
        new(WoundSeverity.Healed, 0),
    ];

    public void SetWoundSeverity(EntityUid uid, FixedPoint2 severity, WoundComponent? wound = null, WoundableComponent? woundable = null)
    {
        if (!Resolve(uid, ref wound) || !Resolve(wound.HoldingWoundable, ref woundable))
            return;

        var old = wound.WoundSeverityPoint;
        var holding = wound.HoldingWoundable;
        var upperLimit = wound.WoundSeverityPoint + woundable.WoundableIntegrity;
        wound.WoundSeverityPoint = FixedPoint2.Clamp(ApplySeverityModifiers(holding, severity), 0, upperLimit);

        if (wound.WoundSeverityPoint != old)
        {
            var ev = new WoundSeverityPointChangedEvent(wound, old, wound.WoundSeverityPoint);
            RaiseLocalEvent(uid, ref ev);
        }

        if (TerminatingOrDeleted(uid) || wound.HoldingWoundable != holding)
            return;

        CheckSeverityThresholds(uid, holding, wound, woundable);
        Dirty(uid, wound);
        UpdateWoundableIntegrity(holding);
        CheckWoundableSeverityThresholds(holding);
    }

    public void ApplyWoundSeverity(EntityUid uid, FixedPoint2 severity, WoundComponent? wound = null, WoundableComponent? woundable = null)
    {
        if (!Resolve(uid, ref wound) || !Resolve(wound.HoldingWoundable, ref woundable))
            return;

        var old = wound.WoundSeverityPoint;
        var holding = wound.HoldingWoundable;
        var rawValue = severity > 0 ? old + ApplySeverityModifiers(holding, severity) : old + severity;
        var upperLimit = wound.WoundSeverityPoint + woundable.WoundableIntegrity;
        wound.WoundSeverityPoint = FixedPoint2.Clamp(rawValue, 0, upperLimit);
        Dirty(uid, wound);

        if (wound.WoundSeverityPoint != old || rawValue > wound.WoundSeverityPoint)
        {
            FixedPoint2? overflow = rawValue > wound.WoundSeverityPoint ? rawValue - wound.WoundSeverityPoint : null;
            var ev = new WoundSeverityPointChangedEvent(wound, old, wound.WoundSeverityPoint, overflow);
            RaiseLocalEvent(uid, ref ev);
        }

        // Рана изуродовала часть: травмы из списка раны (обычно перелом) случаются наверняка
        if (severity > 0 && wound.MangleSeverity is { } mangle && wound.WoundSeverity >= mangle
            && woundable.WoundableSeverity >= WoundableSeverity.Mangled)
        {
            _trauma.ApplyMangledTraumas(holding, uid, severity);
        }

        if (TerminatingOrDeleted(uid) || wound.HoldingWoundable != holding)
            return;

        CheckSeverityThresholds(uid, holding, wound, woundable);
    }

    public FixedPoint2 ApplySeverityModifiers(EntityUid woundable, FixedPoint2 severity, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || component.SeverityMultipliers.Count == 0)
            return severity;

        var sum = 0f;
        foreach (var multiplier in component.SeverityMultipliers)
        {
            sum += (float) multiplier.Value.Change;
        }

        return severity * (sum / component.SeverityMultipliers.Count);
    }

    public bool TryAddWoundableSeverityMultiplier(EntityUid owner, EntityUid woundable, FixedPoint2 change, string identifier,
        WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || !component.SeverityMultipliers.TryAdd(owner, new WoundableSeverityMultiplier(change, identifier)))
            return false;

        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            CheckSeverityThresholds(wound, woundable, wound, component);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);
        return true;
    }

    public bool TryRemoveWoundableSeverityMultiplier(EntityUid owner, EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || !component.SeverityMultipliers.Remove(owner))
            return false;

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);
        return true;
    }

    private void CheckSeverityThresholds(EntityUid wound, EntityUid woundable, WoundComponent? component = null,
        WoundableComponent? woundableComp = null)
    {
        if (!Resolve(wound, ref component, false) || !Resolve(woundable, ref woundableComp))
            return;

        var nearestSeverity = component.WoundSeverity;
        foreach (var (severity, value) in WoundThresholds)
        {
            var scaledThreshold = value * (woundableComp.IntegrityCap / 100);
            if (component.WoundSeverityPoint < scaledThreshold)
                continue;

            if (severity == WoundSeverity.Healed && component.WoundSeverityPoint > 0)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity == component.WoundSeverity)
            return;

        var ev = new WoundSeverityChangedEvent(component.WoundSeverity, nearestSeverity);
        component.WoundSeverity = nearestSeverity;
        Dirty(wound, component);
        RaiseLocalEvent(wound, ref ev);

        if (!TerminatingOrDeleted(component.HoldingWoundable))
            UpdateWoundableAppearance(component.HoldingWoundable);
    }

    public void CheckWoundableSeverityThresholds(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component, false))
            return;

        component.SortedThresholds ??= [.. component.Thresholds.OrderByDescending(kv => kv.Value)];

        var nearestSeverity = component.WoundableSeverity;
        foreach (var (severity, value) in component.SortedThresholds)
        {
            if (component.WoundableIntegrity >= component.IntegrityCap)
            {
                nearestSeverity = WoundableSeverity.Healthy;
                break;
            }

            if (component.WoundableIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity == component.WoundableSeverity)
            return;

        var ev = new WoundableSeverityChangedEvent(component.WoundableSeverity, nearestSeverity);
        component.WoundableSeverity = nearestSeverity;
        Dirty(woundable, component);
        RaiseLocalEvent(woundable, ref ev);


        if (TryComp<BodyPartComponent>(woundable, out var bodyPart) && bodyPart.Body is { } body)
            UpdateBodyStatus(body);

        UpdateWoundableAppearance(woundable);
    }

    /// <summary>Обновить куклу состояния тела у владельца.</summary>
    public void UpdateBodyStatus(EntityUid body)
    {
        if (!TryComp<TargetingComponent>(body, out var targeting))
            return;

        targeting.BodyStatus = GetWoundableStatesOnBody(body);
        Dirty(body, targeting);

        if (_net.IsServer)
            RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(body)), body);
    }

    public void UpdateWoundableIntegrity(EntityUid uid, WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || component.Wounds == null)
            return;

        var damage = FixedPoint2.Zero;
        foreach (var woundEntity in component.Wounds.ContainedEntities)
        {
            if (TryComp<WoundComponent>(woundEntity, out var woundComp) && !woundComp.IsScar)
                damage += woundComp.WoundIntegrityDamage;
        }

        var newIntegrity = FixedPoint2.Clamp(component.IntegrityCap - damage, 0, component.IntegrityCap);
        if (newIntegrity == component.WoundableIntegrity)
            return;

        var ev = new WoundableIntegrityChangedEvent(component.WoundableIntegrity, newIntegrity);
        RaiseLocalEvent(uid, ref ev);

        if (TryComp<BodyPartComponent>(uid, out var bodyPart) && bodyPart.Body is { } body)
        {
            var ev1 = new WoundableIntegrityChangedOnBodyEvent((uid, component), component.WoundableIntegrity, newIntegrity);
            RaiseLocalEvent(body, ref ev1);
        }

        component.WoundableIntegrity = newIntegrity;
        Dirty(uid, component);
    }

    /// <summary>
    /// Удар по разрушенной части может её оторвать: шанс зависит от вида и силы урона (см. DismemberChances).
    /// Грудь, пах и голову так оторвать нельзя (это решает сервер по категории части).
    /// </summary>
    public void TryDismemberDestroyed(EntityUid woundable, DamageSpecifier hit, WoundableComponent? component = null)
    {
        if (_net.IsClient || !Resolve(woundable, ref component, false) || !IsDestroyed(component)
            || IsWoundableRoot(woundable, component)
            || !TryComp<Content.Shared.Body.Part.BodyPartComponent>(woundable, out var part) || part.Body is not { } body)
            return;

        // Ломать и отрывать может только удар, который вообще способен оторвать (порез, ушиб, укол, ожог) —
        // яд, едкая слизь, холод разрушенной части кость не ломают
        var physical = false;
        foreach (var (type, value) in hit.DamageDict)
        {
            if (value > 0 && component.DismemberChances.ContainsKey(type))
                physical = true;
        }

        if (!physical)
            return;

        // Сначала ломается кость: целую кость не оторвать, разрушенная часть с целой костью получает перелом
        if (_trauma.GetBone(component) is { } bone
            && TryComp<Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components.BoneComponent>(bone, out var boneComp)
            && boneComp.BoneSeverity != Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.BoneSeverity.Broken)
        {
            BreakBone(woundable, component, bone, boneComp);
            return;
        }

        var keep = 1f;
        foreach (var (type, value) in hit.DamageDict)
        {
            if (value <= 0 || !component.DismemberChances.TryGetValue(type, out var chance))
                continue;

            // Броня на части снижает шанс отрыва
            var armor = EntityManager.System<Content.Shared._Dystopia.Health.Armor.ArmorCoverageSystem>()
                .GetPartProtection(body, part.PartType, type.Id);
            keep *= 1f - chance * (1f - armor) * MathF.Min(1f, value.Float() / component.DismemberFullDamage);
        }

        if (!_random.Prob(1f - keep))
            return;

        var dismember = new Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.DismemberRequestEvent(body, woundable);
        RaiseLocalEvent(ref dismember);
    }

    /// <summary>Сломать кость части полностью (с травмой «перелом» на ране — чтобы рана не заживала раньше кости).</summary>
    private void BreakBone(EntityUid woundable, WoundableComponent component, EntityUid bone,
        Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components.BoneComponent boneComp)
    {
        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (!TryComp<Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components.TraumaInflicterComponent>(wound, out var inflicter))
                continue;

            _trauma.ApplyBoneTrauma(bone, (woundable, component), (wound, inflicter), boneComp.BoneIntegrity, boneComp);
            return;
        }

        _trauma.SetBoneIntegrity(bone, 0, boneComp);
    }
}
