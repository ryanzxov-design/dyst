// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: обмороки от кровопотери и боли, значок боли.

using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Medical.Pain;

public sealed partial class ConsciousnessSystem : EntitySystem
{
    private static readonly ProtoId<AlertPrototype> PainAlert = "DystopiaPain";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SleepingSystem _sleeping = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ConsciousnessComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextCheck)
                continue;

            comp.NextCheck = now + comp.CheckInterval;
            Check((uid, comp), now);
        }
    }

    private void Check(Entity<ConsciousnessComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        UpdatePainAlert(ent);

        if (_mobState.IsIncapacitated(ent))
        {
            SetUnconscious(ent, false);
            return;
        }

        var blood = _bloodstream.GetBloodLevel(ent.Owner);

        // Слишком мало крови — без сознания, пока её не восполнят
        if (blood < comp.UnconsciousBloodLevel)
        {
            if (!comp.Unconscious)
                _popup.PopupEntity(Loc.GetString("consciousness-blood-out"), ent, ent, PopupType.LargeCaution);

            KnockOut(ent.Owner, comp.CheckInterval + TimeSpan.FromSeconds(1));
            SetUnconscious(ent, true);
            return;
        }

        SetUnconscious(ent, false);

        // Обморок закончился — приходит в себя сам (обычный сон так не прерывается)
        if (comp.KnockedOutUntil is { } until && now >= until)
        {
            comp.KnockedOutUntil = null;
            _sleeping.TryWaking(ent.Owner);
        }

        // Мало крови — случайные обмороки, тем чаще, чем ближе к пределу
        if (blood < comp.FaintBloodLevel && now >= comp.NextFaint)
        {
            var depth = (comp.FaintBloodLevel - blood) / MathF.Max(0.01f, comp.FaintBloodLevel - comp.UnconsciousBloodLevel);
            if (_random.Prob(Math.Clamp(depth, 0f, 1f) * comp.MaxFaintChance))
            {
                comp.NextFaint = now + comp.FaintCooldown;
                _popup.PopupEntity(Loc.GetString("consciousness-faint-blood"), ent, ent, PopupType.LargeCaution);
                KnockOut(ent.Owner, comp.FaintDuration);
                return;
            }
        }

        if (blood < comp.DizzyBloodLevel && now >= comp.NextDizzy)
        {
            comp.NextDizzy = now + comp.DizzyInterval;
            _popup.PopupEntity(Loc.GetString("consciousness-dizzy"), ent, ent, PopupType.SmallCaution);
        }
    }

    /// <summary>Потеря сознания на время (принудительный сон).</summary>
    public void KnockOut(Entity<ConsciousnessComponent?> ent, TimeSpan duration)
    {
        if (!Resolve(ent, ref ent.Comp, false) || _mobState.IsDead(ent))
            return;

        if (!_status.TryUpdateStatusEffectDuration(ent, ent.Comp.UnconsciousEffect, duration))
            return;

        var until = _timing.CurTime + duration;
        if (ent.Comp.KnockedOutUntil is not { } current || until > current)
            ent.Comp.KnockedOutUntil = until;
    }

    private void SetUnconscious(Entity<ConsciousnessComponent> ent, bool value)
    {
        if (ent.Comp.Unconscious == value)
            return;

        ent.Comp.Unconscious = value;
        Dirty(ent);
    }

    /// <summary>Значок боли: от лёгкой до болевого шока, нет боли — значка нет.</summary>
    private void UpdatePainAlert(EntityUid uid)
    {
        if (!TryComp<PainComponent>(uid, out var pain) || pain.Level == PainLevel.None || _mobState.IsDead(uid))
        {
            _alerts.ClearAlert(uid, PainAlert);
            return;
        }

        _alerts.ShowAlert(uid, PainAlert, (short) ((int) pain.Level - 1));
    }
}
