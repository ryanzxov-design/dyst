// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: кровотечение из ран, перевязка по части тела, жгут.

using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Medical.Bleeding;

public sealed partial class WoundBleedingSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> TourniquetTag = "Tourniquet";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems.TraumaSystem _traumas = default!;
    [Dependency] private Content.Shared.DoAfter.SharedDoAfterSystem _doAfter = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Medical.Organs.OrganFunctionSystem _organs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BleedInflicterComponent, WoundSeverityPointChangedEvent>(OnWoundSeverityChanged);
        SubscribeLocalEvent<BleedInflicterComponent, WoundHealAttemptEvent>(OnWoundHealAttempt);
        SubscribeLocalEvent<WoundBleedingComponent, BleedModifierEvent>(OnBleedModifier);
        SubscribeLocalEvent<WoundBleedingComponent, BandageAppliedEvent>(OnBandageApplied);
        SubscribeLocalEvent<WoundBleedingComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WoundBleedingComponent, GetVerbsEvent<InteractionVerb>>(OnGetCauterizeVerbs);
        SubscribeLocalEvent<WoundBleedingComponent, CauterizeDoAfterEvent>(OnCauterizeDoAfter);
        SubscribeLocalEvent<CauterizingWoundComponent, WoundSeverityPointChangedEvent>(OnBurn);
    }

    // ===================== Раны =====================

    private void OnWoundSeverityChanged(Entity<BleedInflicterComponent> ent, ref WoundSeverityPointChangedEvent args)
    {
        if (_net.IsClient || args.NewSeverity <= args.OldSeverity || args.NewSeverity < ent.Comp.SeverityThreshold)
            return;

        // Новая или разбереженная рана снова кровоточит, повязка не держит
        ent.Comp.IsBleeding = true;
        ent.Comp.Bandaged = false;
        ent.Comp.PackedFactor = 1f;
        ent.Comp.BleedingStarted = _timing.CurTime;
        Dirty(ent);
    }

    /// <summary>Пока рана кровоточит, она не заживает.</summary>
    private void OnWoundHealAttempt(Entity<BleedInflicterComponent> ent, ref WoundHealAttemptEvent args)
    {
        if (!args.IgnoreBlockers && ent.Comp.IsBleeding && !ent.Comp.Bandaged)
            args.Cancelled = true;
    }

    /// <summary>Ванильное «кровотечение само ослабевает» выключено: сворачиваются конкретные раны.</summary>
    private void OnBleedModifier(Entity<WoundBleedingComponent> ent, ref BleedModifierEvent args)
    {
        args.BleedReductionAmount = 0;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WoundBleedingComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var bleeding, out var bloodstream))
        {
            if (now < bleeding.NextUpdate)
                continue;

            bleeding.NextUpdate = now + bleeding.UpdateInterval;
            UpdateBody((uid, bleeding), bloodstream, now);
        }
    }

    private void UpdateBody(Entity<WoundBleedingComponent> body, BloodstreamComponent bloodstream, TimeSpan now)
    {
        var total = 0f;
        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp<WoundableComponent>(part, out var woundable))
                continue;

            var underTourniquet = IsUnderTourniquet(part);
            var veinsDamaged = HasVeinDamage(part, woundable);
            if (TryComp<TourniquetAppliedComponent>(part, out var tourniquet))
                TickTourniquet(body, part, tourniquet, now);

            foreach (var wound in _wounds.GetWoundableWounds(part, woundable))
            {
                if (!TryComp<BleedInflicterComponent>(wound, out var bleed) || !bleed.IsBleeding)
                    continue;

                // Небольшие раны сворачиваются сами (но не при повреждённых венах)
                if (!veinsDamaged && wound.Comp.WoundSeverityPoint < bleed.ClotSeverityLimit
                    && (now - bleed.BleedingStarted).TotalSeconds > bleed.ClotTime)
                {
                    bleed.IsBleeding = false;
                    bleed.BleedingAmount = 0;
                    Dirty(wound, bleed);
                    continue;
                }

                var amount = bleed.Bandaged || underTourniquet
                    ? 0f
                    : wound.Comp.WoundSeverityPoint.Float() * bleed.BleedingCoefficient
                      * (veinsDamaged ? body.Comp.VeinBleedMultiplier : 1f)
                      * bleed.PackedFactor;

                if (MathF.Abs(amount - bleed.BleedingAmount) > 0.01f)
                {
                    bleed.BleedingAmount = amount;
                    Dirty(wound, bleed);
                }

                total += amount;
            }
        }

        // Повреждённое сердце гонит кровь хуже — раны кровоточат сильнее
        total *= _organs.GetBleedMultiplier(body);

        // Кровоточащий человек кровь не восстанавливает: компенсируем ванильное восстановление,
        // иначе оно гасит кровотечение до 1 ед. за такт и рана «сильно кровоточит», а кровь не убывает
        if (total > 0f && _bloodstream.GetBloodLevel((body, bloodstream)) < 1f)
            total += body.Comp.RegenerationOffset;

        // Лекарства меняют кровотечение напрямую — запоминаем их вклад, а не затираем
        var comp = body.Comp;
        var external = bloodstream.BleedAmount - comp.LastSetBleed;
        if (MathF.Abs(external) > 0.001f)
            comp.ExternalModifier += external;

        var decay = comp.ExternalDecay * (float) comp.UpdateInterval.TotalSeconds;
        comp.ExternalModifier = comp.ExternalModifier > 0
            ? MathF.Max(0f, comp.ExternalModifier - decay)
            : MathF.Min(0f, comp.ExternalModifier + decay);
        comp.ExternalModifier = Math.Clamp(comp.ExternalModifier, -bloodstream.MaxBleedAmount, bloodstream.MaxBleedAmount);

        total = Math.Clamp(total + comp.ExternalModifier, 0f, bloodstream.MaxBleedAmount);
        var delta = total - bloodstream.BleedAmount;
        if (MathF.Abs(delta) > 0.001f)
            _bloodstream.TryModifyBleedAmount((body, bloodstream), delta);
        comp.LastSetBleed = bloodstream.BleedAmount;
    }

    /// <summary>Перекрыта ли кровь этой части жгутом (на ней самой или выше: на ноге для стопы).</summary>
    public bool IsUnderTourniquet(EntityUid part)
    {
        var current = (EntityUid?) part;
        while (current is { } p)
        {
            if (HasComp<TourniquetAppliedComponent>(p))
                return true;

            current = _wounds.GetParentWoundable(p);
        }

        return false;
    }

    /// <summary>Остановить кровотечение всех ран части (хирургия). Возвращает, было ли что останавливать.</summary>
    public bool StopBleeding(EntityUid part)
    {
        if (!TryComp<WoundableComponent>(part, out var woundable))
            return false;

        var any = false;
        foreach (var wound in _wounds.GetWoundableWounds(part, woundable))
        {
            if (!TryComp<BleedInflicterComponent>(wound, out var bleed) || !bleed.IsBleeding)
                continue;

            bleed.IsBleeding = false;
            bleed.Bandaged = false;
            bleed.PackedFactor = 1f;
            bleed.BleedingAmount = 0;
            Dirty(wound, bleed);
            any = true;
        }

        return any;
    }

    /// <summary>Кровоточит ли часть.</summary>
    public bool IsBleeding(EntityUid part)
    {
        if (!TryComp<WoundableComponent>(part, out var woundable))
            return false;

        foreach (var wound in _wounds.GetWoundableWounds(part, woundable))
        {
            if (TryComp<BleedInflicterComponent>(wound, out var bleed) && bleed.IsBleeding && !bleed.Bandaged)
                return true;
        }

        return false;
    }

    private bool HasVeinDamage(EntityUid part, WoundableComponent woundable)
    {
        foreach (var trauma in _traumas.GetWoundableTraumas(part, woundable))
        {
            if (trauma.Comp.TraumaType == Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems.TraumaSystem.VeinsDamage)
                return true;
        }

        return false;
    }

    // ===================== Прижигание =====================

    /// <summary>Сильный ожог прижигает рану: кровь части останавливается.</summary>
    private void OnBurn(Entity<CauterizingWoundComponent> ent, ref WoundSeverityPointChangedEvent args)
    {
        if (_net.IsClient || args.NewSeverity - args.OldSeverity < ent.Comp.Threshold)
            return;

        StopBleeding(args.Component.HoldingWoundable);
    }

    private void OnGetCauterizeVerbs(Entity<WoundBleedingComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } used
            || !TryComp<Content.Shared._Dystopia.Health.Surgery.SurgeryToolComponent>(used, out var tool)
            || !tool.Kinds.Contains(Content.Shared._Dystopia.Health.Surgery.SurgeryToolKind.Cautery)
            || _wounds.GetAimedPart(ent, args.User) is not { } part)
            return;

        var user = args.User;
        var body = ent.Owner;
        var time = ent.Comp.CauterizeTime;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cauterize-verb", ("part", Name(part))),
            Act = () =>
            {
                _popup.PopupClient(Loc.GetString("cauterize-start", ("part", Name(part))), body, user);
                _doAfter.TryStartDoAfter(new Content.Shared.DoAfter.DoAfterArgs(EntityManager, user, time,
                    new CauterizeDoAfterEvent(GetNetEntity(part)), body, target: body, used: used)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                });
            },
        });
    }

    private void OnCauterizeDoAfter(Entity<WoundBleedingComponent> ent, ref CauterizeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || _net.IsClient)
            return;

        args.Handled = true;
        var part = GetEntity(args.Part);
        if (!Exists(part))
            return;

        var stopped = StopBleeding(part);
        _wounds.DamagePart(ent, part, new DamageSpecifier(ent.Comp.CauterizeDamage), args.User);
        // Ожог от прижигания сам по себе снова останавливает кровь (на случай новой раны)
        StopBleeding(part);
        _popup.PopupEntity(Loc.GetString(stopped ? "cauterize-done" : "cauterize-nothing", ("part", Name(part))), ent, args.User);
    }

    // ===================== Перевязка и жгут =====================

    private void OnBandageApplied(Entity<WoundBleedingComponent> ent, ref BandageAppliedEvent args)
    {
        if (args.Handled || args.BloodlossModifier >= 0)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        var part = _wounds.GetAimedPart(ent, args.User) ?? FirstBleedingPart(ent);
        if (part is not { } target)
            return;

        var partName = Name(target);

        if (_tags.HasTag(args.Item, TourniquetTag))
        {
            ApplyTourniquet(ent, target, args.User, partName);
            return;
        }

        // Марля держит неглубокие раны; гемостатическая губка — любые (или до своего предела)
        var maxSeverity = TryComp<HemostaticComponent>(args.Item, out var hemostatic)
            ? hemostatic.MaxSeverity
            : ent.Comp.BandageMaxSeverity;

        var stopped = 0;
        var tooDeep = 0;
        if (TryComp<WoundableComponent>(target, out var woundable))
        {
            foreach (var wound in _wounds.GetWoundableWounds(target, woundable))
            {
                if (!TryComp<BleedInflicterComponent>(wound, out var bleed) || !bleed.IsBleeding || bleed.Bandaged)
                    continue;

                // Слишком глубокая для этой повязки: туго забинтовать — кровь идёт слабее, но не останавливается
                if (maxSeverity > 0 && wound.Comp.WoundSeverityPoint > maxSeverity)
                {
                    if (bleed.PackedFactor > ent.Comp.BandagePackedFactor)
                    {
                        bleed.PackedFactor = ent.Comp.BandagePackedFactor;
                        Dirty(wound, bleed);
                    }

                    tooDeep++;
                    continue;
                }

                bleed.Bandaged = true;
                bleed.PackedFactor = 1f;
                bleed.BleedingAmount = 0;
                Dirty(wound, bleed);
                stopped++;
            }
        }

        var key = tooDeep > 0
            ? stopped > 0 ? "bleeding-bandaged-partial" : "bleeding-too-deep"
            : stopped > 0 ? "bleeding-bandaged" : "bleeding-bandage-nothing";
        _popup.PopupEntity(Loc.GetString(key, ("part", partName), ("target", Identity.Entity(ent, EntityManager))),
            ent, args.User);
    }

    private void ApplyTourniquet(EntityUid body, EntityUid part, EntityUid user, string partName)
    {
        if (!TryComp<BodyPartComponent>(part, out var bodyPart)
            || bodyPart.PartType is not (BodyPartType.Arm or BodyPartType.Leg or BodyPartType.Hand or BodyPartType.Foot))
        {
            // Жгут на грудь или голову не накладывают — возвращаем его в руки
            _popup.PopupEntity(Loc.GetString("tourniquet-only-limbs"), body, user, PopupType.SmallCaution);
            GiveTourniquet(user, body);
            return;
        }

        if (HasComp<TourniquetAppliedComponent>(part))
        {
            _popup.PopupEntity(Loc.GetString("tourniquet-already", ("part", partName)), body, user);
            GiveTourniquet(user, body);
            return;
        }

        var comp = EnsureComp<TourniquetAppliedComponent>(part);
        comp.AppliedAt = _timing.CurTime;
        var safe = TryComp<WoundBleedingComponent>(body, out var bodyBleeding) ? bodyBleeding.TourniquetSafeTime : TimeSpan.FromMinutes(5);
        comp.NextNecrosis = comp.AppliedAt + safe;
        Dirty(part, comp);
        _popup.PopupEntity(Loc.GetString("tourniquet-applied", ("part", partName), ("target", Identity.Entity(body, EntityManager))),
            body, user);
    }

    private void GiveTourniquet(EntityUid user, EntityUid near)
    {
        EntProtoId proto = TryComp<WoundBleedingComponent>(near, out var bleeding) ? bleeding.TourniquetItem : "Tourniquet";
        var item = Spawn(proto.Id, Transform(near).Coordinates);
        _hands.PickupOrDrop(user, item);
    }

    private EntityUid? FirstBleedingPart(EntityUid body)
    {
        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp<WoundableComponent>(part, out var woundable))
                continue;

            foreach (var wound in _wounds.GetWoundableWounds(part, woundable))
            {
                if (TryComp<BleedInflicterComponent>(wound, out var bleed) && bleed.IsBleeding && !bleed.Bandaged)
                    return part;
            }
        }

        return null;
    }

    /// <summary>Под жгутом дольше 5 минут конечность отмирает: понемногу клеточный урон.</summary>
    private void TickTourniquet(Entity<WoundBleedingComponent> body, EntityUid part, TourniquetAppliedComponent comp, TimeSpan now)
    {
        if (_mobState.IsDead(body))
            return;

        if (!comp.Warned && now - comp.AppliedAt > body.Comp.TourniquetSafeTime - body.Comp.TourniquetWarning)
        {
            comp.Warned = true;
            _popup.PopupEntity(Loc.GetString("tourniquet-numb", ("part", Name(part))), body, body, PopupType.SmallCaution);
        }

        if (now < comp.NextNecrosis)
            return;

        comp.NextNecrosis = now + body.Comp.NecrosisInterval;
        _wounds.DamagePart(body, part, new DamageSpecifier(body.Comp.NecrosisDamage));
    }

    private void OnGetVerbs(Entity<WoundBleedingComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        foreach (var (part, _) in _body.GetBodyChildren(ent))
        {
            if (!HasComp<TourniquetAppliedComponent>(part))
                continue;

            var target = part;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("tourniquet-remove-verb", ("part", Name(part))),
                Act = () => RemoveTourniquet(ent, target, user),
            });
        }
    }

    public void RemoveTourniquet(EntityUid body, EntityUid part, EntityUid user)
    {
        if (!RemComp<TourniquetAppliedComponent>(part) || _net.IsClient)
            return;

        GiveTourniquet(user, body);
        _popup.PopupEntity(Loc.GetString("tourniquet-removed", ("part", Name(part))), body, user);
    }
}
