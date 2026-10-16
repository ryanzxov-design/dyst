// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: тигель — плавка по материалу, нагрев и остывание, снятие шлака.

using Content.Server.Atmos.EntitySystems;
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
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DystopiaHotItemSystem _hot = default!;

    public const float KelvinOffset = 273.15f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaCrucibleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaCrucibleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaCrucibleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaCrucibleComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaCrucibleComponent, SolutionTransferAttemptEvent>(OnTransferAttempt);
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
        // Клещи, изложницы, другие тигли и всё, чем переливают, — не загрузка в тигель
        if (args.Handled ||
            HasComp<DystopiaTongsComponent>(args.Used) ||
            HasComp<DystopiaMouldComponent>(args.Used) ||
            HasComp<DystopiaCrucibleComponent>(args.Used) ||
            HasComp<SolutionTransferComponent>(args.Used))
        {
            return;
        }

        if (!TryGetSmelt(args.Used, ent.Comp.ForeignSlagPer100, out _, out _, out var perUnit, ent.Comp.ForeignSlag) ||
            perUnit <= 0)
        {
            return;
        }

        args.Handled = true;

        // Не плавим вместе с содержимым: ящик с инструментами сначала опустошить.
        if (TryComp<ContainerManagerComponent>(args.Used, out var manager))
        {
            foreach (var inner in manager.Containers.Values)
            {
                if (inner.ContainedEntities.Count == 0)
                    continue;

                _popup.PopupEntity(Loc.GetString("dystopia-crucible-not-empty-item", ("item", args.Used)), ent, args.User);
                return;
            }
        }

        if (!_container.TryGetContainer(ent, ent.Comp.ChargeContainer, out var container))
            return;

        var free = ent.Comp.MaxCharge - ChargeMaterial(ent);
        var fits = free / perUnit;
        if (fits <= 0)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-crucible-full"), ent, args.User);
            return;
        }

        var item = args.Used;
        if (TryComp<StackComponent>(item, out var stack) && stack.Count > fits)
        {
            if (_stack.Split((item, stack), fits, Transform(ent).Coordinates, args.User) is not { } split)
                return;

            item = split;
        }

        _container.Insert(item, container);
        _popup.PopupEntity(Loc.GetString("dystopia-crucible-loaded", ("item", item)), ent, args.User);
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

        var slag = solution.GetTotalPrototypeQuantity(ent.Comp.SlagReagent);
        var lumps = (int) MathF.Floor((float) (slag / ent.Comp.SlagPerLump));
        if (lumps <= 0)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-crucible-skim-too-little"), ent, user);
            return;
        }

        _solution.RemoveReagent(soln.Value, ent.Comp.SlagReagent.Id, ent.Comp.SlagPerLump * lumps);
        var coords = Transform(ent).Coordinates;
        for (var i = 0; i < lumps; i++)
        {
            Spawn(ent.Comp.SlagLump, coords);
        }

        _popup.PopupEntity(Loc.GetString("dystopia-crucible-skimmed", ("count", lumps)), ent, user);
    }

    // ------------------------------------------------------------------ нагрев

    /// <summary>Источник жара греет тигель к target со скоростью rate (K/с) до момента until.</summary>
    public void ApplyHeat(Entity<DystopiaCrucibleComponent> ent, float target, float rate, TimeSpan until)
    {
        ent.Comp.HeatTarget = target;
        ent.Comp.HeatRate = rate;
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

        if (_solution.TryGetSolution(ent.Owner, comp.Solution, out var soln, out var solution) &&
            solution.Volume > 0 &&
            MathF.Abs(solution.Temperature - comp.Temperature) > 0.5f)
        {
            _solution.SetTemperature(soln.Value, comp.Temperature);
        }

        _hot.SetHot(ent.Owner, comp.Temperature > comp.HotThreshold);
        if (_light.TryGetLight(ent, out var light))
            _light.SetEnabled(ent, comp.Temperature > comp.GlowThreshold, light);
    }

    private void Melt(Entity<DystopiaCrucibleComponent> ent, float dt)
    {
        var comp = ent.Comp;
        if (!_container.TryGetContainer(ent, comp.ChargeContainer, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            comp.MeltProgress = 0;
            return;
        }

        if (!_solution.TryGetSolution(ent.Owner, comp.Solution, out var soln, out var solution))
            return;

        var budget = comp.MeltProgress + comp.MeltRate * dt;
        var meltedAny = false;

        foreach (var item in new List<EntityUid>(container.ContainedEntities))
        {
            if (!TryGetSmelt(item, comp.ForeignSlagPer100, out var products, out var meltTemp, out var perUnit, comp.ForeignSlag) ||
                perUnit <= 0 ||
                comp.Temperature < meltTemp)
            {
                continue;
            }

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
                meltedAny = true;
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
        }

        // Если ничего не плавится (холодно), прогресс не копится.
        comp.MeltProgress = meltedAny ? MathF.Min(budget, comp.MeltRate * dt) : 0;
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
            args.PushMarkup(Loc.GetString("dystopia-crucible-charge",
                ("material", ChargeMaterial(ent)), ("max", ent.Comp.MaxCharge)));
        }
    }
}
