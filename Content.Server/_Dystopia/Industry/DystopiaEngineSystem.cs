// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: промышленный двигатель внутреннего сгорания (см. DystopiaEngineControllerComponent).
// Раз в такт: ряд за пультом (цилиндры, радиаторы в конце и по бокам) → если включён, не заклинен и есть топливо — сжигает топливо
// (отдача по виду топлива), масло, греется; охлаждается радиаторами и водой (вода → пар → газовая труба); выдаёт
// мощность в высоковольтную сеть; выхлоп CO₂ — в воздух клетки; износ и перегрев — урон пульту. Перегрев видно и
// слышно: дым и искры (DystopiaEngineVisuals.Overheat у всего ряда), сирена на пороге и шипение.

using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.NodeContainer;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaEngineSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaEngineControllerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaEngineControllerComponent, BoundUIOpenedEvent>(OnUiOpened);
        Subs.BuiEvents<DystopiaEngineControllerComponent>(DystopiaEngineUiKey.Key, subs =>
        {
            subs.Event<DystopiaEngineToggleMessage>(OnToggle);
            subs.Event<DystopiaEngineSetThrottleMessage>(OnSetThrottle);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaEngineControllerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var engine, out var xform))
        {
            if (now < engine.NextUpdate)
                continue;

            engine.NextUpdate = now + TimeSpan.FromSeconds(engine.Interval);
            Tick((uid, engine), xform, engine.Interval);

            if (_ui.IsUiOpen(uid, DystopiaEngineUiKey.Key))
                UpdateUi((uid, engine));
        }
    }

    // ------------------------------------------------------------------ ряд

    /// <summary>
    /// Цилиндры — подряд с задней стороны пульта; радиаторы — в конце ряда (до MaxRadiators) и по бокам от любого
    /// цилиндра (по одному с каждой стороны). Остальное ряд обрывает.
    /// </summary>
    private void ScanRow(Entity<DystopiaEngineControllerComponent> ent, TransformComponent xform)
    {
        var engine = ent.Comp;
        engine.Cylinders = 0;
        engine.Radiators = 0;
        engine.Row.Clear();

        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var back = (xform.LocalRotation + Direction.North.ToAngle()).GetCardinalDir();
        var left = (xform.LocalRotation + Direction.West.ToAngle()).GetCardinalDir();
        var right = (xform.LocalRotation + Direction.East.ToAngle()).GetCardinalDir();
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var endRadiators = 0;
        var cylinderTiles = new List<Vector2i>();
        for (var i = 0; i < engine.MaxCylinders + engine.MaxRadiators; i++)
        {
            tile = tile.Offset(back);
            var cylinder = Find<DystopiaEngineCylinderComponent>(gridUid, grid, tile);
            if (cylinder != null && endRadiators == 0 && engine.Cylinders < engine.MaxCylinders)
            {
                engine.Cylinders++;
                engine.Row.Add(cylinder.Value);
                cylinderTiles.Add(tile);
                continue;
            }

            var radiator = Find<DystopiaEngineRadiatorComponent>(gridUid, grid, tile);
            if (radiator != null && engine.Cylinders > 0 && endRadiators < engine.MaxRadiators)
            {
                endRadiators++;
                engine.Row.Add(radiator.Value);
                continue;
            }

            break;
        }

        // Радиаторы по бокам от цилиндров
        foreach (var cylTile in cylinderTiles)
        {
            foreach (var side in new[] { left, right })
            {
                if (Find<DystopiaEngineRadiatorComponent>(gridUid, grid, cylTile.Offset(side)) is { } radiator &&
                    !engine.Row.Contains(radiator))
                {
                    engine.Row.Add(radiator);
                }
            }
        }

        foreach (var part in engine.Row)
        {
            if (HasComp<DystopiaEngineRadiatorComponent>(part))
                engine.Radiators++;
        }
    }

    private EntityUid? Find<T>(EntityUid gridUid, MapGridComponent grid, Vector2i tile) where T : IComponent
    {
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<T>(other))
                return other;
        }

        return null;
    }

    // ------------------------------------------------------------------ работа

    private void Tick(Entity<DystopiaEngineControllerComponent> ent, TransformComponent xform, float dt)
    {
        var engine = ent.Comp;
        var oldRow = new List<EntityUid>(engine.Row);
        ScanRow(ent, xform);

        engine.Power = 0f;
        engine.FuelRate = 0f;
        engine.OilRate = 0f;
        engine.WaterRate = 0f;
        var running = false;

        if (Status(ent) == DystopiaEngineStatus.Running &&
            _solution.TryGetSolution(ent.Owner, engine.FuelTank, out var fuelEnt, out var fuel))
        {
            var load = engine.Cylinders * engine.Throttle;

            // Топливо: смесь сгорает пропорционально; отдача — средняя по сгоревшему.
            var fuelIds = new List<ProtoId<ReagentPrototype>>(engine.Fuels.Keys);
            var have = fuel.GetTotalPrototypeQuantity(fuelIds.ToArray());
            if (have > FixedPoint2.Zero && load > 0f)
            {
                var efficiency = 0f;
                var wearFactor = 0f;
                foreach (var id in fuelIds)
                {
                    var share = fuel.GetTotalPrototypeQuantity(id).Float() / have.Float();
                    efficiency += share * engine.Fuels[id];
                    wearFactor += share * (engine.FuelWear.TryGetValue(id, out var w) ? w : 1f);
                }

                var need = load * engine.FuelPerCylinder * dt / MathF.Max(0.05f, efficiency);
                var burned = FixedPoint2.Min(have, FixedPoint2.New(need));
                fuel.SplitSolutionWithOnly(burned, fuelIds.ToArray());
                _solution.UpdateChemicals(fuelEnt.Value, false);

                var fraction = need > 0f ? MathF.Min(1f, burned.Float() / need) : 0f;
                running = fraction > 0f;
                engine.Power = engine.Cylinders * engine.PowerPerCylinder * engine.Throttle * fraction;
                engine.FuelRate = burned.Float() / dt;

                // Выхлоп
                if (_atmos.GetContainingMixture((ent.Owner, xform), false, true) is { } air)
                    air.AdjustMoles(Gas.CarbonDioxide, burned.Float() * engine.ExhaustPerFuelUnit);

                // Масло
                var dry = true;
                if (_solution.TryGetSolution(ent.Owner, engine.OilTank, out var oilEnt, out var oil))
                {
                    var oilNeed = FixedPoint2.New(load * fraction * engine.OilPerCylinder * dt);
                    var oilHave = oil.GetTotalPrototypeQuantity(engine.Lubricant);
                    if (oilHave >= oilNeed && oilNeed > FixedPoint2.Zero)
                    {
                        _solution.RemoveReagent(oilEnt.Value, engine.Lubricant.Id, oilNeed);
                        engine.OilRate = oilNeed.Float() / dt;
                        dry = false;
                    }
                }

                // Нагрев
                engine.Temperature += load * fraction * engine.HeatPerCylinder * dt * (dry ? 1.5f : 1f);

                // Износ: от нагрузки, перегрузки, топлива, сухого хода и перегрева
                var wear = load * fraction * wearFactor;
                if (engine.Throttle > 1f)
                    wear *= 1f + (engine.Throttle - 1f) * engine.OverloadWear;
                if (dry)
                    wear *= engine.DryWear;
                if (engine.Temperature > engine.WarnTemperature)
                    wear *= 2f;

                engine.PendingWear += wear * engine.WearRate * dt;
            }
        }

        Cool(ent, dt);
        VentSteam(ent, dt);

        if (engine.Temperature > engine.OverheatTemperature)
            engine.PendingHeat += (engine.Temperature - engine.OverheatTemperature) * engine.OverheatRate * dt;

        ApplyDamage(ent);
        Overheat(ent, oldRow);

        if (TryComp<PowerSupplierComponent>(ent, out var supplier))
            supplier.MaxSupply = engine.Power;

        SetRunning(ent, running, oldRow);
    }

    /// <summary>Остывание: само по себе, радиаторами и водой (выкипает в пар, пока есть куда его деть).</summary>
    private void Cool(Entity<DystopiaEngineControllerComponent> ent, float dt)
    {
        var engine = ent.Comp;
        var delta = engine.Temperature - engine.AmbientTemperature;
        if (delta > 0f)
        {
            engine.Temperature -= delta * engine.PassiveCooling * dt;
            engine.Temperature -= engine.Radiators * engine.RadiatorCooling * (delta / 70f) * dt;
        }

        if (engine.Temperature > engine.WaterCoolingFrom &&
            _solution.TryGetSolution(ent.Owner, engine.WaterTank, out var waterEnt, out var water))
        {
            var wanted = (engine.Temperature - engine.WaterCoolingFrom) / engine.CoolingPerWaterUnit;
            var limit = MathF.Max(1, engine.Cylinders) * engine.WaterPerCylinder * dt;
            var room = (engine.SteamCapacity - engine.SteamMoles) / MathF.Max(0.001f, engine.SteamPerWaterUnit);
            var units = MathF.Min(MathF.Min(wanted, limit), MathF.Min(room, water.GetTotalPrototypeQuantity(engine.Coolant).Float()));
            if (units > 0.01f)
            {
                _solution.RemoveReagent(waterEnt.Value, engine.Coolant.Id, FixedPoint2.New(units));
                engine.Temperature -= units * engine.CoolingPerWaterUnit;
                engine.SteamMoles += units * engine.SteamPerWaterUnit;
                engine.WaterRate = units / dt;
            }
        }

        engine.Temperature = Math.Clamp(engine.Temperature, engine.AmbientTemperature, engine.MaxTemperature);
    }

    /// <summary>Пар — в газовую трубу, пока давление в ней ниже предела.</summary>
    private void VentSteam(Entity<DystopiaEngineControllerComponent> ent, float dt)
    {
        var engine = ent.Comp;
        engine.SteamRate = 0f;
        if (engine.SteamMoles <= 0f || !_nodes.TryGetNode<PipeNode>(ent.Owner, engine.SteamNode, out var pipe))
            return;

        if (pipe.Air.Pressure >= engine.MaxSteamPressure)
            return;

        var steam = new GasMixture { Temperature = engine.SteamTemperature };
        steam.AdjustMoles(Gas.WaterVapor, engine.SteamMoles);
        _atmos.Merge(pipe.Air, steam);
        engine.SteamRate = engine.SteamMoles / dt;
        engine.SteamMoles = 0f;
    }

    /// <summary>Урон хранится с точностью до сотых: мелкий износ копим и наносим, когда наберётся.</summary>
    private void ApplyDamage(Entity<DystopiaEngineControllerComponent> ent)
    {
        var engine = ent.Comp;
        if (engine.PendingWear >= 0.1f && !engine.WearDamage.Empty)
        {
            _damageable.TryChangeDamage(ent.Owner, engine.WearDamage * engine.PendingWear, interruptsDoAfters: false);
            engine.PendingWear = 0f;
        }

        if (engine.PendingHeat >= 0.1f && !engine.OverheatDamage.Empty)
        {
            _damageable.TryChangeDamage(ent.Owner, engine.OverheatDamage * engine.PendingHeat, interruptsDoAfters: false);
            engine.PendingHeat = 0f;
        }
    }

    /// <summary>Перегрев: дым и искры у всего ряда, сирена при переходе через порог, шипение, пока горит.</summary>
    private void Overheat(Entity<DystopiaEngineControllerComponent> ent, List<EntityUid> oldRow)
    {
        var engine = ent.Comp;
        var hot = engine.Temperature > engine.OverheatTemperature;
        var now = _timing.CurTime;

        if (hot && !engine.Overheated)
        {
            _audio.PlayPvs(engine.OverheatAlarm, ent);
            engine.NextOverheatSound = now + TimeSpan.FromSeconds(engine.OverheatSoundInterval);
        }
        else if (hot && now >= engine.NextOverheatSound)
        {
            _audio.PlayPvs(engine.OverheatSound, ent);
            engine.NextOverheatSound = now + TimeSpan.FromSeconds(engine.OverheatSoundInterval);
        }

        engine.Overheated = hot;
        _appearance.SetData(ent, DystopiaEngineVisuals.Overheat, hot);
        foreach (var old in oldRow)
        {
            if (!engine.Row.Contains(old) && !TerminatingOrDeleted(old))
                _appearance.SetData(old, DystopiaEngineVisuals.Overheat, false);
        }

        foreach (var part in engine.Row)
            _appearance.SetData(part, DystopiaEngineVisuals.Overheat, hot);
    }

    private void SetRunning(Entity<DystopiaEngineControllerComponent> ent, bool running, List<EntityUid> oldRow)
    {
        // Ряд поменялся — у выбывших блоков гасим анимацию.
        foreach (var old in oldRow)
        {
            if (!ent.Comp.Row.Contains(old) && !TerminatingOrDeleted(old))
                _appearance.SetData(old, DystopiaProcessorVisuals.Running, false);
        }

        var changed = ent.Comp.Running != running;
        ent.Comp.Running = running;
        if (changed)
        {
            _appearance.SetData(ent, DystopiaProcessorVisuals.Running, running);
            _ambient.SetAmbience(ent, running);
        }

        foreach (var part in ent.Comp.Row)
            _appearance.SetData(part, DystopiaProcessorVisuals.Running, running);
    }

    /// <summary>Износ — суммарный урон пульта (поле TotalDamage закрыто, читаем через систему).</summary>
#pragma warning disable CS0618 // Do not rely on a numerically quantifiable amount of damage — здесь это и есть износ
    private FixedPoint2 TotalDamage(Entity<DamageableComponent> ent)
    {
        return _damageable.GetTotalDamage((ent.Owner, ent.Comp));
    }
#pragma warning restore CS0618

    private DystopiaEngineStatus Status(Entity<DystopiaEngineControllerComponent> ent)
    {
        if (TryComp<DamageableComponent>(ent, out var damageable) && TotalDamage((ent.Owner, damageable)) >= ent.Comp.SeizeDamage)
            return DystopiaEngineStatus.Seized;
        if (!ent.Comp.On)
            return DystopiaEngineStatus.Off;
        if (ent.Comp.Cylinders < ent.Comp.MinCylinders)
            return DystopiaEngineStatus.ShortRow;
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.FuelTank, out _, out var fuel) ||
            fuel.GetTotalPrototypeQuantity(new List<ProtoId<ReagentPrototype>>(ent.Comp.Fuels.Keys).ToArray()) <= FixedPoint2.Zero)
        {
            return DystopiaEngineStatus.NoFuel;
        }

        return DystopiaEngineStatus.Running;
    }

    // ------------------------------------------------------------------ заправка из труб (экспортер)

    /// <summary>Экспортер: топливо — в топливный бак, масло — в масляный, вода — в водяной. Остальное не берёт.</summary>
    public FixedPoint2 Fill(Entity<DystopiaEngineControllerComponent> ent, Solution net, FixedPoint2 max)
    {
        var moved = FixedPoint2.Zero;
        var fuels = new List<ProtoId<ReagentPrototype>>(ent.Comp.Fuels.Keys).ToArray();
        moved += FillTank(ent, ent.Comp.FuelTank, net, max - moved, fuels);
        moved += FillTank(ent, ent.Comp.OilTank, net, max - moved, ent.Comp.Lubricant);
        moved += FillTank(ent, ent.Comp.WaterTank, net, max - moved, ent.Comp.Coolant);
        return moved;
    }

    private FixedPoint2 FillTank(EntityUid uid, string tank, Solution net, FixedPoint2 max,
        params ProtoId<ReagentPrototype>[] reagents)
    {
        if (max <= FixedPoint2.Zero || !_solution.TryGetSolution(uid, tank, out var tankEnt, out var tankSol))
            return FixedPoint2.Zero;

        var amount = FixedPoint2.Min(max,
            FixedPoint2.Min(tankSol.AvailableVolume, net.GetTotalPrototypeQuantity(reagents)));
        if (amount <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        _solution.TryAddSolution(tankEnt.Value, net.SplitSolutionWithOnly(amount, reagents));
        return amount;
    }

    // ------------------------------------------------------------------ меню

    private void OnUiOpened(Entity<DystopiaEngineControllerComponent> ent, ref BoundUIOpenedEvent args)
    {
        ScanRow(ent, Transform(ent));
        UpdateUi(ent);
    }

    private void OnToggle(Entity<DystopiaEngineControllerComponent> ent, ref DystopiaEngineToggleMessage args)
    {
        ent.Comp.On = !ent.Comp.On;
        ent.Comp.NextUpdate = TimeSpan.Zero;
        UpdateUi(ent);
    }

    private void OnSetThrottle(Entity<DystopiaEngineControllerComponent> ent, ref DystopiaEngineSetThrottleMessage args)
    {
        if (!float.IsFinite(args.Throttle))
            return;

        ent.Comp.Throttle = Math.Clamp(args.Throttle, 0f, ent.Comp.MaxThrottle);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DystopiaEngineControllerComponent> ent)
    {
        var engine = ent.Comp;
        var fuel = new List<DystopiaEngineTankInfo>();
        float fuelMax = 0f, oil = 0f, oilMax = 0f, water = 0f, waterMax = 0f;
        if (_solution.TryGetSolution(ent.Owner, engine.FuelTank, out _, out var fuelSol))
        {
            fuelMax = fuelSol.MaxVolume.Float();
            foreach (var quantity in fuelSol.Contents)
                fuel.Add(new DystopiaEngineTankInfo(quantity.Reagent.Prototype, quantity.Quantity.Float()));
        }

        if (_solution.TryGetSolution(ent.Owner, engine.OilTank, out _, out var oilSol))
        {
            oil = oilSol.Volume.Float();
            oilMax = oilSol.MaxVolume.Float();
        }

        if (_solution.TryGetSolution(ent.Owner, engine.WaterTank, out _, out var waterSol))
        {
            water = waterSol.Volume.Float();
            waterMax = waterSol.MaxVolume.Float();
        }

        // Подключено — если в сети патрубка есть ещё узлы (одинокий патрубок — сам себе сеть).
        var steamConnected = _nodes.TryGetNode<PipeNode>(ent.Owner, engine.SteamNode, out var pipe) &&
                             pipe.NodeGroup is { } group && group.Nodes.Count > 1;
        var pressure = pipe?.Air.Pressure ?? 0f;

        var wear = TryComp<DamageableComponent>(ent, out var damageable) && engine.SeizeDamage > FixedPoint2.Zero
            ? MathF.Min(1f, TotalDamage((ent.Owner, damageable)).Float() / engine.SeizeDamage.Float())
            : 0f;

        _ui.SetUiState(ent.Owner, DystopiaEngineUiKey.Key, new DystopiaEngineUiState(Status(ent), engine.On,
            engine.Cylinders, engine.MinCylinders, engine.MaxCylinders, engine.Radiators, engine.Throttle,
            engine.MaxThrottle, engine.Power, engine.Cylinders * engine.PowerPerCylinder * engine.MaxThrottle,
            engine.Temperature, engine.WarnTemperature, engine.OverheatTemperature, wear, fuel, fuelMax, oil, oilMax,
            water, waterMax, engine.SteamMoles, engine.SteamCapacity, pressure, steamConnected, engine.FuelRate,
            engine.OilRate, engine.WaterRate, engine.SteamRate));
    }

    // ------------------------------------------------------------------ осмотр

    private void OnExamined(Entity<DystopiaEngineControllerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        ScanRow(ent, Transform(ent));
        using (args.PushGroup(nameof(DystopiaEngineSystem)))
        {
            args.PushMarkup(Loc.GetString("dystopia-engine-examine-row",
                ("cylinders", ent.Comp.Cylinders), ("radiators", ent.Comp.Radiators)));
            args.PushMarkup(Loc.GetString("dystopia-engine-examine-state",
                ("temperature", (int) MathF.Round(ent.Comp.Temperature)), ("power", MathF.Round(ent.Comp.Power / 1000f, 1))));
        }
    }
}
