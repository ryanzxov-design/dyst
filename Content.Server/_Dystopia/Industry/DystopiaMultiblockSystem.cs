// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: сборка фиксированных многоблоков из секций каркаса и разборка обратно (см. DystopiaMultiblockComponents).

using Content.Server.Fluids.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaMultiblockSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Секции, которые надо проверить на следующем такте (не в MapInit: стройка ещё возится с сущностью).</summary>
    private readonly HashSet<EntityUid> _pending = new();

    /// <summary>Сложился, но на каркасе кто-то стоит — проверить ещё раз позже.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _retry = new();

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Секции, уже ушедшие в машину на этом такте. Удаление отложенное (QueueDel), поэтому без этого набора остальные
    /// секции той же пачки (спавн с Ctrl/Shift ставит много за раз) находили бы их снова и собирали ещё машины.
    /// </summary>
    private readonly HashSet<EntityUid> _consumed = new();

    /// <summary>Идёт разборка: появляющиеся секции не собираются обратно.</summary>
    private bool _disassembling;

    /// <summary>Больше этого секции не обходим (защита от огромных скоплений).</summary>
    private const int ScanLimit = 64;

    private static readonly Vector2i[] Neighbours = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaMultiblockFrameComponent, MapInitEvent>(OnFrameMapInit);
        SubscribeLocalEvent<DystopiaMultiblockFrameComponent, AnchorStateChangedEvent>(OnFrameAnchor);
        SubscribeLocalEvent<DystopiaMultiblockFrameComponent, ExaminedEvent>(OnFrameExamined);
        SubscribeLocalEvent<DystopiaMultiblockComponent, ExaminedEvent>(OnExamined);
    }

    private void OnFrameMapInit(Entity<DystopiaMultiblockFrameComponent> ent, ref MapInitEvent args)
    {
        if (!_disassembling)
            _pending.Add(ent);
    }

    private void OnFrameAnchor(Entity<DystopiaMultiblockFrameComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored && !_disassembling)
            _pending.Add(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_retry.Count > 0)
        {
            var now = _timing.CurTime;
            foreach (var (uid, at) in new Dictionary<EntityUid, TimeSpan>(_retry))
            {
                if (now < at)
                    continue;

                _retry.Remove(uid);
                _pending.Add(uid);
            }
        }

        if (_pending.Count == 0)
            return;

        var list = new List<EntityUid>(_pending);
        _pending.Clear();
        _consumed.Clear();
        foreach (var uid in list)
        {
            if (_consumed.Contains(uid) || TerminatingOrDeleted(uid) ||
                !TryComp<DystopiaMultiblockFrameComponent>(uid, out var frame))
            {
                continue;
            }

            TryAssemble((uid, frame));
        }

        _consumed.Clear();
    }

    // ------------------------------------------------------------------ обход секций

    private readonly record struct Section(EntityUid Uid, DystopiaMultiblockFrameComponent Frame, Vector2i Tile);

    private bool TryGetTile(EntityUid uid, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        grid = default;
        tile = default;
        var xform = Transform(uid);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        grid = (gridUid, gridComp);
        tile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        return true;
    }

    private Section? SectionAt(Entity<MapGridComponent> grid, Vector2i tile, string key)
    {
        foreach (var other in _map.GetAnchoredEntities(grid, grid.Comp, tile))
        {
            if (TryComp<DystopiaMultiblockFrameComponent>(other, out var frame) && frame.Key == key &&
                !_consumed.Contains(other) && !TerminatingOrDeleted(other))
            {
                return new Section(other, frame, tile);
            }
        }

        return null;
    }

    /// <summary>Все секции того же ключа, связанные с этой по соседству (до ScanLimit).</summary>
    private List<Section> Scan(Entity<DystopiaMultiblockFrameComponent> start, Entity<MapGridComponent> grid, Vector2i tile)
    {
        var found = new List<Section> { new(start, start.Comp, tile) };
        var seen = new HashSet<Vector2i> { tile };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(tile);
        while (queue.Count > 0 && found.Count <= ScanLimit)
        {
            var current = queue.Dequeue();
            foreach (var step in Neighbours)
            {
                var next = current + step;
                if (!seen.Add(next) || SectionAt(grid, next, start.Comp.Key) is not { } section)
                    continue;

                found.Add(section);
                queue.Enqueue(next);
            }
        }

        return found;
    }

    /// <summary>Сложился ли ровно нужный прямоугольник; что из него выйдет и где центр.</summary>
    private bool Fits(DystopiaMultiblockFrameComponent frame, List<Section> sections, out EntProtoId result,
        out Vector2i center, out bool rotated)
    {
        result = default;
        center = default;
        rotated = false;
        var min = sections[0].Tile;
        var max = min;
        foreach (var s in sections)
        {
            min = Vector2i.ComponentMin(min, s.Tile);
            max = Vector2i.ComponentMax(max, s.Tile);
        }

        var width = max.X - min.X + 1;
        var height = max.Y - min.Y + 1;
        if (sections.Count != width * height)
            return false;

        if (width == frame.Width && height == frame.Height)
            result = frame.Result;
        else if (width == frame.Height && height == frame.Width && frame.ResultRotated is { } other)
            result = other;
        else if (width == frame.Height && height == frame.Width && frame.RotateResult)
        {
            result = frame.Result;
            rotated = true;
        }
        else
            return false;

        center = new Vector2i(min.X + width / 2, min.Y + height / 2);
        if (!frame.RequireCore)
            return true;

        var cores = 0;
        var coreInCenter = false;
        foreach (var s in sections)
        {
            if (!s.Frame.Core)
                continue;

            cores++;
            coreInCenter |= s.Tile == center;
        }

        return cores == 1 && coreInCenter;
    }

    private void TryAssemble(Entity<DystopiaMultiblockFrameComponent> ent)
    {
        if (!TryGetTile(ent, out var grid, out var tile))
            return;

        var sections = Scan(ent, grid, tile);
        if (sections.Count > ent.Comp.Width * ent.Comp.Height ||
            !Fits(ent.Comp, sections, out var result, out var center, out var rotated))
        {
            return;
        }

        // Кто-то стоит на лесах — машина появилась бы прямо на нём. Подождём, пока сойдёт.
        foreach (var s in sections)
        {
            foreach (var other in _lookup.GetLocalEntitiesIntersecting(grid, s.Tile, gridComp: grid.Comp))
            {
                if (!HasComp<MobStateComponent>(other))
                    continue;

                _retry[ent] = _timing.CurTime + RetryDelay;
                return;
            }
        }

        foreach (var s in sections)
        {
            _consumed.Add(s.Uid);
            _retry.Remove(s.Uid);
            QueueDel(s.Uid);
        }

        var coords = _map.GridTileToLocal(grid, grid.Comp, center);
        var machine = EntityManager.SpawnAtPosition(result, coords, rotated ? Direction.East.ToAngle() : Angle.Zero);
        var xform = Transform(machine);
        if (!xform.Anchored)
            _transform.AnchorEntity((machine, xform), grid);
    }

    // ------------------------------------------------------------------ разборка

    /// <summary>Машина снова становится секциями каркаса (жидкость из баков проливается на пол).</summary>
    public void Disassemble(EntityUid uid)
    {
        if (!TryComp<DystopiaMultiblockComponent>(uid, out var mb))
            return;

        var xform = Transform(uid);
        if (_solution.TryGetDrainableSolution(uid, out var drain, out var solution) && solution.Volume > 0)
            _puddle.TrySpillAt(xform.Coordinates, _solution.SplitSolution(drain.Value, solution.Volume), out _);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var dir = xform.LocalRotation.GetCardinalDir();
        var sideways = dir is Direction.East or Direction.West;
        var width = sideways ? mb.Height : mb.Width;
        var height = sideways ? mb.Width : mb.Height;
        var x0 = center.X - width / 2;
        var y0 = center.Y - height / 2;
        _disassembling = true;
        try
        {
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var tile = new Vector2i(x0 + x, y0 + y);
                    var proto = tile == center && mb.CoreFrame is { } core ? core : mb.Frame;
                    Spawn(proto, _map.GridTileToLocal(gridUid, grid, tile));
                }
            }
        }
        finally
        {
            _disassembling = false;
        }
    }

    // ------------------------------------------------------------------ осмотр

    private void OnFrameExamined(Entity<DystopiaMultiblockFrameComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var name = _proto.TryIndex(ent.Comp.Result, out var proto) ? proto.Name : ent.Comp.Result.Id;
        var size = ent.Comp.ResultRotated != null || ent.Comp.RotateResult
            ? Loc.GetString("dystopia-multiblock-size-either", ("w", ent.Comp.Width), ("h", ent.Comp.Height))
            : Loc.GetString("dystopia-multiblock-size", ("w", ent.Comp.Width), ("h", ent.Comp.Height));
        args.PushMarkup(Loc.GetString("dystopia-multiblock-frame-examine", ("machine", name), ("size", size)));
        if (ent.Comp.RequireCore)
            args.PushMarkup(Loc.GetString("dystopia-multiblock-frame-core"));

        if (!TryGetTile(ent, out var grid, out var tile))
            return;

        var sections = Scan(ent, grid, tile);
        var need = ent.Comp.Width * ent.Comp.Height;
        if (sections.Count > need)
        {
            args.PushMarkup(Loc.GetString("dystopia-multiblock-frame-extra", ("count", sections.Count), ("need", need)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("dystopia-multiblock-frame-count", ("count", sections.Count), ("need", need)));
            if (sections.Count == need)
            {
                args.PushMarkup(Loc.GetString(Fits(ent.Comp, sections, out _, out _, out _)
                    ? "dystopia-multiblock-frame-blocked"
                    : "dystopia-multiblock-frame-shape"));
            }
        }
    }

    private void OnExamined(Entity<DystopiaMultiblockComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.PortsHint is { } hint)
            args.PushMarkup(Loc.GetString(hint));
    }
}

/// <summary>Действие графа строительства: разобрать многоблок обратно в секции каркаса.</summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class DystopiaMultiblockDisassemble : IGraphAction
{
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        entityManager.System<DystopiaMultiblockSystem>().Disassemble(uid);
    }
}
