// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: нефтепровод — два терминала, связанные закопанной трубой. Стопка секций: тычок в терминал — конец
// закреплён, тычок во второй — трасса проложена по прямой (до MaxLength клеток, одна секция на MarkerSpacing клеток,
// не через космос), вдоль неё колодцы. Насос отправляющего терминала (на токе) гонит Rate ед/с в бак принимающего.
// Колодец с уроном ≥ LeakDamage течёт (доля перекачки — лужей на его клетке), ≥ BreakDamage или уничтожен — труба
// порвана: всё, что качают, льётся там. Чинят сваркой. Альтернативное действие — разобрать трассу (секции вернутся).

using System.Linq;
using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaPipelineSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaPipelineTerminalComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaPipelineTerminalComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaPipelineTerminalComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaPipelineTerminalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DystopiaPipelineTerminalComponent, BoundUIOpenedEvent>(OnOpened);
        Subs.BuiEvents<DystopiaPipelineTerminalComponent>(DystopiaPipelineUiKey.Key, subs =>
        {
            subs.Event<DystopiaPipelineModeMessage>(OnMode);
            subs.Event<DystopiaPipelineRateMessage>(OnRate);
        });
    }

    // ------------------------------------------------------------------ прокладка

    private ProtoId<StackPrototype>? SectionStackType(DystopiaPipelineTerminalComponent terminal)
    {
        return _proto.TryIndex(terminal.SectionStack, out var proto) &&
               proto.TryGetComponent<StackComponent>(out var stack, EntityManager.ComponentFactory)
            ? (ProtoId<StackPrototype>?) stack.StackTypeId
            : null;
    }

    /// <summary>Центр клетки с полом или грунтом под точкой карты — на любой сетке (сетка-фундамент или сама
    /// планета). Нет такой клетки — значит, там пустота.</summary>
    private bool TryGroundAt(MapId mapId, Vector2 world, out EntityCoordinates spot)
    {
        spot = default;
        if (!_map.TryFindGridAt(mapId, world, out var gridUid, out var grid))
            return false;

        var tile = _map.WorldToTile(gridUid, grid, world);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        spot = _map.GridTileToLocal(gridUid, grid, tile);
        return true;
    }

    private void OnInteractUsing(Entity<DystopiaPipelineTerminalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DystopiaLinkerComponent>(args.Used, out var linker) ||
            !TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != SectionStackType(ent.Comp))
        {
            return;
        }

        args.Handled = true;
        var first = linker.First;
        if (first == null || first == ent.Owner || TerminatingOrDeleted(first.Value) ||
            !TryComp<DystopiaPipelineTerminalComponent>(first.Value, out var other))
        {
            if (ent.Comp.Linked != null)
            {
                _popup.PopupEntity(Loc.GetString("dystopia-pipeline-already"), ent, args.User);
                return;
            }

            linker.First = ent;
            _popup.PopupEntity(Loc.GetString("dystopia-pipeline-first"), ent, args.User);
            return;
        }

        linker.First = null;
        if (ent.Comp.Linked != null || other.Linked != null)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pipeline-already"), ent, args.User);
            return;
        }

        // трасса может идти через несколько сеток (фундамент и грунт планеты): считаем в координатах карты
        var pa = _xform.GetMapCoordinates(first.Value);
        var pb = _xform.GetMapCoordinates(ent);
        if (pa.MapId != pb.MapId)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pipeline-other-grid"), ent, args.User);
            return;
        }

        var length = (pb.Position - pa.Position).Length();
        if (length > other.MaxLength)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pipeline-too-far", ("length", (int) MathF.Ceiling(length)),
                ("max", (int) other.MaxLength)), ent, args.User);
            return;
        }

        // не через космос: по всей прямой под трубой — пол или грунт какой-нибудь сетки
        var steps = Math.Max(1, (int) MathF.Ceiling(length * 2f));
        for (var i = 0; i <= steps; i++)
        {
            if (!TryGroundAt(pa.MapId, Vector2.Lerp(pa.Position, pb.Position, i / (float) steps), out _))
            {
                _popup.PopupEntity(Loc.GetString("dystopia-pipeline-gap"), ent, args.User);
                return;
            }
        }

        var spacing = Math.Max(1, other.MarkerSpacing);
        var need = Math.Max(1, (int) MathF.Ceiling(length / spacing));
        if (!_stack.TryUse((args.Used, stack), need))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-pipeline-not-enough", ("need", need)), ent, args.User);
            return;
        }

        // трассу ведёт первый терминал (колодцы — от него к второму)
        other.Linked = ent;
        other.Sections = need;
        other.Markers.Clear();
        other.MarkerSpots.Clear();
        ent.Comp.Linked = first.Value;
        ent.Comp.Sections = 0;
        ent.Comp.Markers.Clear();
        ent.Comp.MarkerSpots.Clear();
        var dir = length > 0f ? (pb.Position - pa.Position) / length : Vector2.Zero;
        for (var d = (float) spacing; d < length - 0.5f; d += spacing)
        {
            if (!TryGroundAt(pa.MapId, pa.Position + dir * d, out var spot))
                continue;

            var marker = Spawn(other.Marker, spot);
            EnsureComp<DystopiaPipelineMarkerComponent>(marker).Terminal = first.Value;
            other.Markers.Add(marker);
            other.MarkerSpots.Add(spot);
        }

        _popup.PopupEntity(Loc.GetString("dystopia-pipeline-linked", ("length", (int) MathF.Ceiling(length)),
            ("sections", need)), ent, args.User);
    }

    /// <summary>Терминал, что вёл трассу (у него колодцы и секции).</summary>
    private Entity<DystopiaPipelineTerminalComponent>? Builder(Entity<DystopiaPipelineTerminalComponent> ent)
    {
        if (ent.Comp.Sections > 0 || ent.Comp.Markers.Count > 0)
            return ent;

        return ent.Comp.Linked is { } other && TryComp<DystopiaPipelineTerminalComponent>(other, out var comp)
            ? (other, comp)
            : null;
    }

    private void Unlink(Entity<DystopiaPipelineTerminalComponent> ent, bool refund, EntityUid? user)
    {
        var builder = Builder(ent);
        if (builder is { } b)
        {
            foreach (var marker in b.Comp.Markers)
            {
                if (!TerminatingOrDeleted(marker))
                    QueueDel(marker);
            }

            if (refund && b.Comp.Sections > 0)
            {
                var stack = Spawn(b.Comp.SectionStack, Transform(ent).Coordinates);
                _stack.SetCount(new Entity<StackComponent?>(stack, null), b.Comp.Sections);
            }

            b.Comp.Markers.Clear();
            b.Comp.MarkerSpots.Clear();
            b.Comp.Sections = 0;
        }

        if (ent.Comp.Linked is { } other && TryComp<DystopiaPipelineTerminalComponent>(other, out var otherComp))
        {
            otherComp.Linked = null;
            otherComp.Mode = DystopiaPipelineMode.Stop;
        }

        ent.Comp.Linked = null;
        ent.Comp.Mode = DystopiaPipelineMode.Stop;
        if (user != null)
            _popup.PopupEntity(Loc.GetString("dystopia-pipeline-unlinked"), ent, user.Value);
    }

    private void OnShutdown(Entity<DystopiaPipelineTerminalComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Linked != null)
            Unlink(ent, false, null);
    }

    private void OnGetVerbs(Entity<DystopiaPipelineTerminalComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Linked == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-pipeline-verb-unlink"),
            Act = () => Unlink(ent, true, user),
        });
    }

    // ------------------------------------------------------------------ перекачка

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        const float dt = 1f;
        _nextTick = now + TimeSpan.FromSeconds(dt);

        var query = EntityQueryEnumerator<DystopiaPipelineTerminalComponent>();
        while (query.MoveNext(out var uid, out var terminal))
        {
            terminal.LastFlow = 0f;
            if (terminal.Mode == DystopiaPipelineMode.Send)
                Pump((uid, terminal), dt);

            UpdateLeaks((uid, terminal));
            _appearance.SetData(uid, DystopiaPipelineVisuals.Mode, (int) terminal.Mode);
            _appearance.SetData(uid, DystopiaPipelineVisuals.Leak, terminal.Leaks > 0 || terminal.Broken);
            if (_ui.IsUiOpen(uid, DystopiaPipelineUiKey.Key))
                UpdateUi((uid, terminal));
        }
    }

    private float MarkerDamage(EntityUid marker)
    {
        if (!TryComp<DamageableComponent>(marker, out var damageable))
            return 0f;

#pragma warning disable CS0618 // здесь урон колодца — это и есть состояние трубы
        return _damageable.GetTotalDamage((marker, damageable)).Float();
#pragma warning restore CS0618
    }

    /// <summary>Сколько колодцев течёт и порвана ли труба (для меню и вида — с обоих концов).</summary>
    private void UpdateLeaks(Entity<DystopiaPipelineTerminalComponent> ent)
    {
        ent.Comp.Leaks = 0;
        ent.Comp.Broken = false;
        if (Builder(ent) is not { } b)
            return;

        foreach (var marker in b.Comp.Markers)
        {
            if (TerminatingOrDeleted(marker))
            {
                ent.Comp.Broken = true;
                continue;
            }

            var damage = MarkerDamage(marker);
            if (damage >= b.Comp.BreakDamage)
                ent.Comp.Broken = true;
            else if (damage >= b.Comp.LeakDamage)
                ent.Comp.Leaks++;
        }
    }

    private void Pump(Entity<DystopiaPipelineTerminalComponent> ent, float dt)
    {
        if (ent.Comp.Linked is not { } destUid || TerminatingOrDeleted(destUid) ||
            !TryComp<DystopiaPipelineTerminalComponent>(destUid, out var dest) || dest.Mode != DystopiaPipelineMode.Receive ||
            !_power.IsPowered(ent.Owner) ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var srcEnt, out var src) ||
            !_solution.TryGetSolution(destUid, dest.Solution, out var dstEnt, out var dst))
        {
            return;
        }

        var amount = FixedPoint2.Min(FixedPoint2.New(MathF.Max(0f, ent.Comp.Rate) * dt),
            FixedPoint2.Min(src.Volume, dst.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
            return;

        var moving = _solution.SplitSolution(srcEnt.Value, amount);
        if (Builder(ent) is { } b && b.Comp.Markers.Count > 0)
        {
            // колодцы по ходу потока: от отправителя
            var order = Enumerable.Range(0, b.Comp.Markers.Count);
            if (b.Owner != ent.Owner)
                order = order.Reverse();

            foreach (var i in order)
            {
                var marker = b.Comp.Markers[i];
                if (i >= b.Comp.MarkerSpots.Count || !b.Comp.MarkerSpots[i].IsValid(EntityManager))
                    continue;

                var coords = b.Comp.MarkerSpots[i];
                var damage = TerminatingOrDeleted(marker) ? float.MaxValue : MarkerDamage(marker);
                if (damage >= b.Comp.BreakDamage)
                {
                    _puddle.TrySpillAt(coords, moving, out _, sound: false);
                    moving = new Solution();
                    break;
                }

                if (damage >= b.Comp.LeakDamage && moving.Volume > FixedPoint2.Zero)
                {
                    var leak = moving.SplitSolution(moving.Volume * b.Comp.LeakShare);
                    _puddle.TrySpillAt(coords, leak, out _, sound: false);
                }
            }
        }

        ent.Comp.LastFlow = moving.Volume.Float() / dt;
        if (moving.Volume > FixedPoint2.Zero)
            _solution.TryAddSolution(dstEnt.Value, moving);
    }

    // ------------------------------------------------------------------ меню и осмотр

    private void OnOpened(Entity<DystopiaPipelineTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateLeaks(ent);
        UpdateUi(ent);
    }

    private void OnMode(Entity<DystopiaPipelineTerminalComponent> ent, ref DystopiaPipelineModeMessage args)
    {
        ent.Comp.Mode = args.Mode;
        // второй конец — встречный режим
        if (ent.Comp.Linked is { } other && TryComp<DystopiaPipelineTerminalComponent>(other, out var otherComp))
        {
            otherComp.Mode = args.Mode switch
            {
                DystopiaPipelineMode.Send => DystopiaPipelineMode.Receive,
                DystopiaPipelineMode.Receive => DystopiaPipelineMode.Send,
                _ => DystopiaPipelineMode.Stop,
            };
        }

        UpdateUi(ent);
    }

    private void OnRate(Entity<DystopiaPipelineTerminalComponent> ent, ref DystopiaPipelineRateMessage args)
    {
        if (!float.IsFinite(args.Rate))
            return;

        ent.Comp.Rate = Math.Clamp(args.Rate, 0f, ent.Comp.MaxRate);
        if (ent.Comp.Linked is { } other && TryComp<DystopiaPipelineTerminalComponent>(other, out var otherComp))
            otherComp.Rate = Math.Clamp(args.Rate, 0f, otherComp.MaxRate);
        UpdateUi(ent);
    }

    private int Length(Entity<DystopiaPipelineTerminalComponent> ent)
    {
        if (ent.Comp.Linked is not { } other || TerminatingOrDeleted(other))
            return 0;

        return (int) MathF.Ceiling((_xform.GetWorldPosition(ent) - _xform.GetWorldPosition(other)).Length());
    }

    private void UpdateUi(Entity<DystopiaPipelineTerminalComponent> ent)
    {
        var volume = 0f;
        var max = 0f;
        var contents = string.Empty;
        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var sol))
        {
            volume = sol.Volume.Float();
            max = sol.MaxVolume.Float();
            contents = string.Join(", ", sol.Contents.OrderByDescending(r => r.Quantity).Take(3).Select(r =>
                $"{(_proto.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(r.Reagent.Prototype, out var p) ? p.LocalizedName : r.Reagent.Prototype)} {r.Quantity.Int()}"));
        }

        // поток показываем с отправителя
        var flow = ent.Comp.LastFlow;
        if (ent.Comp.Linked is { } other && TryComp<DystopiaPipelineTerminalComponent>(other, out var otherComp))
            flow = MathF.Max(flow, otherComp.LastFlow);

        _ui.SetUiState(ent.Owner, DystopiaPipelineUiKey.Key, new DystopiaPipelineUiState(ent.Comp.Linked != null,
            Length(ent), ent.Comp.Mode, ent.Comp.Rate, ent.Comp.MaxRate, volume, max, contents, flow, ent.Comp.Leaks,
            ent.Comp.Broken, _power.IsPowered(ent.Owner)));
    }

    private void OnExamined(Entity<DystopiaPipelineTerminalComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(ent.Comp.Linked == null
            ? Loc.GetString("dystopia-pipeline-examine-unlinked")
            : Loc.GetString("dystopia-pipeline-examine-linked", ("length", Length(ent)),
                ("mode", Loc.GetString($"dystopia-pipeline-mode-{ent.Comp.Mode.ToString().ToLowerInvariant()}"))));
        if (ent.Comp.Broken)
            args.PushMarkup(Loc.GetString("dystopia-pipeline-examine-broken"));
        else if (ent.Comp.Leaks > 0)
            args.PushMarkup(Loc.GetString("dystopia-pipeline-examine-leaks", ("leaks", ent.Comp.Leaks)));
    }
}
