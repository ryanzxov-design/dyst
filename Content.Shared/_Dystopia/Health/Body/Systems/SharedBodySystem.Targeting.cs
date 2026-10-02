// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: перевод между частями тела и прицелом, выбор части, в которую попал удар.

using System.Linq;
using Content.Shared._Dystopia.Health.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Robust.Shared.Random;

namespace Content.Shared.Body.Systems;

public sealed partial class SharedBodySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>Жизненно важные части, прикреплённые к груди (голова, пах).</summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetVitalBodyChildren(
        EntityUid? id,
        BodyComponent? body = null,
        BodyPartComponent? rootPart = null)
    {
        if (id is null || !TryGetRootPart(id.Value, out var root))
            yield break;

        foreach (var child in GetBodyPartChildren(root.Value.Owner, root.Value.Comp))
        {
            if ((int) (child.Component.PartType & BodyPartType.Vital) != 0)
                yield return child;
        }
    }

    /// <summary>Куда попал удар: по таблице шансов цели. Лежачего и обездвиженного бьют точно в выбранное.</summary>
    public TargetBodyPart? GetRandomBodyPart(EntityUid target,
        EntityUid attacker,
        TargetingComponent? targetComp = null,
        TargetingComponent? attackerComp = null)
    {
        if (!Resolve(target, ref targetComp, false) || !Resolve(attacker, ref attackerComp, false))
            return TargetBodyPart.Chest;

        if (_mobState.IsIncapacitated(target) || _standing.IsDown(target))
            return attackerComp.Target;

        return Roll(targetComp, attackerComp.Target);
    }

    public TargetBodyPart GetRandomBodyPart(EntityUid target,
        TargetBodyPart targetPart = TargetBodyPart.Chest,
        TargetingComponent? targetComp = null)
    {
        if (!Resolve(target, ref targetComp, false))
            return TargetBodyPart.Chest;

        if (_mobState.IsIncapacitated(target) || _standing.IsDown(target))
            return targetPart;

        return Roll(targetComp, targetPart);
    }

    public TargetBodyPart GetRandomBodyPart(EntityUid target)
    {
        var children = GetVitalBodyChildren(target).ToList();
        if (children.Count == 0)
            return TargetBodyPart.Chest;

        return GetTargetBodyPart(_random.PickAndTake(children));
    }

    public TargetBodyPart GetRandomBodyPart(EntityUid target,
        EntityUid? attacker,
        TargetBodyPart? targetPart = null,
        TargetingComponent? targetComp = null)
    {
        if (!Resolve(target, ref targetComp, false))
            return TargetBodyPart.Chest;

        if (targetPart.HasValue)
            return GetRandomBodyPart(target, targetPart: targetPart.Value);

        if (attacker.HasValue && TryComp(attacker.Value, out TargetingComponent? attackerComp))
            return GetRandomBodyPart(target, targetPart: attackerComp.Target);

        return GetRandomBodyPart(target);
    }

    private TargetBodyPart Roll(TargetingComponent targetComp, TargetBodyPart aimed)
    {
        if (!targetComp.TargetOdds.TryGetValue(aimed, out var odds))
            return aimed;

        var randomValue = _random.NextFloat() * odds.Values.Sum();
        foreach (var (part, weight) in odds)
        {
            if (randomValue <= weight)
                return part;
            randomValue -= weight;
        }

        return aimed;
    }

    /// <summary>Выбранная цель без броска шансов.</summary>
    public TargetBodyPart GetTargetBodyPart(EntityUid target,
        EntityUid? attacker,
        TargetBodyPart? targetPart = null,
        TargetingComponent? targetComp = null)
    {
        if (!Resolve(target, ref targetComp, false))
            return TargetBodyPart.Chest;

        if (targetPart.HasValue)
            return targetPart.Value;

        if (attacker.HasValue && TryComp(attacker.Value, out TargetingComponent? attackerComp))
            return attackerComp.Target;

        return GetRandomBodyPart(target);
    }

    public TargetBodyPart GetTargetBodyPart(EntityUid partId)
    {
        return TryComp(partId, out BodyPartComponent? part) ? GetTargetBodyPart(part) : TargetBodyPart.Chest;
    }

    public TargetBodyPart GetTargetBodyPart(Entity<BodyPartComponent> part) => GetTargetBodyPart(part.Comp.PartType, part.Comp.Symmetry);

    public TargetBodyPart GetTargetBodyPart((EntityUid Id, BodyPartComponent Component) part) =>
        GetTargetBodyPart(part.Component.PartType, part.Component.Symmetry);

    public TargetBodyPart GetTargetBodyPart(BodyPartComponent part) => GetTargetBodyPart(part.PartType, part.Symmetry);

    /// <summary>Тип и сторона части тела → часть прицела.</summary>
    public TargetBodyPart GetTargetBodyPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => TargetBodyPart.Head,
            (BodyPartType.Chest, _) => TargetBodyPart.Chest,
            (BodyPartType.Groin, _) => TargetBodyPart.Groin,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => TargetBodyPart.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => TargetBodyPart.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => TargetBodyPart.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => TargetBodyPart.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => TargetBodyPart.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => TargetBodyPart.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => TargetBodyPart.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => TargetBodyPart.RightFoot,
            _ => TargetBodyPart.Chest,
        };
    }

    /// <summary>Часть прицела → тип и сторона части тела.</summary>
    public (BodyPartType Type, BodyPartSymmetry Symmetry) ConvertTargetBodyPart(TargetBodyPart? targetPart)
    {
        return targetPart switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, BodyPartSymmetry.None),
            TargetBodyPart.Chest => (BodyPartType.Chest, BodyPartSymmetry.None),
            TargetBodyPart.Groin => (BodyPartType.Groin, BodyPartSymmetry.None),
            TargetBodyPart.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            TargetBodyPart.LeftHand => (BodyPartType.Hand, BodyPartSymmetry.Left),
            TargetBodyPart.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            TargetBodyPart.RightHand => (BodyPartType.Hand, BodyPartSymmetry.Right),
            TargetBodyPart.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            TargetBodyPart.LeftFoot => (BodyPartType.Foot, BodyPartSymmetry.Left),
            TargetBodyPart.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            TargetBodyPart.RightFoot => (BodyPartType.Foot, BodyPartSymmetry.Right),
            _ => (BodyPartType.Chest, BodyPartSymmetry.None),
        };
    }

    /// <summary>Сущность части тела, в которую целятся (null — такой части нет: отрезана).</summary>
    public EntityUid? GetTargetedPartEntity(EntityUid body, TargetBodyPart target)
    {
        var (type, symmetry) = ConvertTargetBodyPart(target);
        foreach (var part in GetBodyChildrenOfType(body, type, symmetry: symmetry))
        {
            return part.Id;
        }

        return null;
    }
}
