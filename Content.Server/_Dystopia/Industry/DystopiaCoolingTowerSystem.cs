// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: градирня — патрубки (отдельные сущности-узлы газовой трубы на краевых клетках), конденсация пара в воду.

using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaCoolingTowerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaCoolingTowerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaCoolingTowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DystopiaCoolingTowerComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>Патрубки — на краевых клетках, повёрнуты наружу (туда подводят газовую трубу).</summary>
    private void OnMapInit(Entity<DystopiaCoolingTowerComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var spec in ent.Comp.Inlets)
        {
            var coords = _map.GridTileToLocal(gridUid, grid, center + spec.Offset);
            var inlet = EntityManager.SpawnAtPosition(ent.Comp.InletPrototype, coords, spec.Direction.ToAngle());
            var inletXform = Transform(inlet);
            if (!inletXform.Anchored)
                _transform.AnchorEntity((inlet, inletXform), new Entity<MapGridComponent>(gridUid, grid));

            EnsureComp<DystopiaCoolingTowerInletComponent>(inlet).Tower = ent;
            ent.Comp.InletEntities.Add(inlet);
        }
    }

    private void OnShutdown(Entity<DystopiaCoolingTowerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var inlet in ent.Comp.InletEntities)
        {
            if (!TerminatingOrDeleted(inlet))
                QueueDel(inlet);
        }

        ent.Comp.InletEntities.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaCoolingTowerComponent>();
        while (query.MoveNext(out var uid, out var tower))
        {
            if (now < tower.NextUpdate)
                continue;

            tower.NextUpdate = now + TimeSpan.FromSeconds(tower.Interval);
            Tick((uid, tower));
        }
    }

    private void Tick(Entity<DystopiaCoolingTowerComponent> ent)
    {
        var tower = ent.Comp;
        tower.SteamRate = 0f;
        tower.WaterRate = 0f;
        var now = _timing.CurTime;

        if (_solution.TryGetSolution(ent.Owner, tower.WaterSolution, out var waterEnt, out var water))
        {
            var factor = _power.IsPowered(ent.Owner) ? 1f : tower.UnpoweredFactor;
            var keep = 1f - tower.Loss;
            // Сколько пара можно взять: по производительности и по месту в баке.
            var room = water.AvailableVolume.Float() / MathF.Max(0.001f, tower.WaterPerMole * keep);
            var budget = MathF.Min(tower.SteamPerSecond * factor * tower.Interval, room);
            var taken = 0f;

            foreach (var inlet in tower.InletEntities)
            {
                if (budget - taken <= 0.001f)
                    break;

                if (!_nodes.TryGetNode<PipeNode>(inlet, tower.InletNode, out var pipe))
                    continue;

                var vapor = pipe.Air.GetMoles(Gas.WaterVapor);
                var take = MathF.Min(vapor, budget - taken);
                if (take <= 0.001f)
                    continue;

                pipe.Air.AdjustMoles(Gas.WaterVapor, -take);
                taken += take;
            }

            if (taken > 0.001f)
            {
                var units = taken * tower.WaterPerMole * keep;
                _solution.TryAddReagent(waterEnt.Value, tower.Water, FixedPoint2.New(units), out _);
                tower.SteamRate = taken / tower.Interval;
                tower.WaterRate = units / tower.Interval;
                tower.LastWorked = now;
            }
        }

        var working = tower.LastWorked > TimeSpan.Zero && now - tower.LastWorked < TimeSpan.FromSeconds(tower.WorkingHold);
        _appearance.SetData(ent, DystopiaCoolingTowerVisuals.Working, working);
        _ambient.SetAmbience(ent, working);
    }

    private void OnExamined(Entity<DystopiaCoolingTowerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var powered = _power.IsPowered(ent.Owner);
        args.PushMarkup(Loc.GetString(powered ? "dystopia-cooling-tower-examine-fan" : "dystopia-cooling-tower-examine-draft"));
        args.PushMarkup(Loc.GetString("dystopia-cooling-tower-examine-rate",
            ("steam", MathF.Round(ent.Comp.SteamRate, 2)), ("water", MathF.Round(ent.Comp.WaterRate, 2))));
        if (_solution.TryGetSolution(ent.Owner, ent.Comp.WaterSolution, out _, out var water))
        {
            args.PushMarkup(Loc.GetString("dystopia-cooling-tower-examine-water",
                ("amount", (int) water.Volume.Float()), ("max", (int) water.MaxVolume.Float())));
            if (water.AvailableVolume <= FixedPoint2.Zero)
                args.PushMarkup(Loc.GetString("dystopia-cooling-tower-examine-full"));
        }
    }
}
