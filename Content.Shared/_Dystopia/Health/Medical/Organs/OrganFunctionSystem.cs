// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: последствия повреждённых органов, смерть мозга, восстановление органов.

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Medical.Organs;

public sealed partial class OrganFunctionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private TraumaSystem _traumas = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Medical.Chemistry.BloodChemistrySystem _chemistry = default!;
    [Dependency] private Robust.Shared.Random.IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganFunctionComponent, OrganDestroyedEvent>(OnOrganDestroyed);
    }

    /// <summary>Мозг разрушен — человек умирает, сам орган остаётся (разрушенным) на месте.</summary>
    private void OnOrganDestroyed(Entity<OrganFunctionComponent> ent, ref OrganDestroyedEvent args)
    {
        if (args.Handled || args.Category is not { } category || !ent.Comp.FatalOrgans.Contains(category))
            return;

        args.Handled = true;
        if (_mobState.IsDead(ent))
            return;

        _popup.PopupEntity(Loc.GetString("organ-fatal-destroyed", ("organ", Name(args.Organ))), ent, ent, PopupType.LargeCaution);
        _mobState.ChangeMobState(ent, MobState.Dead);
    }

    /// <summary>Доля работы органа: 1 — цел, 0 — разрушен.</summary>
    public static float Efficiency(OrganIntegrityComponent? integrity)
    {
        if (integrity == null || integrity.IntegrityCap <= 0)
            return 1f;

        return Math.Clamp((integrity.Integrity / integrity.IntegrityCap).Float(), 0f, 1f);
    }

    /// <summary>Множитель кровотечения от повреждённого сердца.</summary>
    public float GetBleedMultiplier(EntityUid body)
    {
        if (!TryComp<OrganFunctionComponent>(body, out var comp))
            return 1f;

        var heart = 1f;
        foreach (var (organ, category) in Organs(body))
        {
            if (category == "Heart")
                heart = MathF.Min(heart, Efficiency(CompOrNull<OrganIntegrityComponent>(organ)));
        }

        return 1f + (1f - heart) * (comp.HeartBleedMultiplier - 1f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<OrganFunctionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateInterval;
            if (_mobState.IsDead(uid))
                continue;

            UpdateBody((uid, comp), now);
        }
    }

    private void UpdateBody(Entity<OrganFunctionComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        var seconds = (float) comp.UpdateInterval.TotalSeconds;
        var recovery = comp.RecoveryPerSecond * seconds * RecoveryMultiplier(ent);
        var eyesImpaired = false;
        DamageSpecifier? failure = null;

        foreach (var (organ, category) in Organs(ent))
        {
            // Вещества, которые в большой дозе бьют по органу (парацетамол — печень, дигоксин — сердце)
            foreach (var toxin in comp.OrganToxins)
            {
                if (toxin.Organ == category && _chemistry.GetQuantity(ent, toxin.Reagent) >= toxin.Threshold)
                    _traumas.DebugDamageOrgan(organ, toxin.Damage);
            }

            if (!TryComp<OrganIntegrityComponent>(organ, out var integrity))
                continue;

            // Восстановление (разрушенный орган сам не восстанавливается); точечные лекарства — сильнее
            if (integrity.Severity != OrganSeverity.Destroyed && integrity.Integrity < integrity.IntegrityCap)
            {
                var organRecovery = recovery;
                if (category != null && comp.OrganRecoveryBoost.TryGetValue(category, out var boost))
                    organRecovery *= 1f + _chemistry.Weighted(ent, boost);
                _traumas.RecoverOrgan(organ, organRecovery, integrity);
            }

            var efficiency = Efficiency(integrity);
            if (efficiency >= 1f || category == null)
                continue;

            if (category == "Eyes" && integrity.Severity != OrganSeverity.Normal)
                eyesImpaired = true;

            if (comp.FailureDamage.TryGetValue(category, out var damage))
            {
                failure ??= new DamageSpecifier();
                failure += damage * (1f - efficiency);
            }

            if (efficiency < comp.WarningEfficiency
                && (!comp.NextWarning.TryGetValue(category, out var next) || now >= next))
            {
                comp.NextWarning[category] = now + comp.WarningInterval;
                var key = $"organ-failing-{category}";
                if (Loc.TryGetString(key, out var message))
                    _popup.PopupEntity(message, ent, ent, PopupType.SmallCaution);
            }
        }

        // Лекарства нервов понемногу снимают повреждение нервов
        var nerveChance = _chemistry.Weighted(ent, comp.NerveRepair);
        if (nerveChance > 0 && _random.Prob(MathF.Min(1f, nerveChance)))
            RepairOneNerve(ent);

        // Последствия: урон идёт в грудь (удушье проходит в тело, яд оставляет рану)
        if (failure != null && !failure.Empty && _body.TryGetRootPart(ent, out var root))
            _wounds.DamagePart(ent, root.Value.Owner, failure);

        if (eyesImpaired != comp.EyesImpaired)
        {
            comp.EyesImpaired = eyesImpaired;
            _blindable.SetMinDamage(ent.Owner, eyesImpaired ? comp.ImpairedEyeDamage : 0);
        }
    }

    private void RepairOneNerve(EntityUid body)
    {
        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            foreach (var trauma in _traumas.GetWoundableTraumas(part))
            {
                if (trauma.Comp.TraumaType != TraumaSystem.NerveDamage)
                    continue;

                _traumas.RemoveTrauma(trauma);
                return;
            }
        }
    }

    private float RecoveryMultiplier(Entity<OrganFunctionComponent> ent)
    {
        var multiplier = 1f;
        foreach (var name in ent.Comp.ReagentSolutions)
        {
            if (!_solutions.TryGetSolution(ent.Owner, name, out _, out var solution))
                continue;

            foreach (var (reagent, perUnit) in ent.Comp.RecoveryBoost)
            {
                multiplier += solution.GetTotalPrototypeQuantity(reagent).Float() * perUnit;
            }
        }

        return MathF.Min(multiplier, ent.Comp.MaxRecoveryMultiplier);
    }

    private IEnumerable<(EntityUid Organ, string? Category)> Organs(EntityUid body)
    {
        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            foreach (var (organ, organComp) in _body.GetPartOrgans(part))
            {
                yield return (organ, organComp.Category?.Id);
            }
        }
    }
}
