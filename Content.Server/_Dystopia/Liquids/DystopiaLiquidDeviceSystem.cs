// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: жидкостные устройства.
// Порт — выравнивает заполненность сети и прикрученной цистерны на своей клетке.
// Насос — из сети входа в сеть выхода, от питания, вкл/выкл через ПКМ.
// Экспортер — из сети в объект: литейный стол и изложницы берут только охладители и только горячими (закалка),
// машины с баком — только реагенты своих рецептов, цистерны — всё; тигли, горны и дуговая печь — ничего.
// Импортер — из объекта в сеть: цистерны и баки машин; литейное оборудование — нет.

using Content.Server._Dystopia.Industry;
using Content.Server.Power.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared._Dystopia.Liquids;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, ExaminedEvent>(OnExamined);
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

            var working = false;
            if (xform.Anchored && device.On && (!device.RequirePower || _power.IsPowered(uid)))
            {
                var max = FixedPoint2.New(device.Rate * device.Interval);
                working = Act((uid, device), xform, max) > FixedPoint2.Zero;
            }

            _appearance.SetData(uid, DystopiaLiquidVisuals.Working, working);
        }
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
        }

        return FixedPoint2.Zero;
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

        _liquid.PushNetInfo(args, _liquid.GetNet(ent, ent.Comp.Inlet), 4);
    }
}
