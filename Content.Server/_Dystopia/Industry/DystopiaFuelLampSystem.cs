// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: топливные лампы — горение, заправка кусками, вешание на стену и снятие (см. DystopiaFuelLampComponents).

using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Content.Shared.Wall;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaFuelLampSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFuelLampComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaFuelLampComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DystopiaFuelLampComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<DystopiaFuelLampComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaFuelLampComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerbs);
        SubscribeLocalEvent<DystopiaFuelLampComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<DystopiaFuelLampComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<DystopiaFuelLampComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent);
    }

    // ------------------------------------------------------------------ включить / выключить

    private void OnUseInHand(Entity<DystopiaFuelLampComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        Toggle(ent, args.User);
        args.Handled = true;
    }

    /// <summary>Настенную лампу включают щелчком (ручную на полу щелчок поднимает).</summary>
    private void OnActivate(Entity<DystopiaFuelLampComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || ent.Comp.ItemPrototype == null)
            return;

        Toggle(ent, args.User);
        args.Handled = true;
    }

    private void Toggle(Entity<DystopiaFuelLampComponent> ent, EntityUid user)
    {
        if (!ent.Comp.On && FuelSeconds(ent) <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-lamp-no-fuel"), ent, user);
            return;
        }

        SetOn(ent, !ent.Comp.On);
    }

    private void SetOn(Entity<DystopiaFuelLampComponent> ent, bool on)
    {
        ent.Comp.On = on;
        ent.Comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Interval);
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<DystopiaFuelLampComponent> ent)
    {
        _appearance.SetData(ent, DystopiaFuelLampVisuals.On, ent.Comp.On);
        _light.SetEnabled(ent, ent.Comp.On);
        if (HasComp<ItemComponent>(ent))
            _item.SetHeldPrefix(ent, ent.Comp.On ? "on" : "off");
    }

    // ------------------------------------------------------------------ горение

    /// <summary>На сколько секунд горения хватит топлива.</summary>
    private float FuelSeconds(Entity<DystopiaFuelLampComponent> ent)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return 0f;

        var seconds = 0f;
        foreach (var (reagent, perUnit) in ent.Comp.Fuels)
        {
            seconds += solution.GetTotalPrototypeQuantity(reagent).Float() * perUnit;
        }

        return seconds;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaFuelLampComponent>();
        while (query.MoveNext(out var uid, out var lamp))
        {
            if (!lamp.On || now < lamp.NextUpdate)
                continue;

            lamp.NextUpdate = now + TimeSpan.FromSeconds(lamp.Interval);
            if (!Burn((uid, lamp)))
                SetOn((uid, lamp), false);
        }
    }

    private bool Burn(Entity<DystopiaFuelLampComponent> ent)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solnEnt, out var solution))
            return false;

        foreach (var (reagent, perUnit) in ent.Comp.Fuels)
        {
            var have = solution.GetTotalPrototypeQuantity(reagent);
            if (have <= FixedPoint2.Zero || perUnit <= 0f)
                continue;

            var units = FixedPoint2.Min(have, FixedPoint2.New(ent.Comp.Interval / perUnit));
            _solution.RemoveReagent(solnEnt.Value, reagent.Id, units);
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------------ заправка кусками (карбид)

    private void OnInteractUsing(Entity<DystopiaFuelLampComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack) ||
            !ent.Comp.LoadItems.TryGetValue(stack.StackTypeId, out var reagents) ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solnEnt, out var solution))
        {
            return;
        }

        args.Handled = true;
        var need = FixedPoint2.Zero;
        foreach (var amount in reagents.Values)
        {
            need += amount;
        }

        if (solution.AvailableVolume < need)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-lamp-full"), ent, args.User);
            return;
        }

        if (!_stack.TryUse((args.Used, stack), 1))
            return;

        foreach (var (reagent, amount) in reagents)
        {
            _solution.TryAddReagent(solnEnt.Value, reagent.Id, amount, out _);
        }

        _popup.PopupEntity(Loc.GetString("dystopia-lamp-loaded"), ent, args.User);
    }

    // ------------------------------------------------------------------ на стену и обратно

    private void OnUtilityVerbs(Entity<DystopiaFuelLampComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using != ent.Owner || ent.Comp.MountedPrototype == null ||
            !HasComp<WallComponent>(args.Target))
        {
            return;
        }

        var user = args.User;
        var wall = args.Target;
        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("dystopia-lamp-verb-mount"),
            Act = () => Mount(ent, wall, user),
            Priority = 2,
        });
    }

    private void OnAltVerbs(Entity<DystopiaFuelLampComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (ent.Comp.ItemPrototype != null)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-lamp-verb-unmount"),
                Act = () => Unmount(ent, user),
                Priority = 2,
            });
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.On ? "dystopia-lamp-verb-off" : "dystopia-lamp-verb-on"),
            Act = () => Toggle(ent, user),
            Priority = 3,
        });
    }

    /// <summary>Повесить: на соседнюю со стеной клетку со стороны игрока, лампа смотрит от стены.</summary>
    private void Mount(Entity<DystopiaFuelLampComponent> ent, EntityUid wall, EntityUid user)
    {
        var wallXform = Transform(wall);
        if (ent.Comp.MountedPrototype is not { } mounted || wallXform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var wallTile = _map.TileIndicesFor(gridUid, grid, wallXform.Coordinates);
        var userTile = _map.TileIndicesFor(gridUid, grid, Transform(user).Coordinates);
        var delta = userTile - wallTile;
        if (delta == Vector2i.Zero)
            return;

        var dir = Angle.FromWorldVec(new System.Numerics.Vector2(delta.X, delta.Y)).GetCardinalDir();
        var tile = wallTile.Offset(dir);
        var rotation = dir.ToAngle();
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<DystopiaFuelLampComponent>(other) &&
                Transform(other).LocalRotation.GetCardinalDir() == dir)
            {
                _popup.PopupEntity(Loc.GetString("dystopia-lamp-occupied"), wall, user);
                return;
            }
        }

        var lamp = EntityManager.SpawnAtPosition(mounted, _map.GridTileToLocal(gridUid, grid, tile), rotation);
        MoveFuel(ent, lamp);
        if (TryComp<DystopiaFuelLampComponent>(lamp, out var lampComp))
            SetOn((lamp, lampComp), ent.Comp.On);

        _popup.PopupEntity(Loc.GetString("dystopia-lamp-mounted"), lamp, user);
        QueueDel(ent);
    }

    private void Unmount(Entity<DystopiaFuelLampComponent> ent, EntityUid user)
    {
        if (ent.Comp.ItemPrototype is not { } itemProto)
            return;

        var item = Spawn(itemProto, Transform(ent).Coordinates);
        MoveFuel(ent, item);
        if (TryComp<DystopiaFuelLampComponent>(item, out var itemComp))
            SetOn((item, itemComp), ent.Comp.On);

        _hands.PickupOrDrop(user, item);
        QueueDel(ent);
    }

    private void MoveFuel(Entity<DystopiaFuelLampComponent> from, EntityUid to)
    {
        if (!TryComp<DystopiaFuelLampComponent>(to, out var toLamp) ||
            !_solution.TryGetSolution(from.Owner, from.Comp.Solution, out var fromSoln, out var fromSolution) ||
            !_solution.TryGetSolution(to, toLamp.Solution, out var toSoln, out _))
        {
            return;
        }

        _solution.RemoveAllSolution(toSoln.Value);
        _solution.TryAddSolution(toSoln.Value, _solution.SplitSolution(fromSoln.Value, fromSolution.Volume));
    }

    private void OnExamined(Entity<DystopiaFuelLampComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var minutes = (int) MathF.Ceiling(FuelSeconds(ent) / 60f);
        args.PushMarkup(minutes > 0
            ? Loc.GetString("dystopia-lamp-examine-fuel", ("minutes", minutes))
            : Loc.GetString("dystopia-lamp-examine-empty"));
    }
}
