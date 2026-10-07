// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: тихое событие «вспышка болезни» — один (на большом онлайне двое) живой игрок подхватывает
// случайную болезнь из списка. Без объявления: о болезни узнают по симптомам. Редкость и задержки —
// в прототипе события (Resources/Prototypes/_Dystopia/GameRules/disease_outbreak.yml).

using Content.Server.StationEvents.Events;
using Content.Shared._Goobstation.Disease.Components;
using Content.Shared._Goobstation.Disease.Systems;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.Health.Disease;

public sealed partial class DiseaseOutbreakRule : StationEventSystem<DiseaseOutbreakRuleComponent>
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedDiseaseSystem _disease = default!;

    protected override void Started(EntityUid uid, DiseaseOutbreakRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.Diseases.Count == 0)
            return;

        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<ActorComponent, DiseaseCarrierComponent, MobStateComponent>();
        while (query.MoveNext(out var target, out _, out _, out var mobState))
        {
            if (!_mobState.IsAlive(target, mobState))
                continue;

            candidates.Add(target);
        }

        if (candidates.Count == 0)
            return;

        RobustRandom.Shuffle(candidates);

        var targets = component.Targets + candidates.Count / Math.Max(component.PlayersPerExtraTarget, 1);
        var infected = 0;
        foreach (var target in candidates)
        {
            if (infected >= targets)
                break;

            var disease = RobustRandom.Pick(component.Diseases);
            // иммунитет к этому генотипу или та же болезнь — пробуем следующего
            if (!_disease.TryInfect(target, disease, out _))
                continue;

            infected++;
            AdminLogManager.Add(LogType.EventRan, LogImpact.Medium,
                $"Disease outbreak infected {ToPrettyString(target):player} with {disease}");
        }
    }
}

[RegisterComponent, Access(typeof(DiseaseOutbreakRule))]
public sealed partial class DiseaseOutbreakRuleComponent : Component
{
    /// <summary>
    /// Болезни и их веса.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, float> Diseases = new();

    /// <summary>
    /// Сколько человек заражается.
    /// </summary>
    [DataField]
    public int Targets = 1;

    /// <summary>
    /// Ещё один заражённый на каждые столько живых игроков.
    /// </summary>
    [DataField]
    public int PlayersPerExtraTarget = 30;
}
