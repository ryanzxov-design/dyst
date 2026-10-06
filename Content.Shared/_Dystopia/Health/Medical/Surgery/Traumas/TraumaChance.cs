// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;

public readonly struct TraumaChanceArgs(
    IEntityManager entityManager,
    Entity<WoundableComponent> target,
    Entity<TraumaInflicterComponent> inflicter,
    FixedPoint2 severity,
    BodyPartComponent part,
    EntityUid body)
{
    public readonly IEntityManager EntityManager = entityManager;
    public readonly Entity<WoundableComponent> Target = target;
    public readonly Entity<TraumaInflicterComponent> Inflicter = inflicter;
    public readonly FixedPoint2 Severity = severity;
    public readonly BodyPartComponent Part = part;
    public readonly EntityUid Body = body;
}

/// <summary>Шанс травмы. null — травма невозможна.</summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class TraumaChance
{
    public abstract FixedPoint2? Calculate(in TraumaChanceArgs args);
}

public sealed partial class FlatTraumaChance : TraumaChance
{
    [DataField]
    public FixedPoint2 Chance;

    public override FixedPoint2? Calculate(in TraumaChanceArgs args) => Chance;
}

/// <summary>Перелом: чем сильнее изранена часть и слабее кость — тем вероятнее.</summary>
public sealed partial class BoneFractureChance : TraumaChance
{
    [DataField]
    public Dictionary<WoundableSeverity, FixedPoint2> SeverityMultipliers = new()
    {
        { WoundableSeverity.Healthy, 0 },
        { WoundableSeverity.Minor, 0.01 },
        { WoundableSeverity.Moderate, 0.04 },
        { WoundableSeverity.Severe, 0.12 },
        { WoundableSeverity.Critical, 0.21 },
        { WoundableSeverity.Mangled, 0.21 },
        { WoundableSeverity.Severed, 0 },
    };

    public override FixedPoint2? Calculate(in TraumaChanceArgs args)
    {
        var target = args.Target;
        var bone = target.Comp.Bone.ContainedEntities.FirstOrDefault();
        if (!bone.IsValid()
            || !args.EntityManager.TryGetComponent(bone, out BoneComponent? boneComp)
            || boneComp.BoneSeverity == BoneSeverity.Broken)
            return null;

        return target.Comp.IntegrityCap / (target.Comp.WoundableIntegrity + boneComp.BoneIntegrity)
            * SeverityMultipliers.GetValueOrDefault(target.Comp.WoundableSeverity);
    }
}

/// <summary>Нервы: возможна у тел, которые чувствуют боль. Повреждённые нервы усиливают боль части.</summary>
public sealed partial class NerveTraumaChance : TraumaChance
{
    [DataField]
    public float MinPainFeels = 0.2f;

    [DataField]
    public FixedPoint2 Divisor = 20;

    /// <summary>Шанс при ране, выросшей на Divisor: столько.</summary>
    [DataField]
    public FixedPoint2 ChanceAtDivisor = 0.15;

    public override FixedPoint2? Calculate(in TraumaChanceArgs args)
    {
        if (!args.EntityManager.HasComponent<Content.Shared._Dystopia.Health.Medical.Pain.PainComponent>(args.Body))
            return null;

        return args.Severity / Divisor * ChanceAtDivisor;
    }
}

/// <summary>Повреждение органа: только если в части есть живые органы.</summary>
public sealed partial class OrganTraumaChance : TraumaChance
{
    [DataField]
    public FixedPoint2 BaseChance = 0.4;

    public override FixedPoint2? Calculate(in TraumaChanceArgs args)
    {
        var body = args.EntityManager.System<SharedBodySystem>();
        foreach (var organ in body.GetPartOrgans(args.Target, args.Part))
        {
            if (args.EntityManager.TryGetComponent(organ.Id, out OrganIntegrityComponent? integrity) && integrity.Integrity <= 0)
                continue;

            return BaseChance;
        }

        return null;
    }
}

/// <summary>Отрыв: тем вероятнее, чем меньше осталось от части и чем сильнее сломана кость.</summary>
public sealed partial class DismembermentChance : TraumaChance
{
    [DataField]
    public float IntegrityExponent = 1.3f;

    [DataField]
    public Dictionary<BoneSeverity, float> BoneMultipliers = new()
    {
        { BoneSeverity.Normal, 0.3f },
        { BoneSeverity.Damaged, 0.6f },
        { BoneSeverity.Cracked, 1f },
        { BoneSeverity.Broken, 1.2f },
    };

    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> DamageTypeMultipliers = new();

    public override FixedPoint2? Calculate(in TraumaChanceArgs args)
    {
        var em = args.EntityManager;
        var target = args.Target;
        if (em.System<Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems.WoundSystem>().GetParentWoundable(target) is not { } parentWoundable)
            return null;

        // Dystopia: случайный отрыв возможен только у разрушенной части со сломанной костью
        if (!Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems.WoundSystem.IsDestroyed(target.Comp)
            || target.Comp.Bone.ContainedEntities.FirstOrDefault() is not { Valid: true } brokenBone
            || !em.TryGetComponent<BoneComponent>(brokenBone, out var brokenBoneComp)
            || brokenBoneComp.BoneSeverity != BoneSeverity.Broken)
            return null;

        if (args.Part.PartType == BodyPartType.Chest
            || args.Part.PartType == BodyPartType.Groin
            && em.GetComponent<WoundableComponent>(parentWoundable).WoundableSeverity != WoundableSeverity.Mangled)
            return null;

        var multiplier = 1f;
        var bone = target.Comp.Bone.ContainedEntities.FirstOrDefault();
        if (bone.IsValid() && em.TryGetComponent(bone, out BoneComponent? boneComp))
            multiplier = BoneMultipliers.GetValueOrDefault(boneComp.BoneSeverity, 1f);

        if (DamageTypeMultipliers.Count > 0 && em.TryGetComponent(args.Inflicter, out WoundComponent? wound))
            multiplier *= DamageTypeMultipliers.GetValueOrDefault(wound.DamageType, 1f);

        return (1f - (MathF.Pow(target.Comp.WoundableIntegrity.Float(), IntegrityExponent) / target.Comp.IntegrityCap.Float() - 1f))
            * multiplier;
    }
}
