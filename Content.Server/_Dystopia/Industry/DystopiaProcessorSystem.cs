// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: машины переработки руды (дробилки ×2, мельницы и обогатители ×3). Ручная работает, пока игрок крутит
// (повторяется, пока не отойдёт); электрические — сами, пока есть сырьё, ток, реагенты и место для продукции.
// За цикл — до BatchesPerCycle партий: 7 кусков ручная и щековая дробилки дробят за 7 циклов, конусная — за 3.
// Продукция: у ручных — на пол, у электрических — в выходной контейнер (его разгружает конвейерный извлекатель).

using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Audio;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaProcessorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SolutionTransferSystem _transfer = default!;
    [Dependency] private PuddleSystem _puddle = default!;

    private static readonly ProtoId<TagPrototype> OreTag = "Ore";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaProcessorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DystopiaProcessorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaProcessorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaProcessorComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<DystopiaProcessorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaProcessorComponent, DystopiaProcessorWorkDoAfterEvent>(OnWork);
        SubscribeLocalEvent<DystopiaProcessorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaProcessorComponent, DystopiaConveyorInsertEvent>(OnConveyorInsert);
        SubscribeLocalEvent<DystopiaProcessorComponent, DystopiaConveyorExtractEvent>(OnConveyorExtract);
    }

    private void OnInit(Entity<DystopiaProcessorComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
        if (ent.Comp.OutputContainer is { } output)
            _container.EnsureContainer<Container>(ent, output);
    }

    private void OnMapInit(Entity<DystopiaProcessorComponent> ent, ref MapInitEvent args)
    {
        SetRunning(ent, false);
    }

    // ------------------------------------------------------------------ рецепты и счёт

    /// <summary>Рецепт группы машины для этого предмета (по его стеку). Null — машина это не берёт.</summary>
    public DystopiaProcessRecipePrototype? GetRecipe(Entity<DystopiaProcessorComponent> ent, EntityUid item)
    {
        if (!TryComp<StackComponent>(item, out var stack))
            return null;

        foreach (var recipe in _proto.EnumeratePrototypes<DystopiaProcessRecipePrototype>())
        {
            if (recipe.Group == ent.Comp.Group && recipe.Input == stack.StackTypeId)
                return recipe;
        }

        return null;
    }

    private int CountUnits(BaseContainer container)
    {
        var total = 0;
        foreach (var item in container.ContainedEntities)
        {
            if (!EntityManager.IsQueuedForDeletion(item))
                total += _stack.GetCount(item);
        }

        return total;
    }

    /// <summary>Сколько единиц сырья в загрузке.</summary>
    public int LoadedUnits(Entity<DystopiaProcessorComponent> ent)
    {
        return _container.TryGetContainer(ent, ent.Comp.Container, out var container) ? CountUnits(container) : 0;
    }

    /// <summary>Сколько единиц продукции внутри.</summary>
    public int OutputUnits(Entity<DystopiaProcessorComponent> ent)
    {
        return ent.Comp.OutputContainer is { } id && _container.TryGetContainer(ent, id, out var container)
            ? CountUnits(container)
            : 0;
    }

    /// <summary>Сырьё в загрузке по типам стопок.</summary>
    private Dictionary<ProtoId<StackPrototype>, int> LoadedByStack(Entity<DystopiaProcessorComponent> ent)
    {
        var result = new Dictionary<ProtoId<StackPrototype>, int>();
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return result;

        foreach (var item in container.ContainedEntities)
        {
            if (EntityManager.IsQueuedForDeletion(item) || !TryComp<StackComponent>(item, out var stack))
                continue;

            result[stack.StackTypeId] = result.GetValueOrDefault(stack.StackTypeId) + stack.Count;
        }

        return result;
    }

    private bool HasReagents(Entity<DystopiaProcessorComponent> ent, DystopiaProcessRecipePrototype recipe,
        Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> reserved)
    {
        if (recipe.Reagents.Count == 0)
            return true;

        if (ent.Comp.Tank is not { } tank || !_solution.TryGetSolution(ent.Owner, tank, out _, out var solution))
            return false;

        foreach (var (reagent, amount) in recipe.Reagents)
        {
            if (solution.GetTotalPrototypeQuantity(reagent) - reserved.GetValueOrDefault(reagent) < amount)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Какие партии можно сделать за цикл прямо сейчас (с учётом сырья, реагентов и места для продукции).
    /// <paramref name="reason"/> — почему ни одной (для сообщения игроку).
    /// </summary>
    private List<DystopiaProcessRecipePrototype> PlanCycle(Entity<DystopiaProcessorComponent> ent, out string? reason)
    {
        reason = null;
        var plan = new List<DystopiaProcessRecipePrototype>();
        var loaded = LoadedByStack(ent);
        if (loaded.Count == 0)
        {
            reason = "dystopia-processor-empty";
            return plan;
        }

        var reserved = new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>();
        var room = ent.Comp.OutputContainer != null ? ent.Comp.OutputCapacity - OutputUnits(ent) : int.MaxValue;
        var noReagents = false;
        var noRoom = false;

        for (var i = 0; i < ent.Comp.BatchesPerCycle; i++)
        {
            DystopiaProcessRecipePrototype? chosen = null;
            foreach (var recipe in _proto.EnumeratePrototypes<DystopiaProcessRecipePrototype>())
            {
                if (recipe.Group != ent.Comp.Group || loaded.GetValueOrDefault(recipe.Input) < recipe.InputCount)
                    continue;

                if (!HasReagents(ent, recipe, reserved))
                {
                    noReagents = true;
                    continue;
                }

                var produced = recipe.Amount;
                foreach (var extra in recipe.Byproducts.Values)
                {
                    produced += extra;
                }

                if (produced > room)
                {
                    noRoom = true;
                    continue;
                }

                chosen = recipe;
                room -= produced;
                break;
            }

            if (chosen == null)
                break;

            plan.Add(chosen);
            loaded[chosen.Input] -= chosen.InputCount;
            foreach (var (reagent, amount) in chosen.Reagents)
            {
                reserved[reagent] = reserved.GetValueOrDefault(reagent) + amount;
            }
        }

        if (plan.Count == 0)
        {
            reason = noRoom ? "dystopia-processor-output-full"
                : noReagents ? "dystopia-processor-no-reagents"
                : "dystopia-processor-not-enough";
        }

        return plan;
    }

    // ------------------------------------------------------------------ загрузка

    /// <summary>Положить сырьё в загрузку (сколько влезет). Возвращает, положено ли хоть что-то.</summary>
    public bool TryInsert(Entity<DystopiaProcessorComponent> ent, EntityUid used, EntityUid? user)
    {
        if (GetRecipe(ent, used) == null || !_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return false;

        var free = ent.Comp.Capacity - LoadedUnits(ent);
        if (free <= 0)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dystopia-processor-full"), ent, user.Value);
            return false;
        }

        var item = used;
        if (TryComp<StackComponent>(item, out var stack) && stack.Count > free)
        {
            if (_stack.Split((item, stack), free, Transform(ent).Coordinates, user) is not { } split)
                return false;

            item = split;
        }

        if (!_container.Insert(item, container))
            return false;

        _audio.PlayPvs(ent.Comp.InsertSound, ent);
        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-processor-loaded",
                ("item", item), ("loaded", LoadedUnits(ent)), ("capacity", ent.Comp.Capacity)), ent, user.Value);
        }

        return true;
    }

    private void OnInteractUsing(Entity<DystopiaProcessorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (GetRecipe(ent, args.Used) == null)
        {
            // Руда, которую эта машина не берёт, — сказать; остальное (ключ, ведро с водой) — не наше.
            if (_tag.HasTag(args.Used, OreTag))
            {
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("dystopia-processor-not-accepted", ("item", args.Used)), ent, args.User);
            }

            return;
        }

        args.Handled = true;
        TryInsert(ent, args.Used, args.User);
    }

    private void OnConveyorInsert(Entity<DystopiaProcessorComponent> ent, ref DystopiaConveyorInsertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.Inserted = TryInsert(ent, args.Item, null);
    }

    // Извлекатель забирает только готовую продукцию.
    private void OnConveyorExtract(Entity<DystopiaProcessorComponent> ent, ref DystopiaConveyorExtractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.OutputContainer is not { } id || !_container.TryGetContainer(ent, id, out var container))
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

    // ------------------------------------------------------------------ ручная работа / выгрузка

    private void OnInteractHand(Entity<DystopiaProcessorComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Manual)
            TryStartWork(ent, args.User);
        else
            EjectOutput(ent, args.User);
    }

    private void OnGetVerbs(Entity<DystopiaProcessorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (ent.Comp.Manual)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(ent.Comp.WorkVerb),
                Act = () => TryStartWork(ent, user),
                Priority = 1,
            });
        }

        if (OutputUnits(ent) > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-processor-unload-verb"),
                Act = () => EjectOutput(ent, user),
                Priority = 1,
            });
        }

        if (LoadedUnits(ent) > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-processor-eject-verb"),
                Act = () => EjectInput(ent, user),
            });
        }

        AddTankVerbs(ent, ref args);
    }

    // ------------------------------------------------------------------ бак: отлить в сосуд, слить на пол

    private void AddTankVerbs(Entity<DystopiaProcessorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (ent.Comp.Tank is not { } tankName ||
            !_solution.TryGetSolution(ent.Owner, tankName, out var tank, out var tankSolution) ||
            tankSolution.Volume <= FixedPoint2.Zero)
        {
            return;
        }

        var user = args.User;
        var tankEnt = tank.Value;

        // Сосуд в руке (стакан, ведро, канистра) — отлить из бака столько, сколько сосуд наливает за раз.
        if (args.Using is { } held &&
            TryComp<SolutionTransferComponent>(held, out var transfer) && transfer.CanReceive &&
            TryComp<RefillableSolutionComponent>(held, out var refillable) &&
            _solution.TryGetRefillableSolution(held, out var heldSoln, out var heldSolution) &&
            heldSolution.AvailableVolume > FixedPoint2.Zero)
        {
            var amount = transfer.TransferAmount;
            if (refillable.MaxRefill is { } maxRefill)
                amount = FixedPoint2.Min(amount, maxRefill);

            var shown = FixedPoint2.Min(amount, FixedPoint2.Min(tankSolution.Volume, heldSolution.AvailableVolume));
            var heldEnt = heldSoln.Value;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-processor-tank-draw-verb", ("amount", shown)),
                Act = () =>
                {
                    var moved = _transfer.Transfer(new SolutionTransferData(user, ent.Owner, tankEnt, held, heldEnt, amount));
                    if (moved > FixedPoint2.Zero)
                    {
                        _popup.PopupEntity(Loc.GetString("dystopia-processor-tank-drawn",
                            ("amount", moved), ("target", held)), ent, user);
                    }
                },
                Priority = 2,
            });
        }

        // Слить весь бак на пол — если налили не того или не столько.
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-processor-tank-dump-verb"),
            Act = () =>
            {
                if (tankEnt.Comp.Solution.Volume <= FixedPoint2.Zero)
                    return;

                var spilled = _solution.SplitSolution(tankEnt, tankEnt.Comp.Solution.Volume);
                _puddle.TrySpillAt(ent.Owner, spilled, out _);
                _popup.PopupEntity(Loc.GetString("dystopia-processor-tank-dumped"), ent, user);
            },
            Priority = -1,
        });
    }

    public void TryStartWork(Entity<DystopiaProcessorComponent> ent, EntityUid user)
    {
        if (!CanWork(ent, user))
            return;

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.CycleTime,
            new DystopiaProcessorWorkDoAfterEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            SetRunning(ent, true);
    }

    private void OnWork(Entity<DystopiaProcessorComponent> ent, ref DystopiaProcessorWorkDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            SetRunning(ent, false);
            return;
        }

        RunCycle(ent);

        // Работают, пока есть из чего и пока не отойдут.
        args.Repeat = PlanCycle(ent, out _).Count > 0;
        SetRunning(ent, args.Repeat);
    }

    private bool CanWork(Entity<DystopiaProcessorComponent> ent, EntityUid? user)
    {
        if (ent.Comp.RequireAnchored && !Transform(ent).Anchored)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dystopia-processor-not-anchored"), ent, user.Value);
            return false;
        }

        if (PlanCycle(ent, out var reason).Count > 0)
            return true;

        if (user != null && reason != null)
            _popup.PopupEntity(Loc.GetString(reason), ent, user.Value);
        return false;
    }

    private void EjectOutput(Entity<DystopiaProcessorComponent> ent, EntityUid user)
    {
        if (ent.Comp.OutputContainer is not { } id || !_container.TryGetContainer(ent, id, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-processor-no-output"), ent, user);
            return;
        }

        _container.EmptyContainer(container, destination: Transform(user).Coordinates);
        _popup.PopupEntity(Loc.GetString("dystopia-processor-unloaded"), ent, user);
    }

    private void EjectInput(Entity<DystopiaProcessorComponent> ent, EntityUid user)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        _container.EmptyContainer(container, destination: Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("dystopia-processor-ejected"), ent, user);
        if (!ent.Comp.Manual)
            SetRunning(ent, false);
    }

    // ------------------------------------------------------------------ электрические: сами

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaProcessorComponent>();
        while (query.MoveNext(out var uid, out var processor))
        {
            if (processor.Manual)
                continue;

            Entity<DystopiaProcessorComponent> ent = (uid, processor);
            var canRun = _power.IsPowered(uid) && (!processor.RequireAnchored || Transform(uid).Anchored);

            if (processor.Running)
            {
                if (!canRun)
                {
                    SetRunning(ent, false); // пропал ток — цикл сорван, сырьё цело
                    continue;
                }

                if (now < processor.CycleEnd)
                    continue;

                RunCycle(ent);
                SetRunning(ent, false);
            }

            if (canRun && PlanCycle(ent, out _).Count > 0)
            {
                processor.CycleEnd = now + TimeSpan.FromSeconds(processor.CycleTime);
                SetRunning(ent, true);
            }
        }
    }

    // ------------------------------------------------------------------ цикл

    /// <summary>Выполнить цикл: партии по плану, продукция — в выходной контейнер или на пол.</summary>
    public void RunCycle(Entity<DystopiaProcessorComponent> ent)
    {
        var plan = PlanCycle(ent, out _);
        if (plan.Count == 0 || !_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        var outputs = new Dictionary<EntProtoId, int>();
        foreach (var recipe in plan)
        {
            // Сырьё — из любых стопок этого типа
            var need = recipe.InputCount;
            foreach (var item in new List<EntityUid>(container.ContainedEntities))
            {
                if (need <= 0)
                    break;

                if (EntityManager.IsQueuedForDeletion(item) ||
                    !TryComp<StackComponent>(item, out var stack) || stack.StackTypeId != recipe.Input)
                {
                    continue;
                }

                var take = Math.Min(need, stack.Count);
                _stack.ReduceCount((item, stack), take); // дошло до нуля — стопка удаляется сама
                need -= take;
            }

            if (recipe.Reagents.Count > 0 && ent.Comp.Tank is { } tank &&
                _solution.TryGetSolution(ent.Owner, tank, out var soln, out _))
            {
                foreach (var (reagent, amount) in recipe.Reagents)
                {
                    _solution.RemoveReagent(soln.Value, reagent.Id, amount);
                }
            }

            outputs[recipe.Output] = outputs.GetValueOrDefault(recipe.Output) + recipe.Amount;
            foreach (var (extra, amount) in recipe.Byproducts)
            {
                outputs[extra] = outputs.GetValueOrDefault(extra) + amount;
            }
        }

        foreach (var (output, amount) in outputs)
        {
            Produce(ent, output, amount);
        }

        _audio.PlayPvs(ent.Comp.CycleSound, ent);
    }

    private void Produce(Entity<DystopiaProcessorComponent> ent, EntProtoId proto, int amount)
    {
        var coords = Transform(ent).Coordinates;
        BaseContainer? output = null;
        if (ent.Comp.OutputContainer is { } id && _container.TryGetContainer(ent, id, out var found))
            output = found;

        foreach (var spawned in _stack.SpawnMultipleAtPosition(proto, amount, coords))
        {
            if (output == null)
            {
                _stack.TryMergeToContacts(spawned);
                continue;
            }

            // В выходной контейнер — дополняя уже лежащие там стопки того же типа.
            foreach (var existing in output.ContainedEntities)
            {
                if (EntityManager.IsQueuedForDeletion(spawned))
                    break;

                _stack.TryMergeStacks(spawned, existing, out _);
            }

            if (!EntityManager.IsQueuedForDeletion(spawned))
                _container.Insert(spawned, output);
        }
    }

    private void SetRunning(Entity<DystopiaProcessorComponent> ent, bool running)
    {
        ent.Comp.Running = running;
        _appearance.SetData(ent, DystopiaProcessorVisuals.Running, running);
        _ambient.SetAmbience(ent, running);
    }

    // ------------------------------------------------------------------ осмотр

    private void OnExamined(Entity<DystopiaProcessorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("dystopia-processor-examine-load",
            ("loaded", LoadedUnits(ent)), ("capacity", ent.Comp.Capacity)));

        if (ent.Comp.OutputContainer != null)
        {
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-output",
                ("units", OutputUnits(ent)), ("capacity", ent.Comp.OutputCapacity)));
        }

        if (ent.Comp.Tank is { } tank && _solution.TryGetSolution(ent.Owner, tank, out _, out var solution))
        {
            // Одной строкой: строки с равным приоритетом сортируются по алфавиту и разбежались бы.
            var parts = new List<string>();
            foreach (var (reagent, quantity) in solution.GetReagentPrototypes(_proto))
            {
                parts.Add(Loc.GetString("dystopia-processor-examine-tank-entry",
                    ("color", reagent.SubstanceColor.ToHexNoAlpha()), ("name", reagent.LocalizedName),
                    ("amount", quantity)));
            }

            args.PushMarkup(Loc.GetString(parts.Count == 0
                    ? "dystopia-processor-examine-tank-empty"
                    : "dystopia-processor-examine-tank",
                ("volume", solution.Volume), ("max", solution.MaxVolume), ("contents", string.Join(", ", parts))));
        }

        args.PushMarkup(Loc.GetString("dystopia-processor-examine-cycle",
            ("batches", ent.Comp.BatchesPerCycle), ("seconds", ent.Comp.CycleTime)));

        if (ent.Comp.RequireAnchored && !Transform(ent).Anchored)
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-loose"));
        else if (!ent.Comp.Manual && !_power.IsPowered(ent.Owner))
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-unpowered"));
    }
}
