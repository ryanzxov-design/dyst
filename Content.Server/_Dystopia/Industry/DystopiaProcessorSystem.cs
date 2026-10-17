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
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
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

    /// <summary>
    /// Рецепт группы машины, в который идёт этот предмет (основным или дополнительным сырьём). Null — машина это
    /// не берёт.
    /// </summary>
    public DystopiaProcessRecipePrototype? GetRecipe(Entity<DystopiaProcessorComponent> ent, EntityUid item)
    {
        if (!TryComp<StackComponent>(item, out var stack))
            return null;

        foreach (var recipe in GroupRecipes(ent.Comp.Group))
        {
            if (recipe.Input == stack.StackTypeId || recipe.ExtraInputs.ContainsKey(stack.StackTypeId))
                return recipe;
        }

        return null;
    }

    private readonly Dictionary<string, List<DystopiaProcessRecipePrototype>> _groupRecipes = new();

    /// <summary>
    /// Рецепты группы в порядке выбора: сначала те, где больше видов сырья (глина с известняком — в клинкер, а не
    /// известняк отдельно в известь), рецепты только из бака — последними.
    /// </summary>
    private List<DystopiaProcessRecipePrototype> GroupRecipes(string group)
    {
        if (_groupRecipes.TryGetValue(group, out var cached))
            return cached;

        var list = new List<DystopiaProcessRecipePrototype>();
        foreach (var recipe in _proto.EnumeratePrototypes<DystopiaProcessRecipePrototype>())
        {
            if (recipe.Group == group)
                list.Add(recipe);
        }

        list.Sort((a, b) =>
        {
            var byItems = (b.Input == null ? 0 : 1).CompareTo(a.Input == null ? 0 : 1);
            if (byItems != 0)
                return byItems;

            var byExtras = b.ExtraInputs.Count.CompareTo(a.ExtraInputs.Count);
            return byExtras != 0 ? byExtras : string.CompareOrdinal(a.ID, b.ID);
        });

        _groupRecipes[group] = list;
        return list;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<DystopiaProcessRecipePrototype>())
            return;

        _groupRecipes.Clear();
        _groupReagents.Clear();
    }

    private static bool HasItems(DystopiaProcessRecipePrototype recipe, Dictionary<ProtoId<StackPrototype>, int> loaded)
    {
        if (recipe.Input is { } input && loaded.GetValueOrDefault(input) < recipe.InputCount)
            return false;

        foreach (var (extra, count) in recipe.ExtraInputs)
        {
            if (loaded.GetValueOrDefault(extra) < count)
                return false;
        }

        return true;
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
        var reserved = new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>();
        var room = ent.Comp.OutputContainer != null ? ent.Comp.OutputCapacity - OutputUnits(ent) : int.MaxValue;
        var tankRoom = TankRoom(ent);
        var separate = ent.Comp.OutputTank != null;
        var noReagents = false;
        var noRoom = false;

        for (var i = 0; i < ent.Comp.BatchesPerCycle; i++)
        {
            DystopiaProcessRecipePrototype? chosen = null;
            foreach (var recipe in GroupRecipes(ent.Comp.Group))
            {
                if (!HasItems(recipe, loaded))
                    continue;

                if (!HasReagents(ent, recipe, reserved))
                {
                    noReagents = true;
                    continue;
                }

                var produced = recipe.Output != null ? recipe.Amount : 0;
                foreach (var extra in recipe.Byproducts.Values)
                {
                    produced += extra;
                }

                // Вещества партии — в бак продукции; если он тот же, что и бак реагентов, место освобождают и её же
                // израсходованные реагенты.
                var liquid = separate ? Sum(recipe.Produces) : Sum(recipe.Produces) - Sum(recipe.Reagents);
                if (produced > room || (recipe.Produces.Count > 0 && (tankRoom == null || liquid > tankRoom)))
                {
                    noRoom = true;
                    continue;
                }

                chosen = recipe;
                room -= produced;
                if (tankRoom != null)
                    tankRoom -= liquid;
                break;
            }

            if (chosen == null)
                break;

            plan.Add(chosen);
            if (chosen.Input is { } input)
                loaded[input] -= chosen.InputCount;
            foreach (var (extra, count) in chosen.ExtraInputs)
            {
                loaded[extra] -= count;
            }
            foreach (var (reagent, amount) in chosen.Reagents)
            {
                reserved[reagent] = reserved.GetValueOrDefault(reagent) + amount;
            }
        }

        if (plan.Count == 0)
        {
            reason = noRoom ? "dystopia-processor-output-full"
                : noReagents ? "dystopia-processor-no-reagents"
                : LoadedUnits(ent) == 0 ? "dystopia-processor-empty"
                : "dystopia-processor-not-enough";
        }

        return plan;
    }

    private static FixedPoint2 Sum(Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> reagents)
    {
        var total = FixedPoint2.Zero;
        foreach (var amount in reagents.Values)
        {
            total += amount;
        }

        return total;
    }

    /// <summary>Бак, куда идут вещества рецептов: бак продукции, если он есть, иначе общий.</summary>
    private string? ProductTank(DystopiaProcessorComponent comp)
    {
        return comp.OutputTank ?? comp.Tank;
    }

    /// <summary>Свободное место в баке продукции (null — бака нет).</summary>
    private FixedPoint2? TankRoom(Entity<DystopiaProcessorComponent> ent)
    {
        return ProductTank(ent.Comp) is { } tank && _solution.TryGetSolution(ent.Owner, tank, out _, out var solution)
            ? solution.AvailableVolume
            : null;
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

        // Сосуд с жидкостью (стакан, мензурка, ведро, канистра) — залить или набрать, как у ванильных цистерн.
        if (TryComp<SolutionTransferComponent>(args.Used, out var transfer) &&
            (ent.Comp.Tank != null || ent.Comp.OutputTank != null))
        {
            args.Handled = true;
            UseVessel(ent, (args.Used, transfer), args.User);
            return;
        }

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

        if (ent.Comp.Tank is { } tank)
            AddTankVerbs(ent, tank, false, ref args);
        if (ent.Comp.OutputTank is { } outputTank && outputTank != ent.Comp.Tank)
            AddTankVerbs(ent, outputTank, true, ref args);
    }

    // ------------------------------------------------------------------ бак: залить, набрать, слить на пол

    private readonly Dictionary<string, ProtoId<ReagentPrototype>[]> _groupReagents = new();

    /// <summary>Вещества, которые берут рецепты группы, — только их машина принимает в бак (рукой и по трубам).</summary>
    public ProtoId<ReagentPrototype>[] GroupReagents(string group)
    {
        if (_groupReagents.TryGetValue(group, out var cached))
            return cached;

        var set = new HashSet<ProtoId<ReagentPrototype>>();
        foreach (var recipe in GroupRecipes(group))
        {
            set.UnionWith(recipe.Reagents.Keys);
        }

        var result = new ProtoId<ReagentPrototype>[set.Count];
        set.CopyTo(result);
        _groupReagents[group] = result;
        return result;
    }

    /// <summary>
    /// Клик сосудом по машине. Есть в сосуде то, что идёт в рецепты, — заливается в бак (прочее остаётся в сосуде).
    /// Нечего заливать — сосуд набирается из бака готового (или из общего бака). За раз — «перемещаемое количество»
    /// сосуда (его меняют, как обычно, через ПКМ по сосуду).
    /// </summary>
    private void UseVessel(Entity<DystopiaProcessorComponent> ent, Entity<SolutionTransferComponent> vessel, EntityUid user)
    {
        if (vessel.Comp.CanSend && TryPour(ent, vessel, vessel.Comp.TransferAmount, user))
            return;

        if (vessel.Comp.CanReceive && DrawSource(ent) is { } source)
        {
            Draw(ent, source, vessel, vessel.Comp.TransferAmount, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dystopia-processor-liquid-nothing"), ent, user);
    }

    private bool TryPour(Entity<DystopiaProcessorComponent> ent, EntityUid vessel, FixedPoint2 max, EntityUid user)
    {
        if (ent.Comp.Tank is not { } tankName ||
            !_solution.TryGetSolution(ent.Owner, tankName, out var tank, out var tankSolution) ||
            !_solution.TryGetDrainableSolution(vessel, out var held, out var heldSolution))
        {
            return false;
        }

        var reagents = GroupReagents(ent.Comp.Group);
        if (reagents.Length == 0)
            return false;

        var usable = heldSolution.GetTotalPrototypeQuantity(reagents);
        if (usable <= FixedPoint2.Zero)
            return false;

        var amount = FixedPoint2.Min(max, FixedPoint2.Min(usable, tankSolution.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-processor-liquid-tank-full"), ent, user);
            return true;
        }

        var poured = heldSolution.SplitSolutionWithOnly(amount, reagents);
        _solution.UpdateChemicals(held.Value);
        _solution.AddSolution(tank.Value, poured);
        _popup.PopupEntity(Loc.GetString("dystopia-processor-liquid-poured", ("amount", poured.Volume)), ent, user);
        return true;
    }

    /// <summary>Откуда набирать: бак готового, если в нём что-то есть, иначе общий бак.</summary>
    private Entity<SolutionComponent>? DrawSource(Entity<DystopiaProcessorComponent> ent)
    {
        foreach (var name in new[] { ent.Comp.OutputTank, ent.Comp.Tank })
        {
            if (name != null && _solution.TryGetSolution(ent.Owner, name, out var soln, out var solution) &&
                solution.Volume > FixedPoint2.Zero)
            {
                return soln;
            }
        }

        return null;
    }

    /// <summary>Сколько войдёт в сосуд за раз: не больше места в нём, горлышка (если ограничено) и max.</summary>
    private FixedPoint2 VesselRoom(EntityUid vessel, Solution heldSolution, FixedPoint2 max)
    {
        var room = FixedPoint2.Min(max, heldSolution.AvailableVolume);
        if (TryComp<RefillableSolutionComponent>(vessel, out var refillable) && refillable.MaxRefill is { } maxRefill)
            room = FixedPoint2.Min(room, maxRefill);

        return room;
    }

    private void Draw(Entity<DystopiaProcessorComponent> ent, Entity<SolutionComponent> source, EntityUid vessel,
        FixedPoint2 max, EntityUid user)
    {
        if (!_solution.TryGetRefillableSolution(vessel, out var held, out var heldSolution))
            return;

        var room = VesselRoom(vessel, heldSolution, max);
        if (room <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-processor-liquid-vessel-full", ("vessel", vessel)), ent, user);
            return;
        }

        var moved = _transfer.Transfer(new SolutionTransferData(user, ent.Owner, source, vessel, held.Value, room));
        if (moved > FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-processor-liquid-drawn", ("amount", moved), ("vessel", vessel)),
                ent, user);
        }
    }

    /// <param name="product">Бак готового: свои подписи, и набрать из него — первым в списке.</param>
    private void AddTankVerbs(Entity<DystopiaProcessorComponent> ent, string tankName, bool product,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!_solution.TryGetSolution(ent.Owner, tankName, out var tank, out var tankSolution) ||
            tankSolution.Volume <= FixedPoint2.Zero)
        {
            return;
        }

        var user = args.User;
        var tankEnt = tank.Value;

        // Сосуд в руке — набрать именно из этого бака, по «перемещаемому количеству» сосуда.
        if (args.Using is { } held &&
            TryComp<SolutionTransferComponent>(held, out var transfer) && transfer.CanReceive &&
            _solution.TryGetRefillableSolution(held, out _, out var heldSolution) &&
            VesselRoom(held, heldSolution, transfer.TransferAmount) is var room && room > FixedPoint2.Zero)
        {
            var shown = FixedPoint2.Min(room, tankSolution.Volume);
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(product
                    ? "dystopia-processor-output-tank-draw-verb"
                    : "dystopia-processor-tank-draw-verb", ("amount", shown)),
                Act = () => Draw(ent, tankEnt, held, room, user),
                Priority = product ? 3 : 2,
            });
        }

        // Слить весь бак на пол — если налили не того или не столько.
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(product
                ? "dystopia-processor-output-tank-dump-verb"
                : "dystopia-processor-tank-dump-verb"),
            Act = () =>
            {
                if (tankEnt.Comp.Solution.Volume <= FixedPoint2.Zero)
                    return;

                var spilled = _solution.SplitSolution(tankEnt, tankEnt.Comp.Solution.Volume);
                _puddle.TrySpillAt(ent.Owner, spilled, out _);
                _popup.PopupEntity(Loc.GetString("dystopia-processor-tank-dumped"), ent, user);
            },
            Priority = product ? -1 : -2,
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
            if (recipe.Input is { } input)
                Consume(container, input, recipe.InputCount);
            foreach (var (extra, count) in recipe.ExtraInputs)
            {
                Consume(container, extra, count);
            }

            if (recipe.Reagents.Count > 0 && ent.Comp.Tank is { } tank &&
                _solution.TryGetSolution(ent.Owner, tank, out var soln, out _))
            {
                foreach (var (reagent, amount) in recipe.Reagents)
                {
                    _solution.RemoveReagent(soln.Value, reagent.Id, amount);
                }
            }

            if (recipe.Produces.Count > 0 && ProductTank(ent.Comp) is { } productTank &&
                _solution.TryGetSolution(ent.Owner, productTank, out var productSoln, out _))
            {
                foreach (var (reagent, amount) in recipe.Produces)
                {
                    _solution.TryAddReagent(productSoln.Value, reagent.Id, amount, out _);
                }
            }

            if (recipe.Output is { } main)
                outputs[main] = outputs.GetValueOrDefault(main) + recipe.Amount;
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

    /// <summary>Списать <paramref name="count"/> единиц сырья этого типа из любых стопок загрузки.</summary>
    private void Consume(BaseContainer container, ProtoId<StackPrototype> type, int count)
    {
        foreach (var item in new List<EntityUid>(container.ContainedEntities))
        {
            if (count <= 0)
                break;

            if (EntityManager.IsQueuedForDeletion(item) ||
                !TryComp<StackComponent>(item, out var stack) || stack.StackTypeId != type)
            {
                continue;
            }

            var take = Math.Min(count, stack.Count);
            _stack.ReduceCount((item, stack), take); // дошло до нуля — стопка удаляется сама
            count -= take;
        }
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

        // Одной группой: строки идут в том порядке, в каком написаны (иначе осмотр сортирует их по алфавиту).
        using var group = args.PushGroup(nameof(DystopiaProcessorComponent));

        // Аппараты только на веществах (без загрузки) строку про сырьё не показывают.
        if (ent.Comp.Capacity > 0)
        {
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-load",
                ("loaded", LoadedUnits(ent)), ("capacity", ent.Comp.Capacity)));
            if (_container.TryGetContainer(ent, ent.Comp.Container, out var input))
                PushItems(args, input);
        }

        if (ent.Comp.OutputContainer is { } outputId)
        {
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-output",
                ("units", OutputUnits(ent)), ("capacity", ent.Comp.OutputCapacity)));
            if (_container.TryGetContainer(ent, outputId, out var output))
                PushItems(args, output);
        }

        if (ent.Comp.Tank is { } tank)
            ExamineTank(ent, tank, "dystopia-processor-examine-tank", "dystopia-processor-examine-tank-empty", args);
        if (ent.Comp.OutputTank is { } outputTank && outputTank != ent.Comp.Tank)
        {
            ExamineTank(ent, outputTank, "dystopia-processor-examine-output-tank",
                "dystopia-processor-examine-output-tank-empty", args);
        }

        args.PushMarkup(Loc.GetString("dystopia-processor-examine-cycle",
            ("batches", ent.Comp.BatchesPerCycle), ("seconds", ent.Comp.CycleTime)));

        if (ent.Comp.RequireAnchored && !Transform(ent).Anchored)
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-loose"));
        else if (!ent.Comp.Manual && !_power.IsPowered(ent.Owner))
            args.PushMarkup(Loc.GetString("dystopia-processor-examine-unpowered"));
    }

    /// <summary>Что лежит в контейнере — по строке на вид: «— известняк × 4».</summary>
    private void PushItems(ExaminedEvent args, BaseContainer container)
    {
        foreach (var (name, count) in DystopiaFoundryExamine.CountItems(EntityManager, _proto, container.ContainedEntities))
        {
            args.PushMarkup(Loc.GetString("dystopia-crucible-inside-entry", ("name", name), ("count", count)));
        }
    }

    private void ExamineTank(EntityUid uid, string tank, LocId full, LocId empty, ExaminedEvent args)
    {
        if (!_solution.TryGetSolution(uid, tank, out _, out var solution))
            return;

        var parts = new List<string>();
        foreach (var (reagent, quantity) in solution.GetReagentPrototypes(_proto))
        {
            parts.Add(Loc.GetString("dystopia-processor-examine-tank-entry",
                ("color", reagent.SubstanceColor.ToHexNoAlpha()), ("name", reagent.LocalizedName),
                ("amount", quantity)));
        }

        args.PushMarkup(Loc.GetString(parts.Count == 0 ? empty : full,
            ("volume", solution.Volume), ("max", solution.MaxVolume), ("contents", string.Join(", ", parts))));
    }
}
