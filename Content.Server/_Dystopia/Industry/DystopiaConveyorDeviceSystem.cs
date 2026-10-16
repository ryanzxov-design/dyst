// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: конвейерные устройства. Извлекатель: из хранилища позади — на клетку впереди (на конвейер).
// Вставщик: с клетки позади (с конвейера) — в хранилище впереди. Сортировщик: с клетки позади — в зелёный выход
// (налево), синий (направо) или жёлтый (вперёд, всё остальное). Хранилища: машины переработки, тигли и горны
// (их системы отвечают на события), ящики и шкафы (закрытые — можно, запертые или заваренные — нет), латы.
// Настройка — меню (клик по устройству): поиск предметов, списки, режим; плюс ПКМ с предметом в руке.

using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaConveyorDeviceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private MaterialStorageSystem _materialStorage = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaConveyorDeviceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaConveyorDeviceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaConveyorDeviceComponent, BoundUIOpenedEvent>(OnUiOpened);

        Subs.BuiEvents<DystopiaConveyorDeviceComponent>(DystopiaConveyorUiKey.Key, subs =>
        {
            subs.Event<DystopiaConveyorSetWhitelistMessage>(OnSetWhitelist);
            subs.Event<DystopiaConveyorAddMessage>(OnAdd);
            subs.Event<DystopiaConveyorRemoveMessage>(OnRemove);
            subs.Event<DystopiaConveyorAddHeldMessage>(OnAddHeld);
            subs.Event<DystopiaConveyorClearMessage>(OnClear);
        });
    }

    // ------------------------------------------------------------------ меню

    private HashSet<string> GetList(DystopiaConveyorDeviceComponent comp, DystopiaConveyorList list)
    {
        return list switch
        {
            DystopiaConveyorList.Green => comp.SortGreen,
            DystopiaConveyorList.Blue => comp.SortBlue,
            _ => comp.Filter,
        };
    }

    /// <summary>Добавить ключ в список. В сортировщике предмет идёт только в один выход.</summary>
    private void AddKey(DystopiaConveyorDeviceComponent comp, DystopiaConveyorList list, string key)
    {
        GetList(comp, list).Add(key);
        if (list == DystopiaConveyorList.Green)
            comp.SortBlue.Remove(key);
        else if (list == DystopiaConveyorList.Blue)
            comp.SortGreen.Remove(key);
    }

    private void UpdateUi(Entity<DystopiaConveyorDeviceComponent> ent)
    {
        static List<string> Sorted(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        _ui.SetUiState(ent.Owner, DystopiaConveyorUiKey.Key, new DystopiaConveyorUiState(ent.Comp.Mode,
            ent.Comp.Whitelist, Sorted(ent.Comp.Filter), Sorted(ent.Comp.SortGreen), Sorted(ent.Comp.SortBlue)));
    }

    private void OnUiOpened(Entity<DystopiaConveyorDeviceComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSetWhitelist(Entity<DystopiaConveyorDeviceComponent> ent, ref DystopiaConveyorSetWhitelistMessage args)
    {
        ent.Comp.Whitelist = args.Whitelist;
        UpdateUi(ent);
    }

    private void OnAdd(Entity<DystopiaConveyorDeviceComponent> ent, ref DystopiaConveyorAddMessage args)
    {
        // Ключ должен быть настоящим прототипом или стопкой — клиенту на слово не верим.
        var valid = args.Key.StartsWith(DystopiaConveyorKeys.StackPrefix, StringComparison.Ordinal)
            ? _proto.HasIndex<StackPrototype>(args.Key[DystopiaConveyorKeys.StackPrefix.Length..])
            : _proto.HasIndex<EntityPrototype>(args.Key);
        if (!valid)
            return;

        AddKey(ent.Comp, args.List, args.Key);
        UpdateUi(ent);
    }

    private void OnRemove(Entity<DystopiaConveyorDeviceComponent> ent, ref DystopiaConveyorRemoveMessage args)
    {
        GetList(ent.Comp, args.List).Remove(args.Key);
        UpdateUi(ent);
    }

    private void OnAddHeld(Entity<DystopiaConveyorDeviceComponent> ent, ref DystopiaConveyorAddHeldMessage args)
    {
        if (_hands.GetActiveItem(args.Actor) is not { } held || FilterKey(held) is not { } key)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-conveyor-ui-hand-empty"), ent, args.Actor);
            return;
        }

        AddKey(ent.Comp, args.List, key);
        UpdateUi(ent);
    }

    private void OnClear(Entity<DystopiaConveyorDeviceComponent> ent, ref DystopiaConveyorClearMessage args)
    {
        GetList(ent.Comp, args.List).Clear();
        UpdateUi(ent);
    }

    // ------------------------------------------------------------------ геометрия

    private bool TryGetTile(EntityUid uid, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        grid = default;
        tile = default;
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        grid = (gridUid, gridComp);
        tile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        return true;
    }

    /// <summary>Куда смотрит устройство (выход). При повороте 0 — вниз.</summary>
    private Direction Front(EntityUid uid)
    {
        return Transform(uid).LocalRotation.GetCardinalDir();
    }

    // Направления в enum идут против часовой: +2 — поворот налево на 90°, +6 — направо.
    private static Direction Left(Direction dir) => (Direction) (((int) dir + 2) & 7);
    private static Direction Right(Direction dir) => (Direction) (((int) dir + 6) & 7);

    /// <summary>Свободные предметы на клетке (не в контейнерах, не прикручены).</summary>
    private List<EntityUid> ItemsOn(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var result = new List<EntityUid>();
        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(grid.Owner, tile, gridComp: grid.Comp))
        {
            if (HasComp<ItemComponent>(uid) && !Transform(uid).Anchored && !_container.IsEntityInContainer(uid))
                result.Add(uid);
        }

        result.Sort();
        return result;
    }

    /// <summary>
    /// Входящие предметы: на клетке позади и на клетке самого устройства (конвейер довозит предметы через край,
    /// они останавливаются на устройстве).
    /// </summary>
    private List<EntityUid> Incoming(Entity<MapGridComponent> grid, Vector2i own, Vector2i back)
    {
        var result = ItemsOn(grid, own);
        result.AddRange(ItemsOn(grid, back));
        return result;
    }

    /// <summary>Можно ли положить предмет на клетку: не стена/машина и не завалена.</summary>
    private bool CanDropOn(Entity<DystopiaConveyorDeviceComponent> ent, Entity<MapGridComponent> grid, Vector2i tile)
    {
        return !_turf.IsTileBlocked(grid.Owner, tile, CollisionGroup.Impassable, grid.Comp) &&
               ItemsOn(grid, tile).Count < ent.Comp.MaxItemsOnTile;
    }

    private void DropOn(EntityUid item, Entity<MapGridComponent> grid, Vector2i tile)
    {
        var coords = _map.GridTileToLocal(grid.Owner, grid.Comp, tile);
        if (_container.TryGetContainingContainer(item, out var container))
            _container.Remove(item, container, destination: coords);
        else
            _xform.SetCoordinates(item, coords);
    }

    // ------------------------------------------------------------------ фильтр

    /// <summary>Ключ предмета для фильтра: тип стопки (все стопки одного вида) или прототип.</summary>
    public string? FilterKey(EntityUid item)
    {
        if (TryComp<StackComponent>(item, out var stack))
            return DystopiaConveyorKeys.StackPrefix + stack.StackTypeId.Id;

        return MetaData(item).EntityPrototype?.ID;
    }

    private string KeyName(string key)
    {
        return DystopiaConveyorKeys.Name(_proto, key);
    }

    private bool PassesFilter(DystopiaConveyorDeviceComponent comp, EntityUid item)
    {
        var listed = FilterKey(item) is { } key && comp.Filter.Contains(key);
        return comp.Whitelist ? listed : !listed;
    }

    // ------------------------------------------------------------------ хранилища

    private bool IsLocked(EntityUid uid)
    {
        return TryComp<LockComponent>(uid, out var lockComp) && lockComp.Locked ||
               TryComp<WeldableComponent>(uid, out var weld) && weld.IsWelded;
    }

    /// <summary>
    /// Хранилища на клетке: машины переработки, горны, дуговая печь, литейные столы, ящики, шкафы, латы
    /// (не предметы на полу). Большие конструкции (печь 3×3) находятся с любой клетки корпуса.
    /// </summary>
    private List<EntityUid> StoragesOn(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var result = new List<EntityUid>();
        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(grid.Owner, tile, gridComp: grid.Comp))
        {
            if (HasComp<DystopiaProcessorComponent>(uid) ||
                HasComp<DystopiaForgeComponent>(uid) ||
                HasComp<DystopiaArcFurnaceComponent>(uid) ||
                HasComp<DystopiaMouldComponent>(uid) && !HasComp<ItemComponent>(uid) ||
                HasComp<EntityStorageComponent>(uid) ||
                HasComp<MaterialStorageComponent>(uid) ||
                HasComp<StorageComponent>(uid) && !HasComp<ItemComponent>(uid))
            {
                result.Add(uid);
            }
        }

        result.Sort();
        return result;
    }

    private EntityUid? TryExtract(EntityUid storage, Predicate<EntityUid> accept)
    {
        if (IsLocked(storage))
            return null;

        var ev = new DystopiaConveyorExtractEvent(accept);
        RaiseLocalEvent(storage, ref ev);
        if (ev.Handled)
            return ev.Item;

        if (TryComp<EntityStorageComponent>(storage, out var entityStorage))
        {
            foreach (var item in entityStorage.Contents.ContainedEntities)
            {
                if (!HasComp<ItemComponent>(item) || !accept(item))
                    continue;

                return _entityStorage.Remove(item, storage, entityStorage) ? item : null;
            }

            return null;
        }

        if (TryComp<StorageComponent>(storage, out var storageComp))
        {
            foreach (var item in storageComp.Container.ContainedEntities)
            {
                if (!accept(item))
                    continue;

                return _container.Remove(item, storageComp.Container) ? item : null;
            }
        }

        return null;
    }

    private bool TryInsert(EntityUid device, EntityUid storage, EntityUid item)
    {
        if (IsLocked(storage))
            return false;

        var ev = new DystopiaConveyorInsertEvent(item);
        RaiseLocalEvent(storage, ref ev);
        if (ev.Handled)
            return ev.Inserted;

        if (TryComp<EntityStorageComponent>(storage, out var entityStorage))
        {
            return _entityStorage.CanInsert(item, storage, entityStorage) &&
                   _entityStorage.Insert(item, storage, entityStorage);
        }

        if (HasComp<MaterialStorageComponent>(storage))
            return _materialStorage.TryInsertMaterialEntity(device, item, storage);

        if (TryComp<StorageComponent>(storage, out var storageComp))
            return _storage.Insert(storage, item, out _, storageComp: storageComp, playSound: false);

        return false;
    }

    // ------------------------------------------------------------------ работа

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaConveyorDeviceComponent>();
        while (query.MoveNext(out var uid, out var device))
        {
            if (now < device.NextAction)
                continue;

            device.NextAction = now + TimeSpan.FromSeconds(device.Interval);
            Entity<DystopiaConveyorDeviceComponent> ent = (uid, device);

            var working = (!device.RequireAnchored || Transform(uid).Anchored) && _power.IsPowered(uid) &&
                          TryGetTile(uid, out var grid, out var tile) && Act(ent, grid, tile);
            _appearance.SetData(uid, DystopiaConveyorDeviceVisuals.Working, working);
        }
    }

    /// <summary>Один перенос. Возвращает, перенесено ли что-нибудь.</summary>
    private bool Act(Entity<DystopiaConveyorDeviceComponent> ent, Entity<MapGridComponent> grid, Vector2i tile)
    {
        var front = Front(ent);
        var backTile = tile.Offset(front.GetOpposite());
        var frontTile = tile.Offset(front);

        switch (ent.Comp.Mode)
        {
            case DystopiaConveyorDeviceMode.Extract:
            {
                if (!CanDropOn(ent, grid, frontTile))
                    return false;

                foreach (var storage in StoragesOn(grid, backTile))
                {
                    if (TryExtract(storage, item => PassesFilter(ent.Comp, item)) is { } item)
                    {
                        DropOn(item, grid, frontTile);
                        return true;
                    }
                }

                return false;
            }

            case DystopiaConveyorDeviceMode.Insert:
            {
                var storages = StoragesOn(grid, frontTile);
                if (storages.Count == 0)
                    return false;

                foreach (var item in Incoming(grid, tile, backTile))
                {
                    if (!PassesFilter(ent.Comp, item))
                        continue;

                    foreach (var storage in storages)
                    {
                        if (TryInsert(ent, storage, item))
                            return true;
                    }
                }

                return false;
            }

            case DystopiaConveyorDeviceMode.Sort:
            {
                foreach (var item in Incoming(grid, tile, backTile))
                {
                    var key = FilterKey(item);
                    var dir = key != null && ent.Comp.SortGreen.Contains(key) ? Left(front)
                        : key != null && ent.Comp.SortBlue.Contains(key) ? Right(front)
                        : front;

                    var target = tile.Offset(dir);
                    if (!CanDropOn(ent, grid, target))
                        continue; // этот выход занят — следующий предмет может уйти в другой

                    DropOn(item, grid, target);
                    return true;
                }

                return false;
            }
        }

        return false;
    }

    // ------------------------------------------------------------------ настройка

    private void OnGetVerbs(Entity<DystopiaConveyorDeviceComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        var key = args.Using is { } used ? FilterKey(used) : null;

        if (ent.Comp.Mode == DystopiaConveyorDeviceMode.Sort)
        {
            if (key != null)
            {
                AddToggleVerb(ref args, ent, ent.Comp.SortGreen, ent.Comp.SortBlue, key, user,
                    "dystopia-conveyor-sort-green-add", "dystopia-conveyor-sort-green-remove");
                AddToggleVerb(ref args, ent, ent.Comp.SortBlue, ent.Comp.SortGreen, key, user,
                    "dystopia-conveyor-sort-blue-add", "dystopia-conveyor-sort-blue-remove");
            }

            if (ent.Comp.SortGreen.Count > 0 || ent.Comp.SortBlue.Count > 0)
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString("dystopia-conveyor-clear-verb"),
                    Act = () =>
                    {
                        ent.Comp.SortGreen.Clear();
                        ent.Comp.SortBlue.Clear();
                        UpdateUi(ent);
                        _popup.PopupEntity(Loc.GetString("dystopia-conveyor-cleared"), ent, user);
                    },
                });
            }

            return;
        }

        if (key != null)
            AddToggleVerb(ref args, ent, ent.Comp.Filter, null, key, user,
                "dystopia-conveyor-filter-add", "dystopia-conveyor-filter-remove");

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Whitelist ? "dystopia-conveyor-to-blacklist" : "dystopia-conveyor-to-whitelist"),
            Act = () =>
            {
                ent.Comp.Whitelist = !ent.Comp.Whitelist;
                UpdateUi(ent);
                _popup.PopupEntity(Loc.GetString(ent.Comp.Whitelist
                    ? "dystopia-conveyor-mode-whitelist"
                    : "dystopia-conveyor-mode-blacklist"), ent, user);
            },
        });

        if (ent.Comp.Filter.Count > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-conveyor-clear-verb"),
                Act = () =>
                {
                    ent.Comp.Filter.Clear();
                    UpdateUi(ent);
                    _popup.PopupEntity(Loc.GetString("dystopia-conveyor-cleared"), ent, user);
                },
            });
        }
    }

    private void AddToggleVerb(ref GetVerbsEvent<AlternativeVerb> args, Entity<DystopiaConveyorDeviceComponent> ent,
        HashSet<string> list, HashSet<string>? other, string key, EntityUid user, string addLoc, string removeLoc)
    {
        var name = KeyName(key);
        var listed = list.Contains(key);
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(listed ? removeLoc : addLoc, ("item", name)),
            Act = () =>
            {
                if (listed)
                {
                    list.Remove(key);
                }
                else
                {
                    list.Add(key);
                    other?.Remove(key); // в сортировщике предмет идёт только в один выход
                }

                UpdateUi(ent);
                _popup.PopupEntity(Loc.GetString(listed ? removeLoc : addLoc, ("item", name)), ent, user);
            },
        });
    }

    private string ListNames(HashSet<string> keys)
    {
        if (keys.Count == 0)
            return Loc.GetString("dystopia-conveyor-list-empty");

        var names = new List<string>();
        foreach (var key in keys)
        {
            names.Add(KeyName(key));
        }

        names.Sort();
        return string.Join(", ", names);
    }

    private void OnExamined(Entity<DystopiaConveyorDeviceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        switch (ent.Comp.Mode)
        {
            case DystopiaConveyorDeviceMode.Extract:
            case DystopiaConveyorDeviceMode.Insert:
                args.PushMarkup(Loc.GetString(ent.Comp.Mode == DystopiaConveyorDeviceMode.Extract
                    ? "dystopia-conveyor-examine-extract"
                    : "dystopia-conveyor-examine-insert"));
                args.PushMarkup(Loc.GetString(ent.Comp.Whitelist
                        ? "dystopia-conveyor-examine-whitelist"
                        : "dystopia-conveyor-examine-blacklist",
                    ("items", ListNames(ent.Comp.Filter))));
                break;

            case DystopiaConveyorDeviceMode.Sort:
                args.PushMarkup(Loc.GetString("dystopia-conveyor-examine-sort"));
                args.PushMarkup(Loc.GetString("dystopia-conveyor-examine-green", ("items", ListNames(ent.Comp.SortGreen))));
                args.PushMarkup(Loc.GetString("dystopia-conveyor-examine-blue", ("items", ListNames(ent.Comp.SortBlue))));
                break;
        }

        if (ent.Comp.RequireAnchored && !Transform(ent).Anchored)
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-loose"));
        else if (!_power.IsPowered(ent.Owner))
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-unpowered"));
    }
}
