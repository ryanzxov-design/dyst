// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: мазутная форсунка — жжёт жидкое топливо в горне, что прямо перед ней (см. DystopiaFuelBurnerComponents).

using Content.Shared._Dystopia.Industry;
using Content.Shared.Audio;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaFuelBurnerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DystopiaForgeSystem _forge = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFuelBurnerComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<DystopiaFuelBurnerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaFuelBurnerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnActivate(Entity<DystopiaFuelBurnerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        Toggle(ent, args.User);
        args.Handled = true;
    }

    private void OnGetVerbs(Entity<DystopiaFuelBurnerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.On ? "dystopia-burner-verb-off" : "dystopia-burner-verb-on"),
            Act = () => Toggle(ent, user),
            Priority = 3,
        });
    }

    private void Toggle(Entity<DystopiaFuelBurnerComponent> ent, EntityUid user)
    {
        ent.Comp.On = !ent.Comp.On;
        _popup.PopupEntity(Loc.GetString(ent.Comp.On ? "dystopia-burner-turned-on" : "dystopia-burner-turned-off"),
            ent, user);
        if (!ent.Comp.On)
            SetBurning(ent, false);
    }

    /// <summary>Горн прямо перед форсункой (не электрический).</summary>
    private Entity<DystopiaForgeComponent>? FindForge(EntityUid uid)
    {
        var xform = Transform(uid);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates).Offset(xform.LocalRotation.GetCardinalDir());
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (TryComp<DystopiaForgeComponent>(other, out var forge) && !forge.Electric)
                return (other, forge);
        }

        return null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaFuelBurnerComponent>();
        while (query.MoveNext(out var uid, out var burner))
        {
            if (now < burner.NextUpdate)
                continue;

            burner.NextUpdate = now + TimeSpan.FromSeconds(burner.Interval);
            if (!burner.On)
                continue;

            SetBurning((uid, burner), Burn((uid, burner)));
        }
    }

    /// <summary>Подкинуть горну топлива (сколько влезет) и раздуть его. Горит — если есть и горн, и топливо.</summary>
    private bool Burn(Entity<DystopiaFuelBurnerComponent> ent)
    {
        var burner = ent.Comp;
        if (FindForge(ent) is not { } forge ||
            !_solution.TryGetSolution(ent.Owner, burner.Solution, out var solnEnt, out var solution))
        {
            return false;
        }

        var budget = burner.Rate * burner.Interval;
        var any = false;
        foreach (var (reagent, perUnit) in burner.Fuels)
        {
            var have = solution.GetTotalPrototypeQuantity(reagent).Float();
            if (have <= 0f || perUnit <= 0f)
                continue;

            any = true;
            if (budget <= 0f)
                break;

            var room = (forge.Comp.MaxFuel - forge.Comp.Fuel) / perUnit;
            var units = MathF.Min(MathF.Min(have, budget), room);
            if (units > 0.01f)
            {
                _forge.Feed(forge, units * perUnit);
                _solution.RemoveReagent(solnEnt.Value, reagent.Id, FixedPoint2.New(units));
                budget -= units;
            }
        }

        if (!any)
            return false;

        // Горн полон, но не горит — форсунка разжигает его своим факелом.
        if (!forge.Comp.Lit)
            _forge.Feed(forge, 0f);

        _forge.Blow(forge, burner.Interval * 2f);
        return forge.Comp.Lit;
    }

    private void SetBurning(Entity<DystopiaFuelBurnerComponent> ent, bool burning)
    {
        if (ent.Comp.Burning == burning)
            return;

        ent.Comp.Burning = burning;
        _appearance.SetData(ent, DystopiaFuelBurnerVisuals.Burning, burning);
        _ambient.SetAmbience(ent, burning);
    }

    private void OnExamined(Entity<DystopiaFuelBurnerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Burning ? "dystopia-burner-examine-burning"
            : ent.Comp.On ? "dystopia-burner-examine-idle" : "dystopia-burner-examine-off"));
        if (FindForge(ent) == null)
            args.PushMarkup(Loc.GetString("dystopia-burner-examine-no-forge"));

        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
        {
            var fuel = 0f;
            foreach (var reagent in ent.Comp.Fuels.Keys)
            {
                fuel += solution.GetTotalPrototypeQuantity(reagent).Float();
            }

            args.PushMarkup(Loc.GetString("dystopia-burner-examine-fuel",
                ("amount", (int) MathF.Round(fuel)), ("max", (int) solution.MaxVolume.Float())));
        }
    }
}
