// SPDX-License-Identifier: AGPL-3.0-or-later
// Хирургия Dystopia: тело без ног и с протезами.

using Content.Shared.Body;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;

namespace Content.Shared._Dystopia.Health.Surgery;

/// <summary>
/// Без ноги (любой) встать нельзя — человек падает и ползёт, пока ему не пришьют ногу или протез.
/// Без стопы — хромает. Деревянные протезы медленнее живых ног.
/// Общая система (клиент и сервер), чтобы движение предсказывалось без рывков.
/// </summary>
public sealed partial class SurgeryLimbLossSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private const float NoFoot = 0.7f;
    private const float Minimum = 0.15f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryLimbLossComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SurgeryLimbLossComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<SurgeryLimbLossComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<SurgeryLimbLossComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SurgeryLimbLossComponent, StandUpAttemptEvent>(OnStandUpAttempt);
    }

    private void OnStartup(Entity<SurgeryLimbLossComponent> ent, ref ComponentStartup args) => Refresh(ent);

    private void OnOrganInserted(Entity<SurgeryLimbLossComponent> ent, ref OrganInsertedIntoEvent args) => Refresh(ent);

    private void OnOrganRemoved(Entity<SurgeryLimbLossComponent> ent, ref OrganRemovedFromEvent args) => Refresh(ent);

    private void Refresh(EntityUid uid)
    {
        _movement.RefreshMovementSpeedModifiers(uid);

        // Нет ноги — человек падает и дальше только ползёт
        if (!HasBothLegs(uid) && !_standing.IsDown(uid))
            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(1), refresh: true, autoStand: false, drop: false, force: true);
    }

    private void OnStandUpAttempt(Entity<SurgeryLimbLossComponent> ent, ref StandUpAttemptEvent args)
    {
        if (HasBothLegs(ent))
            return;

        args.Cancelled = true;
        args.Autostand = false;
        args.Message = (Loc.GetString("surgery-no-leg-stand"), PopupType.SmallCaution);
    }

    private Dictionary<string, float> Limbs(EntityUid uid)
    {
        var present = new Dictionary<string, float>();
        foreach (var organ in _body.EnumerateOrgans<OrganComponent>(uid))
        {
            if (organ.Comp1.Category is not { } cat)
                continue;

            present[cat.Id] = TryComp<SurgeryProstheticComponent>(organ, out var prosthetic) ? prosthetic.SpeedMultiplier : 1f;
        }

        return present;
    }

    public bool HasBothLegs(EntityUid uid)
    {
        var limbs = Limbs(uid);
        return limbs.ContainsKey("LegLeft") && limbs.ContainsKey("LegRight");
    }

    private void OnRefreshSpeed(Entity<SurgeryLimbLossComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var limbs = Limbs(ent);
        var speed = Side(limbs, "LegLeft", "FootLeft") * Side(limbs, "LegRight", "FootRight");
        speed = MathF.Max(Minimum, speed);
        if (speed < 0.999f)
            args.ModifySpeed(speed, speed);
    }

    /// <summary>Нет ноги — модификатора нет (скорость задаёт ползание), нет стопы — хромота, протез — свой множитель.</summary>
    private static float Side(Dictionary<string, float> limbs, string leg, string foot)
    {
        if (!limbs.TryGetValue(leg, out var legMult))
            return 1f;

        return limbs.TryGetValue(foot, out var footMult) ? legMult * footMult : legMult * NoFoot;
    }
}
