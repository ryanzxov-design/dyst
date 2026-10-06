// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: во вкладке осмотра «Здоровье» видно, что с частями тела.

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared._Dystopia.Health.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.HealthExaminable;
using Content.Shared.IdentityManagement;

namespace Content.Shared._Dystopia.Health.Examine;

public sealed partial class HealthPartsExamineSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;

    /// <summary>Порядок частей в описании: сверху вниз.</summary>
    private static readonly TargetBodyPart[] Order =
    {
        TargetBodyPart.Head, TargetBodyPart.Chest, TargetBodyPart.LeftArm, TargetBodyPart.LeftHand,
        TargetBodyPart.RightArm, TargetBodyPart.RightHand, TargetBodyPart.Groin,
        TargetBodyPart.LeftLeg, TargetBodyPart.LeftFoot, TargetBodyPart.RightLeg, TargetBodyPart.RightFoot,
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TargetingComponent, HealthBeingExaminedEvent>(OnHealthExamined);
    }

    private void OnHealthExamined(EntityUid uid, TargetingComponent comp, HealthBeingExaminedEvent args)
    {
        var ent = new Entity<TargetingComponent>(uid, comp);
        var parts = new Dictionary<TargetBodyPart, (EntityUid Id, BodyPartComponent Part)>();
        foreach (var (id, part) in _body.GetBodyChildren(ent))
        {
            parts.TryAdd(_body.GetTargetBodyPart(part), (id, part));
        }

        // Не тело с частями (или части ещё не пришли по сети) — молчим
        if (parts.Count == 0)
            return;

        var lines = new List<string>();
        foreach (var target in Order)
        {
            if (!parts.TryGetValue(target, out var entry))
            {
                lines.Add(Loc.GetString("health-examine-missing", ("part", PartName(target))));
                continue;
            }

            if (!TryComp<WoundableComponent>(entry.Id, out var woundable) || woundable.WoundableSeverity == WoundableSeverity.Healthy)
                continue;

            var wounds = _wounds.GetWoundableWounds(entry.Id, woundable)
                .Where(w => w.Comp.WoundVisibility == WoundVisibility.Always && !w.Comp.IsScar)
                .Select(w => Loc.GetString($"part-status-damage-{w.Comp.DamageType.Id}"))
                .Distinct()
                .ToList();

            var state = Loc.GetString($"part-status-severity-{woundable.WoundableSeverity}");
            lines.Add(wounds.Count == 0
                ? Loc.GetString("health-examine-part", ("part", PartName(target)), ("state", state))
                : Loc.GetString("health-examine-part-wounds", ("part", PartName(target)), ("state", state),
                    ("wounds", string.Join(", ", wounds))));
        }

        if (lines.Count == 0)
            return;

        var msg = args.Message;
        if (!msg.IsEmpty)
            msg.PushNewline();

        msg.AddMarkupOrThrow(Loc.GetString("health-examine-header", ("target", Identity.Entity(ent, EntityManager))));
        foreach (var line in lines)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(line);
        }
    }

    private string PartName(TargetBodyPart part)
    {
        var (type, symmetry) = _body.ConvertTargetBodyPart(part);
        return Loc.GetString($"part-status-part-{type}-{symmetry}");
    }
}
