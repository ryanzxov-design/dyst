// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: ЛЭП. Моток провода: тычок в опору — конец закреплён, тычок во вторую — провод натянут (списывается по
// клетке пролёта). Кусачки по опоре — все её провода сняты (провод возвращается). Связанные опоры — одна
// высоковольтная сеть (DystopiaPylonNode); после изменения связей сеть пересобирается.

using System.Linq;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.NodeContainer;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaPowerLineSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private NodeGroupSystem _nodeGroups = default!;

    private static readonly ProtoId<ToolQualityPrototype> Cutting = "Cutting";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaPylonComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaPylonComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaPylonComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInteractUsing(Entity<DystopiaPylonComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<DystopiaLinkerComponent>(args.Used, out var linker) && TryComp<StackComponent>(args.Used, out var stack) &&
            stack.StackTypeId == StackOf(ent.Comp))
        {
            args.Handled = true;
            Link(ent, (args.Used, linker, stack), args.User);
            return;
        }

        if (_tool.HasQuality(args.Used, Cutting.Id) && ent.Comp.Links.Count > 0)
        {
            args.Handled = true;
            CutAll(ent, args.User);
        }
    }

    /// <summary>Тип стопки провода (по прототипу того, что возвращается при снятии).</summary>
    private ProtoId<StackPrototype>? StackOf(DystopiaPylonComponent pylon)
    {
        return _proto.TryIndex(pylon.WireStack, out var proto) &&
               proto.TryGetComponent<StackComponent>(out var stack, EntityManager.ComponentFactory)
            ? (ProtoId<StackPrototype>?) stack.StackTypeId
            : null;
    }

    [Dependency] private IPrototypeManager _proto = default!;

    private float Span(EntityUid a, EntityUid b)
    {
        var pa = _xform.GetWorldPosition(a);
        var pb = _xform.GetWorldPosition(b);
        return (pa - pb).Length();
    }

    private void Link(Entity<DystopiaPylonComponent> ent, Entity<DystopiaLinkerComponent, StackComponent> wire, EntityUid user)
    {
        var first = wire.Comp1.First;
        if (first == null || first == ent.Owner || TerminatingOrDeleted(first.Value) ||
            !TryComp<DystopiaPylonComponent>(first.Value, out var other))
        {
            wire.Comp1.First = ent;
            _popup.PopupEntity(Loc.GetString("dystopia-pylon-first"), ent, user);
            return;
        }

        wire.Comp1.First = null;
        if (ent.Comp.Links.Contains(first.Value))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pylon-already"), ent, user);
            return;
        }

        // опоры могут стоять на разных сетках (фундамент и грунт планеты) — лишь бы в одном мире
        if (Transform(ent).MapID != Transform(first.Value).MapID)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pylon-other-grid"), ent, user);
            return;
        }

        var span = Span(ent, first.Value);
        if (span > MathF.Min(ent.Comp.MaxSpan, other.MaxSpan))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pylon-too-far", ("span", (int) MathF.Ceiling(span)),
                ("max", (int) ent.Comp.MaxSpan)), ent, user);
            return;
        }

        if (ent.Comp.Links.Count >= ent.Comp.MaxLinks || other.Links.Count >= other.MaxLinks)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pylon-full"), ent, user);
            return;
        }

        var need = Math.Max(1, (int) MathF.Ceiling(span / MathF.Max(0.1f, wire.Comp1.TilesPerItem)));
        if (!_stack.TryUse((wire.Owner, wire.Comp2), need))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pylon-not-enough", ("need", need)), ent, user);
            return;
        }

        ent.Comp.Links.Add(first.Value);
        other.Links.Add(ent);
        Dirty(ent);
        Dirty(first.Value, other);
        Reflood(ent);
        Reflood(first.Value);
        _popup.PopupEntity(Loc.GetString("dystopia-pylon-linked", ("span", (int) MathF.Ceiling(span))), ent, user);
    }

    private void CutAll(Entity<DystopiaPylonComponent> ent, EntityUid? user)
    {
        var returned = 0;
        foreach (var other in ent.Comp.Links.ToArray())
        {
            if (!TerminatingOrDeleted(other) && TryComp<DystopiaPylonComponent>(other, out var otherPylon))
            {
                returned += Math.Max(1, (int) MathF.Ceiling(Span(ent, other)));
                otherPylon.Links.Remove(ent);
                Dirty(other, otherPylon);
                Reflood(other);
            }
        }

        ent.Comp.Links.Clear();
        Dirty(ent);
        Reflood(ent);

        if (returned > 0)
        {
            var stack = Spawn(ent.Comp.WireStack, Transform(ent).Coordinates);
            _stack.SetCount(new Entity<StackComponent?>(stack, null), returned);
        }

        if (user != null)
            _popup.PopupEntity(Loc.GetString("dystopia-pylon-cut"), ent, user.Value);
    }

    private void OnShutdown(Entity<DystopiaPylonComponent> ent, ref ComponentShutdown args)
    {
        // разобранная или сломанная опора: соседи забывают про неё (провод падает вместе с ней)
        foreach (var other in ent.Comp.Links)
        {
            if (TerminatingOrDeleted(other) || !TryComp<DystopiaPylonComponent>(other, out var otherPylon))
                continue;

            otherPylon.Links.Remove(ent);
            Dirty(other, otherPylon);
            Reflood(other);
        }
    }

    private void Reflood(EntityUid uid)
    {
        if (!TryComp<DystopiaPylonComponent>(uid, out var pylon) ||
            !_nodes.TryGetNode<Node>(uid, pylon.Node, out var node))
        {
            return;
        }

        _nodeGroups.QueueReflood(node);
    }

    private void OnExamined(Entity<DystopiaPylonComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("dystopia-pylon-examine", ("links", ent.Comp.Links.Count),
            ("max", ent.Comp.MaxLinks), ("span", (int) ent.Comp.MaxSpan)));
    }
}
