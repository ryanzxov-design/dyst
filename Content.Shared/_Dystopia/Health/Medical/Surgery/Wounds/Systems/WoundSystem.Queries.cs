// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Targeting;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    /// <summary>Прототип раны для типа урона. В нашей сборке у ран префикс Wound (WoundBlunt, WoundHeat...).</summary>
    public static string WoundPrototypeFor(string damageType) => "Wound" + damageType;

    private bool IsWoundPrototypeValid(string damageType)
    {
        return _prototype.TryIndex<EntityPrototype>(WoundPrototypeFor(damageType), out var proto) &&
               proto.TryComp<WoundComponent>(out _, _factory);
    }

    /// <summary>Корневая часть (грудь): её нельзя оторвать.</summary>
    public bool IsWoundableRoot(EntityUid woundable, WoundableComponent? comp = null)
    {
        // Считаем по живым связям частей: сохранённая иерархия при появлении тела ещё не собрана
        if (TryComp<BodyPartComponent>(woundable, out var part))
            return part.PartType == BodyPartType.Chest;

        return Resolve(woundable, ref comp, false) && comp.RootWoundable == woundable;
    }

    /// <summary>Родительская часть по живым связям тела (для стопы — нога, для руки — грудь).</summary>
    public EntityUid? GetParentWoundable(EntityUid woundable)
    {
        return _body.TryGetParentBodyPart(woundable, out var parent, out _) && HasComp<WoundableComponent>(parent.Value)
            ? parent
            : null;
    }

    public IEnumerable<Entity<WoundComponent>> GetWoundableWounds(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component, false) || component.Wounds == null)
            yield break;

        foreach (var wound in component.Wounds.ContainedEntities.ToArray())
        {
            if (TryComp<WoundComponent>(wound, out var comp))
                yield return (wound, comp);
        }
    }

    /// <summary>Все части тела с ранами, начиная с корня (у тела) или с этой части и её детей.</summary>
    public IEnumerable<Entity<WoundableComponent>> GetAllWoundableChildren(EntityUid target, WoundableComponent? component = null)
    {
        if (!Resolve(target, ref component, false))
            yield break;

        yield return (target, component);
        foreach (var (child, _) in _body.GetBodyPartChildren(target).ToArray())
        {
            if (!TryComp<WoundableComponent>(child, out var childComp))
                continue;

            foreach (var item in GetAllWoundableChildren(child, childComp))
            {
                yield return item;
            }
        }
    }

    public IEnumerable<Entity<WoundComponent>> GetAllWounds(EntityUid target, WoundableComponent? component = null)
    {
        foreach (var woundable in GetAllWoundableChildren(target, component))
        {
            foreach (var wound in GetWoundableWounds(woundable, woundable))
            {
                yield return wound;
            }
        }
    }

    public FixedPoint2 GetWoundableSeverityPoint(EntityUid woundable, WoundableComponent? component = null)
    {
        var total = FixedPoint2.Zero;
        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            total += wound.Comp.WoundSeverityPoint;
        }

        return total;
    }

    /// <summary>Состояние каждой части тела для куклы (нет части — «отделена»).</summary>
    public Dictionary<TargetBodyPart, WoundableSeverity> GetWoundableStatesOnBody(EntityUid body)
    {
        var result = new Dictionary<TargetBodyPart, WoundableSeverity>();
        foreach (var part in SharedTargetingSystem.GetValidParts())
        {
            result[part] = WoundableSeverity.Severed;
        }

        foreach (var (partId, partComp) in _body.GetBodyChildren(body))
        {
            if (!TryComp<WoundableComponent>(partId, out var woundable))
                continue;

            result[_body.GetTargetBodyPart(partComp)] = woundable.WoundableSeverity;
        }

        return result;
    }

    public bool TryInduceWound(EntityUid uid, string damageType, FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundInduced, WoundableComponent? woundable = null)
    {
        woundInduced = null;
        if (!Resolve(uid, ref woundable))
            return false;

        if (TryContinueWound(uid, damageType, severity, out woundInduced, woundable))
            return true;

        return TryCreateWound(uid, damageType, severity, out woundInduced, GetDamageGroupByType(damageType)?.ID, woundable);
    }

    public bool TryCreateWound(EntityUid uid, string damageType, FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundCreated, ProtoId<DamageGroupPrototype>? damageGroup,
        WoundableComponent? woundable = null)
    {
        woundCreated = null;
        if (!IsWoundPrototypeValid(damageType) || !Resolve(uid, ref woundable))
            return false;

        var wound = Spawn(WoundPrototypeFor(damageType));
        if (AddWound(uid, wound, severity, damageGroup, woundable))
        {
            woundCreated = (wound, Comp<WoundComponent>(wound));
            return true;
        }

        QueueDel(wound);
        return false;
    }

    public bool TryContinueWound(EntityUid uid, string damageType, FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundContinued, WoundableComponent? woundable = null)
    {
        woundContinued = null;
        if (!Resolve(uid, ref woundable))
            return false;

        foreach (var wound in GetWoundableWounds(uid, woundable))
        {
            if (wound.Comp.DamageType.Id != damageType || wound.Comp.IsScar)
                continue;

            ApplyWoundSeverity(wound, severity, wound, woundable);
            UpdateWoundableIntegrity(uid, woundable);
            CheckWoundableSeverityThresholds(uid, woundable);
            woundContinued = wound;
            return true;
        }

        return false;
    }

    private bool AddWound(EntityUid target, EntityUid wound, FixedPoint2 severity, ProtoId<DamageGroupPrototype>? damageGroup,
        WoundableComponent? woundable = null, WoundComponent? woundComp = null)
    {
        if (!Resolve(target, ref woundable) || !Resolve(wound, ref woundComp) || woundable.Wounds == null ||
            woundable.Wounds.Contains(wound) || !woundable.AllowWounds)
        {
            return false;
        }

        woundComp.HoldingWoundable = target;
        woundComp.DamageGroup = damageGroup;
        if (!_container.Insert(wound, woundable.Wounds))
            return false;

        SetWoundSeverity(wound, severity, woundComp, woundable);
        Dirty(wound, woundComp);
        Dirty(target, woundable);

        if (TryComp<WoundableComponent>(woundable.RootWoundable, out var root))
        {
            var ev = new WoundAddedEvent(woundComp, woundable, root);
            RaiseLocalEvent(wound, ref ev);
            var ev1 = new WoundAddedEvent(woundComp, woundable, root);
            RaiseLocalEvent(target, ref ev1);
        }

        return true;
    }

    private bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        if (!Resolve(woundEntity, ref wound, false) || !TryComp(wound.HoldingWoundable, out WoundableComponent? woundable))
            return false;

        var holding = wound.HoldingWoundable;
        if (TryComp<WoundableComponent>(woundable.RootWoundable, out var root))
        {
            var ev = new WoundRemovedEvent(wound, woundable, root);
            RaiseLocalEvent(woundEntity, ref ev);
            var ev1 = new WoundRemovedEvent(wound, woundable, root);
            RaiseLocalEvent(holding, ref ev1);
        }

        _container.Remove(woundEntity, woundable.Wounds, false, true);
        wound.HoldingWoundable = EntityUid.Invalid;
        QueueDel(woundEntity);

        UpdateWoundableIntegrity(holding, woundable);
        CheckWoundableSeverityThresholds(holding, woundable);
        UpdateWoundableAppearance(holding);
        return true;
    }

    private void UpdateWoundableAppearance(EntityUid woundable)
    {
        if (!TryComp<WoundableComponent>(woundable, out var comp) || comp.Wounds == null)
            return;

        var count = comp.Wounds.ContainedEntities.Count;
        _appearance.SetData(woundable, WoundableVisualizerKeys.Wounds, count);
    }

    private void OnWoundSeverityChanged(EntityUid wound, WoundComponent component, ref WoundSeverityChangedEvent args)
    {
        if (args.NewSeverity == WoundSeverity.Healed)
            RemoveWound(wound, component);
    }

    private void HealWoundsOnWoundableAttempt(Entity<WoundableComponent> woundable, ref WoundHealAttemptOnWoundableEvent args)
    {
        if (woundable.Comp.WoundableSeverity == WoundableSeverity.Severed)
            args.Cancelled = true;
    }
}
