using Content.Server.Atmos.EntitySystems;
using Content.Shared._Dystopia.FleshCult;
using Content.Shared.Damage.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.FleshCult;

/// <summary>
/// Огонь по плоти. Ванильное горение не срабатывает на наростах (струя пролетает над ними, а прогрев от пожара
/// слишком медленный), поэтому у плоти своё горение:
///  - струя огнемёта поджигает плоть на каждой клетке, над которой пролетает;
///  - пожар на клетке поджигает плоть на ней;
///  - горящая плоть получает урон каждую секунду (на соседей огонь сам не переходит);
///  - к концу полёта струя шире и поджигает соседние клетки.
/// </summary>
public sealed partial class DystopiaFleshFireSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly Vector2i[] Neighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private readonly List<EntityUid> _toIgnite = new();
    private readonly List<EntityUid> _buffer = new();
    private TimeSpan _nextHotspotCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFleshBurningComponent, ComponentShutdown>(OnBurningShutdown);
        SubscribeLocalEvent<DystopiaFleshIgniterComponent, MapInitEvent>(OnIgniterInit);
    }

    private void OnIgniterInit(Entity<DystopiaFleshIgniterComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.SpawnTime = _timing.CurTime;
    }

    private void OnBurningShutdown(Entity<DystopiaFleshBurningComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Effect is { } effect)
            QueueDel(effect);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        _toIgnite.Clear();

        // 1. Струя огнемёта: плоть на клетке под каждой частью струи
        var igniters = EntityQueryEnumerator<DystopiaFleshIgniterComponent, TransformComponent>();
        while (igniters.MoveNext(out _, out var igniter, out var xform))
        {
            CollectFleshOnTile(xform, Vector2i.Zero);

            // К концу дальности облако огня шире
            if ((now - igniter.SpawnTime).TotalSeconds >= igniter.WideAfter)
            {
                foreach (var dir in Neighbors)
                {
                    CollectFleshOnTile(xform, dir);
                }
            }
        }

        // 2. Пожар на клетке (раз в секунду)
        if (now >= _nextHotspotCheck)
        {
            _nextHotspotCheck = now + TimeSpan.FromSeconds(1);
            var burnables = EntityQueryEnumerator<DystopiaFleshBurnableComponent, TransformComponent>();
            while (burnables.MoveNext(out var uid, out _, out var xform))
            {
                if (HasComp<DystopiaFleshBurningComponent>(uid) || xform.GridUid is not { } grid ||
                    !TryComp<MapGridComponent>(grid, out var gridComp))
                {
                    continue;
                }

                var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
                if (_atmosphere.IsHotspotActive(grid, tile))
                    _toIgnite.Add(uid);
            }
        }

        // 3. Горение: урон каждую секунду
        _buffer.Clear();
        var burning = EntityQueryEnumerator<DystopiaFleshBurningComponent>();
        while (burning.MoveNext(out var uid, out _))
        {
            _buffer.Add(uid);
        }

        foreach (var uid in _buffer)
        {
            if (TerminatingOrDeleted(uid) ||
                !TryComp<DystopiaFleshBurningComponent>(uid, out var fire) ||
                !TryComp<DystopiaFleshBurnableComponent>(uid, out var burnable))
            {
                continue;
            }

            if (now >= fire.EndTime)
            {
                RemComp<DystopiaFleshBurningComponent>(uid);
                continue;
            }

            if (now < fire.NextTick)
                continue;

            fire.NextTick = now + TimeSpan.FromSeconds(1);
            _damageable.TryChangeDamage(uid, burnable.BurnDamage, ignoreResistances: false, interruptsDoAfters: false);
        }

        foreach (var uid in _toIgnite)
        {
            Ignite(uid);
        }
    }

    private void CollectFleshOnTile(TransformComponent xform, Vector2i offset)
    {
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates) + offset;
        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (HasComp<DystopiaFleshBurnableComponent>(anchored))
                _toIgnite.Add(anchored);
        }
    }

    /// <summary>Поджечь плоть (или продлить горение).</summary>
    public void Ignite(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !TryComp<DystopiaFleshBurnableComponent>(uid, out var burnable))
            return;

        var now = _timing.CurTime;
        if (TryComp<DystopiaFleshBurningComponent>(uid, out var existing))
        {
            existing.EndTime = now + TimeSpan.FromSeconds(burnable.BurnTime);
            return;
        }

        var fire = AddComp<DystopiaFleshBurningComponent>(uid);
        fire.EndTime = now + TimeSpan.FromSeconds(burnable.BurnTime);
        fire.NextTick = now;
        fire.Effect = Spawn(burnable.FireEffect, Transform(uid).Coordinates);
    }
}
