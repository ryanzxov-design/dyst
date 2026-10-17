// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: закалка — вода (и другие охладители) на раскалённом тигле или изложнице вскипает и забирает жар.
// Брызнули (распылитель, огнетушитель, плеснули) — остывает сразу; налили внутрь — охладитель выкипает.

using Content.Server.Atmos.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaQuenchSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaQuenchableComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<DystopiaQuenchableComponent, SolutionTransferredEvent>(OnTransferred);
    }

    /// <summary>Температура предмета, K (null — система предмета её не ведёт).</summary>
    public float? GetTemperature(EntityUid uid)
    {
        var ev = new DystopiaGetTemperatureEvent();
        RaiseLocalEvent(uid, ref ev);
        return ev.Temperature;
    }

    private bool IsHot(Entity<DystopiaQuenchableComponent> ent)
    {
        return GetTemperature(ent) is { } temperature && temperature >= ent.Comp.MinTemperature;
    }

    // Брызнули на раскалённое.
    private void OnReaction(Entity<DystopiaQuenchableComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch ||
            !ent.Comp.Coolants.TryGetValue(args.Reagent.ID, out var perUnit) ||
            !IsHot(ent))
        {
            return;
        }

        Cool(ent, (float) args.ReagentQuantity.Quantity * perUnit, (float) args.ReagentQuantity.Quantity);
    }

    // Налили внутрь.
    private void OnTransferred(Entity<DystopiaQuenchableComponent> ent, ref SolutionTransferredEvent args)
    {
        TryEvaporate(ent.Owner);
    }

    /// <summary>
    /// Если предмет горячий — налитые в него охладители выкипают и остужают его. Вызывают и системы предметов
    /// (вода, налитая в холодный тигель, выкипит, когда тигель нагреют).
    /// </summary>
    public bool TryEvaporate(Entity<DystopiaQuenchableComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) ||
            !IsHot((ent.Owner, ent.Comp)) ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution))
        {
            return false;
        }

        var cooling = 0f;
        var units = 0f;
        foreach (var (reagent, perUnit) in ent.Comp.Coolants)
        {
            var amount = solution.GetTotalPrototypeQuantity(reagent);
            if (amount <= FixedPoint2.Zero)
                continue;

            _solution.RemoveReagent(soln.Value, reagent.Id, amount);
            cooling += (float) amount * perUnit;
            units += (float) amount;
        }

        if (cooling <= 0f)
            return false;

        Cool((ent.Owner, ent.Comp), cooling, units);
        return true;
    }

    /// <summary>
    /// Остудить горячий предмет охладителями из чужого раствора (например, из жидкостной сети), не наливая их внутрь:
    /// забирает из <paramref name="source"/> не больше <paramref name="max"/> единиц охладителей. Холодный — ничего.
    /// </summary>
    public FixedPoint2 TryQuench(Entity<DystopiaQuenchableComponent?> ent, Solution source, FixedPoint2 max)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !IsHot((ent.Owner, ent.Comp)))
            return FixedPoint2.Zero;

        var taken = FixedPoint2.Zero;
        var cooling = 0f;
        foreach (var (reagent, perUnit) in ent.Comp.Coolants)
        {
            var amount = FixedPoint2.Min(max - taken, source.GetTotalPrototypeQuantity(reagent));
            if (amount <= FixedPoint2.Zero)
                continue;

            source.RemoveReagent(reagent.Id, amount);
            taken += amount;
            cooling += (float) amount * perUnit;
        }

        if (cooling > 0f)
            Cool((ent.Owner, ent.Comp), cooling, (float) taken);

        return taken;
    }

    private void Cool(Entity<DystopiaQuenchableComponent> ent, float kelvin, float units)
    {
        var ev = new DystopiaCoolEvent(kelvin);
        RaiseLocalEvent(ent, ref ev);

        // Вскипевшая вода уходит паром в воздух клетки (ванильный водяной пар — его видно облаком).
        if (units > 0f && _atmosphere.GetContainingMixture(ent.Owner, false, true) is { } air)
            air.AdjustMoles(Gas.WaterVapor, units * ent.Comp.VaporPerUnit);

        var now = _timing.CurTime;
        if (now < ent.Comp.NextEffect)
            return;

        ent.Comp.NextEffect = now + TimeSpan.FromSeconds(ent.Comp.EffectCooldown);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString("dystopia-quench-hiss", ("item", ent.Owner)), ent);
        if (ent.Comp.SteamEffect is { } steam)
            Spawn(steam.Id, Transform(ent).Coordinates);
    }
}
