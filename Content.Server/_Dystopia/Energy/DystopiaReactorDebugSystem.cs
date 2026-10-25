// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: отладка реактора.
// «Размещение реактора» — тычок в клетку: вокруг неё строится реактор по шаблону (ТВЭЛы вставлены, барабаны залиты);
// альтернативное действие — следующий шаблон.
// «Пульт отладки реактора» — тычок в колонну, барабан или пульт привязывает реактор и открывает окно: температура,
// ксенон, стержни, выгорание и активность ТВЭЛов, вода и давление барабанов, ускорение времени, аварии.

using System.Linq;
using System.Text;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaReactorDebugSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DystopiaReactorSystem _reactor = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaReactorPlacerComponent, AfterInteractEvent>(OnPlace);
        SubscribeLocalEvent<DystopiaReactorPlacerComponent, GetVerbsEvent<AlternativeVerb>>(OnPlacerVerbs);
        SubscribeLocalEvent<DystopiaReactorPlacerComponent, ExaminedEvent>(OnPlacerExamined);

        SubscribeLocalEvent<DystopiaReactorDebugToolComponent, AfterInteractEvent>(OnLink);
        SubscribeLocalEvent<DystopiaReactorDebugToolComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DystopiaReactorDebugToolComponent, BoundUIOpenedEvent>(OnOpened);
        Subs.BuiEvents<DystopiaReactorDebugToolComponent>(DystopiaReactorDebugUiKey.Key, subs =>
        {
            subs.Event<DystopiaReactorDebugActionMessage>(OnAction);
        });
    }

    // ------------------------------------------------------------------ размещение

    private void OnPlacerExamined(Entity<DystopiaReactorPlacerComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Layouts.Count == 0)
            return;

        var layout = ent.Comp.Layouts[Math.Clamp(ent.Comp.Index, 0, ent.Comp.Layouts.Count - 1)];
        args.PushMarkup(Loc.GetString("dystopia-reactor-placer-examine", ("layout", Loc.GetString(layout.Name))));
    }

    private void OnPlacerVerbs(Entity<DystopiaReactorPlacerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || ent.Comp.Layouts.Count < 2)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-reactor-placer-next"),
            Act = () =>
            {
                ent.Comp.Index = (ent.Comp.Index + 1) % ent.Comp.Layouts.Count;
                _popup.PopupEntity(Loc.GetString("dystopia-reactor-placer-examine",
                    ("layout", Loc.GetString(ent.Comp.Layouts[ent.Comp.Index].Name))), ent, user);
            },
        });
    }

    private static List<string> Tokens(string row)
    {
        return row.Contains(' ')
            ? row.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
            : row.Select(c => c.ToString()).ToList();
    }

    private void OnPlace(Entity<DystopiaReactorPlacerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || ent.Comp.Layouts.Count == 0)
            return;

        args.Handled = true;
        if (_xform.GetGrid(args.ClickLocation) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-reactor-placer-no-grid"), ent, args.User);
            return;
        }

        var layout = ent.Comp.Layouts[Math.Clamp(ent.Comp.Index, 0, ent.Comp.Layouts.Count - 1)];
        var rows = layout.Rows.Select(Tokens).ToList();
        var height = rows.Count;
        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var center = _map.TileIndicesFor(gridUid, grid, args.ClickLocation);
        var origin = center + new Vector2i(-width / 2, (height - 1) / 2);
        var placed = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < rows[y].Count; x++)
            {
                var key = rows[y][x];
                if (!ent.Comp.Legend.TryGetValue(key, out var proto))
                    continue;

                var tile = origin + new Vector2i(x, -y);
                var spawned = Spawn(proto, _map.GridTileToLocal(gridUid, grid, tile));
                placed++;

                if (ent.Comp.FuelKeys.Contains(key) && layout.Rod is { } rodProto &&
                    TryComp<DystopiaReactorColumnComponent>(spawned, out var col))
                {
                    var slot = _container.EnsureContainer<ContainerSlot>(spawned, col.FuelSlot);
                    var rod = Spawn(rodProto, _map.GridTileToLocal(gridUid, grid, tile));
                    if (!_container.Insert(rod, slot))
                        QueueDel(rod);
                }

                if (TryComp<DystopiaReactorSeparatorComponent>(spawned, out var sep))
                    FillSeparator(spawned, sep);
            }
        }

        _popup.PopupEntity(Loc.GetString("dystopia-reactor-placer-done", ("count", placed),
            ("layout", Loc.GetString(layout.Name))), ent, args.User);
    }

    private void FillSeparator(EntityUid uid, DystopiaReactorSeparatorComponent sep)
    {
        if (!_solution.TryGetSolution(uid, sep.WaterSolution, out var solEnt, out var sol))
            return;

        var free = sol.MaxVolume - sol.Volume;
        if (free > FixedPoint2.Zero)
            _solution.TryAddReagent(solEnt.Value, sep.Water, free, out _);
    }

    // ------------------------------------------------------------------ пульт отладки

    private void OnLink(Entity<DystopiaReactorDebugToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        if (_reactor.ReactorNear(target) == null)
            return;

        args.Handled = true;
        ent.Comp.Target = target;
        _popup.PopupEntity(Loc.GetString("dystopia-reactor-debug-linked"), ent, args.User);
        _ui.OpenUi(ent.Owner, DystopiaReactorDebugUiKey.Key, args.User);
        UpdateUi(ent);
    }

    private void OnUseInHand(Entity<DystopiaReactorDebugToolComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.OpenUi(ent.Owner, DystopiaReactorDebugUiKey.Key, args.User);
        UpdateUi(ent);
    }

    private void OnOpened(Entity<DystopiaReactorDebugToolComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private DystopiaReactorSystem.Reactor? Linked(Entity<DystopiaReactorDebugToolComponent> ent)
    {
        return ent.Comp.Target is { } target && !TerminatingOrDeleted(target) ? _reactor.ReactorNear(target) : null;
    }

    private IEnumerable<(EntityUid Uid, DystopiaReactorColumnComponent Col)> Columns(DystopiaReactorSystem.Reactor reactor)
    {
        foreach (var uid in reactor.Columns)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<DystopiaReactorColumnComponent>(uid, out var col))
                yield return (uid, col);
        }
    }

    private IEnumerable<DystopiaFuelRodComponent> Rods(DystopiaReactorSystem.Reactor reactor)
    {
        foreach (var (uid, col) in Columns(reactor))
        {
            if (_reactor.RodOf(uid, col) is { } rod && TryComp<DystopiaFuelRodComponent>(rod, out var comp))
                yield return comp;
        }
    }

    private void OnAction(Entity<DystopiaReactorDebugToolComponent> ent, ref DystopiaReactorDebugActionMessage args)
    {
        var value = float.IsFinite(args.Value) ? args.Value : 0f;
        if (args.Action == DystopiaReactorDebugActions.Speed)
        {
            _reactor.Speed = Math.Clamp((int) MathF.Round(value), 1, 50);
            UpdateUi(ent);
            return;
        }

        if (Linked(ent) is not { } reactor || _reactor.Settings() is not { } cfg)
        {
            UpdateUi(ent);
            return;
        }

        var center = _reactor.Center(reactor);
        switch (args.Action)
        {
            case DystopiaReactorDebugActions.Temperature:
                foreach (var (_, col) in Columns(reactor))
                    col.Temperature = Math.Clamp(value, -50f, 3000f);
                break;
            case DystopiaReactorDebugActions.ClearXenon:
                foreach (var (_, col) in Columns(reactor))
                {
                    col.Xenon = 0f;
                    col.Iodine = 0f;
                }

                break;
            case DystopiaReactorDebugActions.MaxXenon:
                foreach (var (_, col) in Columns(reactor))
                {
                    if (col.Kind == DystopiaReactorColumnKind.Fuel)
                        col.Xenon = cfg.MaxXenonPoison / MathF.Max(0.0001f, cfg.XenonWorth);
                }

                break;
            case DystopiaReactorDebugActions.Rods:
                foreach (var (_, col) in Columns(reactor))
                {
                    if (col.Kind != DystopiaReactorColumnKind.Control)
                        continue;

                    col.Insertion = col.Target = Math.Clamp(value, 0f, 1f);
                    col.Scrammed = false;
                }

                break;
            case DystopiaReactorDebugActions.ResetScram:
                _reactor.ResetScram(reactor);
                break;
            case DystopiaReactorDebugActions.Burnup:
                foreach (var rod in Rods(reactor))
                    rod.Burnup = Math.Clamp(value, 0f, 1f);
                break;
            case DystopiaReactorDebugActions.Activity:
                foreach (var rod in Rods(reactor))
                    rod.Activity = MathF.Max(0f, value);
                break;
            case DystopiaReactorDebugActions.FillWater:
                foreach (var sepUid in reactor.Separators)
                {
                    if (TryComp<DystopiaReactorSeparatorComponent>(sepUid, out var sep))
                        FillSeparator(sepUid, sep);
                }

                break;
            case DystopiaReactorDebugActions.DrainWater:
                foreach (var sepUid in reactor.Separators)
                {
                    if (TryComp<DystopiaReactorSeparatorComponent>(sepUid, out var sep) &&
                        _solution.TryGetSolution(sepUid, sep.WaterSolution, out var solEnt, out _))
                    {
                        _solution.RemoveAllSolution(solEnt.Value);
                    }
                }

                break;
            case DystopiaReactorDebugActions.Pressure:
                foreach (var sepUid in reactor.Separators)
                {
                    if (TryComp<DystopiaReactorSeparatorComponent>(sepUid, out var sep))
                        sep.SteamMoles = MathF.Max(0f, value) * sep.DrumVolume / (Atmospherics.R * cfg.SteamTemperature);
                }

                break;
            case DystopiaReactorDebugActions.Kick:
                foreach (var (_, col) in Columns(reactor))
                {
                    if (col.Kind == DystopiaReactorColumnKind.Fuel)
                        col.Flux += MathF.Max(0f, value);
                }

                break;
            case DystopiaReactorDebugActions.Explosion:
                _reactor.SteamExplosion(reactor, cfg, true);
                break;
            case DystopiaReactorDebugActions.Meltdown:
                // топливо раскалено и каналы разрушены сразу: кориум; взрыв — если реактор на мощности
                foreach (var (uid, col) in Columns(reactor).ToList())
                {
                    if (col.Kind != DystopiaReactorColumnKind.Fuel)
                        continue;

                    col.Temperature = MathF.Max(col.Temperature, 1500f);
                    _reactor.DamageColumn(uid, 100000f);
                }

                break;
            case DystopiaReactorDebugActions.Runaway:
                // все стержни вынуты, автоматика пультов выключена, толчок потока — разгон до разрыва
                foreach (var (_, col) in Columns(reactor))
                {
                    if (col.Kind == DystopiaReactorColumnKind.Control)
                    {
                        col.Insertion = col.Target = 0f;
                        col.Scrammed = false;
                        col.Displacer = false;
                    }
                    else if (col.Kind == DystopiaReactorColumnKind.Fuel)
                    {
                        col.Flux += 3000f;
                    }
                }

                var consoles = EntityQueryEnumerator<DystopiaReactorConsoleComponent>();
                while (consoles.MoveNext(out var consoleUid, out var console))
                {
                    if (_reactor.FindReactor(consoleUid, console.ControlNode) != reactor)
                        continue;

                    console.AutoScram = false;
                    console.AutoRegulator = false;
                }

                break;
            case DystopiaReactorDebugActions.Corium:
                if (center is { } coriumAt)
                    _reactor.SpawnCorium(coriumAt, cfg.CoriumSpread);
                break;
            case DystopiaReactorDebugActions.GraphiteFire:
                if (center is { } fireAt)
                {
                    for (var i = 0; i < 6; i++)
                        _reactor.SpawnDebris(fireAt.Offset(_random.NextVector2(3f)), true);
                }

                break;
            case DystopiaReactorDebugActions.Contamination:
                if (center is { } dustAt)
                    _reactor.Contaminate(dustAt, cfg);
                break;
            case DystopiaReactorDebugActions.BreakChannel:
                var fuel = Columns(reactor).Where(c => c.Col.Kind == DystopiaReactorColumnKind.Fuel).ToList();
                if (fuel.Count > 0)
                    _reactor.DamageColumn(_random.Pick(fuel).Uid, 100000f);
                break;
        }

        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DystopiaReactorDebugToolComponent> ent)
    {
        if (Linked(ent) is not { } reactor || _reactor.Settings() is not { } cfg)
        {
            _ui.SetUiState(ent.Owner, DystopiaReactorDebugUiKey.Key,
                new DystopiaReactorDebugUiState(false, Loc.GetString("dystopia-reactor-debug-unlinked"), _reactor.Speed));
            return;
        }

        var cols = Columns(reactor).ToList();
        var controls = cols.Where(c => c.Col.Kind == DystopiaReactorColumnKind.Control).ToList();
        var rods = Rods(reactor).ToList();
        var water = 0f;
        var waterMax = 0f;
        foreach (var sepUid in reactor.Separators)
        {
            if (TryComp<DystopiaReactorSeparatorComponent>(sepUid, out var sep) &&
                _solution.TryGetSolution(sepUid, sep.WaterSolution, out _, out var sol))
            {
                water += sol.GetTotalPrototypeQuantity(sep.Water).Float();
                waterMax += sol.MaxVolume.Float();
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("dystopia-reactor-debug-summary-1", ("columns", cols.Count),
            ("power", MathF.Round(reactor.ThermalPower / 1000f, 2)), ("temperature", (int) reactor.MaxTemperature),
            ("xenon", (int) MathF.Round(reactor.AverageXenon * 100f))));
        sb.AppendLine(Loc.GetString("dystopia-reactor-debug-summary-2",
            ("rods", controls.Count),
            ("insertion", controls.Count == 0 ? 0 : (int) MathF.Round(controls.Average(c => c.Col.Insertion) * 100f)),
            ("fuel", rods.Count),
            ("burnup", rods.Count == 0 ? 0 : (int) MathF.Round(rods.Average(r => r.Burnup) * 100f)),
            ("activity", rods.Count == 0 ? 0 : MathF.Round(rods.Average(r => r.Activity), 1))));
        sb.Append(Loc.GetString("dystopia-reactor-debug-summary-3", ("separators", reactor.Separators.Count),
            ("water", (int) water), ("max", (int) waterMax),
            ("pressure", (int) _reactor.MaxSeparatorPressure(reactor)), ("steam", MathF.Round(reactor.SteamRate, 1))));
        _ui.SetUiState(ent.Owner, DystopiaReactorDebugUiKey.Key,
            new DystopiaReactorDebugUiState(true, sb.ToString(), _reactor.Speed));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator = 0f;
        var query = EntityQueryEnumerator<DystopiaReactorDebugToolComponent>();
        while (query.MoveNext(out var uid, out var tool))
        {
            if (_ui.IsUiOpen(uid, DystopiaReactorDebugUiKey.Key))
                UpdateUi((uid, tool));
        }
    }

    private float _accumulator;
}
