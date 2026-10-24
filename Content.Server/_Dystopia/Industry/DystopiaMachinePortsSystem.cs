// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: патрубки и клеммы многоклеточных машин (см. DystopiaMachinePortsComponent). Ставятся при появлении машины
// на её краевых клетках с учётом поворота, удаляются вместе с ней. Разворот на 180° (действие, если можно) —
// машина поворачивается, узлы переставляются.

using System.Numerics;
using Content.Shared._Dystopia.Industry;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaMachinePortsSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaMachinePortsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaMachinePortsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DystopiaMachinePortsComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaMachinePortsComponent, DystopiaMachineFlipDoAfterEvent>(OnFlipDoAfter);
    }

    private void OnMapInit(Entity<DystopiaMachinePortsComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Spawned.Count > 0)
            return; // узлы уже есть (сохранены с картой)

        SpawnPorts(ent);
    }

    private void OnShutdown(Entity<DystopiaMachinePortsComponent> ent, ref ComponentShutdown args)
    {
        DeletePorts(ent);
    }

    /// <summary>Узел машины по ключу (патрубок, клемма).</summary>
    public bool TryGetPort(EntityUid uid, string key, out EntityUid port)
    {
        port = default;
        if (!TryComp<DystopiaMachinePortsComponent>(uid, out var ports) ||
            !ports.Spawned.TryGetValue(key, out var found) || TerminatingOrDeleted(found))
        {
            return false;
        }

        port = found;
        return true;
    }

    private void SpawnPorts(Entity<DystopiaMachinePortsComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var rotation = xform.LocalRotation;
        foreach (var spec in ent.Comp.Ports)
        {
            var offset = rotation.RotateVec(new Vector2(spec.Offset.X, spec.Offset.Y));
            var tile = center + new Vector2i((int) MathF.Round(offset.X), (int) MathF.Round(offset.Y));
            var coords = _map.GridTileToLocal(gridUid, grid, tile);
            var angle = (rotation + spec.Direction.ToAngle()).GetCardinalDir().ToAngle();
            var port = EntityManager.SpawnAtPosition(spec.Prototype, coords, angle);
            var portXform = Transform(port);
            if (!portXform.Anchored)
                _transform.AnchorEntity((port, portXform), new Entity<MapGridComponent>(gridUid, grid));

            var comp = EnsureComp<DystopiaMachinePortComponent>(port);
            comp.Machine = ent;
            comp.Key = spec.Key;
            ent.Comp.Spawned[spec.Key] = port;
        }
    }

    private void DeletePorts(Entity<DystopiaMachinePortsComponent> ent)
    {
        foreach (var port in ent.Comp.Spawned.Values)
        {
            if (!TerminatingOrDeleted(port))
                QueueDel(port);
        }

        ent.Comp.Spawned.Clear();
    }

    // ------------------------------------------------------------------ разворот

    private void OnGetVerbs(Entity<DystopiaMachinePortsComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!ent.Comp.Flippable || !args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("dystopia-machine-flip-verb"),
            Act = () =>
            {
                var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.FlipDelay,
                    new DystopiaMachineFlipDoAfterEvent(), ent.Owner, target: ent.Owner)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                };
                if (_doAfter.TryStartDoAfter(doAfter))
                    _popup.PopupEntity(Loc.GetString("dystopia-machine-flip-start"), ent, user);
            },
        });
    }

    private void OnFlipDoAfter(Entity<DystopiaMachinePortsComponent> ent, ref DystopiaMachineFlipDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var xform = Transform(ent);
        DeletePorts(ent);
        _transform.SetLocalRotation(ent, xform.LocalRotation + new Angle(Math.PI), xform);
        SpawnPorts(ent);
        _popup.PopupEntity(Loc.GetString("dystopia-machine-flip-done"), ent, args.User);
    }
}
