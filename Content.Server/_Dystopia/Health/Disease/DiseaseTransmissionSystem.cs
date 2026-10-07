// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: пути передачи болезней сверх переноса из Goob — контакт (люди и предметы), иглы шприцев,
// еда и животные. Компоненты — Content.Shared/_Dystopia/Health/Disease/DiseaseTransmission.cs

using Content.Shared._Dystopia.Health.Disease;
using Content.Shared._Goobstation.Disease;
using Content.Shared._Goobstation.Disease.Components;
using Content.Shared._Goobstation.Disease.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Health.Disease;

public sealed partial class DiseaseTransmissionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedDiseaseSystem _disease = default!;
    [Dependency] private DiseaseNeedleSystem _needle = default!;

    private static readonly ProtoId<DiseaseSpreadPrototype> ContactSpread = "Contact";
    private static readonly ProtoId<DiseaseSpreadPrototype> BloodSpread = "Blood";

    /// <summary>
    /// Больше записей на одном предмете не держим — старые вытесняются.
    /// </summary>
    private const int MaxEntries = 8;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseCarrierComponent, ContactInteractionEvent>(OnCarrierContact);
        SubscribeLocalEvent<DiseaseContaminatedComponent, ContactInteractionEvent>(OnContaminatedContact);
        SubscribeLocalEvent<DiseaseOnTouchComponent, ContactInteractionEvent>(OnTouchSourceContact);
        SubscribeLocalEvent<DiseaseOnIngestComponent, IngestedEvent>(OnIngested);
        SubscribeLocalEvent<DiseaseNeedleComponent, DiseaseInjectorContactEvent>(OnNeedleContact);
    }

    #region Контакт

    /// <summary>
    /// Больной коснулся кого-то или чего-то: заражаем человека напрямую или оставляем заразу на предмете.
    /// Событие приходит обеим сторонам, так что каждая сторона передаёт только свои болезни.
    /// </summary>
    private void OnCarrierContact(Entity<DiseaseCarrierComponent> ent, ref ContactInteractionEvent args)
    {
        var (uid, carrier) = ent;
        var other = args.Other;
        if (other == uid || carrier.Diseases.Count == 0 || TerminatingOrDeleted(other))
            return;

        var otherIsCarrier = HasComp<DiseaseCarrierComponent>(other);
        var diseases = new List<EntityUid>(carrier.Diseases.ContainedEntities);
        foreach (var diseaseUid in diseases)
        {
            if (!TryComp<DiseaseComponent>(diseaseUid, out var disease)
                || !TryGetMarker<DiseaseContactSpreadEffectComponent>(disease, out var contact, out var severity))
                continue;

            // заразность растёт с тяжестью болезни
            var chance = contact.SpreadParams.Chance * severity * (0.25f + 0.75f * disease.InfectionProgress);
            var power = contact.SpreadParams.Power;

            // перчатки и костюм больного мешают передать заразу
            var outgoing = new DiseaseOutgoingSpreadAttemptEvent(power, chance, contact.SpreadParams.Type);
            RaiseLocalEvent(uid, ref outgoing);
            if (outgoing.Power < 0 || outgoing.Chance < 0)
                continue;

            if (otherIsCarrier)
            {
                _disease.DoInfectionAttempt(other, diseaseUid, outgoing.Power, outgoing.Chance, contact.SpreadParams.Type);
                continue;
            }

            if (!_random.Prob(Math.Clamp(contact.SurfaceChance * outgoing.Power, 0f, 1f)))
                continue;

            Contaminate(other,
                diseaseUid,
                new DiseaseSpreadSpecifier(outgoing.Chance, outgoing.Power, contact.SpreadParams.Type),
                contact.SurfaceLifetime);
        }
    }

    /// <summary>
    /// Кто-то тронул заражённый предмет.
    /// </summary>
    private void OnContaminatedContact(Entity<DiseaseContaminatedComponent> ent, ref ContactInteractionEvent args)
    {
        if (!HasComp<DiseaseCarrierComponent>(args.Other))
            return;

        InfectFrom(ent, args.Other, ContactSpread);
    }

    /// <summary>
    /// Погладили животное-переносчика.
    /// </summary>
    private void OnTouchSourceContact(Entity<DiseaseOnTouchComponent> ent, ref ContactInteractionEvent args)
    {
        if (args.Other == ent.Owner || !HasComp<DiseaseCarrierComponent>(args.Other))
            return;

        foreach (var entry in ent.Comp.Diseases)
        {
            _disease.DoInfectionAttempt(args.Other, entry.Disease, entry.SpreadParams);
        }
    }

    #endregion

    #region Еда

    private void OnIngested(Entity<DiseaseOnIngestComponent> ent, ref IngestedEvent args)
    {
        if (!HasComp<DiseaseCarrierComponent>(args.Target))
            return;

        foreach (var entry in ent.Comp.Diseases)
        {
            _disease.DoInfectionAttempt(args.Target, entry.Disease, entry.SpreadParams);
        }
    }

    #endregion

    #region Иглы

    /// <summary>
    /// Укол или забор крови: зараза с иглы попадает в кровь, а кровь больного — на иглу.
    /// </summary>
    private void OnNeedleContact(Entity<DiseaseNeedleComponent> ent, ref DiseaseInjectorContactEvent args)
    {
        if (!TryComp<DiseaseCarrierComponent>(args.Target, out var carrier))
            return;

        _needle.MarkUsed(ent);

        if (TryComp<DiseaseContaminatedComponent>(ent, out var contaminated))
            InfectFrom((ent.Owner, contaminated), args.Target, BloodSpread);

        var diseases = new List<EntityUid>(carrier.Diseases.ContainedEntities);
        foreach (var diseaseUid in diseases)
        {
            if (!TryComp<DiseaseComponent>(diseaseUid, out var disease)
                || !TryGetMarker<DiseaseBloodborneEffectComponent>(disease, out var blood, out var severity))
                continue;

            var chance = blood.SpreadParams.Chance * severity * (0.25f + 0.75f * disease.InfectionProgress);
            Contaminate(ent.Owner,
                diseaseUid,
                new DiseaseSpreadSpecifier(chance, blood.SpreadParams.Power, blood.SpreadParams.Type),
                blood.NeedleLifetime);
        }
    }

    #endregion

    #region Помощники

    /// <summary>
    /// Ищет у болезни симптом-метку пути передачи. Тяжесть — сила этого симптома.
    /// </summary>
    private bool TryGetMarker<T>(DiseaseComponent disease, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? marker, out float severity)
        where T : class, IComponent
    {
        marker = default;
        severity = 0f;
        foreach (var effectUid in disease.Effects.ContainedEntities)
        {
            if (!TryComp<T>(effectUid, out var found) || !TryComp<DiseaseEffectComponent>(effectUid, out var effect))
                continue;

            marker = found;
            severity = effect.Severity;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Оставляет на предмете (или игле) заразу от болезни-источника. Повторное касание продлевает срок.
    /// </summary>
    private void Contaminate(EntityUid target, EntityUid disease, DiseaseSpreadSpecifier spread, TimeSpan lifetime)
    {
        var comp = EnsureComp<DiseaseContaminatedComponent>(target);
        var expires = _timing.CurTime + lifetime;
        Prune(comp);

        foreach (var entry in comp.Entries)
        {
            if (entry.Disease != disease || entry.Spread.Type != spread.Type)
                continue;

            entry.Spread = spread;
            entry.Expires = expires;
            return;
        }

        if (comp.Entries.Count >= MaxEntries)
            comp.Entries.RemoveAt(0);

        comp.Entries.Add(new DiseaseContamination(disease, spread, expires));
    }

    /// <summary>
    /// Пытается заразить существо всем, что лежит на предмете, указанным путём.
    /// </summary>
    private void InfectFrom(Entity<DiseaseContaminatedComponent> source, EntityUid target, ProtoId<DiseaseSpreadPrototype> type)
    {
        Prune(source.Comp);
        if (source.Comp.Entries.Count == 0)
        {
            RemCompDeferred<DiseaseContaminatedComponent>(source);
            return;
        }

        var entries = new List<DiseaseContamination>(source.Comp.Entries);
        foreach (var entry in entries)
        {
            if (entry.Spread.Type != type)
                continue;

            _disease.DoInfectionAttempt(target, entry.Disease, entry.Spread);
        }
    }

    /// <summary>
    /// Убирает истёкшую заразу и ту, чей источник уже вылечен.
    /// </summary>
    private void Prune(DiseaseContaminatedComponent comp)
    {
        var now = _timing.CurTime;
        comp.Entries.RemoveAll(e => e.Expires <= now || TerminatingOrDeleted(e.Disease) || !HasComp<DiseaseComponent>(e.Disease));
    }

    #endregion
}
