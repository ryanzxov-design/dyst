using Content.Server.Atmos.Components;
using Content.Server.Popups;
using Content.Shared._Dystopia.FleshCult;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.FleshCult;

/// <summary>
/// Наросты Плоти: кисты растят наросты (до лимита), потом одна крайняя клетка становится новой кистой.
/// Вокруг кисты — ядовитая зона. Трупы на наростах перевариваются и уходят биомассой в Колыбель.
/// Наросты без своей кисты отмирают.
/// </summary>
public sealed partial class DystopiaFleshGrowthSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private DystopiaFleshCradleSystem _cradle = default!;

    private static readonly Vector2i[] Neighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFleshCystComponent, MapInitEvent>(OnCystInit);
        SubscribeLocalEvent<DystopiaFleshGrowthComponent, ComponentShutdown>(OnGrowthShutdown);
    }

    private void OnCystInit(Entity<DystopiaFleshCystComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpread = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SpreadInterval);
        ent.Comp.NextZoneTick = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnGrowthShutdown(Entity<DystopiaFleshGrowthComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Cyst is { } cyst && TryComp<DystopiaFleshCystComponent>(cyst, out var cystComp))
            cystComp.Growths.Remove(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        // --- Кисты: рост и ядовитая зона ---
        var cysts = EntityQueryEnumerator<DystopiaFleshCystComponent, TransformComponent>();
        while (cysts.MoveNext(out var uid, out var cyst, out var xform))
        {
            if (now >= cyst.NextSpread)
            {
                var speed = new DystopiaFleshGrowthSpeedEvent(uid);
                RaiseLocalEvent(ref speed);
                var interval = cyst.SpreadInterval * MathF.Max(1f, speed.Multiplier) * _random.NextFloat(0.8f, 1.2f);
                cyst.NextSpread = now + TimeSpan.FromSeconds(interval);
                Spread((uid, cyst, xform));
            }

            if (now >= cyst.NextZoneTick)
            {
                cyst.NextZoneTick = now + TimeSpan.FromSeconds(1);
                PoisonZone((uid, cyst, xform));
            }
        }

        // --- Наросты: отмирание без кисты и переваривание трупов ---
        var growths = EntityQueryEnumerator<DystopiaFleshGrowthComponent, TransformComponent>();
        while (growths.MoveNext(out var uid, out var growth, out var xform))
        {
            if (now < growth.NextCheck)
                continue;

            growth.NextCheck = now + TimeSpan.FromSeconds(2);

            if (growth.Cyst is not { } cystUid || !Exists(cystUid) || TerminatingOrDeleted(cystUid))
            {
                growth.WitherAt ??= now + TimeSpan.FromSeconds(_random.NextFloat(15f, 45f));
                if (now >= growth.WitherAt)
                {
                    QueueDel(uid);
                    continue;
                }
            }

            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, 0.45f))
            {
                if (!_mobState.IsDead(mob) || HasComp<DystopiaFleshDissolvingComponent>(mob))
                    continue;

                var dissolving = AddComp<DystopiaFleshDissolvingComponent>(mob);
                dissolving.Growth = uid;
                dissolving.EndTime = now + TimeSpan.FromSeconds(growth.DissolveTime);
                _popup.PopupEntity(Loc.GetString("dystopia-flesh-growth-dissolve-start", ("body", Name(mob))), mob, PopupType.MediumCaution);
            }
        }

        // --- Переваривание ---
        var bodies = EntityQueryEnumerator<DystopiaFleshDissolvingComponent, TransformComponent>();
        while (bodies.MoveNext(out var uid, out var dissolving, out var xform))
        {
            // Тело унесли с нароста или нарост сожгли — переваривание прерывается
            if (!Exists(dissolving.Growth) || TerminatingOrDeleted(dissolving.Growth) || !_mobState.IsDead(uid) ||
                !_transform.InRange(xform.Coordinates, Transform(dissolving.Growth).Coordinates, 0.8f))
            {
                RemCompDeferred<DystopiaFleshDissolvingComponent>(uid);
                continue;
            }

            if (now < dissolving.EndTime)
                continue;

            _cradle.DropEverything(uid);
            if (_cradle.TryGetCradle(out var cradle))
            {
                var amount = HasComp<HumanoidProfileComponent>(uid) ? cradle.Comp.HumanoidBiomass : cradle.Comp.CreatureBiomass;
                _cradle.AddBiomass(cradle, amount);
            }

            _popup.PopupCoordinates(Loc.GetString("dystopia-flesh-growth-dissolved"), xform.Coordinates, PopupType.MediumCaution);
            QueueDel(uid);
        }
    }

    private void Spread(Entity<DystopiaFleshCystComponent, TransformComponent> cyst)
    {
        if (_transform.GetGrid(cyst.Comp2.Coordinates) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        // Живые наросты этой кисты
        cyst.Comp1.Growths.RemoveWhere(g => !Exists(g) || TerminatingOrDeleted(g));

        if (cyst.Comp1.Growths.Count < cyst.Comp1.MaxGrowths)
        {
            var candidates = new List<Vector2i>();
            var sources = new List<Vector2i> { _map.TileIndicesFor(gridUid, grid, cyst.Comp2.Coordinates) };
            foreach (var g in cyst.Comp1.Growths)
            {
                sources.Add(_map.TileIndicesFor(gridUid, grid, Transform(g).Coordinates));
            }

            foreach (var source in sources)
            {
                foreach (var dir in Neighbors)
                {
                    var tile = source + dir;
                    if (IsFreeTile(gridUid, grid, tile) && !candidates.Contains(tile))
                        candidates.Add(tile);
                }
            }

            if (candidates.Count == 0)
                return;

            var chosen = _random.Pick(candidates);
            var growth = Spawn(cyst.Comp1.Growth, _map.GridTileToLocal(gridUid, grid, chosen));
            _transform.SetLocalRotation(growth, Angle.FromDegrees(90 * _random.Next(4)));
            EnsureComp<DystopiaFleshGrowthComponent>(growth).Cyst = cyst.Owner;
            cyst.Comp1.Growths.Add(growth);
            return;
        }

        // Лимит достигнут: одна из крайних клеток становится новой кистой (один раз на кисту)
        if (cyst.Comp1.ChildSpawned)
            return;

        var edges = new List<EntityUid>();
        foreach (var g in cyst.Comp1.Growths)
        {
            var tile = _map.TileIndicesFor(gridUid, grid, Transform(g).Coordinates);
            foreach (var dir in Neighbors)
            {
                if (IsFreeTile(gridUid, grid, tile + dir))
                {
                    edges.Add(g);
                    break;
                }
            }
        }

        if (edges.Count == 0)
            return;

        var edge = _random.Pick(edges);
        var coords = Transform(edge).Coordinates;

        // Стабилизаторы не дают появиться кисте в своей зоне (этап К3)
        var attempt = new DystopiaFleshSeedAttemptEvent(coords);
        RaiseLocalEvent(ref attempt);
        if (attempt.Cancelled)
            return;

        cyst.Comp1.ChildSpawned = true;
        cyst.Comp1.Growths.Remove(edge);
        QueueDel(edge);
        Spawn(cyst.Comp1.Cyst, coords);
    }

    /// <summary>Клетка годится для нароста: пол есть, нет стен/дверей и другой плоти.</summary>
    private bool IsFreeTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<AirtightComponent>(anchored) ||
                HasComp<DystopiaFleshGrowthComponent>(anchored) ||
                HasComp<DystopiaFleshCystComponent>(anchored) ||
                HasComp<DystopiaFleshSeedlingComponent>(anchored))
            {
                return false;
            }
        }

        return true;
    }

    private void PoisonZone(Entity<DystopiaFleshCystComponent, TransformComponent> cyst)
    {
        if (cyst.Comp1.ZoneDamage.Empty)
            return;

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(cyst.Comp2.Coordinates, cyst.Comp1.ZoneRadius))
        {
            if (_mobState.IsDead(mob) || HasComp<DystopiaFleshCultistComponent>(mob) || IsProtected(mob))
                continue;

            _damageable.TryChangeDamage(mob.Owner, cyst.Comp1.ZoneDamage, interruptsDoAfters: false, origin: cyst.Owner);
            if (_random.Prob(0.15f))
                _popup.PopupEntity(Loc.GetString("dystopia-flesh-zone-choke"), mob, mob, PopupType.SmallCaution);
        }
    }

    /// <summary>Защищён ли от спор: противогаз (или капюшон с фильтром) на лице и не приспущен.</summary>
    private bool IsProtected(EntityUid mob)
    {
        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(mob, slot, out var item) || !HasComp<BreathToolComponent>(item))
                continue;

            if (TryComp<MaskComponent>(item, out var mask) && mask.IsToggled)
                continue;

            return true;
        }

        return false;
    }
}
