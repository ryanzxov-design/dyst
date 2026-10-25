// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: реактор РБМК из колонн (см. DystopiaReactorComponents). Реакторы — связные по стороне группы колонн на
// сетке; пересобираются, когда колонны или сепараторы появляются, исчезают, закрепляются. Раз в секунду на каждый
// реактор: нейтроны (топливо → 4 направления, графит замедляет, стержни и вода поглощают), тепло (деления, между
// соседями, вода снимает выше кипения → пар в барабаны-сепараторы), йод и ксенон, выгорание, ход стержней,
// перегрев (урон колонне), авторегулятор и автоматическая защита пультов, излучение (один источник в центре).

using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Destructible;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Radiation.Systems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.NodeContainer;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaReactorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private RadiationSystem _radiation = default!;

    private static readonly ProtoId<DystopiaReactorPrototype> ConfigId = "Default";
    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";

    /// <summary>Защита от зацикливания при поиске колонн (реактор без предела размера, но не бесконечный).</summary>
    private const int MaxColumns = 4096;

    private static readonly Vector2i[] Directions = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };

    /// <summary>Реактор — связная группа колонн. Состояние физики — в колоннах, тут только сводка.</summary>
    public sealed class Reactor
    {
        public EntityUid Grid;
        public readonly List<EntityUid> Columns = new();
        public readonly Dictionary<Vector2i, EntityUid> ByTile = new();
        public readonly Dictionary<EntityUid, Vector2i> TileOf = new();
        public readonly List<EntityUid> Separators = new();
        public Vector2i Min;
        public Vector2i Max;
        public float ThermalPower;
        public float SteamRate;
        public float Water;
        public float WaterMax;
        public float MaxTemperature;
        public float AverageXenon;
        public float Dryness;
        public bool Alarm;
        public EntityUid? RadiationCenter;
    }

    /// <summary>Отладка: сколько тактов за секунду (1 — обычное время).</summary>
    public int Speed = 1;

    private readonly List<Reactor> _reactors = new();
    private readonly Dictionary<EntityUid, Reactor> _reactorOf = new();
    private bool _dirty = true;
    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaReactorColumnComponent, ComponentStartup>((_, _, _) => _dirty = true);
        SubscribeLocalEvent<DystopiaReactorColumnComponent, ComponentShutdown>((_, _, _) => _dirty = true);
        SubscribeLocalEvent<DystopiaReactorColumnComponent, AnchorStateChangedEvent>(OnColumnAnchor);
        SubscribeLocalEvent<DystopiaReactorSeparatorComponent, ComponentStartup>((_, _, _) => _dirty = true);
        SubscribeLocalEvent<DystopiaReactorSeparatorComponent, ComponentShutdown>((_, _, _) => _dirty = true);
        SubscribeLocalEvent<DystopiaReactorSeparatorComponent, AnchorStateChangedEvent>(OnSeparatorAnchor);

        SubscribeLocalEvent<DystopiaReactorColumnComponent, ComponentInit>(OnColumnInit);
        SubscribeLocalEvent<DystopiaReactorColumnComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaReactorColumnComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaReactorColumnComponent, ExaminedEvent>(OnColumnExamined);
        SubscribeLocalEvent<DystopiaFuelRodComponent, ExaminedEvent>(OnRodExamined);
        SubscribeLocalEvent<DystopiaReactorSeparatorComponent, ExaminedEvent>(OnSeparatorExamined);
        InitializeFuel();
    }

    private void OnColumnAnchor(Entity<DystopiaReactorColumnComponent> ent, ref AnchorStateChangedEvent args)
    {
        _dirty = true;
    }

    private void OnSeparatorAnchor(Entity<DystopiaReactorSeparatorComponent> ent, ref AnchorStateChangedEvent args)
    {
        _dirty = true;
    }

    private void OnColumnInit(Entity<DystopiaReactorColumnComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.Kind == DystopiaReactorColumnKind.Fuel)
            _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.FuelSlot);
    }

    // ------------------------------------------------------------------ сборка реакторов

    private EntityUid? ColumnAt(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<DystopiaReactorColumnComponent>(uid.Value))
                return uid.Value;
        }

        return null;
    }

    private EntityUid? SeparatorAt(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<DystopiaReactorSeparatorComponent>(uid.Value))
                return uid.Value;
        }

        return null;
    }

    private void Rebuild()
    {
        _dirty = false;
        _reactors.Clear();
        _reactorOf.Clear();

        var query = EntityQueryEnumerator<DystopiaReactorColumnComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_reactorOf.ContainsKey(uid) || !xform.Anchored || xform.GridUid is not { } gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var grid))
            {
                continue;
            }

            var reactor = new Reactor { Grid = gridUid };
            var start = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var queue = new Queue<(EntityUid, Vector2i)>();
            queue.Enqueue((uid, start));
            _reactorOf[uid] = reactor;
            reactor.Min = start;
            reactor.Max = start;
            var separators = new HashSet<EntityUid>();

            while (queue.Count > 0 && reactor.Columns.Count < MaxColumns)
            {
                var (col, tile) = queue.Dequeue();
                reactor.Columns.Add(col);
                reactor.ByTile[tile] = col;
                reactor.TileOf[col] = tile;
                reactor.Min = Vector2i.ComponentMin(reactor.Min, tile);
                reactor.Max = Vector2i.ComponentMax(reactor.Max, tile);

                foreach (var dir in Directions)
                {
                    var next = tile + dir;
                    if (ColumnAt(gridUid, grid, next) is { } other)
                    {
                        if (_reactorOf.ContainsKey(other))
                            continue;

                        _reactorOf[other] = reactor;
                        queue.Enqueue((other, next));
                    }
                    else if (SeparatorAt(gridUid, grid, next) is { } separator)
                    {
                        separators.Add(separator);
                    }
                }
            }

            reactor.Separators.AddRange(separators);
            _reactors.Add(reactor);
        }
    }

    /// <summary>Реактор, к которому подключён пульт: колонна вплотную или колонна в той же сети кабеля управления.</summary>
    public Reactor? FindReactor(EntityUid console, string controlNode)
    {
        if (_dirty)
            Rebuild();

        var xform = Transform(console);
        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            foreach (var dir in Directions)
            {
                if (ColumnAt(gridUid, grid, tile + dir) is { } col && _reactorOf.TryGetValue(col, out var near))
                    return near;
            }
        }

        if (_nodes.TryGetNode<Node>(console, controlNode, out var node) && node.NodeGroup is { } group)
        {
            foreach (var other in group.Nodes)
            {
                if (_reactorOf.TryGetValue(other.Owner, out var linked))
                    return linked;
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ такт

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick || !_proto.TryIndex(ConfigId, out var cfg))
            return;

        const float dt = 1f;
        _nextTick = now + TimeSpan.FromSeconds(dt);

        // отладка: Speed тактов за секунду (ускорение времени реактора, топлива и бассейнов)
        for (var step = 0; step < Math.Clamp(Speed, 1, 50); step++)
        {
            if (_dirty)
                Rebuild();

            foreach (var reactor in _reactors)
                Tick(reactor, cfg, dt);

            CheckRupture(cfg);

            UpdateConsoles(cfg);

            var separators = EntityQueryEnumerator<DystopiaReactorSeparatorComponent>();
            while (separators.MoveNext(out var uid, out var separator))
                SeparatorTick((uid, separator), cfg, dt);

            RodsTick(cfg, dt);
            _pool.Tick(cfg, dt);
        }
    }

    private EntityUid? Rod(EntityUid column, DystopiaReactorColumnComponent col)
    {
        return col.Kind == DystopiaReactorColumnKind.Fuel &&
               _container.TryGetContainer(column, col.FuelSlot, out var slot) && slot.ContainedEntities.Count > 0 &&
               HasComp<DystopiaFuelRodComponent>(slot.ContainedEntities[0])
            ? slot.ContainedEntities[0]
            : null;
    }

    private void Tick(Reactor reactor, DystopiaReactorPrototype cfg, float dt)
    {
        var cols = new Dictionary<EntityUid, DystopiaReactorColumnComponent>();
        foreach (var uid in reactor.Columns)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<DystopiaReactorColumnComponent>(uid, out var c))
                cols[uid] = c;
        }

        var dryness = reactor.Dryness;

        // 1. Деления: выход нейтронов топливных каналов
        var output = new Dictionary<EntityUid, float>();
        var rods = 0;
        foreach (var (uid, col) in cols)
        {
            col.Power = 0f;
            if (Rod(uid, col) is not { } rodUid || !TryComp<DystopiaFuelRodComponent>(rodUid, out var rod))
                continue;

            rods++;
            if (rod.Burnup >= 1f)
                continue;

            var xenon = MathF.Min(cfg.MaxXenonPoison, col.Xenon * cfg.XenonWorth);
            var doppler = MathF.Max(0.2f, 1f - cfg.Doppler * MathF.Max(0f, col.Temperature - cfg.BoilTemperature));
            var voidFactor = 1f + (cfg.VoidBoost - 1f) * dryness;
            var k = rod.Reactivity * (1f - rod.Burnup * rod.Burnup) * (1f - xenon) * doppler * voidFactor;
            var o = MathF.Min(cfg.MaxFlux, (cfg.Source + col.Flux) * k);
            output[uid] = o;
            col.Power = o * cfg.PowerPerFlux;
        }

        // 2. Перенос нейтронов по четырём направлениям
        var incoming = new Dictionary<EntityUid, float>();
        foreach (var (uid, o) in output)
        {
            var origin = reactor.TileOf[uid];
            foreach (var dir in Directions)
            {
                var f = o / 4f;
                var moderated = false;
                var pos = origin;
                DystopiaReactorColumnKind? last = null;
                for (var step = 0; step < cfg.Travel && f > 0.001f; step++)
                {
                    pos += dir;
                    if (!reactor.ByTile.TryGetValue(pos, out var target) || !cols.TryGetValue(target, out var tc))
                    {
                        // край реактора: любая колонна — графитовый блок (каналы — трубы в графите), отражает часть назад
                        if (last != null)
                            incoming[uid] = incoming.GetValueOrDefault(uid) + f * cfg.Reflector * (moderated ? 1f : cfg.FastFactor);
                        break;
                    }

                    last = tc.Kind;

                    if (tc.Kind == DystopiaReactorColumnKind.Fuel && Rod(target, tc) != null)
                    {
                        incoming[target] = incoming.GetValueOrDefault(target) + f * (moderated ? 1f : cfg.FastFactor);
                        break;
                    }

                    switch (tc.Kind)
                    {
                        case DystopiaReactorColumnKind.Graphite:
                            f *= cfg.GraphitePass;
                            moderated = true;
                            break;
                        case DystopiaReactorColumnKind.Control:
                            // канал стержня стоит в графитовом блоке (с вытеснителем): замедляет, стержень — поглощает
                            f *= (1f - tc.Insertion * cfg.RodAbsorb) * cfg.ControlPass;
                            if (tc.Displacer)
                                f *= 1f + cfg.EndEffect;
                            moderated = true;
                            break;
                        default: // пароводяной или пустой топливный канал — вода в графитовом блоке: замедляет, чуть поглощает
                            f *= cfg.WaterPass + (cfg.DryPass - cfg.WaterPass) * dryness;
                            moderated = true;
                            break;
                    }
                }
            }
        }

        foreach (var (uid, col) in cols)
        {
            var next = MathF.Min(cfg.MaxFlux, incoming.GetValueOrDefault(uid));
            col.Flux += (next - col.Flux) * Math.Clamp(cfg.FluxRelax * dt, 0f, 1f);
        }

        // 3. Тепло: деления, теплопроводность между соседями
        foreach (var col in cols.Values)
            col.Temperature += col.Power * dt / cfg.HeatCapacity;

        foreach (var (uid, col) in cols)
        {
            var tile = reactor.TileOf[uid];
            foreach (var dir in new[] { new Vector2i(1, 0), new Vector2i(0, 1) })
            {
                if (!reactor.ByTile.TryGetValue(tile + dir, out var other) || !cols.TryGetValue(other, out var oc))
                    continue;

                var diff = oc.Temperature - col.Temperature;
                var flow = Math.Clamp(cfg.Conduction * diff * dt / cfg.HeatCapacity, -MathF.Abs(diff) / 2f, MathF.Abs(diff) / 2f);
                col.Temperature += flow;
                oc.Temperature -= flow;
            }
        }

        // 4. Вода: кипит в топливных и пароводяных каналах выше точки кипения
        var waters = new List<(EntityUid Uid, float Amount)>();
        reactor.Water = 0f;
        reactor.WaterMax = 0f;
        foreach (var sepUid in reactor.Separators)
        {
            if (TerminatingOrDeleted(sepUid) || !TryComp<DystopiaReactorSeparatorComponent>(sepUid, out var sep) ||
                !_solution.TryGetSolution(sepUid, sep.WaterSolution, out _, out var sol))
            {
                continue;
            }

            var amount = sol.GetTotalPrototypeQuantity(sep.Water).Float();
            waters.Add((sepUid, amount));
            reactor.Water += amount;
            reactor.WaterMax += sol.MaxVolume.Float();
        }

        var desired = new Dictionary<EntityUid, float>();
        var totalDesired = 0f;
        foreach (var (uid, col) in cols)
        {
            var cap = col.Kind switch
            {
                DystopiaReactorColumnKind.Fuel => cfg.FuelCoolingCap,
                DystopiaReactorColumnKind.Coolant => cfg.CoolantCoolingCap,
                _ => 0f,
            };
            var q = Math.Clamp((col.Temperature - cfg.BoilTemperature) * cfg.CoolingPerKelvin, 0f, cap);
            if (q <= 0f)
                continue;

            desired[uid] = q;
            totalDesired += q;
        }

        var molesWanted = totalDesired * dt / cfg.SteamEnergy;
        var waterWanted = molesWanted * cfg.WaterPerMole;
        var fraction = waterWanted > 0f ? MathF.Min(1f, reactor.Water / waterWanted) : 1f;
        // Сухость — для парового эффекта: воды не хватает на всё кипение
        reactor.Dryness = waterWanted > 0f ? 1f - fraction : (reactor.Water > 0f || reactor.Separators.Count > 0 ? 0f : 1f);

        foreach (var (uid, col) in cols)
        {
            col.Cooling = 0f;
            if (desired.TryGetValue(uid, out var q))
            {
                col.Cooling = q * fraction;
                col.Temperature -= col.Cooling * dt / cfg.HeatCapacity;
            }

            col.Temperature -= (col.Temperature - 20f) * cfg.PassiveCooling * dt;
        }

        var moles = molesWanted * fraction;
        var waterUsed = moles * cfg.WaterPerMole;
        reactor.SteamRate = moles / dt;
        if (waterUsed > 0f && reactor.Water > 0f)
        {
            foreach (var (sepUid, amount) in waters)
            {
                var share = amount / reactor.Water;
                var sep = Comp<DystopiaReactorSeparatorComponent>(sepUid);
                if (_solution.TryGetSolution(sepUid, sep.WaterSolution, out var solEnt, out _))
                    _solution.RemoveReagent(solEnt.Value, sep.Water, FixedPoint2.New(waterUsed * share));
                sep.SteamMoles += moles * share;
            }

            reactor.Water = MathF.Max(0f, reactor.Water - waterUsed);
        }

        // 5. Йод, ксенон, выгорание, стержни, перегрев, вид
        var iodineDecay = MathF.Log(2f) / cfg.IodineHalfLife;
        var xenonDecay = MathF.Log(2f) / cfg.XenonHalfLife;
        reactor.ThermalPower = 0f;
        reactor.MaxTemperature = 0f;
        var xenonSum = 0f;
        var fuelCount = 0;
        foreach (var (uid, col) in cols)
        {
            reactor.ThermalPower += col.Power;
            reactor.MaxTemperature = MathF.Max(reactor.MaxTemperature, col.Temperature);

            if (col.Kind == DystopiaReactorColumnKind.Fuel)
            {
                var p = col.Power / MathF.Max(1f, cfg.NominalPower);
                col.Iodine = MathF.Max(0f, col.Iodine + (cfg.IodineProduction * p - col.Iodine * iodineDecay) * dt);
                col.Xenon = MathF.Max(0f,
                    col.Xenon + (col.Iodine * iodineDecay - col.Xenon * xenonDecay - col.Xenon * cfg.XenonBurn * p) * dt);
                if (Rod(uid, col) is { } rodUid && TryComp<DystopiaFuelRodComponent>(rodUid, out var rod) && col.Power > 0f)
                {
                    rod.Burnup = MathF.Min(1f, rod.Burnup + col.Power * dt / MathF.Max(1f, rod.Energy));
                    rod.Activity += cfg.ActivityGain * p * dt; // продукты деления; распад — в RodsTick
                }
                xenonSum += MathF.Min(cfg.MaxXenonPoison, col.Xenon * cfg.XenonWorth);
                fuelCount++;
                // вид ТВЭЛа (SetData без изменений ничего не шлёт; нужно и после загрузки карты)
                _appearance.SetData(uid, DystopiaReactorVisuals.Rod, ColumnRodGrade(uid, col));
            }

            if (col.Kind == DystopiaReactorColumnKind.Control)
            {
                var target = col.Scrammed ? 1f : col.Target;
                var speed = (col.Scrammed ? cfg.ScramSpeed : cfg.RodSpeed) * dt;
                var previous = col.Insertion;
                col.Insertion = col.Insertion < target
                    ? MathF.Min(target, col.Insertion + speed)
                    : MathF.Max(target, col.Insertion - speed);

                // концевой эффект: пошёл вниз почти из верхнего положения — вытеснитель в зоне, пока не опустится глубже
                if (col.Insertion > previous)
                {
                    if (previous < cfg.EndEffectStart)
                        col.Displacer = true;
                    if (col.Insertion >= cfg.EndEffectDepth)
                        col.Displacer = false;
                }
                else
                {
                    col.Displacer = false;
                }
                _appearance.SetData(uid, DystopiaReactorVisuals.Insertion, (int) MathF.Round(col.Insertion * 4f));
            }

            if (col.Temperature > cfg.DamageTemperature)
            {
                col.PendingDamage += (col.Temperature - cfg.DamageTemperature) * cfg.DamagePerKelvin * dt;
                if (col.PendingDamage >= 0.5f)
                {
                    _damageable.TryChangeDamage(uid, new DamageSpecifier(_proto.Index(HeatDamage), FixedPoint2.New(col.PendingDamage)),
                        interruptsDoAfters: false);
                    col.PendingDamage = 0f;
                }
            }

            var level = col.Temperature >= cfg.HotLevel2 ? 2 : col.Temperature >= cfg.HotLevel1 ? 1 : 0;
            if (level != col.HeatLevel)
            {
                col.HeatLevel = level;
                _appearance.SetData(uid, DystopiaReactorVisuals.Heat, level);
            }
        }

        reactor.AverageXenon = fuelCount > 0 ? xenonSum / fuelCount : 0f;
        reactor.Alarm = reactor.MaxTemperature > cfg.HotLevel2;

        UpdateRadiation(reactor, cfg, rods);
    }

    /// <summary>Весь реактор светит одним источником в колонне у центра; сила — по мощности и числу ТВЭЛов.</summary>
    private void UpdateRadiation(Reactor reactor, DystopiaReactorPrototype cfg, int rods)
    {
        if (reactor.Columns.Count == 0)
            return;

        var cx = (reactor.Min.X + reactor.Max.X) / 2f;
        var cy = (reactor.Min.Y + reactor.Max.Y) / 2f;
        EntityUid? center = null;
        var best = float.MaxValue;
        foreach (var (uid, tile) in reactor.TileOf)
        {
            var d = (tile.X - cx) * (tile.X - cx) + (tile.Y - cy) * (tile.Y - cy);
            if (d < best)
            {
                best = d;
                center = uid;
            }
        }

        var intensity = reactor.ThermalPower / 1000f * cfg.RadsPerMegawatt + rods * cfg.RadsPerRod;
        foreach (var uid in reactor.Columns)
        {
            if (!TryComp<RadiationSourceComponent>(uid, out var source))
                continue;

            if (uid == center && intensity > 0.01f)
            {
                _radiation.SetIntensity((uid, source), intensity);
                if (!source.Enabled)
                    _radiation.SetSourceEnabled((uid, source), true);
            }
            else if (source.Enabled)
            {
                _radiation.SetSourceEnabled((uid, source), false);
            }
        }

        reactor.RadiationCenter = center;
    }

    // ------------------------------------------------------------------ пульты: авторегулятор и защита

    private void UpdateConsoles(DystopiaReactorPrototype cfg)
    {
        var query = EntityQueryEnumerator<DystopiaReactorConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (FindReactor(uid, console.ControlNode) is not { } reactor)
                continue;

            var overpower = reactor.ThermalPower > console.AutoScramPower * MathF.Max(1000f, CoolingCapacity(reactor));
            if (console.AutoScram && (reactor.MaxTemperature > console.AutoScramTemperature || overpower) &&
                !AllScrammed(reactor))
            {
                Scram(reactor);
                _audio.PlayPvs(console.AlarmSound, uid);
            }

            var power = reactor.ThermalPower;
            if (console.AutoRegulator && console.Setpoint > 0f)
            {
                // отклонение от уставки и скорость роста: без второго стержни раскачивают горячее топливо
                var error = (power - console.Setpoint) / console.Setpoint;
                var rate = (power - console.LastPower) / MathF.Max(1f, MathF.Max(power, console.LastPower));
                var delta = (MathF.Abs(error) < cfg.RegulatorDeadband ? 0f : error * cfg.RegulatorGain) +
                            rate * cfg.RegulatorDamping;
                // мощность далеко выше уставки — вниз на полной скорости; топливо горячее — не вынимать (после перегрева
                // иначе выдернет всё до нуля и раскачает реактор)
                if (error > cfg.RegulatorOverpower)
                    delta = cfg.RegulatorMaxStep;
                if (delta < 0f && reactor.MaxTemperature > cfg.RegulatorHotLimit)
                    delta = 0f;
                delta = Math.Clamp(delta, -cfg.RegulatorMaxStep, cfg.RegulatorMaxStep);
                foreach (var col in GroupRods(reactor, 1))
                    col.Target = Math.Clamp(col.Target + delta, 0f, 1f);
            }

            console.LastPower = power;

            if (reactor.Alarm && _timing.CurTime >= console.NextAlarm)
            {
                _audio.PlayPvs(console.AlarmSound, uid);
                console.NextAlarm = _timing.CurTime + TimeSpan.FromSeconds(8);
            }
        }
    }

    /// <summary>Стержни группы (0 — все стержни реактора).</summary>
    public IEnumerable<DystopiaReactorColumnComponent> GroupRods(Reactor reactor, int group)
    {
        foreach (var uid in reactor.Columns)
        {
            if (TryComp<DystopiaReactorColumnComponent>(uid, out var col) &&
                col.Kind == DystopiaReactorColumnKind.Control && (group == 0 || col.Group == group))
            {
                yield return col;
            }
        }
    }

    private bool AllScrammed(Reactor reactor)
    {
        foreach (var uid in reactor.Columns)
        {
            if (TryComp<DystopiaReactorColumnComponent>(uid, out var col) &&
                col.Kind == DystopiaReactorColumnKind.Control && !col.Scrammed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>АЗ-5: все стержни реактора вниз на аварийной скорости.</summary>
    public void Scram(Reactor reactor)
    {
        foreach (var uid in reactor.Columns)
        {
            if (TryComp<DystopiaReactorColumnComponent>(uid, out var col) && col.Kind == DystopiaReactorColumnKind.Control)
                col.Scrammed = true;
        }
    }

    /// <summary>Снять АЗ: стержни снова идут к своим уставкам (уставки — «вниз», чтобы не дёрнуло).</summary>
    public void ResetScram(Reactor reactor)
    {
        foreach (var uid in reactor.Columns)
        {
            if (TryComp<DystopiaReactorColumnComponent>(uid, out var col) &&
                col.Kind == DystopiaReactorColumnKind.Control && col.Scrammed)
            {
                col.Scrammed = false;
                col.Target = 1f;
            }
        }
    }

    // ------------------------------------------------------------------ барабан-сепаратор

    private void SeparatorTick(Entity<DystopiaReactorSeparatorComponent> ent, DystopiaReactorPrototype cfg, float dt)
    {
        var sep = ent.Comp;
        sep.SteamRate = 0f;
        var temperature = cfg.SteamTemperature;
        if (sep.SteamMoles > 0.001f && _nodes.TryGetNode<PipeNode>(ent.Owner, sep.SteamNode, out var pipe))
        {
            var kDrum = Atmospherics.R * temperature / MathF.Max(1f, sep.DrumVolume);
            var kPipe = Atmospherics.R * temperature / MathF.Max(1f, pipe.Air.Volume);
            var drumPressure = sep.SteamMoles * kDrum;
            if (drumPressure > pipe.Air.Pressure)
            {
                var moles = MathF.Min(sep.SteamMoles, (drumPressure - pipe.Air.Pressure) / (kDrum + kPipe));
                if (moles > 0.001f)
                {
                    var steam = new GasMixture { Temperature = temperature };
                    steam.AdjustMoles(Gas.WaterVapor, moles);
                    _atmos.Merge(pipe.Air, steam);
                    sep.SteamMoles -= moles;
                    sep.SteamRate = moles / dt;
                }
            }
        }

        var pressure = Pressure(sep, cfg);
        if (pressure <= sep.DamagePressure || sep.PressureDamage.Empty)
            return;

        sep.PendingDamage += (pressure - sep.DamagePressure) / 100f * sep.PressureDamageRate * dt;
        if (sep.PendingDamage >= 0.5f)
        {
            _damageable.TryChangeDamage(ent.Owner, sep.PressureDamage * sep.PendingDamage, interruptsDoAfters: false);
            sep.PendingDamage = 0f;
        }
    }

    public float Pressure(DystopiaReactorSeparatorComponent sep, DystopiaReactorPrototype cfg)
    {
        return sep.SteamMoles * Atmospherics.R * cfg.SteamTemperature / MathF.Max(1f, sep.DrumVolume);
    }

    public float MaxSeparatorPressure(Reactor reactor)
    {
        if (!_proto.TryIndex(ConfigId, out var cfg))
            return 0f;

        var max = 0f;
        foreach (var uid in reactor.Separators)
        {
            if (TryComp<DystopiaReactorSeparatorComponent>(uid, out var sep))
                max = MathF.Max(max, Pressure(sep, cfg));
        }

        return max;
    }

    // ------------------------------------------------------------------ ТВЭЛы руками, ручной сброс стержня

    private void OnInteractUsing(Entity<DystopiaReactorColumnComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Kind != DystopiaReactorColumnKind.Fuel)
            return;

        // захват: вынуть ТВЭЛ из канала или поставить зажатый
        if (TryComp<DystopiaTongsComponent>(args.Used, out var tongs))
        {
            var channel = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.FuelSlot);
            if (TongsExchange((args.Used, tongs), args.User, channel))
            {
                args.Handled = true;
                _appearance.SetData(ent, DystopiaReactorVisuals.Rod, ColumnRodGrade(ent, ent.Comp));
            }

            return;
        }

        if (!HasComp<DystopiaFuelRodComponent>(args.Used))
            return;

        args.Handled = true;
        var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.FuelSlot);
        if (slot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-reactor-channel-occupied"), ent, args.User);
            return;
        }

        if (_container.Insert(args.Used, slot))
        {
            _appearance.SetData(ent, DystopiaReactorVisuals.Rod, RodGrade(args.Used));
            _popup.PopupEntity(Loc.GetString("dystopia-reactor-rod-inserted"), ent, args.User);
        }
    }

    /// <summary>Сорт для вида и карты: реактивность ×10 (0 — пусто).</summary>
    public int RodGrade(EntityUid? rod)
    {
        return rod is { } uid && TryComp<DystopiaFuelRodComponent>(uid, out var r) ? (int) MathF.Round(r.Reactivity * 10f) : 0;
    }

    private void OnGetVerbs(Entity<DystopiaReactorColumnComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (ent.Comp.Kind == DystopiaReactorColumnKind.Fuel && Rod(ent, ent.Comp) is { } rod)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-reactor-verb-extract"),
                Act = () =>
                {
                    // облучённый ТВЭЛ раскалён — только захватом
                    if (TryComp<DystopiaHotItemComponent>(rod, out var hotRod) && hotRod.Hot)
                    {
                        _popup.PopupEntity(Loc.GetString("dystopia-fuel-rod-too-hot"), ent, user);
                        return;
                    }

                    if (!_container.TryGetContainer(ent, ent.Comp.FuelSlot, out var slot) || !_container.Remove(rod, slot))
                        return;

                    _hands.PickupOrDrop(user, rod);
                    _appearance.SetData(ent, DystopiaReactorVisuals.Rod, 0);
                },
                Priority = 2,
            });
        }

        if (ent.Comp.Kind == DystopiaReactorColumnKind.Control && !ent.Comp.Scrammed)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-reactor-verb-scram"),
                Act = () =>
                {
                    ent.Comp.Scrammed = true;
                    _popup.PopupEntity(Loc.GetString("dystopia-reactor-scram-manual"), ent, user);
                },
                Priority = 3,
            });
        }
    }

    // ------------------------------------------------------------------ осмотр

    private void OnColumnExamined(Entity<DystopiaReactorColumnComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var col = ent.Comp;
        using (args.PushGroup(nameof(DystopiaReactorSystem)))
        {
            args.PushMarkup(Loc.GetString("dystopia-reactor-examine-temperature",
                ("temperature", (int) MathF.Round(col.Temperature))));
            switch (col.Kind)
            {
                case DystopiaReactorColumnKind.Fuel:
                    if (Rod(ent, col) is { } rod && TryComp<DystopiaFuelRodComponent>(rod, out var r))
                    {
                        args.PushMarkup(Loc.GetString("dystopia-reactor-examine-rod",
                            ("rod", Name(rod)), ("burnup", (int) MathF.Round(r.Burnup * 100f)),
                            ("power", (int) MathF.Round(col.Power))));
                    }
                    else
                    {
                        args.PushMarkup(Loc.GetString("dystopia-reactor-examine-empty"));
                    }

                    break;
                case DystopiaReactorColumnKind.Control:
                    args.PushMarkup(Loc.GetString(col.Scrammed ? "dystopia-reactor-examine-scrammed" : "dystopia-reactor-examine-control",
                        ("insertion", (int) MathF.Round(col.Insertion * 100f)), ("group", col.Group)));
                    break;
            }

            if (_dirty)
                Rebuild();

            if (_reactorOf.TryGetValue(ent, out var reactor))
            {
                args.PushMarkup(Loc.GetString("dystopia-reactor-examine-reactor",
                    ("columns", reactor.Columns.Count), ("separators", reactor.Separators.Count),
                    ("power", MathF.Round(reactor.ThermalPower / 1000f, 2))));
            }
        }
    }

    private void OnRodExamined(Entity<DystopiaFuelRodComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
        {
            args.PushMarkup(Loc.GetString(ent.Comp.Burnup >= 1f ? "dystopia-fuel-rod-spent" : "dystopia-fuel-rod-burnup",
                ("burnup", (int) MathF.Round(ent.Comp.Burnup * 100f))));
            ExamineActivity(ent, args);
        }
    }

    private void OnSeparatorExamined(Entity<DystopiaReactorSeparatorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !_proto.TryIndex(ConfigId, out var cfg))
            return;

        var water = _solution.TryGetSolution(ent.Owner, ent.Comp.WaterSolution, out _, out var sol)
            ? sol.GetTotalPrototypeQuantity(ent.Comp.Water).Float()
            : 0f;
        args.PushMarkup(Loc.GetString("dystopia-reactor-separator-examine",
            ("water", (int) water), ("pressure", (int) MathF.Round(Pressure(ent.Comp, cfg)))));
        if (Pressure(ent.Comp, cfg) > ent.Comp.WarnPressure)
            args.PushMarkup(Loc.GetString("dystopia-boiler-examine-danger"));
    }

    // ------------------------------------------------------------------ для пульта

    public float ColumnHealth(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return 1f;

        var max = _destructible.DestroyedAt(uid);
        if (max <= FixedPoint2.Zero || max >= FixedPoint2.MaxValue)
            return 1f;

#pragma warning disable CS0618 // Do not rely on a numerically quantifiable amount of damage — здесь это и есть целость
        return Math.Clamp(1f - _damageable.GetTotalDamage((uid, damageable)).Float() / max.Float(), 0f, 1f);
#pragma warning restore CS0618
    }

    /// <summary>Сколько тепла могут снять водой все каналы реактора, кВт (верх шкалы мощности на пульте).</summary>
    public float CoolingCapacity(Reactor reactor)
    {
        if (!_proto.TryIndex(ConfigId, out var cfg))
            return 0f;

        var sum = 0f;
        foreach (var uid in reactor.Columns)
        {
            if (!TryComp<DystopiaReactorColumnComponent>(uid, out var col))
                continue;

            sum += col.Kind switch
            {
                DystopiaReactorColumnKind.Fuel => cfg.FuelCoolingCap,
                DystopiaReactorColumnKind.Coolant => cfg.CoolantCoolingCap,
                _ => 0f,
            };
        }

        return sum;
    }

    public DystopiaReactorPrototype? Settings() => _proto.TryIndex(ConfigId, out var cfg) ? cfg : null;

    public float RodBurnup(EntityUid column, DystopiaReactorColumnComponent col)
    {
        return Rod(column, col) is { } rod && TryComp<DystopiaFuelRodComponent>(rod, out var r) ? r.Burnup : 0f;
    }

    public int ColumnRodGrade(EntityUid column, DystopiaReactorColumnComponent col)
    {
        return RodGrade(Rod(column, col));
    }
}
