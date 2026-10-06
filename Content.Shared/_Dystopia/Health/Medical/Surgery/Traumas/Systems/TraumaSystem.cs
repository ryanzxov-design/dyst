// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems;

/// <summary>
/// Травмы: переломы, повреждения органов, вен и нервов, отрыв конечностей.
/// Травма может случиться, когда рана резко тяжелеет: шанс считается по виду травмы и виду раны.
/// Травмы меняет только сервер.
/// </summary>
public sealed partial class TraumaSystem : EntitySystem
{
    public static readonly ProtoId<TraumaTypePrototype> BoneDamage = "BoneDamage";
    public static readonly ProtoId<TraumaTypePrototype> OrganDamage = "OrganDamage";
    public static readonly ProtoId<TraumaTypePrototype> NerveDamage = "NerveDamage";
    public static readonly ProtoId<TraumaTypePrototype> Dismemberment = "Dismemberment";
    public static readonly ProtoId<TraumaTypePrototype> VeinsDamage = "VeinsDamage";

    private const string TraumaContainerId = "Traumas";

    [Dependency] private Content.Shared._Dystopia.Health.Armor.ArmorCoverageSystem _armor = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private readonly Dictionary<ProtoId<DamageTypePrototype>, List<CauseEntry>> _causes = new();
    private readonly List<CauseEntry> _anyDamageCauses = new();

    private readonly record struct CauseEntry(ProtoId<TraumaTypePrototype> Id, TraumaTypePrototype Proto, WoundSeverityCause Cause);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraumaInflicterComponent, ComponentInit>(OnInflicterInit);
        SubscribeLocalEvent<TraumaInflicterComponent, WoundSeverityPointChangedEvent>(OnWoundSeverityPointChanged);
        SubscribeLocalEvent<TraumaInflicterComponent, WoundHealAttemptEvent>(OnWoundHealAttempt);
        SubscribeLocalEvent<WoundableComponent, ApplyTraumaEvent>(OnApplyTrauma);
        SubscribeLocalEvent<WoundableComponent, MapInitEvent>(OnWoundableMapInit);
        SubscribeLocalEvent<TraumaComponent, ComponentShutdown>(OnTraumaShutdown);
        SubscribeLocalEvent<BoneComponent, BoneSeverityChangedEvent>(OnBoneSeverityChanged);
        SubscribeLocalEvent<BoneComponent, BoneIntegrityChangedEvent>(OnBoneIntegrityChanged);
        SubscribeLocalEvent<OrganIntegrityComponent, OrganDamageSeverityChanged>(OnOrganSeverityChanged);
        SubscribeLocalEvent<OrganIntegrityComponent, OrganIntegrityChangedEvent>(OnOrganIntegrityChanged);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildCauseIndex();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<TraumaTypePrototype>())
            BuildCauseIndex();
    }

    private void BuildCauseIndex()
    {
        _causes.Clear();
        _anyDamageCauses.Clear();
        foreach (var proto in _prototype.EnumeratePrototypes<TraumaTypePrototype>())
        {
            if (proto.Causes == null)
                continue;

            foreach (var cause in proto.Causes)
            {
                if (cause is not WoundSeverityCause wsc)
                    continue;

                var entry = new CauseEntry(proto.ID, proto, wsc);
                if (wsc.DamageTypes == null || wsc.DamageTypes.Count == 0)
                {
                    _anyDamageCauses.Add(entry);
                    continue;
                }

                foreach (var dt in wsc.DamageTypes)
                {
                    if (!_causes.TryGetValue(dt, out var list))
                        _causes[dt] = list = new List<CauseEntry>();
                    list.Add(entry);
                }
            }
        }
    }

    private void OnInflicterInit(Entity<TraumaInflicterComponent> ent, ref ComponentInit args)
    {
        ent.Comp.TraumaContainer = _container.EnsureContainer<Container>(ent, TraumaContainerId);
    }

    /// <summary>Каждой части тела — своя кость (кроме частей без кости).</summary>
    private void OnWoundableMapInit(Entity<WoundableComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.Bone.ContainedEntities.Count > 0 || string.IsNullOrEmpty(ent.Comp.BoneEntity.Id))
            return;

        if (!_prototype.HasIndex<EntityPrototype>(ent.Comp.BoneEntity.Id))
            return;

        var bone = Spawn(ent.Comp.BoneEntity.Id);
        if (!_container.Insert(bone, ent.Comp.Bone, force: true))
        {
            QueueDel(bone);
            return;
        }

        var boneComp = EnsureComp<BoneComponent>(bone);
        boneComp.BoneWoundable = ent;
        Dirty(bone, boneComp);
    }

    // ===================== Возникновение травм =====================

    private void OnWoundSeverityPointChanged(Entity<TraumaInflicterComponent> woundEnt, ref WoundSeverityPointChangedEvent args)
    {
        if (_net.IsClient || HasComp<GodmodeComponent>(args.Component.HoldingWoundable))
            return;

        var delta = args.Overflow ?? args.NewSeverity - args.OldSeverity;
        if (delta <= 0 || delta < woundEnt.Comp.SeverityThreshold)
            return;

        var woundable = args.Component.HoldingWoundable;
        if (!TryComp<WoundableComponent>(woundable, out var woundableComp))
            return;

        var traumas = RandomTraumaChance(woundable, woundEnt, delta, woundableComp);
        ApplyTraumas((woundable, woundableComp), woundEnt, traumas, delta);
    }

    public List<ProtoId<TraumaTypePrototype>> RandomTraumaChance(EntityUid target, Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 severity, WoundableComponent woundable)
    {
        var result = new List<ProtoId<TraumaTypePrototype>>();
        if (!TryComp<WoundComponent>(inflicter, out var wound))
            return result;

        var partType = CompOrNull<BodyPartComponent>(target)?.PartType ?? BodyPartType.Other;
        foreach (var entry in _anyDamageCauses)
            TryRoll(entry, target, woundable, inflicter, severity, partType, result);

        if (_causes.TryGetValue(wound.DamageType, out var eligible))
        {
            foreach (var entry in eligible)
                TryRoll(entry, target, woundable, inflicter, severity, partType, result);
        }

        return result;
    }

    private void TryRoll(CauseEntry entry, EntityUid target, WoundableComponent woundable, Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 severity, BodyPartType partType, List<ProtoId<TraumaTypePrototype>> result)
    {
        if (severity < entry.Proto.SeverityGate || !entry.Cause.PartAllowed(partType))
            return;

        var chance = GetChance(entry, (target, woundable), inflicter, severity);
        if (chance > 0 && _random.Prob((float) FixedPoint2.Clamp(chance, 0, 1)))
            result.Add(entry.Id);
    }

    private FixedPoint2 GetChance(CauseEntry entry, Entity<WoundableComponent> target, Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 severity)
    {
        if (entry.Cause.Chance is not { } provider)
            return entry.Proto.BaseChance;

        if (!TryComp<BodyPartComponent>(target, out var bodyPart) || bodyPart.Body is not { } body)
            return FixedPoint2.Zero;

        var args = new TraumaChanceArgs(EntityManager, target, inflicter, severity, bodyPart, body);
        if (provider.Calculate(in args) is not { } baseChance)
            return FixedPoint2.Zero;

        // Вычеты: «стойкость» самой части к травме + броня, которая закрывает часть
        var deduction = target.Comp.TraumaDeductions.GetValueOrDefault(entry.Id.Id, FixedPoint2.Zero);
        if (inflicter.Comp.AllowArmourDeduction.Contains(entry.Id) && TryComp<WoundComponent>(inflicter, out var inflictingWound))
        {
            deduction += inflicter.Comp.ArmourDeductionFactor
                         * _armor.GetPartProtection(body, bodyPart.PartType, inflictingWound.DamageType.Id);
        }
        if (deduction >= 1)
            return FixedPoint2.Zero;

        return FixedPoint2.Clamp(baseChance - deduction + inflicter.Comp.TraumasChances.GetValueOrDefault(entry.Id), 0, 1);
    }

    /// <summary>Часть изуродована: травмы из MangledMultipliers раны случаются наверняка.</summary>
    public void ApplyMangledTraumas(EntityUid woundable, EntityUid wound, FixedPoint2 severity)
    {
        if (_net.IsClient
            || !TryComp<TraumaInflicterComponent>(wound, out var inflicter)
            || !TryComp<WoundableComponent>(woundable, out var woundableComp)
            || inflicter.MangledMultipliers == null)
            return;

        var traumas = new List<ProtoId<TraumaTypePrototype>>();
        foreach (var traumaType in inflicter.MangledMultipliers.Keys)
        {
            if (!_prototype.TryIndex(traumaType, out var proto))
                continue;

            if (proto.Target == TraumaTarget.Bone && GetBone(woundableComp) == null)
                continue;

            traumas.Add(traumaType);
        }

        ApplyTraumas((woundable, woundableComp), (wound, inflicter), traumas, severity);
    }

    private void ApplyTraumas(Entity<WoundableComponent> target, Entity<TraumaInflicterComponent> inflicter,
        List<ProtoId<TraumaTypePrototype>> traumas, FixedPoint2 severity)
    {
        if (_net.IsClient || traumas.Count == 0 || CompOrNull<BodyPartComponent>(target)?.Body == null)
            return;

        foreach (var traumaType in traumas)
        {
            if (TerminatingOrDeleted(target))
                return;

            ApplyTrauma(target, inflicter, traumaType, severity);
        }
    }

    private void ApplyTrauma(Entity<WoundableComponent> target, Entity<TraumaInflicterComponent> inflicter,
        ProtoId<TraumaTypePrototype> traumaType, FixedPoint2 severity)
    {
        if (!_prototype.TryIndex(traumaType, out var proto) || SelectTraumaTarget(target, proto.Target) is not { } chosen)
            return;

        var before = new BeforeTraumaInducedEvent(severity, chosen, traumaType);
        RaiseLocalEvent(target, ref before);
        if (before.Cancelled)
            return;

        var apply = new ApplyTraumaEvent(traumaType, target, inflicter, chosen, severity);
        RaiseLocalEvent(target, ref apply);
        if (!apply.Handled)
            AddTrauma(chosen, target, inflicter, traumaType, severity);
    }

    private EntityUid? SelectTraumaTarget(Entity<WoundableComponent> woundable, TraumaTarget target)
    {
        return target switch
        {
            TraumaTarget.Bone => GetBone(woundable.Comp),
            TraumaTarget.Organ => SelectRandomOrgan(woundable),
            TraumaTarget.ParentWoundable => _wound.GetParentWoundable(woundable),
            TraumaTarget.Woundable => woundable.Owner,
            _ => null,
        };
    }

    private EntityUid? SelectRandomOrgan(EntityUid woundable)
    {
        var organs = _body.GetPartOrgans(woundable)
            .Where(o => !TryComp<OrganIntegrityComponent>(o.Id, out var integrity) || integrity.Integrity > 0)
            .ToList();
        return organs.Count == 0 ? null : _random.Pick(organs).Id;
    }

    private void OnApplyTrauma(Entity<WoundableComponent> ent, ref ApplyTraumaEvent args)
    {
        if (args.TraumaType == BoneDamage)
        {
            ApplyBoneTrauma(args.Target, args.Woundable, args.Inflicter, args.Severity);
            args.Handled = true;
        }
        else if (args.TraumaType == OrganDamage)
        {
            var trauma = AddTrauma(args.Target, args.Woundable, args.Inflicter, OrganDamage, args.Severity);
            if (trauma != EntityUid.Invalid)
                SetOrganDamageModifier(args.Target, args.Severity, trauma, "WoundableDamage");
            args.Handled = true;
        }
        else if (args.TraumaType == Dismemberment)
        {
            // args.Target — родительская часть; отрывается та часть, на которой рана
            var part = args.Woundable.Owner;
            if (!_wound.IsWoundableRoot(part) && TryComp<BodyPartComponent>(part, out var bodyPart) && bodyPart.Body is { } body)
            {
                var dismember = new DismemberRequestEvent(body, part);
                RaiseLocalEvent(ref dismember);
            }

            args.Handled = true;
        }
        // Нервы — эффект через боль (Ф6), вены — через кровотечение (Ф5): пока травма просто записывается
    }

    // ===================== Учёт травм =====================

    public EntityUid AddTrauma(EntityUid target, Entity<WoundableComponent> holdingWoundable, Entity<TraumaInflicterComponent> inflicter,
        ProtoId<TraumaTypePrototype> traumaType, FixedPoint2 severity)
    {
        if (TerminatingOrDeleted(inflicter))
            return EntityUid.Invalid;

        foreach (var existing in inflicter.Comp.TraumaContainer.ContainedEntities)
        {
            if (!TryComp<TraumaComponent>(existing, out var existingComp)
                || existingComp.TraumaType != traumaType || existingComp.TraumaTarget != target)
                continue;

            existingComp.TraumaSeverity = severity;
            Dirty(existing, existingComp);
            return existing;
        }

        var entityProto = inflicter.Comp.TraumaPrototypes.TryGetValue(traumaType, out var overrideProto)
            ? overrideProto
            : _prototype.Index(traumaType).TraumaEntity;

        var traumaEnt = Spawn(entityProto.Id);
        var traumaComp = EnsureComp<TraumaComponent>(traumaEnt);
        traumaComp.TraumaType = traumaType;
        traumaComp.TraumaSeverity = severity;
        traumaComp.TraumaTarget = target;
        traumaComp.HoldingWoundable = holdingWoundable;
        _container.Insert(traumaEnt, inflicter.Comp.TraumaContainer);

        var ev = new TraumaInducedEvent((traumaEnt, traumaComp), target, severity, traumaType);
        RaiseLocalEvent(holdingWoundable, ref ev);
        Dirty(traumaEnt, traumaComp);
        return traumaEnt;
    }

    public void RemoveTrauma(Entity<TraumaComponent> trauma)
    {
        if (_net.IsClient)
            return;

        if (trauma.Comp.TraumaTarget is { } target && trauma.Comp.HoldingWoundable is { } woundable)
        {
            var ev = new TraumaBeingRemovedEvent(trauma, target, trauma.Comp.TraumaSeverity, trauma.Comp.TraumaType);
            RaiseLocalEvent(woundable, ref ev);
        }

        QueueDel(trauma);
    }

    /// <summary>Травма пропала (вместе с раной или вручную): снимаем её вклад в повреждение органа.</summary>
    private void OnTraumaShutdown(Entity<TraumaComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.TraumaType == OrganDamage && ent.Comp.TraumaTarget is { } organ && TryComp<OrganIntegrityComponent>(organ, out var integrity))
        {
            if (integrity.Modifiers.Remove(("WoundableDamage", ent.Owner)) && !TerminatingOrDeleted(organ))
                UpdateOrganIntegrity(organ, integrity);
        }
    }

    private void OnWoundHealAttempt(Entity<TraumaInflicterComponent> inflicter, ref WoundHealAttemptEvent args)
    {
        if (args.IgnoreBlockers)
            return;

        foreach (var trauma in GetAllWoundTraumas(inflicter))
        {
            if (!_prototype.TryIndex(trauma.Comp.TraumaType, out var proto) || !proto.BlocksHealing)
                continue;

            if (GetTraumaHealFloor(trauma) is not { } floor)
            {
                args.Cancelled = true;
                continue;
            }

            if (floor > args.SeverityFloor)
                args.SeverityFloor = floor;
        }
    }

    private FixedPoint2? GetTraumaHealFloor(Entity<TraumaComponent> trauma)
    {
        if (trauma.Comp.TraumaTarget is not { } target)
            return null;

        if (TryComp<BoneComponent>(target, out var bone))
            return bone.HealSeverityFloor.GetValueOrDefault(bone.BoneSeverity);

        if (TryComp<OrganIntegrityComponent>(target, out var organ))
            return organ.HealSeverityFloor.GetValueOrDefault(organ.Severity);

        return null;
    }

    public IEnumerable<Entity<TraumaComponent>> GetAllWoundTraumas(EntityUid wound, TraumaInflicterComponent? component = null)
    {
        if (!Resolve(wound, ref component, false) || component.TraumaContainer == null)
            yield break;

        foreach (var trauma in component.TraumaContainer.ContainedEntities.ToArray())
        {
            if (TryComp<TraumaComponent>(trauma, out var comp))
                yield return (trauma, comp);
        }
    }

    /// <summary>Все травмы части тела.</summary>
    public IEnumerable<Entity<TraumaComponent>> GetWoundableTraumas(EntityUid woundable, WoundableComponent? woundableComp = null)
    {
        if (!Resolve(woundable, ref woundableComp, false))
            yield break;

        foreach (var wound in _wound.GetWoundableWounds(woundable, woundableComp))
        {
            foreach (var trauma in GetAllWoundTraumas(wound))
                yield return trauma;
        }
    }

    public EntityUid? GetBone(WoundableComponent woundable)
    {
        if (woundable.Bone == null)
            return null;

        foreach (var bone in woundable.Bone.ContainedEntities)
        {
            if (HasComp<BoneComponent>(bone))
                return bone;
        }

        return null;
    }

    // ===================== Кости =====================

    public bool ApplyBoneTrauma(EntityUid boneEnt, Entity<WoundableComponent> woundable, Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 severity, BoneComponent? boneComp = null)
    {
        if (!Resolve(boneEnt, ref boneComp))
            return false;

        if (_net.IsServer)
            AddTrauma(boneEnt, woundable, inflicter, BoneDamage, severity);

        ApplyDamageToBone(boneEnt, severity, boneComp);
        return true;
    }

    public bool ApplyDamageToBone(EntityUid bone, FixedPoint2 severity, BoneComponent? boneComp = null)
    {
        if (severity == 0 || !Resolve(bone, ref boneComp))
            return false;

        return SetBoneIntegrity(bone, boneComp.BoneIntegrity - severity, boneComp);
    }

    public bool SetBoneIntegrity(EntityUid bone, FixedPoint2 integrity, BoneComponent? boneComp = null)
    {
        if (!Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(integrity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        boneComp.BoneIntegrity = newIntegrity;
        RaiseLocalEvent(bone, ref ev);
        CheckBoneSeverity(bone, boneComp);
        Dirty(bone, boneComp);
        return true;
    }

    private void CheckBoneSeverity(EntityUid bone, BoneComponent boneComp)
    {
        var nearest = boneComp.BoneSeverity;
        foreach (var (severity, value) in boneComp.Thresholds.OrderByDescending(kv => kv.Value))
        {
            if (boneComp.BoneIntegrity < value)
                continue;

            nearest = severity;
            break;
        }

        if (nearest == boneComp.BoneSeverity)
            return;

        var ev = new BoneSeverityChangedEvent((bone, boneComp), boneComp.BoneSeverity, nearest);
        boneComp.BoneSeverity = nearest;
        Dirty(bone, boneComp);
        RaiseLocalEvent(bone, ref ev, true);
    }

    private void OnBoneSeverityChanged(Entity<BoneComponent> bone, ref BoneSeverityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable is not { } woundable || !TryComp<BodyPartComponent>(woundable, out var part) || part.Body is not { } body)
            return;

        // Скорость и способность стоять зависят от костей ног
        RaiseLocalEvent(body, new BoneStateChangedEvent());

        if (args.NewSeverity <= args.OldSeverity || _net.IsClient)
            return;

        var partName = Loc.GetString($"part-status-part-{part.PartType}-{part.Symmetry}");
        _popup.PopupEntity(Loc.GetString($"popup-trauma-BoneDamage-{args.NewSeverity}", ("part", partName)), body, body, PopupType.SmallCaution);

        var volume = bone.Comp.BreakVolume.GetValueOrDefault(args.NewSeverity, 0f);
        _audio.PlayPvs(bone.Comp.BoneBreakSound, body, AudioParams.Default.WithVolume(volume));
    }

    private void OnBoneIntegrityChanged(Entity<BoneComponent> bone, ref BoneIntegrityChangedEvent args)
    {
        // Кость срослась полностью — переломы этой кости больше не травма
        if (args.NewIntegrity < bone.Comp.IntegrityCap || bone.Comp.BoneWoundable is not { } woundable)
            return;

        foreach (var trauma in GetWoundableTraumas(woundable).ToList())
        {
            if (trauma.Comp.TraumaType == BoneDamage && trauma.Comp.TraumaTarget == bone.Owner)
                RemoveTrauma(trauma);
        }
    }

    /// <summary>
    /// Тяжесть кости части (нет кости — как целая). effective: с учётом шины — под шиной сломанная кость
    /// для движения и рук считается треснувшей. Для осмотра и анализатора — effective: false.
    /// </summary>
    public BoneSeverity GetBoneSeverity(EntityUid woundable, bool effective = true)
    {
        if (!TryComp<WoundableComponent>(woundable, out var comp) || GetBone(comp) is not { } bone)
            return BoneSeverity.Normal;

        var severity = Comp<BoneComponent>(bone).BoneSeverity;
        if (effective && TryComp<BoneSplintedComponent>(woundable, out var splint) && severity > splint.StabilizedSeverity)
            severity = splint.StabilizedSeverity;

        return severity;
    }

    // ===================== Органы =====================

    public void SetOrganDamageModifier(EntityUid organ, FixedPoint2 severity, EntityUid owner, string identifier)
    {
        if (severity == 0)
            return;

        var integrity = EnsureComp<OrganIntegrityComponent>(organ);
        integrity.Modifiers[(identifier, owner)] = severity * integrity.DamageMultiplier;
        UpdateOrganIntegrity(organ, integrity);
    }

    /// <summary>Отладка: добавить органу урон напрямую (без раны и травмы).</summary>
    public void DebugDamageOrgan(EntityUid organ, FixedPoint2 amount)
    {
        if (amount <= 0 || TerminatingOrDeleted(organ))
            return;

        var integrity = EnsureComp<OrganIntegrityComponent>(organ);
        var key = ("Debug", organ);
        integrity.Modifiers[key] = integrity.Modifiers.GetValueOrDefault(key) + amount;
        UpdateOrganIntegrity(organ, integrity);
    }

    public void RestoreOrganIntegrity(EntityUid organ, OrganIntegrityComponent? integrity = null)
    {
        if (!Resolve(organ, ref integrity, false))
            return;

        integrity.Modifiers.Clear();
        UpdateOrganIntegrity(organ, integrity);
    }

    private void UpdateOrganIntegrity(EntityUid uid, OrganIntegrityComponent organ)
    {
        var old = organ.Integrity;
        var total = FixedPoint2.Zero;
        foreach (var modifier in organ.Modifiers.Values)
        {
            total += modifier;
        }

        organ.Integrity = FixedPoint2.Clamp(organ.IntegrityCap - total, 0, organ.IntegrityCap);
        if (old != organ.Integrity)
        {
            var ev = new OrganIntegrityChangedEvent(old, organ.Integrity);
            RaiseLocalEvent(uid, ref ev);
        }

        var nearest = organ.Severity;
        foreach (var (severity, value) in organ.Thresholds.OrderBy(kv => kv.Value))
        {
            if (organ.Integrity > value)
                continue;

            nearest = severity;
            break;
        }

        if (nearest != organ.Severity)
        {
            var ev = new OrganDamageSeverityChanged(organ.Severity, nearest);
            organ.Severity = nearest;
            RaiseLocalEvent(uid, ref ev);
        }

        Dirty(uid, organ);
    }

    private void OnOrganIntegrityChanged(Entity<OrganIntegrityComponent> organ, ref OrganIntegrityChangedEvent args)
    {
        if (args.NewIntegrity < organ.Comp.IntegrityCap || !TryComp<OrganComponent>(organ, out var organComp) || organComp.Body is not { } body)
            return;

        foreach (var (part, _) in _body.GetBodyChildren(body).ToList())
        {
            foreach (var trauma in GetWoundableTraumas(part).ToList())
            {
                if (trauma.Comp.TraumaType == OrganDamage && trauma.Comp.TraumaTarget == organ.Owner)
                    RemoveTrauma(trauma);
            }
        }
    }

    private void OnOrganSeverityChanged(Entity<OrganIntegrityComponent> organ, ref OrganDamageSeverityChanged args)
    {
        if (_net.IsClient || args.NewSeverity <= args.OldSeverity
            || !TryComp<OrganComponent>(organ, out var organComp) || organComp.Body is not { } body)
            return;

        _popup.PopupEntity(Loc.GetString($"popup-trauma-OrganDamage-{args.NewSeverity}", ("organ", Name(organ))), body, body,
            PopupType.SmallCaution);

        if (args.NewSeverity != OrganSeverity.Destroyed)
            return;

        // Орган разрушен: человека скручивает от боли, орган пропадает
        if (!_mobState.IsDead(body))
            _stun.TryKnockdown(body, organ.Comp.DestroyedKnockdown, refresh: true, force: true);

        _audio.PlayPvs(organ.Comp.DestroyedSound, body);

        if (organ.Comp.Indestructible)
            return;

        // Смертельно важный орган (мозг) не пропадает — вместо этого умирает тело
        var destroyed = new OrganDestroyedEvent(organ.Owner, organComp.Category?.Id);
        RaiseLocalEvent(body, ref destroyed);
        if (destroyed.Handled)
            return;

        QueueDel(organ);
    }

    /// <summary>
    /// Орган понемногу восстанавливается: снимаем часть накопленных повреждений.
    /// Когда орган цел, травмы органа с ран снимаются и раны могут зажить.
    /// </summary>
    /// <param name="allowDestroyed">Восстанавливать и разрушенный орган (хирургия; сам по себе он не заживает).</param>
    public void RecoverOrgan(EntityUid uid, FixedPoint2 amount, OrganIntegrityComponent organ, bool allowDestroyed = false)
    {
        if (amount <= 0 || organ.Modifiers.Count == 0 || organ.Severity == OrganSeverity.Destroyed && !allowDestroyed)
            return;

        var total = FixedPoint2.Zero;
        foreach (var value in organ.Modifiers.Values)
        {
            total += value;
        }

        if (total <= 0)
            return;

        foreach (var key in organ.Modifiers.Keys.ToList())
        {
            var value = organ.Modifiers[key];
            var reduced = value - amount * (value / total);
            if (reduced <= FixedPoint2.New(0.01))
                organ.Modifiers.Remove(key);
            else
                organ.Modifiers[key] = reduced;
        }

        UpdateOrganIntegrity(uid, organ);
    }
}

/// <summary>Орган разрушен (вызывается на теле). Handled — орган не удалять (например, смерть мозга).</summary>
[ByRefEvent]
public record struct OrganDestroyedEvent(EntityUid Organ, string? Category, bool Handled = false);

/// <summary>Состояние костей тела изменилось (скорость, способность стоять).</summary>
public sealed class BoneStateChangedEvent : EntityEventArgs;
