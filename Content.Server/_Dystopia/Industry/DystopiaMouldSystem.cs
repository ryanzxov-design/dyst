// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: изложница — остывание залитого расплава и выход отливки.

using Content.Server.Atmos.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaMouldComponent, ExaminedEvent>(OnExamined);
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
            UpdateMould((uid, mould));
        }
    }

    private void UpdateMould(Entity<DystopiaMouldComponent> ent)
    {
        var comp = ent.Comp;
        if (!_solution.TryGetSolution(ent.Owner, comp.Solution, out var soln, out var solution) || solution.Volume <= 0)
        {
            _hot.SetHot(ent.Owner, false);
            return;
        }

        // Остывание к температуре окружения.
        var ambient = _atmosphere.GetContainingMixture(ent.Owner)?.Temperature ?? comp.FallbackAmbient;
        var decay = MathF.Min(1f, comp.CoolingRate * comp.UpdateInterval);
        var temperature = solution.Temperature - (solution.Temperature - ambient) * decay;
        _solution.SetTemperature(soln.Value, temperature);
        _hot.SetHot(ent.Owner, temperature > comp.HotThreshold);

        // Застыл ли основной металл?
        if (solution.GetPrimaryReagentId() is not { } primary ||
            !_proto.TryIndex(primary.Prototype, out var reagent) ||
            reagent.MeltingPoint is not { } melting ||
            temperature > melting + DystopiaCrucibleSystem.KelvinOffset ||
            !_proto.TryIndex(comp.Shape, out var shape))
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
            Spawn(product, coords);
        }

        // Недолив застыл комом — брак.
        if (solution.Volume > 0)
        {
            _solution.RemoveAllSolution(soln.Value);
            Spawn(shape.Defect, coords);
        }

        _hot.SetHot(ent.Owner, false);
    }

    private void OnExamined(Entity<DystopiaMouldComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (_proto.TryIndex(ent.Comp.Shape, out var shape))
        {
            args.PushMarkup(Loc.GetString("dystopia-mould-shape",
                ("shape", Loc.GetString(shape.Name)), ("volume", shape.Volume)));
        }

        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) && solution.Volume > 0)
        {
            args.PushMarkup(Loc.GetString("dystopia-foundry-temperature",
                ("temperature", MathF.Round(solution.Temperature - DystopiaCrucibleSystem.KelvinOffset))));
        }
    }
}
