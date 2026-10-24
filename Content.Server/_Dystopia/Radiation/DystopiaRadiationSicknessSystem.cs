// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лучевая болезнь (см. DystopiaRadiationSicknessPrototype). Облучение (то же, что наносит урон, после
// защиты костюма и йодида калия) копится дозой; доза спадает сама и быстрее с препаратами. Выше порога — болезнь,
// её сила — по дозе (прогресс болезни ведёт эта система, сама болезнь не растёт и иммунитет её не берёт). Доза
// ниже порога — болезнь проходит.

using Content.Server._Goobstation.Disease;
using Content.Shared._Dystopia.Radiation;
using Content.Shared._Goobstation.Disease.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Radiation.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Radiation;

public sealed partial class DystopiaRadiationSicknessSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private DiseaseSystem _disease = default!;

    private static readonly ProtoId<DystopiaRadiationSicknessPrototype> Config = "Default";
    private const string RadiationType = "Radiation";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DiseaseCarrierComponent, OnIrradiatedEvent>(OnIrradiated);
    }

    private void OnIrradiated(Entity<DiseaseCarrierComponent> ent, ref OnIrradiatedEvent args)
    {
        if (!_proto.TryIndex(Config, out var config))
            return;

        var rads = args.TotalRads;
        if (rads <= 0f)
            return;

        // Защита костюма — тем же событием, что режет урон.
        var damage = new DamageSpecifier();
        damage.DamageDict.Add(RadiationType, FixedPoint2.New(rads));
        var modify = new DamageModifyEvent(damage, args.Origin);
        RaiseLocalEvent(ent, modify);
        rads = modify.Damage.DamageDict.TryGetValue(RadiationType, out var left) ? MathF.Max(0f, left.Float()) : 0f;

        // Йодид калия и подобное — часть облучения не доходит.
        var protection = 0f;
        foreach (var (reagent, share) in config.Protectors)
        {
            if (InBlood(ent, reagent.Id))
                protection = MathF.Max(protection, share);
        }

        rads *= 1f - Math.Clamp(protection, 0f, 1f);
        if (rads <= 0f)
            return;

        EnsureComp<DystopiaRadiationDoseComponent>(ent).Dose += rads;
    }

    private bool InBlood(EntityUid uid, string reagent)
    {
        return TryComp<BloodstreamComponent>(uid, out var blood) &&
               _solution.TryGetSolution(uid, blood.BloodSolutionName, out _, out var solution) &&
               solution.GetTotalPrototypeQuantity(reagent) > 0;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_proto.TryIndex(Config, out var config))
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaRadiationDoseComponent, DiseaseCarrierComponent>();
        while (query.MoveNext(out var uid, out var dose, out var carrier))
        {
            if (now < dose.NextUpdate)
                continue;

            dose.NextUpdate = now + TimeSpan.FromSeconds(config.Interval);
            var dt = config.Interval;

            // Спад: сам и быстрее с препаратами (берётся самый сильный).
            var speed = 1f;
            foreach (var (reagent, factor) in config.Medicines)
            {
                if (InBlood(uid, reagent.Id))
                    speed = MathF.Max(speed, factor);
            }

            dose.Dose -= (config.DecayFlat + dose.Dose * config.DecayFraction) * speed * dt;
            if (dose.Dose < 0f)
                dose.Dose = 0f;

            Sync((uid, dose, carrier), config);

            if (dose.Dose <= 0f && dose.DiseaseUid == null)
                RemCompDeferred<DystopiaRadiationDoseComponent>(uid);
        }
    }

    /// <summary>Болезнь по дозе: появилась, усилилась, ослабла, прошла.</summary>
    private void Sync(Entity<DystopiaRadiationDoseComponent, DiseaseCarrierComponent> ent,
        DystopiaRadiationSicknessPrototype config)
    {
        var dose = ent.Comp1;
        // Болезнь лежит в контейнере носителя — её родитель он сам (контейнер Goob нам только на чтение).
        if (dose.DiseaseUid is { } old && (TerminatingOrDeleted(old) || Transform(old).ParentUid != ent.Owner))
            dose.DiseaseUid = null;

        var target = Math.Clamp((dose.Dose - config.Threshold) / MathF.Max(1f, config.Severe - config.Threshold), 0f, 1f);
        if (dose.Dose < config.Threshold)
        {
            // Ниже порога — болезнь проходит (прогресс в ноль: болезнь сама снимается).
            if (dose.DiseaseUid is { } cured && TryComp<DiseaseComponent>(cured, out var curedComp))
                _disease.ChangeInfectionProgress((cured, curedComp), -curedComp.InfectionProgress - 0.01f);
            return;
        }

        if (dose.DiseaseUid == null)
        {
            if (!_disease.TryInfect(ent.Owner, config.Disease, out var spawned, force: true))
                return;

            dose.DiseaseUid = spawned;
        }

        if (dose.DiseaseUid is { } disease && TryComp<DiseaseComponent>(disease, out var comp))
            _disease.ChangeInfectionProgress((disease, comp), MathF.Max(0.01f, target) - comp.InfectionProgress);
    }
}
