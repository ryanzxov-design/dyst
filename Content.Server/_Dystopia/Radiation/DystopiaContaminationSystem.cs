// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: радиоактивное заражение (см. DystopiaContaminationPrototype). Раз в пару секунд пересчитывает лужи: если
// в луже есть радиоактивное вещество — она светит (ванильный RadiationSource, сила — по количеству), нет — источник
// снимается. Собрали шваброй, нейтрализовали очистителем, лужа высохла до чистой — перестала светить.
// Contaminate — разбросать радиоактивную пыль вокруг точки (аварии; пригодится реактору).

using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Radiation.Systems;
using Content.Shared._Dystopia.Radiation;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Radiation;

public sealed partial class DystopiaContaminationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private RadiationSystem _radiation = default!;
    [Dependency] private PuddleSystem _puddle = default!;

    private static readonly ProtoId<DystopiaContaminationPrototype> Config = "Default";

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate || !_proto.TryIndex(Config, out var config))
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(config.Interval);

        var query = EntityQueryEnumerator<PuddleComponent>();
        while (query.MoveNext(out var uid, out var puddle))
        {
            var intensity = 0f;
            if (_solution.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
                intensity = Intensity(config, solution);

            var contaminated = HasComp<DystopiaContaminatedComponent>(uid);
            if (intensity >= config.MinIntensity)
            {
                if (!contaminated && HasComp<RadiationSourceComponent>(uid))
                    continue; // светит само по себе — не наше

                EnsureComp<DystopiaContaminatedComponent>(uid);
                var source = EnsureComp<RadiationSourceComponent>(uid);
                _radiation.SetIntensity((uid, source), intensity);
                _radiation.SetSourceEnabled((uid, source), true);
            }
            else if (contaminated)
            {
                RemComp<RadiationSourceComponent>(uid);
                RemComp<DystopiaContaminatedComponent>(uid);
            }
        }
    }

    /// <summary>Рад/с от раствора: сумма по радиоактивным веществам.</summary>
    public static float Intensity(DystopiaContaminationPrototype config, Solution solution)
    {
        var total = 0f;
        foreach (var (reagent, perUnit) in config.Reagents)
        {
            total += solution.GetTotalPrototypeQuantity(reagent).Float() * perUnit;
        }

        return total;
    }

    /// <summary>
    /// Разбросать радиоактивное вещество лужами вокруг точки: <paramref name="spots"/> луж по
    /// <paramref name="units"/> единиц в радиусе <paramref name="radius"/> клеток.
    /// </summary>
    public void Contaminate(EntityCoordinates center, ProtoId<ReagentPrototype> reagent, float units, int spots, float radius)
    {
        for (var i = 0; i < spots; i++)
        {
            var offset = _random.NextVector2(radius);
            var coords = center.Offset(new Vector2(offset.X, offset.Y));
            _puddle.TrySpillAt(coords, new Solution(reagent.Id, FixedPoint2.New(units)), out _, sound: false);
        }
    }
}
