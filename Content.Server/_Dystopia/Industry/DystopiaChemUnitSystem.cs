// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: химическая установка. Буфер не реагирует; раз в цикл, пока включена и есть ток, установка берёт из буфера
// сырьё выбранной реакции в её пропорциях (не больше UnitsPerCycle за цикл и не больше, чем влезет готового),
// катализаторы оставляет на месте, готовое — в бак продукции. Остальное в буфере не трогает.
// Меню: слот «Контейнер» (любая ёмкость с раствором) — переливание между ним, буфером и баком продукции, вылить.

using Content.Server.Power.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Audio;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed class DystopiaChemUnitSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaChemUnitComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaChemUnitComponent, BoundUIOpenedEvent>(OnUiOpened);
        Subs.BuiEvents<DystopiaChemUnitComponent>(DystopiaChemUnitUiKey.Key, subs =>
        {
            subs.Event<DystopiaChemUnitToggleMessage>(OnToggle);
            subs.Event<DystopiaChemUnitSelectMessage>(OnSelect);
            subs.Event<DystopiaChemUnitTransferMessage>(OnTransfer);
            subs.Event<DystopiaChemUnitEjectMessage>(OnEject);
        });
        SubscribeLocalEvent<DystopiaChemUnitComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<DystopiaChemUnitComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaChemUnitComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var unit, out var xform))
        {
            if (now < unit.NextCycle)
                continue;

            unit.NextCycle = now + TimeSpan.FromSeconds(unit.CycleTime);

            var working = xform.Anchored && unit.On && _power.IsPowered(uid) && Cycle((uid, unit));
            SetWorking((uid, unit), working);

            if (_ui.IsUiOpen(uid, DystopiaChemUnitUiKey.Key))
                UpdateUi((uid, unit));
        }
    }

    private void SetWorking(Entity<DystopiaChemUnitComponent> ent, bool working)
    {
        if (ent.Comp.Working == working)
            return;

        ent.Comp.Working = working;
        _appearance.SetData(ent, DystopiaProcessorVisuals.Running, working);
        _ambient.SetAmbience(ent, working);
    }

    /// <summary>Один цикл: сколько порций реакции можно провести — столько и провести.</summary>
    private bool Cycle(Entity<DystopiaChemUnitComponent> ent)
    {
        if (ent.Comp.Reaction is not { } id ||
            !_proto.TryIndex(id, out var reaction) ||
            !DystopiaChemUnit.Allowed(reaction) ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.Buffer, out var bufferEnt, out var buffer) ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.Output, out var outputEnt, out var output))
        {
            return false;
        }

        // Сколько порций есть в буфере.
        var portions = DystopiaChemUnit.Portions(reaction, r => buffer.GetTotalPrototypeQuantity(r));

        // Не больше UnitsPerCycle сырья за цикл.
        var perPortionIn = 0f;
        foreach (var info in reaction.Reactants.Values)
        {
            if (!info.Catalyst)
                perPortionIn += info.Amount.Float();
        }

        if (perPortionIn > 0f)
            portions = MathF.Min(portions, ent.Comp.UnitsPerCycle / perPortionIn);

        // И не больше, чем влезет в бак продукции.
        var perPortionOut = 0f;
        foreach (var amount in reaction.Products.Values)
            perPortionOut += amount.Float();

        if (perPortionOut > 0f)
            portions = MathF.Min(portions, output.AvailableVolume.Float() / perPortionOut);

        if (reaction.Quantized)
            portions = MathF.Floor(portions);

        if (portions < 0.01f)
            return false;

        foreach (var (reagent, info) in reaction.Reactants)
        {
            if (info.Catalyst)
                continue;

            _solution.RemoveReagent(bufferEnt.Value, reagent.Id, FixedPoint2.New(info.Amount.Float() * portions));
        }

        var products = new Solution();
        foreach (var (reagent, amount) in reaction.Products)
            products.AddReagent(new ReagentId(reagent, null), FixedPoint2.New(amount.Float() * portions));

        products.Temperature = buffer.Temperature;
        _solution.TryAddSolution(outputEnt.Value, products);
        return true;
    }

    // ------------------------------------------------------------------ меню

    private void OnUiOpened(Entity<DystopiaChemUnitComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnToggle(Entity<DystopiaChemUnitComponent> ent, ref DystopiaChemUnitToggleMessage args)
    {
        ent.Comp.On = !ent.Comp.On;
        ent.Comp.NextCycle = TimeSpan.Zero;
        if (!ent.Comp.On)
            SetWorking(ent, false);

        UpdateUi(ent);
    }

    private void OnSelect(Entity<DystopiaChemUnitComponent> ent, ref DystopiaChemUnitSelectMessage args)
    {
        // Клиенту на слово не верим: только реакция, которую установка умеет вести.
        if (args.Reaction == null)
        {
            ent.Comp.Reaction = null;
        }
        else if (_proto.TryIndex<ReactionPrototype>(args.Reaction, out var reaction) && DystopiaChemUnit.Allowed(reaction))
        {
            ent.Comp.Reaction = reaction.ID;
        }
        else
        {
            return;
        }

        ent.Comp.NextCycle = TimeSpan.Zero;
        UpdateUi(ent);
    }

    private void OnContainerChanged<T>(Entity<DystopiaChemUnitComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        if (args.Container.ID == ent.Comp.ContainerSlot)
            UpdateUi(ent);
    }

    private void OnEject(Entity<DystopiaChemUnitComponent> ent, ref DystopiaChemUnitEjectMessage args)
    {
        if (_itemSlots.TryGetSlot(ent.Owner, ent.Comp.ContainerSlot, out var slot))
            _itemSlots.TryEjectToHands(ent, slot, args.Actor);

        UpdateUi(ent);
    }

    /// <summary>Ёмкость в слоте.</summary>
    private EntityUid? Container(Entity<DystopiaChemUnitComponent> ent)
    {
        return _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.ContainerSlot);
    }

    /// <summary>Раствор бака: из ёмкости берём то, что из неё сливается, а льём в то, что в неё наливается.</summary>
    private Entity<SolutionComponent>? Tank(Entity<DystopiaChemUnitComponent> ent, DystopiaChemUnitTank tank, bool pourInto)
    {
        switch (tank)
        {
            case DystopiaChemUnitTank.Buffer:
                return _solution.TryGetSolution(ent.Owner, ent.Comp.Buffer, out var buffer, out _) ? buffer : null;
            case DystopiaChemUnitTank.Output:
                return _solution.TryGetSolution(ent.Owner, ent.Comp.Output, out var output, out _) ? output : null;
            case DystopiaChemUnitTank.Container:
                if (Container(ent) is not { } item)
                    return null;

                if (pourInto)
                {
                    if (_solution.TryGetRefillableSolution(item, out var refill, out _))
                        return refill;
                    if (_solution.TryGetDrainableSolution(item, out var drainAsRefill, out _))
                        return drainAsRefill;
                }
                else
                {
                    if (_solution.TryGetDrainableSolution(item, out var drain, out _))
                        return drain;
                    if (_solution.TryGetRefillableSolution(item, out var refillAsDrain, out _))
                        return refillAsDrain;
                }

                return null;
        }

        return null;
    }

    private void OnTransfer(Entity<DystopiaChemUnitComponent> ent, ref DystopiaChemUnitTransferMessage args)
    {
        if (args.From == DystopiaChemUnitTank.Discard || args.From == args.To ||
            !_proto.HasIndex<ReagentPrototype>(args.Reagent) || !float.IsFinite(args.Amount) ||
            Tank(ent, args.From, false) is not { } source)
        {
            return;
        }

        var have = source.Comp.Solution.GetTotalPrototypeQuantity(args.Reagent);
        var amount = args.Amount > 0f ? FixedPoint2.Min(have, FixedPoint2.New(args.Amount)) : have;

        if (args.To == DystopiaChemUnitTank.Discard)
        {
            if (amount > FixedPoint2.Zero)
                _solution.RemoveReagent(source, args.Reagent, amount);

            UpdateUi(ent);
            return;
        }

        if (Tank(ent, args.To, true) is not { } target)
            return;

        amount = FixedPoint2.Min(amount, target.Comp.Solution.AvailableVolume);
        if (amount <= FixedPoint2.Zero)
            return;

        var moved = source.Comp.Solution.SplitSolutionWithOnly(amount, args.Reagent);
        _solution.UpdateChemicals(source, false);
        _solution.TryAddSolution(target, moved);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DystopiaChemUnitComponent> ent)
    {
        var buffer = new List<DystopiaChemUnitReagent>();
        var output = new List<DystopiaChemUnitReagent>();
        var bufferMax = 0f;
        var outputMax = 0f;
        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Buffer, out _, out var bufferSol))
        {
            bufferMax = bufferSol.MaxVolume.Float();
            foreach (var quantity in bufferSol.Contents)
                buffer.Add(new DystopiaChemUnitReagent(quantity.Reagent.Prototype, quantity.Quantity.Float()));
        }

        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Output, out _, out var outputSol))
        {
            outputMax = outputSol.MaxVolume.Float();
            foreach (var quantity in outputSol.Contents)
                output.Add(new DystopiaChemUnitReagent(quantity.Reagent.Prototype, quantity.Quantity.Float()));
        }

        string? containerName = null;
        var containerReagents = new List<DystopiaChemUnitReagent>();
        var containerMax = 0f;
        if (Container(ent) is { } item)
        {
            containerName = Name(item);
            if (Tank(ent, DystopiaChemUnitTank.Container, false) is { } containerSol)
            {
                containerMax = containerSol.Comp.Solution.MaxVolume.Float();
                foreach (var quantity in containerSol.Comp.Solution.Contents)
                    containerReagents.Add(new DystopiaChemUnitReagent(quantity.Reagent.Prototype, quantity.Quantity.Float()));
            }
        }

        _ui.SetUiState(ent.Owner, DystopiaChemUnitUiKey.Key, new DystopiaChemUnitUiState(ent.Comp.On,
            _power.IsPowered(ent), ent.Comp.Working, ent.Comp.Reaction?.Id, ent.Comp.UnitsPerCycle,
            ent.Comp.CycleTime, buffer, bufferMax, output, outputMax, containerName, containerReagents, containerMax));
    }

    // ------------------------------------------------------------------ осмотр

    private void OnExamined(Entity<DystopiaChemUnitComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var name = ent.Comp.Reaction is { } id && _proto.TryIndex(id, out var reaction)
            ? ReactionName(reaction)
            : Loc.GetString("dystopia-chem-unit-nothing");
        args.PushMarkup(Loc.GetString("dystopia-chem-unit-examine", ("reaction", name)));
    }

    /// <summary>Название реакции — по её продуктам.</summary>
    public string ReactionName(ReactionPrototype reaction)
    {
        var names = new List<string>();
        foreach (var product in reaction.Products.Keys)
            names.Add(_proto.TryIndex(product, out var proto) ? proto.LocalizedName : product.Id);

        return string.Join(", ", names);
    }
}
