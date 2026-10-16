// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: изложница и литейный стол — остывание залитого расплава и выход отливки. Водой остужается быстрее
// (DystopiaQuenchSystem). У стола форма сменная (слот), отливки ×10 выходят одной стопкой.

using Content.Server.Atmos.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaMouldSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DystopiaHotItemSystem _hot = default!;
    [Dependency] private DystopiaQuenchSystem _quench = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaMouldComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaMouldComponent, DystopiaGetTemperatureEvent>(OnGetTemperature);
        SubscribeLocalEvent<DystopiaMouldComponent, DystopiaCoolEvent>(OnCool);
    }

    private float GetAmbient(Entity<DystopiaMouldComponent> ent)
    {
        return _atmosphere.GetContainingMixture(ent.Owner)?.Temperature ?? ent.Comp.FallbackAmbient;
    }

    // Температура изложницы — температура залитого в неё (пустая — комнатная).
    private void OnGetTemperature(Entity<DystopiaMouldComponent> ent, ref DystopiaGetTemperatureEvent args)
    {
        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) && solution.Volume > 0)
            args.Temperature = solution.Temperature;
    }

    private void OnCool(Entity<DystopiaMouldComponent> ent, ref DystopiaCoolEvent args)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution) || solution.Volume <= 0)
            return;

        _solution.SetTemperature(soln.Value, MathF.Max(GetAmbient(ent), solution.Temperature - args.Kelvin));
        UpdateMould(ent); // мог застыть сразу
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaMouldComponent>();
        while (query.MoveNext(out var uid, out var mould))
        {
            if (now < mould.NextUpdate)
                continue;

            mould.NextUpdate = now + TimeSpan.FromSeconds(mould.UpdateInterval);
            Cool((uid, mould));
            UpdateMould((uid, mould));
        }
    }

    // Остывание к температуре окружения.
    private void Cool(Entity<DystopiaMouldComponent> ent)
    {
        var comp = ent.Comp;
        if (!_solution.TryGetSolution(ent.Owner, comp.Solution, out var soln, out var solution) || solution.Volume <= 0)
            return;

        var ambient = GetAmbient(ent);
        var decay = MathF.Min(1f, comp.CoolingRate * comp.UpdateInterval);
        _solution.SetTemperature(soln.Value, solution.Temperature - (solution.Temperature - ambient) * decay);
        _quench.TryEvaporate(ent.Owner); // вода, налитая до металла, выкипает
    }

    private void UpdateMould(Entity<DystopiaMouldComponent> ent)
    {
        var comp = ent.Comp;
        if (!_solution.TryGetSolution(ent.Owner, comp.Solution, out var soln, out var solution) || solution.Volume <= 0)
        {
            _hot.SetHot(ent.Owner, false);
            return;
        }

        var temperature = solution.Temperature;
        _hot.SetHot(ent.Owner, temperature > comp.HotThreshold);

        // Застыл ли основной металл?
        if (solution.GetPrimaryReagentId() is not { } primary ||
            !_proto.TryIndex(primary.Prototype, out var reagent) ||
            reagent.MeltingPoint is not { } melting ||
            temperature > melting + DystopiaCrucibleSystem.KelvinOffset ||
            GetShape(ent) is not { } shape)
        {
            return;
        }

        var coords = Transform(ent).Coordinates;
        while (solution.Volume >= shape.Volume)
        {
            var purity = (float) (solution.GetTotalPrototypeQuantity(primary.Prototype) / solution.Volume);
            var product = purity >= shape.MinPurity && shape.Products.TryGetValue(primary.Prototype, out var cast)
                ? cast
                : shape.Defect;

            _solution.SplitSolution(soln.Value, shape.Volume);
            SpawnCasting(product, coords);
        }

        // Недолив застыл комом — брак.
        if (solution.Volume > 0)
        {
            _solution.RemoveAllSolution(soln.Value);
            SpawnCasting(shape.Defect, coords);
        }

        _hot.SetHot(ent.Owner, false);
    }

    /// <summary>Форма отливки: своя у изложницы, у литейного стола — из вставленной формы. Null — формы нет.</summary>
    public DystopiaMouldShapePrototype? GetShape(Entity<DystopiaMouldComponent> ent)
    {
        if (ent.Comp.FormSlot is { } slot)
        {
            return _itemSlots.GetItemOrNull(ent.Owner, slot) is { } form &&
                   TryComp<DystopiaCastingFormComponent>(form, out var formComp) &&
                   _proto.TryIndex(formComp.Shape, out var formShape)
                ? formShape
                : null;
        }

        return ent.Comp.Shape is { } own && _proto.TryIndex(own, out var shape) ? shape : null;
    }

    // Отливки одного вида складываются в стопку (слитки ×10 — одной стопкой).
    private void SpawnCasting(EntProtoId product, EntityCoordinates coords)
    {
        var spawned = Spawn(product, coords);
        _stack.TryMergeToContacts(spawned);
    }

    private void OnExamined(Entity<DystopiaMouldComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (GetShape(ent) is { } shape)
        {
            args.PushMarkup(Loc.GetString("dystopia-mould-shape",
                ("shape", Loc.GetString(shape.Name)), ("volume", shape.Volume)));
        }
        else if (ent.Comp.FormSlot != null)
        {
            args.PushMarkup(Loc.GetString("dystopia-casting-table-no-form"));
        }

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return;

        if (solution.Volume > 0)
        {
            args.PushMarkup(Loc.GetString("dystopia-foundry-temperature",
                ("temperature", MathF.Round(solution.Temperature - DystopiaCrucibleSystem.KelvinOffset))));
        }

        DystopiaFoundryExamine.PushContents(args, solution, solution.Temperature, _proto);
    }
}
