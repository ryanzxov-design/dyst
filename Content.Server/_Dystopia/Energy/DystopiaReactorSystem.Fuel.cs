// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: энергетика, шаг 3б — облучённое топливо и авария реактора.
// ТВЭЛ копит активность, пока работает; она распадается везде (полураспад ActivityHalfLife). От неё излучение,
// остаточное тепло (раскалён — только захватом) и, вне воды, трескающаяся оболочка (радиоактивная пыль на пол).
// Разрушенный горячий топливный канал — кориум вместо ТВЭЛа; на большой мощности — паровой взрыв: обломки горящего
// графита по залу и радиоактивная пыль на большую область. Отладка — публичные методы ниже.

using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Radiation.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaReactorSystem
{
    [Dependency] private DystopiaHotItemSystem _hot = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private DystopiaSpentFuelPoolSystem _pool = default!;

    /// <summary>Когда реактор на этой сетке взрывался последний раз (один взрыв на одну аварию).</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _lastExplosion = new();

    private void InitializeFuel()
    {
        SubscribeLocalEvent<DystopiaReactorColumnComponent, DestructionEventArgs>(OnColumnDestroyed);
    }

    // ------------------------------------------------------------------ облучённое топливо

    private void RodsTick(DystopiaReactorPrototype cfg, float dt)
    {
        var decay = MathF.Exp(-MathF.Log(2f) / MathF.Max(1f, cfg.ActivityHalfLife) * dt);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaFuelRodComponent>();
        while (query.MoveNext(out var uid, out var rod))
        {
            rod.Activity *= decay;
            if (rod.Activity < 0.001f)
                rod.Activity = 0f;

            var intensity = ShieldedRadiation(uid, rod, cfg);
            if (MathF.Abs(intensity - rod.LastIntensity) > 0.02f && TryComp<RadiationSourceComponent>(uid, out var source))
            {
                _radiation.SetIntensity((uid, source), intensity);
                rod.LastIntensity = intensity;
            }

            var hot = rod.Activity > cfg.HotActivity;
            if (TryComp<DystopiaHotItemComponent>(uid, out var hotItem) && hotItem.Hot != hot)
                _hot.SetHot((uid, hotItem), hot);

            if (rod.Activity <= cfg.AirShedActivity || now < rod.NextShed || IsCooled(uid))
                continue;

            // вне воды горячая оболочка трескается: пыль на пол там, где ТВЭЛ (или то, в чём он)
            rod.NextShed = now + TimeSpan.FromSeconds(cfg.AirShedInterval);
            SpillDust(_xform.GetMoverCoordinates(uid), cfg.AirShedUnits, cfg);
        }
    }

    /// <summary>
    /// Излучение ТВЭЛа с учётом того, где он: в канале реактора — ноль (реактор светит общим источником), в ячейке
    /// бассейна — за вычетом того, что гасит вода.
    /// </summary>
    private float ShieldedRadiation(EntityUid uid, DystopiaFuelRodComponent rod, DystopiaReactorPrototype cfg)
    {
        var raw = RodRadiation(rod, cfg);
        if (!_container.TryGetContainingContainer(uid, out var container))
            return raw;

        var owner = container.Owner;
        if (HasComp<DystopiaReactorColumnComponent>(owner))
            return 0f;

        return TryComp<DystopiaSpentFuelPoolCellComponent>(owner, out var cell)
            ? MathF.Max(0f, raw - _pool.Shield((owner, cell)))
            : raw;
    }

    public float RodRadiation(DystopiaFuelRodComponent rod, DystopiaReactorPrototype cfg)
    {
        return rod.BaseRadiation + rod.Burnup * cfg.SpentRads + rod.Activity * cfg.RadsPerActivity;
    }

    /// <summary>ТВЭЛ в воде: в канале реактора или в ячейке бассейна, где воды хватает.</summary>
    private bool IsCooled(EntityUid rod)
    {
        if (!_container.TryGetContainingContainer(rod, out var container))
            return false;

        var owner = container.Owner;
        return HasComp<DystopiaReactorColumnComponent>(owner) ||
               TryComp<DystopiaSpentFuelPoolCellComponent>(owner, out var cell) && _pool.IsCooled((owner, cell));
    }

    private void ExamineActivity(Entity<DystopiaFuelRodComponent> ent, ExaminedEvent args)
    {
        if (ent.Comp.Activity <= 0.01f || !_proto.TryIndex(ConfigId, out var cfg))
            return;

        var heat = ent.Comp.Activity * cfg.DecayHeatPerActivity;
        var minutes = ent.Comp.Activity > cfg.HotActivity
            ? (int) MathF.Ceiling(MathF.Log(ent.Comp.Activity / cfg.HotActivity) / MathF.Log(2f) * cfg.ActivityHalfLife / 60f)
            : 0;
        args.PushMarkup(Loc.GetString(minutes > 0 ? "dystopia-fuel-rod-irradiated" : "dystopia-fuel-rod-cooled",
            ("heat", MathF.Round(heat, 1)), ("rads", MathF.Round(RodRadiation(ent.Comp, cfg), 1)), ("minutes", minutes)));
    }

    /// <summary>
    /// Захват у канала или ячейки: зажат ТВЭЛ и место свободно — поставить; захват пуст, а ТВЭЛ стоит — вынуть.
    /// </summary>
    public bool TongsExchange(Entity<DystopiaTongsComponent> tongs, EntityUid user, ContainerSlot slot)
    {
        if (!_container.TryGetContainer(tongs, tongs.Comp.Slot, out var grip) || grip is not ContainerSlot gripSlot)
            return false;

        if (gripSlot.ContainedEntity is { } held)
        {
            if (!HasComp<DystopiaFuelRodComponent>(held))
                return false;

            if (slot.ContainedEntity != null)
            {
                _popup.PopupEntity(Loc.GetString("dystopia-reactor-channel-occupied"), slot.Owner, user);
                return true;
            }

            if (!_container.Remove(held, gripSlot))
                return true;

            if (!_container.Insert(held, slot))
            {
                _container.Insert(held, gripSlot);
                return true;
            }

            _popup.PopupEntity(Loc.GetString("dystopia-grabber-placed", ("item", held)), slot.Owner, user);
            return true;
        }

        if (slot.ContainedEntity is not { } inside)
            return false;

        if (!_container.Remove(inside, slot))
            return true;

        if (!_container.Insert(inside, gripSlot))
        {
            _container.Insert(inside, slot);
            return true;
        }

        _popup.PopupEntity(Loc.GetString("dystopia-grabber-taken", ("item", inside)), slot.Owner, user);
        return true;
    }

    private void SpillDust(EntityCoordinates coords, float units, DystopiaReactorPrototype cfg)
    {
        if (units <= 0f)
            return;

        var solution = new Solution(cfg.ShedReagent, FixedPoint2.New(units));
        _puddle.TrySpillAt(coords, solution, out _, sound: false);
    }

    // ------------------------------------------------------------------ авария

    private void OnColumnDestroyed(EntityUid uid, DystopiaReactorColumnComponent component, DestructionEventArgs args)
    {
        if (!_proto.TryIndex(ConfigId, out var cfg))
            return;

        var ent = new Entity<DystopiaReactorColumnComponent>(uid, component);
        // без пересборки: у пересобранного реактора ещё нет мощности прошлого такта

        var col = ent.Comp;
        var coords = _xform.GetMoverCoordinates(ent);
        _reactorOf.TryGetValue(ent, out var reactor);
        var hot = col.Temperature >= cfg.DamageTemperature;

        if (col.Kind == DystopiaReactorColumnKind.Fuel && Rod(ent, col) is { } rod)
        {
            if (hot)
            {
                // топливо расплавилось вместе с каналом
                QueueDel(rod);
                SpawnCorium(coords, cfg.CoriumSpread);
            }
            else if (_container.TryGetContainer(ent, col.FuelSlot, out var slot))
            {
                _container.Remove(rod, slot);
            }
        }

        var exploding = reactor != null && reactor.ThermalPower >= cfg.ExplosionMinPower;
        if (col.Kind == DystopiaReactorColumnKind.Graphite)
        {
            for (var i = 0; i < cfg.DebrisPerGraphite; i++)
                SpawnDebris(coords, hot || exploding || col.Temperature > 400f);
        }

        if (exploding && hot)
            SteamExplosion(reactor!, cfg, false);
    }

    /// <summary>Сколько тактов подряд реактор (сетка, угол) идёт вразнос.</summary>
    private readonly Dictionary<(EntityUid, Vector2i), int> _runaway = new();

    /// <summary>
    /// Разгон: мощность выше RuptureFactor × съёма водой (не меньше 1 МВт) RuptureTime тактов подряд — топливные каналы
    /// рвёт паром разом (кориум, паровой взрыв — через OnColumnDestroyed).
    /// </summary>
    private void CheckRupture(DystopiaReactorPrototype cfg)
    {
        if (cfg.RuptureFactor <= 0f)
            return;

        foreach (var reactor in _reactors.ToArray())
        {
            var key = (reactor.Grid, reactor.Min);
            if (reactor.ThermalPower <= cfg.RuptureFactor * MathF.Max(1000f, CoolingCapacity(reactor)))
            {
                _runaway.Remove(key);
                continue;
            }

            var ticks = _runaway.GetValueOrDefault(key) + 1;
            _runaway[key] = ticks;
            if (ticks < cfg.RuptureTime)
                continue;

            _runaway.Remove(key);
            Rupture(reactor, cfg);
        }
    }

    /// <summary>Разрыв всех топливных каналов реактора разом (раскалены — значит, кориум и взрыв).</summary>
    public void Rupture(Reactor reactor, DystopiaReactorPrototype cfg)
    {
        SteamExplosion(reactor, cfg, false);
        foreach (var uid in reactor.Columns.ToArray())
        {
            if (TerminatingOrDeleted(uid) || !TryComp<DystopiaReactorColumnComponent>(uid, out var col) ||
                col.Kind != DystopiaReactorColumnKind.Fuel)
            {
                continue;
            }

            col.Temperature = MathF.Max(col.Temperature, cfg.DamageTemperature + 300f);
            DamageColumn(uid, 100000f);
        }
    }

    /// <summary>Центр реактора (клетка колонны, ближайшей к середине) в координатах сетки.</summary>
    public EntityCoordinates? Center(Reactor reactor)
    {
        if (!TryComp<MapGridComponent>(reactor.Grid, out var grid))
            return null;

        var cx = (reactor.Min.X + reactor.Max.X) / 2f;
        var cy = (reactor.Min.Y + reactor.Max.Y) / 2f;
        Vector2i? best = null;
        var bestD = float.MaxValue;
        foreach (var tile in reactor.ByTile.Keys)
        {
            var d = (tile.X - cx) * (tile.X - cx) + (tile.Y - cy) * (tile.Y - cy);
            if (d >= bestD)
                continue;

            bestD = d;
            best = tile;
        }

        return best is { } b ? _map.GridTileToLocal(reactor.Grid, grid, b) : null;
    }

    /// <summary>Паровой взрыв: взрыв в центре реактора по мощности, обломки горящего графита, пыль на большую область.</summary>
    public void SteamExplosion(Reactor reactor, DystopiaReactorPrototype cfg, bool force)
    {
        var now = _timing.CurTime;
        if (!force && _lastExplosion.TryGetValue(reactor.Grid, out var last) &&
            now - last < TimeSpan.FromSeconds(cfg.ExplosionCooldown))
        {
            return;
        }

        if (Center(reactor) is not { } center)
            return;

        _lastExplosion[reactor.Grid] = now;
        var power = MathF.Max(reactor.ThermalPower, force ? 5000f : 0f);
        var intensity = Math.Clamp(power / 1000f * cfg.ExplosionPerMegawatt, cfg.ExplosionMinIntensity,
            cfg.ExplosionMaxIntensity);
        _explosion.QueueExplosion(_xform.ToMapCoordinates(center), "Default", intensity, cfg.ExplosionSlope,
            cfg.ExplosionMaxTile, null);

        // графит разлетается и горит
        var graphite = 0;
        foreach (var uid in reactor.Columns)
        {
            if (TryComp<DystopiaReactorColumnComponent>(uid, out var col) && col.Kind == DystopiaReactorColumnKind.Graphite)
                graphite++;
        }

        for (var i = 0; i < Math.Max(4, graphite * cfg.DebrisPerGraphite); i++)
            SpawnDebris(center.Offset(RandomOffset(cfg.DebrisRadius)), true);

        Contaminate(center, cfg);
    }

    private Vector2 RandomOffset(float radius)
    {
        var r = radius * MathF.Sqrt(_random.NextFloat());
        var angle = _random.NextFloat() * MathF.Tau;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
    }

    /// <summary>Радиоактивная пыль на большую область: у центра больше, к краю меньше.</summary>
    public void Contaminate(EntityCoordinates center, DystopiaReactorPrototype cfg)
    {
        for (var i = 0; i < cfg.ContaminationPuddles; i++)
        {
            var offset = RandomOffset(cfg.ContaminationRadius);
            var share = Math.Clamp(offset.Length() / MathF.Max(1f, cfg.ContaminationRadius), 0f, 1f);
            var units = cfg.ContaminationMaxUnits + (cfg.ContaminationMinUnits - cfg.ContaminationMaxUnits) * share;
            SpillDust(center.Offset(offset), units, cfg);
        }
    }

    /// <summary>Кориум на клетке и ещё на spread случайных соседних.</summary>
    public void SpawnCorium(EntityCoordinates coords, int spread)
    {
        if (!_proto.TryIndex(ConfigId, out var cfg))
            return;

        TrySpawnCorium(coords, cfg);
        Vector2[] dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        for (var i = 0; i < spread; i++)
            TrySpawnCorium(coords.Offset(_random.Pick(dirs)), cfg);
    }

    private void TrySpawnCorium(EntityCoordinates coords, DystopiaReactorPrototype cfg)
    {
        if (_xform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<DystopiaCoriumComponent>(uid.Value))
                return;
        }

        Spawn(cfg.Corium, _map.GridTileToLocal(gridUid, grid, tile));
    }

    /// <summary>Обломок графита (горящий, если burning).</summary>
    public void SpawnDebris(EntityCoordinates coords, bool burning)
    {
        if (!_proto.TryIndex(ConfigId, out var cfg))
            return;

        var debris = Spawn(cfg.GraphiteDebris, coords);
        if (burning && TryComp<FlammableComponent>(debris, out var flammable))
            _flammable.AdjustFireStacks(debris, flammable.MaximumFireStacks, flammable, true);
    }

    /// <summary>Реактор, в который входит колонна (для отладки).</summary>
    public Reactor? ReactorOf(EntityUid column)
    {
        if (_dirty)
            Rebuild();

        return _reactorOf.GetValueOrDefault(column);
    }

    public Reactor? ReactorNear(EntityUid uid)
    {
        if (ReactorOf(uid) is { } direct)
            return direct;

        if (HasComp<DystopiaReactorConsoleComponent>(uid))
            return FindReactor(uid, Comp<DystopiaReactorConsoleComponent>(uid).ControlNode);

        foreach (var reactor in _reactors)
        {
            if (reactor.Separators.Contains(uid))
                return reactor;
        }

        return null;
    }

    public EntityUid? RodOf(EntityUid column, DystopiaReactorColumnComponent col) => Rod(column, col);

    public void DamageColumn(EntityUid uid, float amount)
    {
        _damageable.TryChangeDamage(uid, new DamageSpecifier(_proto.Index(HeatDamage), FixedPoint2.New(amount)),
            ignoreResistances: true, interruptsDoAfters: false);
    }
}
