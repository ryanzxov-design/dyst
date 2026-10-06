// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Targeting;
using Content.Shared._Dystopia.Health.Targeting.Events;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.GameObjects;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;

/// <summary>
/// Раны по частям тела.
///
/// Как урон попадает в раны в нашей сборке: урон по-прежнему записывается в тело целиком (модель Injurable —
/// от неё работают крит, смерть, лечение и анализатор). Эта система слушает каждый урон по телу и повторяет его
/// на части тела: положительный урон — в часть, выбранную прицелом атакующего (с броском по таблице попаданий),
/// лечение — по ранам того же типа. Из урона части создаются и растут раны, из ран — целостность части.
///
/// Раны меняет только сервер, клиент получает их состояние по сети.
/// </summary>
public sealed partial class WoundSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private const string WoundContainerId = "Wounds";
    private const string BoneContainerId = "Bone";

    /// <summary>Сколько раз в секунду идёт естественное заживление.</summary>
    private const float MedicalHealingTickrate = 0.5f;

    /// <summary>Сколько секунд после урона тело не заживает само.</summary>
    private static readonly TimeSpan MinimumTimeBeforeHeal = TimeSpan.FromSeconds(2f);

    private readonly Dictionary<string, DamageGroupPrototype?> _damageTypeToGroup = new();
    private readonly Dictionary<EntityUid, TimeSpan> _lastDamaged = new();
    private readonly Dictionary<EntityUid, TimeSpan> _healAt = new();

    /// <summary>Если задано — урон по телу идёт строго в эту часть (естественное заживление части).</summary>
    private EntityUid? _forcedPart;

    /// <summary>
    /// Куда придётся текущий удар. Бросок делается один раз на удар: сначала его спрашивает броня
    /// (закрывает ли она эту часть), потом раны используют тот же результат.
    /// </summary>
    private readonly Dictionary<EntityUid, (GameTick Tick, EntityUid? Origin, TargetBodyPart Part)> _pendingHits = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);
        SubscribeLocalEvent<WoundableComponent, OrganGotInsertedEvent>(OnWoundablePartInserted);
        SubscribeLocalEvent<WoundableComponent, OrganGotRemovedEvent>(OnWoundablePartRemoved);
        SubscribeLocalEvent<WoundComponent, WoundSeverityChangedEvent>(OnWoundSeverityChanged);
        SubscribeLocalEvent<WoundableComponent, WoundHealAttemptOnWoundableEvent>(HealWoundsOnWoundableAttempt);
        // До того, как урон запишется в тело: тело получает ровно то, что легло на раны частей
        SubscribeLocalEvent<BodyComponent, DamageDealtEvent>(OnBodyDamageDealt, before: [typeof(DamageableSystem)]);
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnBodyRejuvenate);

        BuildDamageTypeToGroupCache();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<DamageGroupPrototype>())
            BuildDamageTypeToGroupCache();
    }

    private void BuildDamageTypeToGroupCache()
    {
        _damageTypeToGroup.Clear();
        foreach (var group in _prototype.EnumeratePrototypes<DamageGroupPrototype>())
        {
            foreach (var damageType in group.DamageTypes)
            {
                _damageTypeToGroup[damageType] = group;
            }
        }
    }

    public DamageGroupPrototype? GetDamageGroupByType(string id) => _damageTypeToGroup.GetValueOrDefault(id);

    // ===================== Части тела =====================

    private void OnWoundableInit(EntityUid uid, WoundableComponent comp, ComponentInit args)
    {
        comp.RootWoundable = uid;
        comp.Wounds = _container.EnsureContainer<Container>(uid, WoundContainerId);
        comp.Bone = _container.EnsureContainer<Container>(uid, BoneContainerId);
        comp.SortedThresholds = comp.Thresholds.OrderByDescending(kv => kv.Value).ToArray();

        if (comp.WoundableIntegrity <= 0 && comp.IntegrityCap > 0)
            comp.WoundableIntegrity = comp.IntegrityCap;
    }

    private void OnWoundablePartInserted(Entity<WoundableComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(args.Target))
            return;

        RefreshHierarchy(args.Target);
        UpdateBodyStatus(args.Target);
        _pendingBodySync.Add(args.Target);
    }

    private void OnWoundablePartRemoved(Entity<WoundableComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent))
            return;

        // Отделённая часть становится своим корнем
        ent.Comp.ParentWoundable = null;
        ent.Comp.RootWoundable = ent;
        Dirty(ent);

        // Тело удаляется целиком — пересчитывать нечего
        if (TerminatingOrDeleted(args.Target))
            return;

        RefreshHierarchy(args.Target);
        UpdateBodyStatus(args.Target);
        _pendingBodySync.Add(args.Target);
    }

    /// <summary>Пересобрать связи «родитель — дети» ран по связям частей тела.</summary>
    public void RefreshHierarchy(EntityUid body)
    {
        if (!_body.TryGetRootPart(body, out var root))
            return;

        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp<WoundableComponent>(part, out var woundable))
                continue;

            woundable.ChildWoundables.Clear();
            woundable.RootWoundable = root.Value.Owner;
            woundable.ParentWoundable = _body.TryGetParentBodyPart(part, out var parent, out _) ? parent : null;
        }

        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (TryComp<WoundableComponent>(part, out var woundable) && woundable.ParentWoundable is { } parent &&
                TryComp<WoundableComponent>(parent, out var parentWoundable))
            {
                parentWoundable.ChildWoundables.Add(part);
            }
        }

        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (TryComp<WoundableComponent>(part, out var woundable))
                Dirty(part, woundable);
        }
    }

    // ===================== Урон по телу → раны =====================

    private void OnBodyDamageDealt(Entity<BodyComponent> ent, ref DamageDealtEvent args)
    {
        if (_net.IsClient || args.Damage.Empty)
            return;

        var harm = new DamageSpecifier();
        var heal = new DamageSpecifier();
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value > 0)
                harm.DamageDict[type] = value;
            else if (value < 0)
                heal.DamageDict[type] = value;
        }

        if (!harm.Empty)
        {
            _lastDamaged[ent] = _timing.CurTime;
            if (ChooseTargetPart(ent, args.Origin) is { } part)
                InduceWoundsFromDamage(part, harm);
        }

        if (!heal.Empty)
        {
            if (_forcedPart is { } forced && TryComp<WoundableComponent>(forced, out var forcedComp))
            {
                foreach (var (type, value) in heal.DamageDict)
                {
                    HealWoundsCore(forced, -value, type, out _, forcedComp);
                }

                UpdateWoundableIntegrity(forced, forcedComp);
                CheckWoundableSeverityThresholds(forced, forcedComp);
            }
            else if (GetAimedPart(ent, args.Origin) is { } aimed && TryComp<WoundableComponent>(aimed, out var aimedComp))
            {
                // Перевязка/мазь лечит ту часть, куда целится лечащий; остаток — остальным частям
                var rest = new DamageSpecifier();
                foreach (var (type, value) in heal.DamageDict)
                {
                    HealWoundsCore(aimed, -value, type, out var healed, aimedComp);
                    var left = -value - healed;
                    if (left > 0)
                        rest.DamageDict[type] = -left;
                }

                UpdateWoundableIntegrity(aimed, aimedComp);
                CheckWoundableSeverityThresholds(aimed, aimedComp);
                if (!rest.Empty)
                    TryHealWoundsOnOwner(ent, rest);
            }
            else
            {
                TryHealWoundsOnOwner(ent, heal);
            }
        }

        // Тело получает ровно столько, сколько теперь на ранах его частей (часть не может принять больше
        // своей целостности; оторванная часть уносит свои раны с собой)
        if (GetBodySyncDelta(ent, args.Damage) is { } synced)
            args = new DamageDealtEvent(synced, args.Origin, args.InterruptsDoAfters);
    }

    // ===================== Урон тела = сумма ран частей =====================

    private readonly HashSet<EntityUid> _pendingBodySync = new();

    /// <summary>
    /// Урон, который нужно записать в тело, чтобы по видам ран оно сравнялось с суммой ран частей.
    /// Виды урона без ран (удушье, кровопотеря и т.п.) проходят как есть. null — тело без частей с ранами.
    /// </summary>
    private DamageSpecifier? GetBodySyncDelta(EntityUid body, DamageSpecifier incoming)
    {
        if (!TryComp<DamageableComponent>(body, out var damageable) || GetWoundSums(body) is not { } sums)
            return null;

        var result = new DamageSpecifier();
        foreach (var (type, value) in incoming.DamageDict)
        {
            if (!IsWoundPrototypeValid(type))
                result.DamageDict[type] = value;
        }

        // Словарь урона тела хранит только ненулевые виды: сверяем по объединению видов тела и ран
        var current = _damageable.GetAllDamage((body, damageable)).DamageDict;
        foreach (var type in current.Keys.Union(sums.Keys).ToList())
        {
            if (!IsWoundPrototypeValid(type))
                continue;

            var delta = sums.GetValueOrDefault(type) - current.GetValueOrDefault(type);
            if (delta != 0)
                result.DamageDict[type] = delta;
        }

        return result;
    }

    /// <summary>Сумма тяжести ран по видам урона на всех частях тела; null — частей с ранами нет.</summary>
    private Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>? GetWoundSums(EntityUid body)
    {
        Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>? sums = null;
        foreach (var (part, partComp) in _body.GetBodyChildren(body))
        {
            if (partComp.Body != body || !TryComp<WoundableComponent>(part, out var woundable))
                continue;

            sums ??= new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>();
            foreach (var wound in GetWoundableWounds(part, woundable))
            {
                sums[wound.Comp.DamageType] = sums.GetValueOrDefault(wound.Comp.DamageType) + wound.Comp.WoundSeverityPoint;
            }
        }

        return sums;
    }

    /// <summary>Пересчитать урон тела по ранам (после отрыва или пришивания части и т.п.).</summary>
    public void SyncBodyDamage(EntityUid body)
    {
        if (_net.IsClient || TerminatingOrDeleted(body) || !TryComp<DamageableComponent>(body, out var damageable)
            || GetWoundSums(body) is not { } sums)
            return;

        var target = new DamageSpecifier(_damageable.GetAllDamage((body, damageable)));
        var changed = false;
        foreach (var type in target.DamageDict.Keys.Union(sums.Keys).ToList())
        {
            if (!IsWoundPrototypeValid(type))
                continue;

            var value = sums.GetValueOrDefault(type);
            if (target.DamageDict.GetValueOrDefault(type) == value)
                continue;

            if (value == 0)
                target.DamageDict.Remove(type);
            else
                target.DamageDict[type] = value;
            changed = true;
        }

        if (changed)
            _damageable.SetDamage((body, damageable), target);
    }

    private void ProcessPendingBodySync()
    {
        if (_pendingBodySync.Count == 0)
            return;

        foreach (var body in _pendingBodySync)
        {
            // К следующему тику связи частей уже установлены — собираем иерархию заново
            if (!TerminatingOrDeleted(body))
                RefreshHierarchy(body);
            SyncBodyDamage(body);
        }

        _pendingBodySync.Clear();
    }

    /// <summary>Куда придётся удар по телу (бросок по прицелу атакующего), один раз на удар.</summary>
    public TargetBodyPart PeekTargetPart(EntityUid body, EntityUid? origin)
    {
        if (_pendingHits.TryGetValue(body, out var pending) && pending.Tick == _timing.CurTick && pending.Origin == origin)
            return pending.Part;

        var part = _body.GetRandomBodyPart(body, origin);
        _pendingHits[body] = (_timing.CurTick, origin, part);
        return part;
    }

    /// <summary>Часть тела, в которую пришёлся удар.</summary>
    private EntityUid? ChooseTargetPart(EntityUid body, EntityUid? origin)
    {
        if (_forcedPart is { } forced && HasComp<WoundableComponent>(forced))
            return forced;

        var target = PeekTargetPart(body, origin);
        _pendingHits.Remove(body);

        // Нужной части нет (оторвана) — удар приходится в то, что от неё осталось выше: стопа → нога → пах → грудь
        TargetBodyPart? aim = target;
        while (aim is { } current)
        {
            if (_body.GetTargetedPartEntity(body, current) is { } part && HasComp<WoundableComponent>(part))
                return part;

            aim = ParentTarget(current);
        }

        // Совсем ничего — в любую оставшуюся
        var parts = _body.GetBodyChildrenWithComponent<WoundableComponent>(body).ToList();
        return parts.Count == 0 ? null : _random.Pick(parts).Id;
    }

    /// <summary>Часть, в которую целится лечащий (у него есть прицел), или null.</summary>
    public EntityUid? GetAimedPart(EntityUid body, EntityUid? origin)
    {
        if (origin is not { } user || !TryComp<TargetingComponent>(user, out var aim))
            return null;

        return _body.GetTargetedPartEntity(body, aim.Target);
    }

    private static TargetBodyPart? ParentTarget(TargetBodyPart part) => part switch
    {
        TargetBodyPart.LeftFoot => TargetBodyPart.LeftLeg,
        TargetBodyPart.RightFoot => TargetBodyPart.RightLeg,
        TargetBodyPart.LeftHand => TargetBodyPart.LeftArm,
        TargetBodyPart.RightHand => TargetBodyPart.RightArm,
        TargetBodyPart.LeftLeg or TargetBodyPart.RightLeg => TargetBodyPart.Groin,
        TargetBodyPart.Groin or TargetBodyPart.LeftArm or TargetBodyPart.RightArm or TargetBodyPart.Head => TargetBodyPart.Chest,
        _ => null,
    };

    /// <summary>Урон по части → новые раны или рост старых.</summary>
    public void InduceWoundsFromDamage(EntityUid part, DamageSpecifier damage, WoundableComponent? woundable = null)
    {
        if (!Resolve(part, ref woundable, false) || !woundable.AllowWounds)
            return;

        // Родитель запоминаем заранее: разрушенная часть может оторваться
        var parent = GetParentWoundable(part);
        DamageSpecifier? overflow = null;

        foreach (var (type, value) in damage.DamageDict)
        {
            if (value <= 0 || !IsWoundPrototypeValid(type))
                continue;

            var before = SeverityOfType(part, type, woundable);
            TryInduceWound(part, type, value, out _, woundable);
            UpdateWoundableIntegrity(part, woundable);

            // Часть уже разрушена и больше не принимает: излишек уходит выше (стопа → нога → грудь)
            var absorbed = SeverityOfType(part, type, woundable) - before;
            if (IsDestroyed(woundable) && value - absorbed > FixedPoint2.New(0.01))
            {
                overflow ??= new DamageSpecifier();
                overflow.DamageDict[type] = value - absorbed;
            }
        }

        UpdateWoundableIntegrity(part, woundable);
        CheckWoundableSeverityThresholds(part, woundable);

        if (overflow != null && parent is { } parentPart && parentPart != part && !TerminatingOrDeleted(parentPart))
            InduceWoundsFromDamage(parentPart, overflow);

        TryDismemberDestroyed(part, damage, woundable);
    }

    /// <summary>Урон ран части вместе с её дочерними частями (нога + стопа), по видам урона.</summary>
    public DamageSpecifier GetLimbWoundDamage(EntityUid part)
    {
        var result = new DamageSpecifier();
        foreach (var woundable in GetAllWoundableChildren(part))
        {
            foreach (var wound in GetWoundableWounds(woundable, woundable))
            {
                result.DamageDict[wound.Comp.DamageType] =
                    result.DamageDict.GetValueOrDefault(wound.Comp.DamageType) + wound.Comp.WoundSeverityPoint;
            }
        }

        return result;
    }

    /// <summary>Нанести урон телу строго в эту часть (некроз под жгутом и т.п.).</summary>
    public void DamagePart(EntityUid body, EntityUid part, DamageSpecifier damage, EntityUid? origin = null)
    {
        if (_net.IsClient || !HasComp<WoundableComponent>(part))
            return;

        _forcedPart = part;
        try
        {
            _damageable.ChangeDamage(body, damage, ignoreResistances: true, interruptsDoAfters: false, origin: origin);
        }
        finally
        {
            _forcedPart = null;
        }
    }

    /// <summary>
    /// Хирургическая обработка ран части: снять до amount тяжести ран (поровну по видам), мимо блокировок травм.
    /// Возвращает, сколько снято. Урон тела пересчитывается сразу.
    /// </summary>
    public FixedPoint2 TendWounds(EntityUid part, FixedPoint2 amount)
    {
        if (_net.IsClient || amount <= 0 || !TryComp<WoundableComponent>(part, out var woundable))
            return FixedPoint2.Zero;

        var types = GetWoundableWounds(part, woundable).Select(w => w.Comp.DamageType.Id).Distinct().ToList();
        if (types.Count == 0)
            return FixedPoint2.Zero;

        var total = FixedPoint2.Zero;
        var perType = amount / types.Count;
        foreach (var type in types)
        {
            HealWoundsCore(part, perType, type, out var healed, woundable, ignoreBlockers: true);
            total += healed;
        }

        UpdateWoundableIntegrity(part, woundable);
        CheckWoundableSeverityThresholds(part, woundable);
        if (TryComp<BodyPartComponent>(part, out var bodyPart) && bodyPart.Body is { } body)
            SyncBodyDamage(body);

        return total;
    }

    /// <summary>Есть ли на части раны.</summary>
    public bool HasWounds(EntityUid part)
    {
        return TryComp<WoundableComponent>(part, out var woundable) && GetWoundableWounds(part, woundable).Any();
    }

    /// <summary>Часть разрушена: целостности почти не осталось.</summary>
    public static bool IsDestroyed(WoundableComponent woundable)
    {
        return woundable.IntegrityCap > 0 && woundable.WoundableIntegrity <= woundable.IntegrityCap * woundable.DestroyedFraction;
    }

    private FixedPoint2 SeverityOfType(EntityUid part, string type, WoundableComponent woundable)
    {
        var total = FixedPoint2.Zero;
        foreach (var wound in GetWoundableWounds(part, woundable))
        {
            if (wound.Comp.DamageType.Id == type)
                total += wound.Comp.WoundSeverityPoint;
        }

        return total;
    }

    // ===================== Естественное заживление =====================

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        ProcessPendingBodySync();
        var now = _timing.CurTime;

        // Чистим записи удалённых тел (подписку на удаление тела держит система тела)
        if (_healAt.Count > 0 && _random.Prob(0.01f))
        {
            foreach (var key in _healAt.Keys.ToArray())
            {
                if (TerminatingOrDeleted(key))
                {
                    _healAt.Remove(key);
                    _lastDamaged.Remove(key);
                    _pendingHits.Remove(key);
                }
            }
        }

        var query = EntityQueryEnumerator<BodyComponent>();
        while (query.MoveNext(out var body, out _))
        {
            if (Paused(body) || _mobState.IsIncapacitated(body))
                continue;

            if (_lastDamaged.TryGetValue(body, out var last) && now - last < MinimumTimeBeforeHeal)
                continue;

            if (_healAt.TryGetValue(body, out var healAt) && now < healAt)
                continue;

            _healAt[body] = now + TimeSpan.FromSeconds(1f / MedicalHealingTickrate);
            // Заживляем раны всех частей напрямую, а урон тела пересчитываем один раз —
            // вместо отдельного «лечения тела» на каждую часть каждую секунду
            var healedAny = false;
            foreach (var woundable in _body.GetBodyChildrenWithComponent<WoundableComponent>(body).ToList())
            {
                if (woundable.Component.CanHealDamage)
                    healedAny |= ProcessHealing((woundable.Id, woundable.Component), body);
            }

            if (healedAny)
                SyncBodyDamage(body);
        }
    }

    private bool ProcessHealing(Entity<WoundableComponent> woundable, EntityUid body)
    {
        var healableCount = 0;
        foreach (var wound in GetWoundableWounds(woundable, woundable))
        {
            if (CanHealWound(wound, wound))
                healableCount++;
        }

        if (healableCount == 0)
            return false;

        var healAmount = -woundable.Comp.HealAbility / healableCount;
        var perType = new Dictionary<string, FixedPoint2>();
        foreach (var wound in GetWoundableWounds(woundable, woundable).ToList())
        {
            if (!CanHealWound(wound, wound) || wound.Comp.SelfHealMultiplier <= 0)
                continue;

            var adjusted = ApplyHealingRateMultipliers(wound, woundable, healAmount, woundable);
            if (adjusted == 0)
                continue;

            var type = wound.Comp.DamageType.Id;
            perType[type] = perType.GetValueOrDefault(type) - adjusted;
        }

        if (perType.Count == 0)
            return false;

        var healedAny = false;
        foreach (var (type, amount) in perType)
        {
            HealWoundsCore(woundable, amount, type, out var healed, woundable);
            healedAny |= healed > 0;
        }

        if (!healedAny)
            return false;

        UpdateWoundableIntegrity(woundable, woundable);
        CheckWoundableSeverityThresholds(woundable, woundable);
        return true;
    }
}
