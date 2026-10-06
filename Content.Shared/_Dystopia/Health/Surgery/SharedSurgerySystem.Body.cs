// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: хирургия поверх нашей системы тела.
// В Shitmed у частей тела есть «слоты» для органов и конечностей. У нас слотов нет: органы — сущности
// в контейнере тела с категориями (Heart, ArmLeft...), связанные отношениями родитель/ребёнок, а устройство
// тела (что к чему крепится) задано в InitialBodyComponent.Relationships. Здесь — операции с органами и частями.

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Surgery;

public abstract partial class SharedSurgerySystem
{
    /// <summary>Категории конечностей, которые можно отрезать и пришить.</summary>
    private static readonly HashSet<string> LimbCategories = new()
    {
        "ArmLeft", "ArmRight", "HandLeft", "HandRight", "LegLeft", "LegRight", "FootLeft", "FootRight",
    };

    public string? Category(EntityUid organ)
    {
        return TryComp<OrganComponent>(organ, out var comp) ? comp.Category?.Id : null;
    }

    /// <summary>По устройству тела к этой части крепится (или внутри неё лежит) орган этой категории.</summary>
    public bool PartExpects(EntityUid body, EntityUid part, string category)
    {
        if (Category(part) is not { } partCategory || !TryComp<InitialBodyComponent>(body, out var initial))
            return false;

        // Поле читаем в локальную переменную: методы прямо на чужом поле запрещает анализатор доступа
        var relationships = initial.Relationships;
        return relationships != null
               && relationships.TryGetValue(partCategory, out var children)
               && children.Contains(category);
    }

    /// <summary>Дочерняя часть или орган этой категории (или null).</summary>
    public EntityUid? ChildOfCategory(EntityUid part, string category)
    {
        if (!TryComp<ParentOrganComponent>(part, out var parent))
            return null;

        var children = parent.Children;
        foreach (var child in children)
        {
            if (Category(child) == category)
                return child;
        }

        return null;
    }

    /// <summary>Внутренние органы части (только этих категорий, если список не пуст).</summary>
    public IEnumerable<EntityUid> PartOrgans(EntityUid part, IReadOnlyCollection<string>? categories = null)
    {
        if (!TryComp<ParentOrganComponent>(part, out var parent))
            yield break;

        IEnumerable<EntityUid> set = parent.Children;
        var children = set.ToList();
        foreach (var child in children)
        {
            if (!HasComp<InternalChildOrganComponent>(child))
                continue;

            if (categories is { Count: > 0 } && (Category(child) is not { } cat || !categories.Contains(cat)))
                continue;

            yield return child;
        }
    }

    /// <summary>Повреждённые органы части.</summary>
    public IEnumerable<(EntityUid Organ, OrganIntegrityComponent Integrity)> DamagedOrgans(EntityUid part, IReadOnlyCollection<string>? categories = null)
    {
        foreach (var organ in PartOrgans(part, categories))
        {
            if (TryComp<OrganIntegrityComponent>(organ, out var integrity) && integrity.Integrity < integrity.IntegrityCap)
                yield return (organ, integrity);
        }
    }

    public bool BoneDamaged(EntityUid part)
    {
        return _trauma.GetBoneSeverity(part, effective: false) != BoneSeverity.Normal
               || TryComp<WoundableComponent>(part, out var woundable) && _trauma.GetBone(woundable) is { } bone
               && TryComp<BoneComponent>(bone, out var boneComp) && boneComp.BoneIntegrity < boneComp.IntegrityCap;
    }

    public bool HasTrauma(EntityUid part, ProtoId<TraumaTypePrototype> type)
    {
        if (!HasComp<WoundableComponent>(part))
            return false;

        foreach (var trauma in _trauma.GetWoundableTraumas(part))
        {
            if (trauma.Comp.TraumaType == type)
                return true;
        }

        return false;
    }

    public void RemoveTraumas(EntityUid part, ProtoId<TraumaTypePrototype> type)
    {
        if (!HasComp<WoundableComponent>(part))
            return;

        foreach (var trauma in _trauma.GetWoundableTraumas(part).ToList())
        {
            if (trauma.Comp.TraumaType == type)
                _trauma.RemoveTrauma(trauma);
        }
    }

    // ===================== Органы =====================

    /// <summary>Вставить орган в часть тела (в контейнер тела и связать с частью).</summary>
    public bool InsertOrgan(EntityUid body, EntityUid part, EntityUid organ)
    {
        if (!HasComp<ChildOrganComponent>(organ)
            || !_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
        {
            return false;
        }

        // Insert сам вынимает орган из руки хирурга
        if (!_container.Insert(organ, container))
            return false;

        _relation.Relate(part, organ);
        return true;
    }

    /// <summary>Извлечь орган из тела: он становится отдельным предметом.</summary>
    public bool RemoveOrgan(EntityUid body, EntityUid organ)
    {
        if (!_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
            return false;

        _relation.Orphan(organ);
        return _container.Remove(organ, container);
    }

    // ===================== Конечности =====================

    /// <summary>
    /// Корень конечности нужной категории в том, что держит хирург: отрезанная рука или протез — это
    /// «отделённое тело» с органами, корень — орган без родителя.
    /// </summary>
    public EntityUid? FindLimbRoot(EntityUid held, string category)
    {
        if (!HasComp<BodyComponent>(held) || HasComp<SurgeryTargetComponent>(held))
            return null;

        foreach (var organ in _bodyCore.EnumerateOrgans<OrganComponent>(held))
        {
            if (organ.Comp1.Category?.Id != category)
                continue;

            if (TryComp<ChildOrganComponent>(organ, out var child) && child.Parent != null)
                continue;

            return organ.Owner;
        }

        return null;
    }

    /// <summary>Пришить конечность из рук: органы переходят в тело и связываются с частью.</summary>
    public bool AttachLimb(EntityUid body, EntityUid part, EntityUid held, string category)
    {
        if (FindLimbRoot(held, category) is not { } root
            || !_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
        {
            return false;
        }

        var moving = new List<EntityUid> { root };
        foreach (var descendant in _relation.AllChildren(root))
        {
            moving.Add(descendant.Owner);
        }

        foreach (var organ in moving)
        {
            _container.Insert(organ, container, force: true);
        }

        _relation.Relate(part, root);
        EnsureComp<BodyPartReattachedComponent>(root);
        EnsureComp<SurgeryLimbLossComponent>(body);

        // Пустое «отделённое тело» больше не нужно
        PredictedQueueDel(held);
        return true;
    }

    /// <summary>Отрезать конечность: она отделяется со всем, что к ней прикреплено, и попадает в руки хирургу.</summary>
    public EntityUid? Amputate(EntityUid user, EntityUid body, EntityUid part)
    {
        if (Category(part) is not { } category || !LimbCategories.Contains(category))
            return null;

        // Состояния операции остаются на самой конечности — снимаем их
        RemComp<IncisionOpenComponent>(part);
        RemComp<SkinRetractedComponent>(part);
        RemComp<BleedersClampedComponent>(part);
        RemComp<InternalBleedersClampedComponent>(part);
        RemComp<BonesSawedComponent>(part);
        RemComp<BonesOpenComponent>(part);
        RemComp<BodyPartSawedComponent>(part);
        RemComp<BoneSetComponent>(part);

        EnsureComp<SurgeryLimbLossComponent>(body);
        if (_detachable.Detach(part) is not { } limb)
            return null;

        _metaData.SetEntityName(limb, Loc.GetString("surgery-severed-limb",
            ("part", Loc.GetString($"surgery-severed-{category}")), ("patient", Name(body))));
        _hands.PickupOrDrop(user, limb);
        return limb;
    }
}
