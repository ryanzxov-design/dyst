// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: тигель — плавка по материалу, нагрев и остывание, снятие шлака.

using Content.Server.Atmos.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Temperature;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaCrucibleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DystopiaHotItemSystem _hot = default!;
    [Dependency] private DystopiaQuenchSystem _quench = default!;

    public const float KelvinOffset = 273.15f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaCrucibleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaCrucibleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaCrucibleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaCrucibleComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<DystopiaCrucibleComponent, DystopiaConveyorInsertEvent>(OnConveyorInsert);
        SubscribeLocalEvent<DystopiaCrucibleComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaCrucibleComponent, SolutionTransferAttemptEvent>(OnTransferAttempt);
        SubscribeLocalEvent<DystopiaCrucibleComponent, DystopiaGetTemperatureEvent>(OnGetTemperature);
        SubscribeLocalEvent<DystopiaCrucibleComponent, DystopiaCoolEvent>(OnCool);
    }

    private void OnGetTemperature(Entity<DystopiaCrucibleComponent> ent, ref DystopiaGetTemperatureEvent args)
    {
        args.Temperature = ent.Comp.Temperature;
    }

    // Закалка водой: тигель остывает сразу, но не ниже окружения.
    private void OnCool(Entity<DystopiaCrucibleComponent> ent, ref DystopiaCoolEvent args)
    {
        var ambient = GetAmbient(ent, ent.Comp.FallbackAmbient);
        ent.Comp.Temperature = MathF.Max(ambient, ent.Comp.Temperature - args.Kelvin);
        SyncSolutionTemperature(ent);
        _hot.SetHot(ent.Owner, ent.Comp.Temperature > ent.Comp.HotThreshold);
    }

    // Застывший металл из тигля не льётся: сначала разогреть.
    private void OnTransferAttempt(Entity<DystopiaCrucibleComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.From != ent.Owner || args.CancelReason != null)
            return;

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) ||
            solution.GetPrimaryReagentId() is not { } primary ||
            !_proto.TryIndex<ReagentPrototype>(primary.Prototype, out var reagent) ||
            reagent.MeltingPoint is not { } melting)
        {
            return;
        }

        if (ent.Comp.Temperature < melting + KelvinOffset)
            args.Cancel(Loc.GetString("dystopia-crucible-solidified"));
    }

    private void OnMapInit(Entity<DystopiaCrucibleComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.ChargeContainer);
        ent.Comp.Temperature = GetAmbient(ent, ent.Comp.FallbackAmbient);
    }

    // ------------------------------------------------------------------ плавкость

    /// <summary>
    /// Что даёт плавка одного предмета (одной единицы стопки). False — в предмете нет ни одного плавкого материала.
    /// </summary>
    public bool TryGetSmelt(EntityUid item, FixedPoint2 foreignSlagPer100, out Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> products,
        out float meltTemperature, out int material, ProtoId<ReagentPrototype>? foreignSlag = null)
    {
        products = new();
        meltTemperature = 0f;
        material = 0;

        if (!TryComp<PhysicalCompositionComponent>(item, out var composition))
            return false;

        var smeltable = false;
        var foreign = 0;
        foreach (var (mat, amount) in composition.MaterialComposition)
        {
            material += amount;
            if (!_proto.TryIndex<DystopiaSmeltablePrototype>(mat.Id, out var smelt))
            {
                foreign += amount;
                continue;
            }

            smeltable = true;
            meltTemperature = MathF.Max(meltTemperature, GetMeltTemperature(smelt));
            foreach (var (reagent, per100) in smelt.Products)
            {
                products[reagent] = products.GetValueOrDefault(reagent) + per100 * amount / 100f;
            }
        }

        if (!smeltable)
            return false;

        if (foreign > 0 && foreignSlag is { } slag)
            products[slag] = products.GetValueOrDefault(slag) + foreignSlagPer100 * foreign / 100f;

        return true;
    }

    public float GetMeltTemperature(DystopiaSmeltablePrototype smelt)
    {
        if (smelt.MeltTemperature is { } explicitTemp)
            return explicitTemp;

        var result = 0f;
        foreach (var reagent in smelt.Products.Keys)
        {
            if (_proto.TryIndex(reagent, out var proto) && proto.MeltingPoint is { } melting)
                result = MathF.Max(result, melting + KelvinOffset);
        }

        return result;
    }

    private int ChargeMaterial(Entity<DystopiaCrucibleComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ChargeContainer, out var container))
            return 0;

        var total = 0;
        foreach (var item in container.ContainedEntities)
        {
            if (TryComp<PhysicalCompositionComponent>(item, out var composition))
            {
                var perUnit = 0;
                foreach (var amount in composition.MaterialComposition.Values)
                {
                    perUnit += amount;
                }

                total += perUnit * (TryComp<StackComponent>(item, out var stack) ? stack.Count : 1);
            }
        }

        return total;
    }

    // ------------------------------------------------------------------ загрузка

    private void OnInteractUsing(Entity<DystopiaCrucibleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !CanLoad(ent, args.Used))
            return;

        // Тигель, встроенный в горн: огонь и топливо — горну (уголь в тигель — действием «Засыпать в тигель»).
        if (TryComp<DystopiaForgeComponent>(ent, out var forge) && IsForgeItem(forge, args.Used))
            return;

        args.Handled = true;
        TryLoad(ent, args.Used, args.User);
    }

    // В горн с большим тиглем уголь по клику идёт в топку; в тигель — этим действием.
    private void OnGetInteractionVerbs(Entity<DystopiaCrucibleComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } used ||
            !HasComp<DystopiaForgeComponent>(ent) || !CanLoad(ent, used))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("dystopia-crucible-load-verb"),
            Act = () => TryLoad(ent, used, user),
        });
    }

    private bool IsForgeItem(DystopiaForgeComponent forge, EntityUid item)
    {
        // Электрическому горну огонь и топливо не нужны — уголь идёт прямо в тигель.
        if (forge.Electric)
            return false;

        var hot = new IsHotEvent();
        RaiseLocalEvent(item, hot);
        return hot.IsHot ||
               TryComp<PhysicalCompositionComponent>(item, out var composition) &&
               DystopiaForgeSystem.FuelValue(forge, composition) > 0f;
    }

    /// <summary>Можно ли это загрузить в тигель (плавится и это не литейный инвентарь).</summary>
    private bool CanLoad(Entity<DystopiaCrucibleComponent> ent, EntityUid item)
    {
        // Клещи, изложницы, формы, другие тигли и всё, чем переливают, — не загрузка в тигель
        if (HasComp<DystopiaTongsComponent>(item) ||
            HasComp<DystopiaMouldComponent>(item) ||
            HasComp<DystopiaCastingFormComponent>(item) ||
            HasComp<DystopiaCrucibleComponent>(item) ||
            HasComp<SolutionTransferComponent>(item))
        {
            return false;
        }

        return TryGetSmelt(item, ent.Comp.ForeignSlagPer100, out _, out _, out var perUnit, ent.Comp.ForeignSlag) &&
               perUnit > 0;
    }

    // Конвейерный вставщик: в тигель (и в горн с тиглем) — всё, что плавится, уголь тоже идёт в тигель.
    private void OnConveyorInsert(Entity<DystopiaCrucibleComponent> ent, ref DystopiaConveyorInsertEvent args)
    {
        if (args.Handled || !CanLoad(ent, args.Item))
            return;

        args.Handled = true;
        args.Inserted = TryLoad(ent, args.Item, null);
    }

    /// <summary>Загрузить предмет (или сколько влезет из стопки) в тигель. Возвращает, загружено ли.</summary>
    public bool TryLoad(Entity<DystopiaCrucibleComponent> ent, EntityUid used, EntityUid? user)
    {
        if (!TryGetSmelt(used, ent.Comp.ForeignSlagPer100, out _, out _, out var perUnit, ent.Comp.ForeignSlag) ||
            perUnit <= 0)
        {
            return false;
        }

        // Не плавим вместе с содержимым: ящик с инструментами сначала опустошить.
        if (TryComp<ContainerManagerComponent>(used, out var manager))
        {
            foreach (var inner in manager.Containers.Values)
            {
                if (inner.ContainedEntities.Count == 0)
                    continue;

                if (user != null)
                    _popup.PopupEntity(Loc.GetString("dystopia-crucible-not-empty-item", ("item", used)), ent, user.Value);
                return false;
            }
        }

        if (!_container.TryGetContainer(ent, ent.Comp.ChargeContainer, out var container))
            return false;

        var free = ent.Comp.MaxCharge - ChargeMaterial(ent);
        var fits = free / perUnit;
        if (fits <= 0)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dystopia-crucible-full"), ent, user.Value);
            return false;
        }

        var item = used;
        if (TryComp<StackComponent>(item, out var stack) && stack.Count > fits)
        {
            if (_stack.Split((item, stack), fits, Transform(ent).Coordinates, user) is not { } split)
                return false;

            item = split;
        }

        if (!_container.Insert(item, container))
            return false;

        if (user != null)
            _popup.PopupEntity(Loc.GetString("dystopia-crucible-loaded", ("item", item)), ent, user.Value);
        return true;
    }

    // ------------------------------------------------------------------ шлак

    private void OnGetVerbs(Entity<DystopiaCrucibleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) ||
            solution.GetTotalPrototypeQuantity(ent.Comp.SlagReagent) <= 0)
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-crucible-skim-verb"),
            Act = () => SkimSlag(ent, user),
        });
    }

    public void SkimSlag(Entity<DystopiaCrucibleComponent> ent, EntityUid user)
    {
        if (!_hot.HasTongs(user))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-crucible-skim-need-tongs"), ent, user);
            return;
        }

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution))
            return;

        // Снимается весь шлак: остаток в расплаве — брак в изложнице.
        var slag = solution.GetTotalPrototypeQuantity(ent.Comp.SlagReagent);
        if (slag <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-crucible-skim-too-little"), ent, user);
            return;
        }

        var lumps = Math.Max(1, (int) MathF.Round((float) (slag / ent.Comp.SlagPerLump)));
        _solution.RemoveReagent(soln.Value, ent.Comp.SlagReagent.Id, slag);

        // Шлак — стопкой (и в стопку, что уже лежит рядом).
        foreach (var spawned in _stack.SpawnMultipleAtPosition(ent.Comp.SlagLump, lumps, Transform(ent).Coordinates))
        {
            _stack.TryMergeToContacts(spawned);
        }

        _popup.PopupEntity(Loc.GetString("dystopia-crucible-skimmed", ("count", lumps)), ent, user);
    }

    // ------------------------------------------------------------------ нагрев

    /// <summary>Источник жара греет тигель к target со скоростью rate (K/с) до момента until.</summary>
    public void ApplyHeat(Entity<DystopiaCrucibleComponent> ent, float target, float rate, TimeSpan until)
    {
        ent.Comp.HeatTarget = target;
        ent.Comp.HeatRate = rate * ent.Comp.HeatMultiplier;
        ent.Comp.HeatedUntil = until;
    }

    private float GetAmbient(EntityUid uid, float fallback)
    {
        return _atmosphere.GetContainingMixture(uid)?.Temperature ?? fallback;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaCrucibleComponent>();
        while (query.MoveNext(out var uid, out var crucible))
        {
            if (now < crucible.NextUpdate)
                continue;

            crucible.NextUpdate = now + TimeSpan.FromSeconds(crucible.UpdateInterval);
            UpdateCrucible((uid, crucible), crucible.UpdateInterval, now);
        }
    }

    private void UpdateCrucible(Entity<DystopiaCrucibleComponent> ent, float dt, TimeSpan now)
    {
        var comp = ent.Comp;
        var ambient = GetAmbient(ent, comp.FallbackAmbient);
        var decay = MathF.Min(1f, comp.CoolingRate * dt);

        if (now < comp.HeatedUntil)
        {
            if (comp.Temperature < comp.HeatTarget)
                comp.Temperature = MathF.Min(comp.HeatTarget, comp.Temperature + comp.HeatRate * dt);
            else
                comp.Temperature -= (comp.Temperature - comp.HeatTarget) * decay;
        }
        else
        {
            comp.Temperature -= (comp.Temperature - ambient) * decay;
        }

        Melt(ent, dt);
        SyncSolutionTemperature(ent);
        _quench.TryEvaporate(ent.Owner); // налитая заранее вода выкипает, когда тигель нагреют

        _hot.SetHot(ent.Owner, comp.Temperature > comp.HotThreshold);
        // Свечение — у тигля-предмета; у горна с тиглем светом управляет огонь горна.
        if (!HasComp<DystopiaForgeComponent>(ent) && !HasComp<DystopiaArcFurnaceComponent>(ent) &&
            _light.TryGetLight(ent, out var light))
            _light.SetEnabled(ent, comp.Temperature > comp.GlowThreshold, light);
    }

    private void SyncSolutionTemperature(Entity<DystopiaCrucibleComponent> ent)
    {
        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution) &&
            solution.Volume > 0 &&
            MathF.Abs(solution.Temperature - ent.Comp.Temperature) > 0.5f)
        {
            _solution.SetTemperature(soln.Value, ent.Comp.Temperature);
        }
    }

    private void Melt(Entity<DystopiaCrucibleComponent> ent, float dt)
    {
        var comp = ent.Comp;
        if (!_container.TryGetContainer(ent, comp.ChargeContainer, out var container) ||
            container.ContainedEntities.Count == 0 ||
            !_solution.TryGetSolution(ent.Owner, comp.Solution, out var soln, out var solution))
        {
            comp.MeltProgress = 0;
            return;
        }

        // Прогресс плавки копится между обновлениями, пока есть что плавить при этой температуре:
        // кусок (100 ед. материала) плавится за 100 / MeltRate секунд.
        var budget = comp.MeltProgress + comp.MeltRate * dt;
        var largestUnit = 0;

        foreach (var item in new List<EntityUid>(container.ContainedEntities))
        {
            if (TerminatingOrDeleted(item) ||
                !TryGetSmelt(item, comp.ForeignSlagPer100, out var products, out var meltTemp, out var perUnit, comp.ForeignSlag) ||
                perUnit <= 0 ||
                comp.Temperature < meltTemp)
            {
                continue;
            }

            largestUnit = Math.Max(largestUnit, perUnit);

            var unitVolume = FixedPoint2.Zero;
            foreach (var amount in products.Values)
            {
                unitVolume += amount;
            }

            TryComp<StackComponent>(item, out var stack);
            var count = stack?.Count ?? 1;
            var melted = 0;
            var full = false;

            while (budget >= perUnit && melted < count)
            {
                if (solution.AvailableVolume < unitVolume)
                {
                    full = true; // тигель полон расплава — слейте
                    break;
                }

                foreach (var (reagent, amount) in products)
                {
                    _solution.TryAddReagent(soln.Value, reagent.Id, amount, out _, comp.Temperature);
                }

                budget -= perUnit;
                melted++;
            }

            if (melted > 0)
            {
                if (melted >= count)
                    QueueDel(item);
                else if (stack != null)
                    _stack.SetCount((item, stack), count - melted);
            }

            if (full)
            {
                comp.MeltProgress = 0;
                return;
            }

            if (budget < perUnit)
                break; // остальное — в следующие обновления
        }

        // Холодно или нечего плавить — прогресс не копится.
        comp.MeltProgress = largestUnit > 0 ? MathF.Min(budget, largestUnit) : 0;
    }

    // ------------------------------------------------------------------ осмотр

    private void OnExamined(Entity<DystopiaCrucibleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("dystopia-foundry-temperature",
            ("temperature", MathF.Round(ent.Comp.Temperature - KelvinOffset))));

        if (_container.TryGetContainer(ent, ent.Comp.ChargeContainer, out var container) &&
            container.ContainedEntities.Count > 0)
        {
            // Какой жар нужен, чтобы расплавилось всё, что лежит
            var needed = 0f;
            foreach (var item in container.ContainedEntities)
            {
                if (TryGetSmelt(item, ent.Comp.ForeignSlagPer100, out _, out var meltTemp, out _, ent.Comp.ForeignSlag))
                    needed = MathF.Max(needed, meltTemp);
            }

            args.PushMarkup(Loc.GetString("dystopia-crucible-charge",
                ("material", ChargeMaterial(ent)), ("max", ent.Comp.MaxCharge),
                ("temperature", MathF.Round(needed - KelvinOffset))));
        }

        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
        {
            DystopiaFoundryExamine.PushContents(args, solution, ent.Comp.Temperature, _proto);
            if (solution.GetTotalPrototypeQuantity(ent.Comp.SlagReagent) > FixedPoint2.Zero)
                args.PushMarkup(Loc.GetString("dystopia-crucible-has-slag"));
        }
    }
}
