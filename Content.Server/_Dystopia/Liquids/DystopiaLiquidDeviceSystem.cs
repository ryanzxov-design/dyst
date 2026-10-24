// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: жидкостные устройства.
// Порт — выравнивает заполненность сети и прикрученной цистерны на своей клетке.
// Насос — из сети входа в сеть выхода, от питания, вкл/выкл через ПКМ.
// Экспортер — из сети в объект: литейный стол и изложницы берут только охладители и только горячими (закалка),
// машины с баком — только реагенты своих рецептов, цистерны — всё; тигли, горны и дуговая печь — ничего.
// Импортер — из объекта в сеть: цистерны и баки машин; литейное оборудование — нет.
// Качалка (Extractor) — из природного источника на своей клетке (нефтяной выход) в сеть; источник конечен.
// Фильтр — выбранное через ПКМ вещество уходит в боковую трубу, остальное проходит прямо; питания не нужно.
// Меню (клик по устройству): вкл/выкл, скорость, у фильтра — выбор вещества, состав сетей у узлов.

using Content.Server._Dystopia.Industry;
using Content.Server.Power.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared._Dystopia.Liquids;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Liquids;

public sealed partial class DystopiaLiquidDeviceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DystopiaLiquidSystem _liquid = default!;
    [Dependency] private DystopiaQuenchSystem _quench = default!;
    [Dependency] private DystopiaProcessorSystem _processor = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private DystopiaEngineSystem _engine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, GetVerbsEvent<Verb>>(OnGetFilterVerbs);
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, MapInitEvent>(OnDeviceMapInit);
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, BoundUIOpenedEvent>(OnUiOpened);
        Subs.BuiEvents<DystopiaLiquidDeviceComponent>(DystopiaLiquidDeviceUiKey.Key, subs =>
        {
            subs.Event<DystopiaLiquidDeviceToggleMessage>(OnUiToggle);
            subs.Event<DystopiaLiquidDeviceSetRateMessage>(OnUiSetRate);
            subs.Event<DystopiaLiquidDeviceSetFilterMessage>(OnUiSetFilter);
        });
        SubscribeLocalEvent<DystopiaLiquidSourceComponent, MapInitEvent>(OnSourceMapInit);
        SubscribeLocalEvent<DystopiaLiquidSourceComponent, ExaminedEvent>(OnSourceExamined);
    }

    // ------------------------------------------------------------------ работа

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaLiquidDeviceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var device, out var xform))
        {
            if (now < device.NextUpdate)
                continue;

            device.NextUpdate = now + TimeSpan.FromSeconds(device.Interval);

            if (xform.Anchored && device.On && device.Rate > 0f && (!device.RequirePower || _power.IsPowered(uid)))
            {
                var max = FixedPoint2.New(device.Rate * device.Interval);
                if (Act((uid, device), xform, max) > FixedPoint2.Zero)
                    device.LastWorked = now;
            }
            else
            {
                device.LastWorked = TimeSpan.Zero;   // выключили или сняли — гаснет сразу
            }

            _appearance.SetData(uid, DystopiaLiquidVisuals.Working, IsWorking(device, now));

            if (_ui.IsUiOpen(uid, DystopiaLiquidDeviceUiKey.Key))
                UpdateUi((uid, device));
        }
    }

    /// <summary>Работает ли — с задержкой <see cref="DystopiaLiquidDeviceComponent.WorkingHold"/>, чтобы не мигать.</summary>
    private static bool IsWorking(DystopiaLiquidDeviceComponent device, TimeSpan now)
    {
        return device.LastWorked > TimeSpan.Zero &&
               now - device.LastWorked < TimeSpan.FromSeconds(Math.Max(device.WorkingHold, device.Interval * 1.5f));
    }

    private void OnDeviceMapInit(Entity<DystopiaLiquidDeviceComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.MaxRate <= 0f)
            ent.Comp.MaxRate = ent.Comp.Rate;
    }

    private FixedPoint2 Act(Entity<DystopiaLiquidDeviceComponent> ent, TransformComponent xform, FixedPoint2 max)
    {
        if (_liquid.GetNet(ent, ent.Comp.Inlet) is not { } net)
            return FixedPoint2.Zero;

        switch (ent.Comp.Mode)
        {
            case DystopiaLiquidDeviceMode.Pump:
            {
                if (_liquid.GetNet(ent, ent.Comp.Outlet) is not { } outlet || outlet == net)
                    return FixedPoint2.Zero;

                return _liquid.Move(net.Solution, outlet.Solution, max);
            }

            case DystopiaLiquidDeviceMode.Port:
            {
                foreach (var target in Targets(ent, xform, Direction.Invalid))
                {
                    if (Refuses(target) || TankSolution(target) is not { } tank)
                        continue;

                    return Equalize(net.Solution, tank, max);
                }

                return FixedPoint2.Zero;
            }

            case DystopiaLiquidDeviceMode.Exporter:
            {
                foreach (var target in Targets(ent, xform, ent.Comp.TargetSide))
                {
                    var moved = Export(net.Solution, target, max);
                    if (moved > FixedPoint2.Zero)
                        return moved;
                }

                return FixedPoint2.Zero;
            }

            case DystopiaLiquidDeviceMode.Importer:
            {
                foreach (var target in Targets(ent, xform, ent.Comp.TargetSide))
                {
                    if (Refuses(target) || IsCasting(target) || DrainSolution(target) is not { } tank)
                        continue;

                    var moved = MoveOut(tank, net.Solution, max);
                    if (moved > FixedPoint2.Zero)
                        return moved;
                }

                return FixedPoint2.Zero;
            }

            case DystopiaLiquidDeviceMode.Filter:
            {
                return Filter(ent.Comp, net.Solution, _liquid.GetNet(ent, ent.Comp.Outlet)?.Solution,
                    _liquid.GetNet(ent, ent.Comp.FilterNode)?.Solution, max);
            }

            case DystopiaLiquidDeviceMode.Extractor:
            {
                return GetSource(ent, xform) is { } source ? Extract(source, net.Solution, max) : FixedPoint2.Zero;
            }
        }

        return FixedPoint2.Zero;
    }

    // ------------------------------------------------------------------ меню

    private void OnUiOpened(Entity<DystopiaLiquidDeviceComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnUiToggle(Entity<DystopiaLiquidDeviceComponent> ent, ref DystopiaLiquidDeviceToggleMessage args)
    {
        if (!ent.Comp.Toggleable)
            return;

        ent.Comp.On = args.On;
        ent.Comp.NextUpdate = TimeSpan.Zero;
        UpdateUi(ent);
    }

    private void OnUiSetRate(Entity<DystopiaLiquidDeviceComponent> ent, ref DystopiaLiquidDeviceSetRateMessage args)
    {
        if (!float.IsFinite(args.Rate))
            return;

        ent.Comp.Rate = Math.Clamp(args.Rate, 0f, MaxRate(ent.Comp));
        UpdateUi(ent);
    }

    private void OnUiSetFilter(Entity<DystopiaLiquidDeviceComponent> ent, ref DystopiaLiquidDeviceSetFilterMessage args)
    {
        if (ent.Comp.Mode != DystopiaLiquidDeviceMode.Filter)
            return;

        // Клиенту на слово не верим: только настоящее вещество.
        if (args.Reagent != null && !_proto.HasIndex<ReagentPrototype>(args.Reagent))
            return;

        ProtoId<ReagentPrototype>? reagent = null;
        if (args.Reagent != null)
            reagent = new ProtoId<ReagentPrototype>(args.Reagent);

        ent.Comp.FilterReagent = reagent;
        UpdateUi(ent);
    }

    private static float MaxRate(DystopiaLiquidDeviceComponent device)
    {
        return device.MaxRate > 0f ? device.MaxRate : device.Rate;
    }

    private void UpdateUi(Entity<DystopiaLiquidDeviceComponent> ent)
    {
        var device = ent.Comp;
        var nets = new List<DystopiaLiquidNetInfo>();
        switch (device.Mode)
        {
            case DystopiaLiquidDeviceMode.Pump:
                nets.Add(NetInfo("dystopia-liquid-ui-inlet", _liquid.GetNet(ent, device.Inlet)));
                nets.Add(NetInfo("dystopia-liquid-ui-outlet", _liquid.GetNet(ent, device.Outlet)));
                break;
            case DystopiaLiquidDeviceMode.Filter:
                nets.Add(NetInfo("dystopia-liquid-ui-inlet", _liquid.GetNet(ent, device.Inlet)));
                nets.Add(NetInfo("dystopia-liquid-ui-outlet", _liquid.GetNet(ent, device.Outlet)));
                nets.Add(NetInfo("dystopia-liquid-ui-side", _liquid.GetNet(ent, device.FilterNode)));
                break;
            default:
                nets.Add(NetInfo("dystopia-liquid-ui-net", _liquid.GetNet(ent, device.Inlet)));
                break;
        }

        // Фильтр: на выбор — то, что сейчас во входной трубе, плюс уже выбранное.
        var options = new List<DystopiaLiquidReagentInfo>();
        if (device.Mode == DystopiaLiquidDeviceMode.Filter)
        {
            options.AddRange(nets[0].Reagents);
            if (device.FilterReagent is { } current && options.All(o => o.Id != current.Id) &&
                _proto.TryIndex(current, out var proto))
            {
                options.Add(new DystopiaLiquidReagentInfo(current.Id, proto.LocalizedName, proto.SubstanceColor, 0f));
            }
        }

        string? source = null;
        if (device.Mode == DystopiaLiquidDeviceMode.Extractor)
        {
            source = GetSource(ent, Transform(ent)) is { } src
                ? SourceReserve(src.Comp)
                : Loc.GetString("dystopia-liquid-source-none");
        }

        _ui.SetUiState(ent.Owner, DystopiaLiquidDeviceUiKey.Key, new DystopiaLiquidDeviceUiState(device.Mode,
            device.Toggleable, device.On, device.RequirePower, !device.RequirePower || _power.IsPowered(ent),
            IsWorking(device, _timing.CurTime), device.Rate, MaxRate(device), device.FilterReagent?.Id, options,
            source, nets));
    }

    private DystopiaLiquidNetInfo NetInfo(string label, DystopiaLiquidNet? net)
    {
        var text = Loc.GetString(label);
        if (net == null)
            return new DystopiaLiquidNetInfo(text, false, 0f, 0f, 0f, new List<DystopiaLiquidReagentInfo>());

        var solution = net.Solution;
        var reagents = new List<DystopiaLiquidReagentInfo>();
        foreach (var (reagent, quantity) in solution.GetReagentPrototypes(_proto).OrderByDescending(p => p.Value.Value))
        {
            reagents.Add(new DystopiaLiquidReagentInfo(reagent.ID, reagent.LocalizedName, reagent.SubstanceColor,
                quantity.Float()));
        }

        return new DystopiaLiquidNetInfo(text, true, solution.Volume.Float(), solution.MaxVolume.Float(),
            solution.Temperature - DystopiaCrucibleSystem.KelvinOffset, reagents);
    }

    // ------------------------------------------------------------------ фильтр

    /// <summary>
    /// Сначала выбранное вещество — вбок (сколько влезет), потом остальное — прямо; вместе не больше max.
    /// Если боковая труба полна, выбранное ждёт во входе, а остальное идёт дальше.
    /// </summary>
    private FixedPoint2 Filter(DystopiaLiquidDeviceComponent device, Solution inlet, Solution? outlet, Solution? side,
        FixedPoint2 max)
    {
        var moved = FixedPoint2.Zero;
        if (device.FilterReagent is { } reagent && side != null && side != inlet)
        {
            var amount = FixedPoint2.Min(max,
                FixedPoint2.Min(inlet.GetTotalPrototypeQuantity(reagent), side.AvailableVolume));
            if (amount > FixedPoint2.Zero)
            {
                side.AddSolution(inlet.SplitSolutionWithOnly(amount, reagent), _proto);
                moved += amount;
            }
        }

        if (outlet == null || outlet == inlet)
            return moved;

        var rest = inlet.Volume - (device.FilterReagent is { } r ? inlet.GetTotalPrototypeQuantity(r) : FixedPoint2.Zero);
        var pass = FixedPoint2.Min(max - moved, FixedPoint2.Min(rest, outlet.AvailableVolume));
        if (pass > FixedPoint2.Zero)
        {
            outlet.AddSolution(device.FilterReagent is { } f
                ? inlet.SplitSolutionWithout(pass, f)
                : inlet.SplitSolution(pass), _proto);
            moved += pass;
        }

        return moved;
    }

    private void OnGetFilterVerbs(Entity<DystopiaLiquidDeviceComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (ent.Comp.Mode != DystopiaLiquidDeviceMode.Filter || !args.CanAccess || !args.CanInteract)
            return;

        // На выбор — то, что сейчас есть во входной трубе, плюс уже выбранное.
        var options = new List<ProtoId<ReagentPrototype>>();
        if (_liquid.GetNet(ent, ent.Comp.Inlet) is { } net)
        {
            foreach (var quantity in net.Solution.Contents)
                options.Add(quantity.Reagent.Prototype);
        }

        if (ent.Comp.FilterReagent is { } current && !options.Contains(current))
            options.Add(current);

        var user = args.User;
        args.Verbs.Add(FilterVerb(ent, user, null, Loc.GetString("dystopia-liquid-filter-none"), 0));
        var priority = -1;
        foreach (var option in options)
        {
            var name = _proto.TryIndex(option, out var proto) ? proto.LocalizedName : option.Id;
            args.Verbs.Add(FilterVerb(ent, user, option, name, priority--));
        }
    }

    private Verb FilterVerb(Entity<DystopiaLiquidDeviceComponent> ent, EntityUid user,
        ProtoId<ReagentPrototype>? reagent, string name, int priority)
    {
        return new Verb
        {
            Text = name,
            Category = FilterCategory,
            Priority = priority,
            Disabled = ent.Comp.FilterReagent == reagent,
            Act = () =>
            {
                ent.Comp.FilterReagent = reagent;
                _popup.PopupEntity(Loc.GetString("dystopia-liquid-filter-set", ("reagent", name)), ent, user);
            },
        };
    }

    private static readonly VerbCategory FilterCategory = new("dystopia-liquid-filter-category", null);

    // ------------------------------------------------------------------ природные источники

    private void OnSourceMapInit(Entity<DystopiaLiquidSourceComponent> ent, ref MapInitEvent args)
    {
        // Запас, заданный в прототипе или на карте, не трогаем.
        if (ent.Comp.Initial <= FixedPoint2.Zero)
        {
            ent.Comp.Initial = FixedPoint2.New(_random.NextFloat(ent.Comp.MinAmount, ent.Comp.MaxAmount));
            ent.Comp.Amount = ent.Comp.Initial;
        }

        _appearance.SetData(ent, DystopiaLiquidSourceVisuals.Depleted, ent.Comp.Amount <= FixedPoint2.Zero);
    }

    /// <summary>Источник на клетке устройства (качалка ставится прямо на него).</summary>
    private Entity<DystopiaLiquidSourceComponent>? GetSource(EntityUid uid, TransformComponent xform)
    {
        foreach (var target in Targets(uid, xform, Direction.Invalid))
        {
            if (TryComp<DystopiaLiquidSourceComponent>(target, out var source))
                return (target, source);
        }

        return null;
    }

    private FixedPoint2 Extract(Entity<DystopiaLiquidSourceComponent> source, Solution net, FixedPoint2 max)
    {
        var amount = FixedPoint2.Min(max, FixedPoint2.Min(source.Comp.Amount, net.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        net.AddReagent(new ReagentId(source.Comp.Reagent, null), amount);
        source.Comp.Amount -= amount;
        if (source.Comp.Amount <= FixedPoint2.Zero)
            _appearance.SetData(source, DystopiaLiquidSourceVisuals.Depleted, true);

        return amount;
    }

    /// <summary>Оценка запаса — на глаз, без чисел: богатый, средний, скудный, иссяк.</summary>
    private string SourceReserve(DystopiaLiquidSourceComponent source)
    {
        if (source.Amount <= FixedPoint2.Zero)
            return Loc.GetString("dystopia-liquid-source-depleted");

        var fraction = source.Initial > FixedPoint2.Zero ? (source.Amount / source.Initial).Float() : 1f;
        return Loc.GetString(fraction > source.RichFraction ? "dystopia-liquid-source-rich"
            : fraction > source.FairFraction ? "dystopia-liquid-source-fair"
            : "dystopia-liquid-source-poor");
    }

    private void OnSourceExamined(Entity<DystopiaLiquidSourceComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(SourceReserve(ent.Comp));
    }

    /// <summary>
    /// Объекты на клетке со стороны <paramref name="side"/> относительно поворота устройства
    /// (<see cref="Direction.Invalid"/> — на своей клетке). Прикрученные к полу, плюс литейные формы.
    /// </summary>
    private IEnumerable<EntityUid> Targets(EntityUid uid, TransformComponent xform, Direction side)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            yield break;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (side != Direction.Invalid)
            tile = tile.Offset((xform.LocalRotation + side.ToAngle()).GetCardinalDir());

        foreach (var target in _lookup.GetLocalEntitiesIntersecting(gridUid, tile, gridComp: grid))
        {
            if (target == uid || HasComp<DystopiaLiquidPipeComponent>(target) ||
                HasComp<DystopiaLiquidDeviceComponent>(target))
            {
                continue;
            }

            if (Transform(target).Anchored || HasComp<DystopiaQuenchableComponent>(target))
                yield return target;
        }
    }

    /// <summary>Тигли, горны и дуговая печь жидкость из труб не берут и не отдают.</summary>
    private bool Refuses(EntityUid uid)
    {
        return HasComp<DystopiaCrucibleComponent>(uid) ||
               HasComp<DystopiaForgeComponent>(uid) ||
               HasComp<DystopiaArcFurnaceComponent>(uid);
    }

    /// <summary>Литейный стол и изложницы: из сети только закалка.</summary>
    private bool IsCasting(EntityUid uid)
    {
        return HasComp<DystopiaQuenchableComponent>(uid);
    }

    private Entity<SolutionComponent>? TankSolution(EntityUid uid)
    {
        if (_solution.TryGetRefillableSolution(uid, out var refill, out _))
            return refill;

        if (_solution.TryGetDrainableSolution(uid, out var drain, out _))
            return drain;

        return null;
    }

    /// <summary>
    /// Откуда забирать (импортёр): у машины с баком продукции — из него; иначе сначала то, из чего сливают
    /// (у отстойника слив — это продукт, а залив — вода), потом то, во что наливают.
    /// </summary>
    private Entity<SolutionComponent>? DrainSolution(EntityUid uid)
    {
        // Химическая установка: забирают только из бака продукции.
        if (TryComp<DystopiaChemUnitComponent>(uid, out var chem))
            return _solution.TryGetSolution(uid, chem.Output, out var chemOutput, out _) ? chemOutput : null;

        if (TryComp<DystopiaProcessorComponent>(uid, out var processor) && processor.OutputTank is { } output &&
            _solution.TryGetSolution(uid, output, out var product, out _))
        {
            return product;
        }

        if (_solution.TryGetDrainableSolution(uid, out var drain, out _))
            return drain;

        if (_solution.TryGetRefillableSolution(uid, out var refill, out _))
            return refill;

        return null;
    }

    private FixedPoint2 Export(Solution net, EntityUid target, FixedPoint2 max)
    {
        if (Refuses(target))
            return FixedPoint2.Zero;

        if (IsCasting(target))
            return _quench.TryQuench(target, net, max);

        // Промышленный двигатель: топливо, масло и вода — каждое в свой бак.
        if (TryComp<DystopiaEngineControllerComponent>(target, out var engine))
            return _engine.Fill((target, engine), net, max);

        // Химическая установка: всё — в буфер (там не смешивается).
        if (TryComp<DystopiaChemUnitComponent>(target, out var chem))
        {
            return _solution.TryGetSolution(target, chem.Buffer, out var chemBuffer, out _)
                ? MoveIn(net, chemBuffer.Value, max)
                : FixedPoint2.Zero;
        }

        // Машина с баком — только то, что идёт в её рецепты.
        if (TryComp<DystopiaProcessorComponent>(target, out var processor))
        {
            if (processor.Tank == null ||
                !_solution.TryGetSolution(target, processor.Tank, out var tank, out var tankSolution))
            {
                return FixedPoint2.Zero;
            }

            var reagents = _processor.GroupReagents(processor.Group);
            if (reagents.Length == 0)
                return FixedPoint2.Zero;

            var amount = FixedPoint2.Min(max,
                FixedPoint2.Min(tankSolution.AvailableVolume, net.GetTotalPrototypeQuantity(reagents)));
            if (amount <= FixedPoint2.Zero)
                return FixedPoint2.Zero;

            _solution.AddSolution(tank.Value, net.SplitSolutionWithOnly(amount, reagents));
            return amount;
        }

        return TankSolution(target) is { } solution ? MoveIn(net, solution, max) : FixedPoint2.Zero;
    }

    /// <summary>Из сети в раствор сущности.</summary>
    private FixedPoint2 MoveIn(Solution net, Entity<SolutionComponent> target, FixedPoint2 max)
    {
        var amount = FixedPoint2.Min(max, FixedPoint2.Min(net.Volume, target.Comp.Solution.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        _solution.AddSolution(target, net.SplitSolution(amount));
        return amount;
    }

    /// <summary>Из раствора сущности в сеть.</summary>
    private FixedPoint2 MoveOut(Entity<SolutionComponent> source, Solution net, FixedPoint2 max)
    {
        var amount = FixedPoint2.Min(max, FixedPoint2.Min(source.Comp.Solution.Volume, net.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        net.AddSolution(_solution.SplitSolution(source, amount), _proto);
        return amount;
    }

    /// <summary>Порт: довести сеть и цистерну до одинаковой заполненности (не больше max за раз).</summary>
    private FixedPoint2 Equalize(Solution net, Entity<SolutionComponent> tank, FixedPoint2 max)
    {
        var tankSolution = tank.Comp.Solution;
        var capacity = net.MaxVolume.Float() + tankSolution.MaxVolume.Float();
        if (capacity <= 0f)
            return FixedPoint2.Zero;

        var fill = (net.Volume.Float() + tankSolution.Volume.Float()) / capacity;
        var delta = fill * tankSolution.MaxVolume.Float() - tankSolution.Volume.Float();

        // Меньше единицы не гоняем — иначе порт дёргается туда-сюда на округлении.
        if (MathF.Abs(delta) < 1f)
            return FixedPoint2.Zero;

        var amount = FixedPoint2.Min(max, FixedPoint2.New(MathF.Abs(delta)));
        return delta > 0f ? MoveIn(net, tank, amount) : MoveOut(tank, net, amount);
    }

    // ------------------------------------------------------------------ вкл/выкл и осмотр

    private void OnGetVerbs(Entity<DystopiaLiquidDeviceComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ent.Comp.Toggleable || !args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.On ? "dystopia-liquid-turn-off" : "dystopia-liquid-turn-on"),
            Act = () =>
            {
                ent.Comp.On = !ent.Comp.On;
                ent.Comp.NextUpdate = TimeSpan.Zero;
                _popup.PopupEntity(Loc.GetString(ent.Comp.On ? "dystopia-liquid-turned-on" : "dystopia-liquid-turned-off",
                    ("device", ent.Owner)), ent, user);
            },
        });
    }

    private void OnExamined(Entity<DystopiaLiquidDeviceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Toggleable)
        {
            var key = !ent.Comp.On ? "dystopia-liquid-examine-off"
                : ent.Comp.RequirePower && !_power.IsPowered(ent) ? "dystopia-liquid-examine-unpowered"
                : "dystopia-liquid-examine-on";
            args.PushMarkup(Loc.GetString(key), 6);
        }

        // Одинаковые приоритеты идут по алфавиту — поэтому у подписи и у сведений о сети они разные.
        if (ent.Comp.Mode == DystopiaLiquidDeviceMode.Pump)
        {
            args.PushMarkup(Loc.GetString("dystopia-liquid-examine-inlet"), 5);
            _liquid.PushNetInfo(args, _liquid.GetNet(ent, ent.Comp.Inlet), 4);
            args.PushMarkup(Loc.GetString("dystopia-liquid-examine-outlet"), 3);
            _liquid.PushNetInfo(args, _liquid.GetNet(ent, ent.Comp.Outlet), 2);
            return;
        }

        if (ent.Comp.Mode == DystopiaLiquidDeviceMode.Filter)
        {
            var reagent = ent.Comp.FilterReagent is { } id && _proto.TryIndex(id, out var proto)
                ? Loc.GetString("dystopia-liquid-filter-examine", ("reagent", proto.LocalizedName))
                : Loc.GetString("dystopia-liquid-filter-examine-none");
            args.PushMarkup(reagent, 7);
            args.PushMarkup(Loc.GetString("dystopia-liquid-examine-inlet"), 5);
            _liquid.PushNetInfo(args, _liquid.GetNet(ent, ent.Comp.Inlet), 4);
            args.PushMarkup(Loc.GetString("dystopia-liquid-examine-outlet"), 3);
            _liquid.PushNetInfo(args, _liquid.GetNet(ent, ent.Comp.Outlet), 2);
            args.PushMarkup(Loc.GetString("dystopia-liquid-filter-examine-side"), 1);
            _liquid.PushNetInfo(args, _liquid.GetNet(ent, ent.Comp.FilterNode), 0);
            return;
        }

        if (ent.Comp.Mode == DystopiaLiquidDeviceMode.Extractor)
        {
            var source = GetSource(ent, Transform(ent));
            args.PushMarkup(source is { } s ? SourceReserve(s.Comp) : Loc.GetString("dystopia-liquid-source-none"), 5);
        }

        _liquid.PushNetInfo(args, _liquid.GetNet(ent, ent.Comp.Inlet), 4);
    }
}
