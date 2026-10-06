// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: зависимость и ломка.

using Content.Shared._Dystopia.Health.Medical.Chemistry;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Medical.Addiction;

public sealed partial class AddictionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private BloodChemistrySystem _chemistry = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AddictionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            var dt = (float) comp.UpdateInterval.TotalSeconds;
            comp.NextUpdate = now + comp.UpdateInterval;
            if (_mobState.IsDead(uid))
                continue;

            var changed = false;
            foreach (var (name, group) in comp.Groups)
            {
                changed |= UpdateGroup((uid, comp), name, group, dt, now);
            }

            if (changed)
                Dirty(uid, comp);
        }
    }

    private bool UpdateGroup(Entity<AddictionComponent> ent, string name, AddictionGroup group, float dt, TimeSpan now)
    {
        var comp = ent.Comp;
        var oldLevel = comp.Levels.GetValueOrDefault(name);
        var oldStage = comp.Stages.GetValueOrDefault(name);

        // Вещество в крови — привыкание растёт, ломки нет
        var exposure = _chemistry.Weighted(ent, group.Reagents);
        var level = oldLevel;
        if (exposure > 0)
        {
            level = MathF.Min(100f, level + exposure * dt);
            comp.LastDose[name] = now;
        }
        else
        {
            level = MathF.Max(0f, level - group.Decay * dt);
        }

        // Антагонист (налоксон) у зависимого — мгновенная ломка: как будто последняя доза была давно
        if (level >= group.AddictedLevel && group.StageDelays.Count >= 2)
        {
            foreach (var antagonist in group.Antagonists)
            {
                if (_chemistry.GetQuantity(ent, antagonist) <= 0)
                    continue;

                exposure = 0;
                var forced = now - group.StageDelays[1];
                if (!comp.LastDose.TryGetValue(name, out var lastDose) || lastDose > forced)
                    comp.LastDose[name] = forced;
                break;
            }
        }

        var stage = 0;
        if (exposure <= 0 && level >= group.AddictedLevel && comp.LastDose.TryGetValue(name, out var last))
        {
            var since = now - last;
            for (var i = 0; i < group.StageDelays.Count; i++)
            {
                if (since >= group.StageDelays[i])
                    stage = i + 1;
            }

            // Тяжёлая ломка — только при сильном привыкании
            if (stage >= 3 && level < group.SevereLevel)
                stage = 2;
        }

        comp.Levels[name] = level;
        comp.Stages[name] = stage;

        if (stage > 0)
            Symptoms(ent, name, group, stage, oldStage, now);

        return MathF.Abs(level - oldLevel) > 0.5f || stage != oldStage
               || (level >= group.AddictedLevel) != (oldLevel >= group.AddictedLevel);
    }

    private void Symptoms(Entity<AddictionComponent> ent, string name, AddictionGroup group, int stage, int oldStage, TimeSpan now)
    {
        var comp = ent.Comp;
        if (stage != oldStage || !comp.NextSymptom.TryGetValue(name, out var next) || now >= next)
        {
            comp.NextSymptom[name] = now + group.SymptomInterval;
            _popup.PopupEntity(Loc.GetString($"addiction-withdrawal-{stage}"), ent, ent,
                stage >= 3 ? PopupType.LargeCaution : PopupType.SmallCaution);

            if (stage >= 2)
                _jitter.DoJitter(ent, TimeSpan.FromSeconds(8), true, 6f * stage, 4f);

            if (stage >= 3 && !_mobState.IsIncapacitated(ent))
            {
                _stun.TryUpdateParalyzeDuration(ent, group.SeizureDuration);
                if (_body.TryGetRootPart(ent, out var root))
                    _wounds.DamagePart(ent, root.Value.Owner, new DamageSpecifier(group.SeizureDamage));
            }
        }
    }

    /// <summary>Боль от ломки (все группы).</summary>
    public float GetWithdrawalPain(EntityUid body)
    {
        if (!TryComp<AddictionComponent>(body, out var comp))
            return 0f;

        var pain = 0f;
        foreach (var (name, group) in comp.Groups)
        {
            var stage = comp.Stages.GetValueOrDefault(name);
            if (stage > 0 && stage <= group.StagePain.Count)
                pain += group.StagePain[stage - 1];
        }

        return pain;
    }

    /// <summary>Насколько слабее действует обезболивание этим веществом (0 — как обычно, 0.5 — вдвое слабее).</summary>
    public float GetTolerance(EntityUid body, ProtoId<ReagentPrototype> reagent)
    {
        if (!TryComp<AddictionComponent>(body, out var comp))
            return 0f;

        var tolerance = 0f;
        foreach (var (name, group) in comp.Groups)
        {
            if (!group.Reagents.ContainsKey(reagent))
                continue;

            var level = comp.Levels.GetValueOrDefault(name);
            tolerance = MathF.Max(tolerance, level / 100f * group.MaxTolerance);
        }

        return tolerance;
    }
}
