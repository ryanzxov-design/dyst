// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: боль и болевой шок.

using Content.Shared._Dystopia.Health.Medical.Bleeding;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Medical.Pain;

public sealed partial class PainSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private TraumaSystem _traumas = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Medical.Addiction.AddictionSystem _addiction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PainComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<PainComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.SpeedModifier.TryGetValue(ent.Comp.Level, out var modifier) && modifier < 1f)
            args.ModifySpeed(modifier, modifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PainComponent>();
        while (query.MoveNext(out var uid, out var pain))
        {
            if (now < pain.NextUpdate)
                continue;

            var dt = (float) pain.UpdateInterval.TotalSeconds;
            pain.NextUpdate = now + pain.UpdateInterval;
            UpdatePain((uid, pain), dt, now);
        }
    }

    private void UpdatePain(Entity<PainComponent> ent, float dt, TimeSpan now)
    {
        var comp = ent.Comp;
        var raw = _mobState.IsDead(ent) ? 0f : CalculateRawPain(ent);
        var suppression = CalculateSuppression(ent);
        var analgesia = suppression <= 0f
            ? 0f
            : MathF.Min(comp.MaxAnalgesia, suppression / (suppression + comp.AnalgesiaHalfStrength));
        var shockBlocked = suppression >= comp.ShockBlockStrength;
        var target = raw * (1f - analgesia);

        // Ощущаемая боль плавно догоняет настоящую
        var step = comp.AdjustRate * dt;
        var pain = comp.Pain < target ? MathF.Min(target, comp.Pain + step) : MathF.Max(target, comp.Pain - step);

        var level = PainLevel.None;
        foreach (var (candidate, threshold) in comp.Thresholds)
        {
            if (pain >= threshold && candidate > level)
                level = candidate;
        }

        // Сильное обезболивание снимает болевой шок: боль остаётся сильной, но человек в сознании
        if (shockBlocked && level > PainLevel.Severe)
            level = PainLevel.Severe;

        var changed = MathF.Abs(pain - comp.Pain) > 0.05f || MathF.Abs(raw - comp.RawPain) > 0.05f
            || MathF.Abs(suppression - comp.Suppression) > 0.05f || level != comp.Level
            || MathF.Abs(analgesia - comp.Analgesia) > 0.005f || shockBlocked != comp.ShockBlocked;
        var oldLevel = comp.Level;

        comp.Pain = pain;
        comp.RawPain = raw;
        comp.Suppression = suppression;
        comp.Analgesia = analgesia;
        comp.ShockBlocked = shockBlocked;
        comp.Level = level;
        if (changed)
            Dirty(ent);

        if (level != oldLevel)
        {
            _movement.RefreshMovementSpeedModifiers(ent.Owner);
            if (level > oldLevel && level >= PainLevel.Moderate)
                _popup.PopupEntity(Loc.GetString($"pain-level-{level}"), ent, ent, PopupType.SmallCaution);
        }

        if (_mobState.IsIncapacitated(ent))
            return;

        // Болевой шок: человек падает и какое-то время не может действовать
        if (level == PainLevel.Shock && now >= comp.NextShock)
        {
            comp.NextShock = now + comp.ShockCooldown;
            // Болевой шок — обморок; без системы сознания хотя бы валим с ног
            if (HasComp<ConsciousnessComponent>(ent))
                _consciousness.KnockOut(ent.Owner, comp.ShockDuration);
            else
                _stun.TryUpdateParalyzeDuration(ent, comp.ShockDuration);
            _popup.PopupEntity(Loc.GetString("pain-shock"), ent, ent, PopupType.LargeCaution);
            return;
        }

        if (level >= PainLevel.Moderate && now >= comp.NextComplaint)
        {
            comp.NextComplaint = now + comp.ComplaintInterval;
            _popup.PopupEntity(Loc.GetString($"pain-complaint-{level}"), ent, ent, PopupType.Small);
        }
    }

    /// <summary>Боль без обезболивания: раны, кости, органы, отмирающие под жгутом конечности.</summary>
    public float CalculateRawPain(Entity<PainComponent> ent)
    {
        var comp = ent.Comp;
        var total = 0f;
        foreach (var (part, partComp) in _body.GetBodyChildren(ent.Owner))
        {
            if (!TryComp<WoundableComponent>(part, out var woundable))
                continue;

            // Местная анестезия: эта часть не болит
            if (TryComp<LocalAnesthesiaComponent>(part, out var local) && _timing.CurTime < local.Until)
                continue;

            var partPain = 0f;
            var perSeverity = comp.PainPerSeverity.GetValueOrDefault(partComp.PartType, 0.8f);
            foreach (var wound in _wounds.GetWoundableWounds(part, woundable))
            {
                partPain += wound.Comp.WoundSeverityPoint.Float() * perSeverity;
            }

            var bone = _traumas.GetBoneSeverity(part, effective: false);
            var bonePain = comp.BonePain.GetValueOrDefault(bone, 0f);
            if (HasComp<BoneSplintedComponent>(part))
                bonePain *= comp.SplintPainMultiplier;
            partPain += bonePain;

            foreach (var (organ, _) in _body.GetPartOrgans(part))
            {
                if (TryComp<OrganIntegrityComponent>(organ, out var integrity))
                    partPain += comp.OrganPain.GetValueOrDefault(integrity.Severity, 0f);
            }

            if (TryComp<TourniquetAppliedComponent>(part, out var tourniquet) && _timing.CurTime > tourniquet.NextNecrosis)
                partPain += comp.NecrosisPain;

            if (HasNerveDamage(part, woundable))
                partPain *= comp.NerveDamageMultiplier;

            total += partPain;
        }

        // Ломка болит всем телом
        return total + _addiction.GetWithdrawalPain(ent);
    }

    private bool HasNerveDamage(EntityUid part, WoundableComponent woundable)
    {
        foreach (var trauma in _traumas.GetWoundableTraumas(part, woundable))
        {
            if (trauma.Comp.TraumaType == TraumaSystem.NerveDamage)
                return true;
        }

        return false;
    }

    /// <summary>Сколько боли глушат обезболивающие в крови.</summary>
    public float CalculateSuppression(Entity<PainComponent> ent)
    {
        var total = 0f;
        foreach (var name in ent.Comp.PainkillerSolutions)
        {
            if (!_solutions.TryGetSolution(ent.Owner, name, out _, out var solution))
                continue;

            foreach (var (reagent, perUnit) in ent.Comp.Painkillers)
            {
                var quantity = solution.GetTotalPrototypeQuantity(reagent).Float();
                if (quantity <= 0)
                    continue;

                // Привыкание: у зависимого то же вещество глушит боль слабее
                total += quantity * perUnit * (1f - _addiction.GetTolerance(ent, reagent));
            }
        }

        return total;
    }
}
