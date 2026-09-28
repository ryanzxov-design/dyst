using Content.Server.Atmos.Components;
using Content.Server.Popups;
using Content.Shared._Dystopia.FleshCult;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
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
    [Dependency] private AppearanceSystem _appearance = default!;

    private static readonly Vector2i[] Neighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private readonly List<Entity<DystopiaFleshCystComponent, TransformComponent>> _cystBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFleshCystComponent, MapInitEvent>(OnCystInit);
        SubscribeLocalEvent<DystopiaFleshGrowthComponent, ComponentShutdown>(OnGrowthShutdown);
        SubscribeLocalEvent<DystopiaFleshGrowthComponent, MapInitEvent>(OnGrowthInit);
        SubscribeLocalEvent<DystopiaFleshGrowthComponent, DamageModifyEvent>(OnGrowthDamageModify);
    }

    private void OnCystInit(Entity<DystopiaFleshCystComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpread = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SpreadInterval);
        ent.Comp.NextZoneTick = _timing.CurTime + TimeSpan.FromSeconds(1);

        // Под самой кистой тоже нарост — чтобы вокруг неё не было чистого пятна.
        var xform = Transform(ent);
        if (_transform.GetGrid(xform.Coordinates) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (TryComp<DystopiaFleshGrowthComponent>(anchored, out var existing))
            {
                // Нарост уже есть (киста выросла на месте нароста) — забираем его себе
                existing.Cyst = ent.Owner;
                existing.WitherAt = null;
                ent.Comp.Growths.Add(anchored);
                return;
            }
        }

        SpawnGrowth(ent, gridUid, grid, tile);
    }

    private EntityUid SpawnGrowth(Entity<DystopiaFleshCystComponent> cyst, EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var growth = Spawn(cyst.Comp.Growth, _map.GridTileToLocal(gridUid, grid, tile));
        EnsureComp<DystopiaFleshGrowthComponent>(growth).Cyst = cyst.Owner;
        cyst.Comp.Growths.Add(growth);
        return growth;
    }

    private void OnGrowthInit(Entity<DystopiaFleshGrowthComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Variant = _random.Next(1, 4);
        ent.Comp.Stage = 1;
        ent.Comp.NextStageAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.StageTime);
        if (TryComp<DamageContactsComponent>(ent, out var contacts))
            ent.Comp.BaseContactDamage = new DamageSpecifier(contacts.Damage);

        UpdateGrowthVisuals(ent);
    }

    /// <summary>
    /// Прочность стадий: порог разрушения в прототипе — прочность 1 стадии, а на старших стадиях
    /// входящий урон делится на множитель стадии (1.4 за стадию) — то же самое, что больше прочности.
    /// </summary>
    private void OnGrowthDamageModify(Entity<DystopiaFleshGrowthComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Stage <= 1)
            return;

        var factor = MathF.Pow(ent.Comp.StageHealthMultiplier, ent.Comp.Stage - 1);
        args.Damage = args.Damage * (1f / factor);
    }

    private void UpdateGrowthVisuals(Entity<DystopiaFleshGrowthComponent> ent)
    {
        _appearance.SetData(ent, DystopiaFleshGrowthVisuals.State, $"kudzu_{ent.Comp.Stage}{ent.Comp.Variant}");
    }

    private void AdvanceStage(Entity<DystopiaFleshGrowthComponent> ent)
    {
        ent.Comp.Stage++;
        ent.Comp.NextStageAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.StageTime);
        UpdateGrowthVisuals(ent);

        // Урон каждой стадии на 30% больше предыдущей
        if (ent.Comp.BaseContactDamage != null && TryComp<DamageContactsComponent>(ent, out var contacts))
        {
            contacts.Damage = ent.Comp.BaseContactDamage * MathF.Pow(ent.Comp.StageDamageMultiplier, ent.Comp.Stage - 1);
            Dirty(ent, contacts);
        }
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
        // Сначала собираем список: рост может породить новую кисту, а менять набор кист во время перебора нельзя.
        _cystBuffer.Clear();
        var cysts = EntityQueryEnumerator<DystopiaFleshCystComponent, TransformComponent>();
        while (cysts.MoveNext(out var uid, out var cyst, out var xform))
        {
            _cystBuffer.Add((uid, cyst, xform));
        }

        foreach (var ent in _cystBuffer)
        {
            if (TerminatingOrDeleted(ent.Owner))
                continue;

            var cyst = ent.Comp1;
            if (now >= cyst.NextSpread)
            {
                var speed = new DystopiaFleshGrowthSpeedEvent(ent.Owner);
                RaiseLocalEvent(ref speed);
                var interval = cyst.SpreadInterval * MathF.Max(1f, speed.Multiplier) * _random.NextFloat(0.8f, 1.2f);
                cyst.NextSpread = now + TimeSpan.FromSeconds(interval);
                Spread(ent);
            }

            if (now >= cyst.NextZoneTick)
            {
                cyst.NextZoneTick = now + TimeSpan.FromSeconds(1);
                PoisonZone(ent);
            }
        }

        // --- Наросты: отмирание без кисты и переваривание трупов ---
        var growths = EntityQueryEnumerator<DystopiaFleshGrowthComponent, TransformComponent>();
        while (growths.MoveNext(out var uid, out var growth, out var xform))
        {
            if (now < growth.NextCheck)
                continue;

            growth.NextCheck = now + TimeSpan.FromSeconds(2);

            if (growth.Stage < growth.MaxStage && now >= growth.NextStageAt)
                AdvanceStage((uid, growth));

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
                // Плоть не переваривает своих: тела культистов остаются
                if (!_mobState.IsDead(mob) || HasComp<DystopiaFleshDissolvingComponent>(mob) ||
                    HasComp<DystopiaFleshCultistComponent>(mob))
                {
                    continue;
                }

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

            SpawnGrowth((cyst.Owner, cyst.Comp1), gridUid, grid, _random.Pick(candidates));
            return;
        }

        // Лимит достигнут: самая дальняя от кисты клетка нароста становится новой кистой (один раз на кисту).
        if (cyst.Comp1.ChildSpawned)
            return;

        var parentTile = _map.TileIndicesFor(gridUid, grid, cyst.Comp2.Coordinates);
        var farCandidates = new List<(EntityUid Growth, int Distance)>();
        foreach (var g in cyst.Comp1.Growths)
        {
            var tile = _map.TileIndicesFor(gridUid, grid, Transform(g).Coordinates);
            var delta = tile - parentTile;
            var distance = delta.X * delta.X + delta.Y * delta.Y;
            if (distance > 0)
                farCandidates.Add((g, distance));
        }

        if (farCandidates.Count == 0)
            return;

        // Самые дальние — первыми; среди равных по дальности порядок случайный
        _random.Shuffle(farCandidates);
        farCandidates.Sort((a, b) => b.Distance.CompareTo(a.Distance));

        foreach (var (growth, _) in farCandidates)
        {
            var coords = Transform(growth).Coordinates;

            // Стабилизаторы не дают появиться кисте в своей зоне (этап К3) — тогда пробуем следующую по дальности
            var attempt = new DystopiaFleshSeedAttemptEvent(coords);
            RaiseLocalEvent(ref attempt);
            if (attempt.Cancelled)
                continue;

            // Нарост на месте новой кисты не удаляем: новая киста заберёт его себе (он окажется под ней).
            cyst.Comp1.ChildSpawned = true;
            cyst.Comp1.Growths.Remove(growth);
            Spawn(cyst.Comp1.Cyst, coords);
            return;
        }
    }

    /// <summary>Клетка годится для нароста: пол есть, нет стен и другой плоти. Незаваренные двери пропускают.</summary>
    private bool IsFreeTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            // Дверь пропускает плоть под собой, если её не заварили
            if (TryComp<DoorComponent>(anchored, out var door))
            {
                if (door.State == DoorState.Welded)
                    return false;

                continue;
            }

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
