// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: дуговая сталеплавильная печь (ДСП). Плавка — тигель на той же сущности (DystopiaCrucibleSystem: загрузка
// кликом и конвейерным вставщиком, плавка, сплавы). Печь: греет от сети, пока включена; сама снимает шлак кусками в
// свой контейнер (его разгружает извлекатель или кнопка в меню); разливает в подведённый к корпусу литейный желоб
// только выбранный сплав и только жидкий.

using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Audio;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaArcFurnaceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private DystopiaCrucibleSystem _crucible = default!;
    [Dependency] private DystopiaCastingChannelSystem _channel = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaArcFurnaceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DystopiaArcFurnaceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaArcFurnaceComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<DystopiaArcFurnaceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaArcFurnaceComponent, DystopiaConveyorExtractEvent>(OnConveyorExtract);

        Subs.BuiEvents<DystopiaArcFurnaceComponent>(DystopiaArcFurnaceUiKey.Key, subs =>
        {
            subs.Event<DystopiaArcFurnaceToggleMessage>(OnToggle);
            subs.Event<DystopiaArcFurnaceSelectMessage>(OnSelect);
            subs.Event<DystopiaArcFurnaceEjectSlagMessage>(OnEjectSlag);
        });
    }

    private void OnInit(Entity<DystopiaArcFurnaceComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.SlagContainer);
    }

    private void OnMapInit(Entity<DystopiaArcFurnaceComponent> ent, ref MapInitEvent args)
    {
        _power.SetLoad(ent.Owner, ent.Comp.On ? ent.Comp.PowerLoad : ent.Comp.IdlePowerLoad);
    }

    // ------------------------------------------------------------------ меню

    private void OnUiOpened(Entity<DystopiaArcFurnaceComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnToggle(Entity<DystopiaArcFurnaceComponent> ent, ref DystopiaArcFurnaceToggleMessage args)
    {
        ent.Comp.On = !ent.Comp.On;
        _power.SetLoad(ent.Owner, ent.Comp.On ? ent.Comp.PowerLoad : ent.Comp.IdlePowerLoad);
        UpdateUi(ent);
    }

    private void OnSelect(Entity<DystopiaArcFurnaceComponent> ent, ref DystopiaArcFurnaceSelectMessage args)
    {
        if (args.Reagent == null)
        {
            ent.Comp.Selected = null;
        }
        else
        {
            // Только то, что печи разрешено разливать.
            foreach (var castable in ent.Comp.Castable)
            {
                if (castable.Id == args.Reagent)
                    ent.Comp.Selected = castable;
            }
        }

        UpdateUi(ent);
    }

    private void OnEjectSlag(Entity<DystopiaArcFurnaceComponent> ent, ref DystopiaArcFurnaceEjectSlagMessage args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.SlagContainer, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return;
        }

        _container.EmptyContainer(container, destination: Transform(args.Actor).Coordinates);
        _popup.PopupEntity(Loc.GetString("dystopia-arc-furnace-slag-ejected"), ent, args.Actor);
        UpdateUi(ent);
    }

    private int SlagLumps(Entity<DystopiaArcFurnaceComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.SlagContainer, out var container))
            return 0;

        var total = 0;
        foreach (var item in container.ContainedEntities)
        {
            if (!EntityManager.IsQueuedForDeletion(item))
                total += _stack.GetCount(item);
        }

        return total;
    }

    private int ChargeMaterial(DystopiaCrucibleComponent crucible, EntityUid uid)
    {
        if (!_container.TryGetContainer(uid, crucible.ChargeContainer, out var container))
            return 0;

        var total = 0;
        foreach (var item in container.ContainedEntities)
        {
            if (!TryComp<PhysicalCompositionComponent>(item, out var composition))
                continue;

            var perUnit = 0;
            foreach (var amount in composition.MaterialComposition.Values)
            {
                perUnit += amount;
            }

            total += perUnit * _stack.GetCount(item);
        }

        return total;
    }

    private void UpdateUi(Entity<DystopiaArcFurnaceComponent> ent)
    {
        if (!TryComp<DystopiaCrucibleComponent>(ent, out var crucible))
            return;

        var state = new DystopiaArcFurnaceUiState
        {
            On = ent.Comp.On,
            Powered = _power.IsPowered(ent.Owner),
            ChannelAttached = _channel.FindAttachedChannel(ent, ent.Comp.Footprint) != null,
            Temperature = crucible.Temperature - DystopiaCrucibleSystem.KelvinOffset,
            MaxTemperature = ent.Comp.MaxTemperature - DystopiaCrucibleSystem.KelvinOffset,
            Charge = ChargeMaterial(crucible, ent),
            MaxCharge = crucible.MaxCharge,
            Selected = ent.Comp.Selected?.Id,
            Slag = SlagLumps(ent),
            SlagCapacity = ent.Comp.SlagCapacity,
        };

        foreach (var castable in ent.Comp.Castable)
        {
            state.Castable.Add(castable.Id);
        }

        if (_solution.TryGetSolution(ent.Owner, crucible.Solution, out _, out var solution))
        {
            state.Volume = (float) solution.Volume;
            state.MaxVolume = (float) solution.MaxVolume;
            foreach (var reagent in solution.Contents)
            {
                state.Contents.Add(new DystopiaArcFurnaceReagent(reagent.Reagent.Prototype, (float) reagent.Quantity));
            }
        }

        _ui.SetUiState(ent.Owner, DystopiaArcFurnaceUiKey.Key, state);
    }

    // ------------------------------------------------------------------ работа

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaArcFurnaceComponent, DystopiaCrucibleComponent>();
        while (query.MoveNext(out var uid, out var furnace, out var crucible))
        {
            if (now < furnace.NextUpdate)
                continue;

            furnace.NextUpdate = now + TimeSpan.FromSeconds(furnace.UpdateInterval);
            Entity<DystopiaArcFurnaceComponent> ent = (uid, furnace);

            var working = furnace.On && _power.IsPowered(uid);
            if (working)
            {
                _crucible.ApplyHeat((uid, crucible), furnace.MaxTemperature, furnace.HeatRate,
                    now + TimeSpan.FromSeconds(furnace.UpdateInterval * 2));
            }

            _appearance.SetData(uid, DystopiaArcFurnaceVisuals.Working, working);
            if (_light.TryGetLight(uid, out var light))
                _light.SetEnabled(uid, working, light);
            _ambient.SetAmbience(uid, working);

            SkimSlag(ent, crucible);
            if (working)
                Pour(ent, crucible);

            if (_ui.IsUiOpen(uid, DystopiaArcFurnaceUiKey.Key))
                UpdateUi(ent);
        }
    }

    /// <summary>Снять шлак целыми кусками в свой контейнер (пока есть место).</summary>
    private void SkimSlag(Entity<DystopiaArcFurnaceComponent> ent, DystopiaCrucibleComponent crucible)
    {
        if (!_solution.TryGetSolution(ent.Owner, crucible.Solution, out var soln, out var solution) ||
            !_container.TryGetContainer(ent, ent.Comp.SlagContainer, out var container))
        {
            return;
        }

        var slag = solution.GetTotalPrototypeQuantity(crucible.SlagReagent);
        var lumps = Math.Min((int) MathF.Floor((float) (slag / crucible.SlagPerLump)), ent.Comp.SlagCapacity - SlagLumps(ent));
        if (lumps <= 0)
            return;

        _solution.RemoveReagent(soln.Value, crucible.SlagReagent.Id, crucible.SlagPerLump * lumps);
        foreach (var spawned in _stack.SpawnMultipleAtPosition(crucible.SlagLump, lumps, Transform(ent).Coordinates))
        {
            foreach (var existing in container.ContainedEntities)
            {
                if (EntityManager.IsQueuedForDeletion(spawned))
                    break;

                _stack.TryMergeStacks(spawned, existing, out _);
            }

            if (!EntityManager.IsQueuedForDeletion(spawned))
                _container.Insert(spawned, container);
        }
    }

    /// <summary>Разлить выбранный сплав (только его и только жидкий) в подведённый желоб.</summary>
    private void Pour(Entity<DystopiaArcFurnaceComponent> ent, DystopiaCrucibleComponent crucible)
    {
        if (ent.Comp.Selected is not { } selected ||
            !_proto.TryIndex(selected, out var reagent) ||
            reagent.MeltingPoint is { } melting && crucible.Temperature < melting + DystopiaCrucibleSystem.KelvinOffset ||
            !_solution.TryGetSolution(ent.Owner, crucible.Solution, out var soln, out var solution) ||
            solution.GetTotalPrototypeQuantity(selected) <= FixedPoint2.Zero ||
            _channel.FindAttachedChannel(ent, ent.Comp.Footprint) is not { } channel)
        {
            return;
        }

        _channel.PourReagent(ent, soln.Value, selected, FixedPoint2.New(ent.Comp.PourAmount), channel);
    }

    // ------------------------------------------------------------------ шлак — извлекателю

    private void OnConveyorExtract(Entity<DystopiaArcFurnaceComponent> ent, ref DystopiaConveyorExtractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!_container.TryGetContainer(ent, ent.Comp.SlagContainer, out var container))
            return;

        foreach (var item in container.ContainedEntities)
        {
            if (EntityManager.IsQueuedForDeletion(item) || !args.Accept(item))
                continue;

            if (_container.Remove(item, container))
                args.Item = item;
            return;
        }
    }

    private void OnExamined(Entity<DystopiaArcFurnaceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.On ? "dystopia-arc-furnace-examine-on" : "dystopia-arc-furnace-examine-off"));
        if (ent.Comp.Selected is { } selected && _proto.TryIndex(selected, out var reagent))
            args.PushMarkup(Loc.GetString("dystopia-arc-furnace-examine-selected", ("alloy", reagent.LocalizedName)));

        var slag = SlagLumps(ent);
        if (slag > 0)
            args.PushMarkup(Loc.GetString("dystopia-arc-furnace-examine-slag", ("count", slag)));
    }
}
