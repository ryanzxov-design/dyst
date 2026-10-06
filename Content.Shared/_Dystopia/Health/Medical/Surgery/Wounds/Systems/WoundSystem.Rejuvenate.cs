// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: «Вылечить» (админ) лечит и части тела — снимает раны и отращивает отсутствующие части и органы.

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Rejuvenate;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    [Dependency] private OrganRelationSystem _relation = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems.TraumaSystem _trauma = default!;
    [Dependency] private BodySystem _organs = default!; // органы тела (EnumerateOrgans) — в нашей системе тела

    private void OnBodyRejuvenate(Entity<BodyComponent> ent, ref RejuvenateEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent))
            return;

        RestoreMissingOrgans(ent);

        foreach (var (part, _) in _body.GetBodyChildren(ent).ToList())
        {
            // Незакрытые операции тоже «заживают»
            RemComp<IncisionOpenComponent>(part);
            RemComp<SkinRetractedComponent>(part);
            RemComp<BleedersClampedComponent>(part);
            RemComp<InternalBleedersClampedComponent>(part);
            RemComp<BonesSawedComponent>(part);
            RemComp<BonesOpenComponent>(part);
            RemComp<BodyPartSawedComponent>(part);
            RemComp<BoneSetComponent>(part);
            RemComp<BodyPartReattachedComponent>(part);

            if (!TryComp<WoundableComponent>(part, out var woundable))
                continue;

            foreach (var wound in GetWoundableWounds(part, woundable).ToList())
            {
                _container.Remove(wound.Owner, woundable.Wounds, false, true);
                QueueDel(wound);
            }

            // Кости срастаются, органы восстанавливаются
            if (_trauma.GetBone(woundable) is { } bone
                && TryComp<Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components.BoneComponent>(bone, out var boneComp))
            {
                _trauma.SetBoneIntegrity(bone, boneComp.IntegrityCap, boneComp);
            }

            foreach (var (organ, _) in _body.GetPartOrgans(part).ToList())
            {
                _trauma.RestoreOrganIntegrity(organ);
            }

            RemComp<Content.Shared._Dystopia.Health.Medical.Bleeding.TourniquetAppliedComponent>(part);
            RemComp<Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components.BoneSplintedComponent>(part);
            woundable.WoundableIntegrity = woundable.IntegrityCap;
            Dirty(part, woundable);
            CheckWoundableSeverityThresholds(part, woundable);
            UpdateWoundableAppearance(part);
        }

        _lastDamaged.Remove(ent);
        RefreshHierarchy(ent);
        UpdateBodyStatus(ent);
    }

    /// <summary>
    /// Отрастить всё, чего не хватает по устройству тела вида: отрезанные руки и ноги (со всем, что на них),
    /// вынутые органы. Идём от груди по связям InitialBody.
    /// </summary>
    private void RestoreMissingOrgans(EntityUid body)
    {
        if (!TryComp<InitialBodyComponent>(body, out var initial) ||
            !_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
        {
            return;
        }

        var organs = initial.Organs;
        var relationships = initial.Relationships;
        if (relationships is null)
            return;

        var present = new Dictionary<string, EntityUid>();
        foreach (var organ in _organs.EnumerateOrgans<OrganComponent>(body))
        {
            if (organ.Comp1.Category is { } cat)
                present.TryAdd(cat.Id, organ.Owner);
        }

        if (!present.ContainsKey("Torso"))
            return;

        var queue = new Queue<string>();
        queue.Enqueue("Torso");
        var visited = new HashSet<string>();
        while (queue.Count > 0)
        {
            var parentCategory = queue.Dequeue();
            if (!visited.Add(parentCategory) || !present.TryGetValue(parentCategory, out var parentOrgan))
                continue;

            if (!relationships.TryGetValue(parentCategory, out var children))
                continue;

            foreach (var childCategory in children)
            {
                if (!present.ContainsKey(childCategory.Id) && organs.TryGetValue(childCategory, out var proto))
                {
                    var spawned = Spawn(proto.Id);
                    if (_container.Insert(spawned, container, force: true))
                    {
                        _relation.Relate(parentOrgan, spawned);
                        present[childCategory.Id] = spawned;
                    }
                    else
                    {
                        QueueDel(spawned);
                    }
                }

                queue.Enqueue(childCategory.Id);
            }
        }
    }
}
