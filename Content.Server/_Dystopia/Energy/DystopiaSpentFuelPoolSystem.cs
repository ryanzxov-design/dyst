// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: бассейн выдержки. Ячейки 1×1; соседние по стороне — один бассейн, вода между ними выравнивается.
// В ячейке один ТВЭЛ: ставят рукой (холодный) или захватом, вынимают так же. Вода гасит излучение ТВЭЛа
// (излучение ТВЭЛа гасит DystopiaReactorSystem по уровню воды) и испаряется от остаточного тепла. Мало воды — ТВЭЛ на воздухе
// (сыплет пыль, см. DystopiaReactorSystem.RodsTick), сухая ячейка с горячим ТВЭЛом получает урон.
// Такт вызывает DystopiaReactorSystem (общее ускорение времени для отладки).

using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaSpentFuelPoolSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DystopiaReactorSystem _reactor = default!;

    private static readonly Vector2i[] Directions = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaSpentFuelPoolCellComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DystopiaSpentFuelPoolCellComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaSpentFuelPoolCellComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaSpentFuelPoolCellComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInit(Entity<DystopiaSpentFuelPoolCellComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Slot);
    }

    private ContainerSlot Slot(Entity<DystopiaSpentFuelPoolCellComponent> ent)
    {
        return _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Slot);
    }

    /// <summary>Доля воды в ячейке (0…1).</summary>
    public float WaterFraction(Entity<DystopiaSpentFuelPoolCellComponent> ent)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.WaterSolution, out _, out var sol) || sol.MaxVolume <= FixedPoint2.Zero)
            return 0f;

        return sol.GetTotalPrototypeQuantity(ent.Comp.Water).Float() / sol.MaxVolume.Float();
    }

    /// <summary>Сколько рад/с вода ячейки гасит у ТВЭЛа (полная — ShieldResistance, пустая — ничего).</summary>
    public float Shield(Entity<DystopiaSpentFuelPoolCellComponent> ent)
    {
        return ent.Comp.ShieldResistance * Math.Clamp(WaterFraction(ent), 0f, 1f);
    }

    public bool IsCooled(Entity<DystopiaSpentFuelPoolCellComponent> ent)
    {
        return WaterFraction(ent) >= ent.Comp.LowWater;
    }

    // ------------------------------------------------------------------ такт

    public void Tick(DystopiaReactorPrototype cfg, float dt)
    {
        var seen = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<DystopiaSpentFuelPoolCellComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var cell, out var xform))
        {
            if (seen.Contains(uid))
                continue;

            var pool = Collect((uid, cell), xform, seen);
            Equalize(pool);
            foreach (var member in pool)
                CellTick(member, cfg, dt);
        }
    }

    /// <summary>Связная по стороне группа закреплённых ячеек.</summary>
    private List<Entity<DystopiaSpentFuelPoolCellComponent>> Collect(Entity<DystopiaSpentFuelPoolCellComponent> start,
        TransformComponent xform, HashSet<EntityUid> seen)
    {
        var result = new List<Entity<DystopiaSpentFuelPoolCellComponent>> { start };
        seen.Add(start);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return result;

        var queue = new Queue<Vector2i>();
        queue.Enqueue(_map.TileIndicesFor(gridUid, grid, xform.Coordinates));
        while (queue.Count > 0 && result.Count < 1024)
        {
            var tile = queue.Dequeue();
            foreach (var dir in Directions)
            {
                var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile + dir);
                while (anchored.MoveNext(out var other))
                {
                    if (seen.Contains(other.Value) || !TryComp<DystopiaSpentFuelPoolCellComponent>(other.Value, out var otherCell))
                        continue;

                    seen.Add(other.Value);
                    result.Add((other.Value, otherCell));
                    queue.Enqueue(tile + dir);
                }
            }
        }

        return result;
    }

    /// <summary>Общая вода бассейна: в каждой ячейке поровну (по доле от объёма).</summary>
    private void Equalize(List<Entity<DystopiaSpentFuelPoolCellComponent>> pool)
    {
        if (pool.Count < 2)
            return;

        var total = 0f;
        var capacity = 0f;
        foreach (var cell in pool)
        {
            if (!_solution.TryGetSolution(cell.Owner, cell.Comp.WaterSolution, out _, out var sol))
                continue;

            total += sol.GetTotalPrototypeQuantity(cell.Comp.Water).Float();
            capacity += sol.MaxVolume.Float();
        }

        if (capacity <= 0f)
            return;

        var fraction = total / capacity;
        foreach (var cell in pool)
        {
            if (!_solution.TryGetSolution(cell.Owner, cell.Comp.WaterSolution, out var solEnt, out var sol))
                continue;

            var have = sol.GetTotalPrototypeQuantity(cell.Comp.Water).Float();
            var want = sol.MaxVolume.Float() * fraction;
            var diff = want - have;
            if (MathF.Abs(diff) < 0.5f)
                continue;

            if (diff > 0f)
                _solution.TryAddReagent(solEnt.Value, cell.Comp.Water, FixedPoint2.New(diff), out _);
            else
                _solution.RemoveReagent(solEnt.Value, cell.Comp.Water, FixedPoint2.New(-diff));
        }
    }

    private void CellTick(Entity<DystopiaSpentFuelPoolCellComponent> ent, DystopiaReactorPrototype cfg, float dt)
    {
        var cell = ent.Comp;
        var rod = Slot(ent).ContainedEntity;
        TryComp<DystopiaFuelRodComponent>(rod, out var rodComp);
        var activity = rodComp?.Activity ?? 0f;

        // остаточное тепло испаряет воду
        var heat = activity * cfg.DecayHeatPerActivity;
        if (heat > 0f && _solution.TryGetSolution(ent.Owner, cell.WaterSolution, out var solEnt, out _))
            _solution.RemoveReagent(solEnt.Value, cell.Water, FixedPoint2.New(heat * cell.EvaporationPerKilowatt * dt));

        var water = WaterFraction(ent);

        // сухая ячейка с горячим ТВЭЛом
        if (rod != null && activity > cfg.HotActivity && water < cell.LowWater && !cell.DryDamage.Empty)
        {
            cell.PendingDamage += dt;
            if (cell.PendingDamage >= 1f)
            {
                _damageable.TryChangeDamage(ent.Owner, cell.DryDamage * cell.PendingDamage, interruptsDoAfters: false);
                cell.PendingDamage = 0f;
            }
        }

        var level = water >= 0.6f ? 2 : water >= 0.15f ? 1 : 0;
        _appearance.SetData(ent, DystopiaPoolVisuals.Water, level);
        _appearance.SetData(ent, DystopiaPoolVisuals.Rod, rod != null);
        _appearance.SetData(ent, DystopiaPoolVisuals.Glow, rod != null && activity > cfg.HotActivity && level > 0);
    }

    // ------------------------------------------------------------------ руками и захватом

    private void OnInteractUsing(Entity<DystopiaSpentFuelPoolCellComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var slot = Slot(ent);
        if (TryComp<DystopiaTongsComponent>(args.Used, out var tongs))
        {
            if (_reactor.TongsExchange((args.Used, tongs), args.User, slot))
                args.Handled = true;
            return;
        }

        if (!HasComp<DystopiaFuelRodComponent>(args.Used))
            return;

        args.Handled = true;
        if (slot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pool-occupied"), ent, args.User);
            return;
        }

        if (_container.Insert(args.Used, slot))
            _popup.PopupEntity(Loc.GetString("dystopia-pool-rod-placed"), ent, args.User);
    }

    private void OnGetVerbs(Entity<DystopiaSpentFuelPoolCellComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || Slot(ent).ContainedEntity is not { } rod)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-reactor-verb-extract"),
            Act = () =>
            {
                if (TryComp<DystopiaHotItemComponent>(rod, out var hot) && hot.Hot)
                {
                    _popup.PopupEntity(Loc.GetString("dystopia-fuel-rod-too-hot"), ent, user);
                    return;
                }

                if (_container.Remove(rod, Slot(ent)))
                    _hands.PickupOrDrop(user, rod);
            },
            Priority = 2,
        });
    }

    private void OnExamined(Entity<DystopiaSpentFuelPoolCellComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("dystopia-pool-examine-water", ("water", (int) MathF.Round(WaterFraction(ent) * 100f))));
        if (Slot(ent).ContainedEntity is { } rod)
            args.PushMarkup(Loc.GetString("dystopia-pool-examine-rod", ("rod", Name(rod))));
        else
            args.PushMarkup(Loc.GetString("dystopia-pool-examine-empty"));

        if (!IsCooled(ent))
            args.PushMarkup(Loc.GetString("dystopia-pool-examine-low"));
    }
}
