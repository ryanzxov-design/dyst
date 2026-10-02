// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: слой совместимости системы здоровья с новой системой тела.
//
// Система здоровья обращается к телу через SharedBodySystem (дети тела, органы части, корневая часть...).
// Здесь — те же методы с теми же сигнатурами, но работающие поверх нашей системы тела:
//   * часть тела   = орган с BodyPartComponent (Torso, Groin, Head, ArmLeft...);
//   * иерархия     = связи органов (ParentOrgan / ChildOrgan);
//   * орган части  = орган с InternalChildOrgan, ребёнок части;
//   * отделение    = DetachableOrganSystem.
// Методы добавляются по мере переноса: в Ф1 — чтение устройства тела и прикрепление/отделение частей.
// Слоты, полость и маркировки (TryCreatePartSlot, CanInsertOrgan, ModifyMarkings...) — в Ф8 вместе с хирургией.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Body.Part;
using Robust.Shared.Containers;

namespace Content.Shared.Body.Systems;

public sealed partial class SharedBodySystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private OrganRelationSystem _relation = default!;
    [Dependency] private DetachableOrganSystem _detachable = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, OrganGotInsertedEvent>(OnPartInserted);
        SubscribeLocalEvent<BodyPartComponent, OrganGotRemovedEvent>(OnPartRemoved);
    }

    private void OnPartInserted(Entity<BodyPartComponent> ent, ref OrganGotInsertedEvent args)
    {
        ent.Comp.Body = args.Target;
        Dirty(ent);
    }

    private void OnPartRemoved(Entity<BodyPartComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (ent.Comp.Body == args.Target)
        {
            ent.Comp.Body = null;
            Dirty(ent);
        }
    }

    // ===================== Части тела =====================

    /// <summary>Все части тела, начиная с корневой (грудь).</summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(
        EntityUid? id,
        BodyComponent? body = null,
        BodyPartComponent? rootPart = null)
    {
        if (id is null)
            yield break;

        if (!TryGetRootPart(id.Value, out var root))
            yield break;

        var queue = new Queue<EntityUid>();
        var seen = new HashSet<EntityUid>();
        queue.Enqueue(root.Value.Owner);
        while (queue.Count > 0)
        {
            var part = queue.Dequeue();
            if (!seen.Add(part) || !TryComp<BodyPartComponent>(part, out var comp))
                continue;

            yield return (part, comp);
            foreach (var child in GetBodyPartChildren(part, comp))
            {
                queue.Enqueue(child.Id);
            }
        }
    }

    /// <summary>Части тела определённого типа (и стороны).</summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null,
        BodyPartSymmetry? symmetry = null)
    {
        foreach (var part in GetBodyChildren(bodyId, body))
        {
            if (part.Component.PartType == type && (symmetry is null || part.Component.Symmetry == symmetry))
                yield return part;
        }
    }

    /// <summary>Части тела с дополнительным компонентом.</summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent BodyPart, T Component)> GetBodyChildrenWithComponent<T>(
        EntityUid? id,
        BodyComponent? body = null,
        BodyPartComponent? rootPart = null)
        where T : IComponent
    {
        foreach (var part in GetBodyChildren(id, body, rootPart))
        {
            if (TryComp<T>(part.Id, out var comp))
                yield return (part.Id, part.Component, comp);
        }
    }

    /// <summary>Непосредственные дочерние части (для груди — голова, руки, пах; для паха — ноги...).</summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!TryComp<ParentOrganComponent>(partId, out var parent))
            yield break;

        var children = parent.Children;
        foreach (var child in children.ToList())
        {
            if (TryComp<BodyPartComponent>(child, out var comp))
                yield return (child, comp);
        }
    }

    public int GetBodyPartCount(EntityUid bodyId, BodyPartType partType, BodyComponent? body = null)
    {
        return GetBodyChildren(bodyId, body).Count(p => p.Component.PartType == partType);
    }

    public bool BodyHasPartType(EntityUid bodyId, BodyPartType type, BodyComponent? body = null)
    {
        return GetBodyChildrenOfType(bodyId, type, body).Any();
    }

    /// <summary>Корневая часть тела — грудь (у нас орган категории Torso).</summary>
    public bool TryGetRootPart(EntityUid bodyId, [NotNullWhen(true)] out Entity<BodyPartComponent>? rootPart, BodyComponent? body = null)
    {
        rootPart = null;
        foreach (var organ in _body.EnumerateOrgans<BodyPartComponent>(bodyId))
        {
            if (organ.Comp2.PartType != BodyPartType.Chest)
                continue;

            rootPart = (organ.Owner, organ.Comp2);
            return true;
        }

        return false;
    }

    /// <summary>Родительская часть тела (для руки — грудь, для кисти — рука, для ноги — пах).</summary>
    public bool TryGetParentBodyPart(
        EntityUid partUid,
        [NotNullWhen(true)] out EntityUid? parentUid,
        [NotNullWhen(true)] out BodyPartComponent? parentComponent)
    {
        parentUid = null;
        parentComponent = null;

        if (!TryComp<ChildOrganComponent>(partUid, out var child) || child.Parent is not { } parent ||
            !TryComp<BodyPartComponent>(parent, out var comp))
        {
            return false;
        }

        parentUid = parent;
        parentComponent = comp;
        return true;
    }

    // ===================== Органы =====================

    /// <summary>Внутренние органы этой части тела (у груди — сердце и лёгкие, у паха — желудок, печень, почки).</summary>
    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!TryComp<ParentOrganComponent>(partId, out var parent))
            yield break;

        var children = parent.Children;
        foreach (var child in children.ToList())
        {
            if (HasComp<InternalChildOrganComponent>(child) && TryComp<OrganComponent>(child, out var organ))
                yield return (child, organ);
        }
    }

    /// <summary>Все внутренние органы тела.</summary>
    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetBodyOrgans(EntityUid? bodyId, BodyComponent? body = null)
    {
        if (bodyId is null)
            yield break;

        foreach (var organ in _body.EnumerateOrgans<OrganComponent>(bodyId.Value))
        {
            if (HasComp<InternalChildOrganComponent>(organ))
                yield return (organ.Owner, organ.Comp1);
        }
    }

    /// <summary>Внутренние органы части тела с нужным компонентом.</summary>
    public bool TryGetBodyPartOrgans(
        EntityUid uid,
        Type type,
        [NotNullWhen(true)] out List<(EntityUid Id, OrganComponent Organ)>? organs,
        BodyPartComponent? part = null)
    {
        organs = new List<(EntityUid Id, OrganComponent Organ)>();
        foreach (var organ in GetPartOrgans(uid, part))
        {
            if (HasComp(organ.Id, type))
                organs.Add(organ);
        }

        if (organs.Count > 0)
            return true;

        organs = null;
        return false;
    }

    /// <summary>Органы тела с компонентом T.</summary>
    public bool TryGetBodyOrganEntityComps<T>(
        Entity<BodyComponent?> entity,
        [NotNullWhen(true)] out List<Entity<T, OrganComponent>>? comps)
        where T : IComponent
    {
        comps = new List<Entity<T, OrganComponent>>();
        foreach (var organ in GetBodyOrgans(entity.Owner))
        {
            if (TryComp<T>(organ.Id, out var comp))
                comps.Add((organ.Id, comp, organ.Component));
        }

        if (comps.Count > 0)
            return true;

        comps = null;
        return false;
    }

    // ===================== Прикрепление и отделение =====================

    /// <summary>
    /// Отделить часть тела (со всеми дочерними частями и органами) — в «отделённое тело», как у ампутации.
    /// slotId сохранён для совместимости сигнатуры и не используется.
    /// </summary>
    public bool DetachPart(EntityUid parentPartId, string slotId, EntityUid partId,
        BodyPartComponent? parentPart = null, BodyPartComponent? part = null)
    {
        return _detachable.Detach(partId) != null;
    }

    /// <summary>
    /// Прикрепить часть тела (и всё, что к ней прикреплено) к родительской части в теле.
    /// slotId сохранён для совместимости сигнатуры и не используется.
    /// </summary>
    public bool AttachPart(EntityUid parentPartId, string slotId, EntityUid partId,
        BodyPartComponent? parentPart = null, BodyPartComponent? part = null)
    {
        if (!TryComp<BodyPartComponent>(parentPartId, out var parentComp) || parentComp.Body is not { } body ||
            !_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
        {
            return false;
        }

        var moving = new List<EntityUid> { partId };
        foreach (var child in _relation.AllChildren(partId))
        {
            moving.Add(child.Owner);
        }

        foreach (var organ in moving)
        {
            _container.Insert(organ, container, force: true);
        }

        _relation.Relate(parentPartId, partId);
        return true;
    }
}
