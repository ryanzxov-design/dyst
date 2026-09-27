using Content.Server.Atmos.Components;
using Content.Server.Popups;
using Content.Shared._Dystopia.FleshCult;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.FleshCult;

/// <summary>
/// Посев Семени Плоти: способность Апостола и Проповедника. Через несколько секунд на клетке появляется росток,
/// а ещё через минуту росток становится Кистой.
/// </summary>
public sealed partial class DystopiaFleshSeedSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFleshSeederComponent, ComponentStartup>(OnSeederStartup);
        SubscribeLocalEvent<DystopiaFleshSeederComponent, ComponentShutdown>(OnSeederShutdown);
        SubscribeLocalEvent<DystopiaFleshSeederComponent, DystopiaFleshPlantSeedActionEvent>(OnPlantAction);
        SubscribeLocalEvent<DystopiaFleshSeederComponent, DystopiaFleshPlantSeedDoAfterEvent>(OnPlantDoAfter);
        SubscribeLocalEvent<DystopiaFleshSeedlingComponent, MapInitEvent>(OnSeedlingInit);
    }

    private void OnSeederStartup(Entity<DystopiaFleshSeederComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.ActionProto);
    }

    private void OnSeederShutdown(Entity<DystopiaFleshSeederComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionEntity is { } action)
            _actions.RemoveAction(ent.Owner, action);
    }

    private void OnPlantAction(Entity<DystopiaFleshSeederComponent> ent, ref DystopiaFleshPlantSeedActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanPlantAt(ent, Transform(ent).Coordinates, out var reason))
        {
            _popup.PopupEntity(reason, ent, ent, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.PlantDelay, new DystopiaFleshPlantSeedDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-flesh-seed-planting"), ent, ent);
            args.Handled = true; // перезарядка способности начинается сразу
        }
    }

    private void OnPlantDoAfter(Entity<DystopiaFleshSeederComponent> ent, ref DystopiaFleshPlantSeedDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var coords = Transform(ent).Coordinates;
        if (!CanPlantAt(ent, coords, out var reason) || !TryGetTile(coords, out var grid, out var gridComp, out var tile))
        {
            _popup.PopupEntity(reason, ent, ent, PopupType.SmallCaution);
            return;
        }

        Spawn(ent.Comp.Seedling, _map.GridTileToLocal(grid, gridComp, tile));
        _popup.PopupEntity(Loc.GetString("dystopia-flesh-seed-planted"), ent, ent);
    }

    private void OnSeedlingInit(Entity<DystopiaFleshSeedlingComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.GrowAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.GrowTime);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaFleshSeedlingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var seedling, out var xform))
        {
            if (seedling.GrowAt == TimeSpan.Zero || now < seedling.GrowAt)
                continue;

            Spawn(seedling.Cyst, xform.Coordinates);
            QueueDel(uid);
        }
    }

    /// <summary>Можно ли посадить семя: пол (не космос), без стен и без другой плоти, вне зоны стабилизаторов.</summary>
    public bool CanPlantAt(EntityUid planter, EntityCoordinates coords, out string reason)
    {
        reason = Loc.GetString("dystopia-flesh-seed-bad-place");
        if (!TryGetTile(coords, out var grid, out var gridComp, out var tile))
            return false;

        if (!_map.TryGetTileRef(grid, gridComp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (HasComp<AirtightComponent>(anchored) ||
                HasComp<DystopiaFleshCystComponent>(anchored) ||
                HasComp<DystopiaFleshSeedlingComponent>(anchored))
            {
                return false;
            }
        }

        var ev = new DystopiaFleshSeedAttemptEvent(coords);
        RaiseLocalEvent(ref ev);
        if (ev.Cancelled)
        {
            reason = ev.Reason ?? Loc.GetString("dystopia-flesh-seed-suppressed");
            return false;
        }

        return true;
    }

    private bool TryGetTile(EntityCoordinates coords, out EntityUid grid, out MapGridComponent gridComp, out Vector2i tile)
    {
        grid = default;
        gridComp = default!;
        tile = default;

        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var comp))
            return false;

        grid = gridUid;
        gridComp = comp;
        tile = _map.TileIndicesFor(gridUid, comp, coords);
        return true;
    }
}
