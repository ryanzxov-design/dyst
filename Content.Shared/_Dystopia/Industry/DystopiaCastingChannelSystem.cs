// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: литейные желоба — сеть, как у труб. Желоба связаны, если у обоих есть отверстия друг к другу.
// Расплав, налитый в сеть (горн с большим тиглем или клещами из малого тигля), обходит её от точки налива
// и по порядку (ближние первыми) заполняет литейные столы и изложницы, к которым подходит отверстие желоба.

using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Industry;

public sealed partial class DystopiaCastingChannelSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SolutionTransferSystem _transfer = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public static readonly Direction[] Sides = { Direction.South, Direction.East, Direction.North, Direction.West };

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

    /// <summary>Есть ли у желоба отверстие в сторону <paramref name="worldDir"/> (с учётом поворота).</summary>
    public bool HasOpening(Entity<DystopiaCastingChannelComponent> channel, Direction worldDir)
    {
        var rotation = Transform(channel).LocalRotation;
        foreach (var opening in channel.Comp.Openings)
        {
            if (rotation.RotateDir(opening) == worldDir)
                return true;
        }

        return false;
    }

    private Entity<DystopiaCastingChannelComponent>? ChannelAt(Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var uid in _map.GetAnchoredEntities(grid, tile))
        {
            if (TryComp<DystopiaCastingChannelComponent>(uid, out var channel))
                return (uid, channel);
        }

        return null;
    }

    private void MouldsAt(Entity<MapGridComponent> grid, Vector2i tile, List<EntityUid> result)
    {
        foreach (var uid in _map.GetAnchoredEntities(grid, tile))
        {
            if (HasComp<DystopiaMouldComponent>(uid) && !result.Contains(uid))
                result.Add(uid);
        }
    }

    /// <summary>
    /// Желоб на соседней клетке, у которого есть отверстие к <paramref name="source"/>. Для больших конструкций
    /// <paramref name="radius"/> — полуразмер корпуса в клетках (1 — конструкция 3×3): желоб ищется вокруг всего корпуса.
    /// </summary>
    public Entity<DystopiaCastingChannelComponent>? FindAttachedChannel(EntityUid source, int radius = 0)
    {
        if (!TryGetTile(source, out var grid, out var center))
            return null;

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var tile = center + new Vector2i(dx, dy);
                foreach (var side in Sides)
                {
                    var next = tile.Offset(side);
                    // Сосед внутри корпуса — не край
                    if (Math.Abs(next.X - center.X) <= radius && Math.Abs(next.Y - center.Y) <= radius)
                        continue;

                    if (ChannelAt(grid, next) is { } channel && HasOpening(channel, side.GetOpposite()))
                        return channel;
                }
            }
        }

        return null;
    }

    /// <summary>Прикрученные литейные столы и изложницы на соседних клетках.</summary>
    public List<EntityUid> FindAdjacentMoulds(EntityUid source)
    {
        var result = new List<EntityUid>();
        if (!TryGetTile(source, out var grid, out var tile))
            return result;

        foreach (var side in Sides)
        {
            MouldsAt(grid, tile.Offset(side), result);
        }

        return result;
    }

    /// <summary>
    /// Обойти сеть желобов от <paramref name="start"/> (ближние первыми). Возвращает столы и изложницы, к которым
    /// подходят отверстия сети, в порядке заполнения; в <paramref name="path"/> — все желоба сети.
    /// </summary>
    public List<EntityUid> GetMoulds(Entity<DystopiaCastingChannelComponent> start, List<EntityUid> path)
    {
        var moulds = new List<EntityUid>();
        if (!Transform(start).Anchored || !TryGetTile(start, out var grid, out var startTile))
            return moulds;

        var visited = new HashSet<EntityUid> { start.Owner };
        var queue = new Queue<(Entity<DystopiaCastingChannelComponent> Channel, Vector2i Tile)>();
        queue.Enqueue((start, startTile));

        while (queue.TryDequeue(out var current) && path.Count < start.Comp.MaxSegments)
        {
            path.Add(current.Channel);
            foreach (var side in Sides)
            {
                if (!HasOpening(current.Channel, side))
                    continue;

                var next = current.Tile.Offset(side);
                if (ChannelAt(grid, next) is { } neighbour)
                {
                    if (HasOpening(neighbour, side.GetOpposite()) && visited.Add(neighbour.Owner))
                        queue.Enqueue((neighbour, next));
                    continue;
                }

                MouldsAt(grid, next, moulds);
            }
        }

        return moulds;
    }

    // ------------------------------------------------------------------ налив

    /// <summary>Налить из <paramref name="source"/> в сеть желобов. Возвращает, сколько разлито.</summary>
    public FixedPoint2 Pour(EntityUid user, EntityUid source, Entity<SolutionComponent> sourceSolution,
        Entity<DystopiaCastingChannelComponent> channel)
    {
        var path = new List<EntityUid>();
        var moulds = GetMoulds(channel, path);
        if (moulds.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-channel-no-moulds"), channel, user);
            return FixedPoint2.Zero;
        }

        var poured = PourInto(user, source, sourceSolution, moulds);
        if (poured > FixedPoint2.Zero)
            ShowFlow(channel, path);

        return poured;
    }

    /// <summary>
    /// Разлить по сети желобов только один реагент (дуговая печь льёт только выбранный сплав), не больше
    /// <paramref name="max"/>. Возвращает, сколько разлито.
    /// </summary>
    public FixedPoint2 PourReagent(EntityUid source, Entity<SolutionComponent> sourceSolution,
        ProtoId<ReagentPrototype> reagent, FixedPoint2 max, Entity<DystopiaCastingChannelComponent> channel)
    {
        var path = new List<EntityUid>();
        var moulds = GetMoulds(channel, path);
        var total = FixedPoint2.Zero;
        var temperature = sourceSolution.Comp.Solution.Temperature;

        foreach (var mould in moulds)
        {
            var left = FixedPoint2.Min(max - total, sourceSolution.Comp.Solution.GetTotalPrototypeQuantity(reagent));
            if (left <= FixedPoint2.Zero)
                break;

            if (!TryComp<DystopiaMouldComponent>(mould, out var comp) ||
                !_solution.TryGetSolution(mould, comp.Solution, out var target, out var targetSolution))
            {
                continue;
            }

            var amount = FixedPoint2.Min(left, targetSolution.AvailableVolume);
            if (amount <= FixedPoint2.Zero)
                continue;

            _solution.RemoveReagent(sourceSolution, reagent.Id, amount);
            _solution.TryAddReagent(target.Value, reagent.Id, amount, out var accepted, temperature);
            total += accepted;

            var ev = new SolutionTransferredEvent(source, mould, source, accepted);
            RaiseLocalEvent(mould, ref ev);
        }

        if (total > FixedPoint2.Zero)
            ShowFlow(channel, path);

        return total;
    }

    private void ShowFlow(Entity<DystopiaCastingChannelComponent> channel, List<EntityUid> path)
    {
        var until = _timing.CurTime + TimeSpan.FromSeconds(channel.Comp.FlowVisualTime);
        foreach (var uid in path)
        {
            if (!TryComp<DystopiaCastingChannelComponent>(uid, out var comp))
                continue;

            comp.FlowUntil = until;
            comp.Flowing = true;
            _appearance.SetData(uid, DystopiaCastingChannelVisuals.Flowing, true);
        }

        _audio.PlayPvs(channel.Comp.FlowSound, channel);
    }

    /// <summary>Разлить по столам и изложницам по порядку, каждой — сколько влезет. Возвращает, сколько разлито.</summary>
    public FixedPoint2 PourInto(EntityUid user, EntityUid source, Entity<SolutionComponent> sourceSolution,
        List<EntityUid> moulds)
    {
        var total = FixedPoint2.Zero;
        var anyRoom = false;
        foreach (var mould in moulds)
        {
            if (sourceSolution.Comp.Solution.Volume <= FixedPoint2.Zero)
                break;

            if (!TryComp<DystopiaMouldComponent>(mould, out var comp) ||
                !_solution.TryGetSolution(mould, comp.Solution, out var target, out var targetSolution) ||
                targetSolution.AvailableVolume <= FixedPoint2.Zero)
            {
                continue;
            }

            anyRoom = true;
            var moved = _transfer.Transfer(new SolutionTransferData(user, source, sourceSolution, mould, target.Value,
                targetSolution.AvailableVolume));
            if (moved <= FixedPoint2.Zero)
                break; // источник отказал (металл застыл) — дальше не льём

            total += moved;
        }

        if (!anyRoom)
            _popup.PopupEntity(Loc.GetString("dystopia-channel-moulds-full"), source, user);

        return total;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaCastingChannelComponent>();
        while (query.MoveNext(out var uid, out var channel))
        {
            if (!channel.Flowing || now < channel.FlowUntil)
                continue;

            channel.Flowing = false;
            _appearance.SetData(uid, DystopiaCastingChannelVisuals.Flowing, false);
        }
    }
}
