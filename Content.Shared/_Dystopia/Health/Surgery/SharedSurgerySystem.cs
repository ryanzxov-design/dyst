// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос хирургии Shitmed из Goob-Station (space-syndicate/Goob-Station, AGPL-3.0).
// Операции и шаги — прототипы-сущности (одна сущность на прототип, в нуль-пространстве); состояние операции
// хранится компонентами на части тела. Доступность операции проверяется событием SurgeryValidEvent
// на сущностях шага и операции (условия — компоненты).
// Dystopia: тело — органы с категориями (а не слоты), раны, травмы и боль — наши.

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Bleeding;
using Content.Shared._Dystopia.Health.Medical.Pain;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared._Dystopia.Health.Surgery.Conditions;
using Content.Shared._Dystopia.Health.Surgery.Steps;
using Content.Shared._Dystopia.Health.Surgery.Steps.Parts;
using Content.Shared._Dystopia.Health.Surgery.Tools;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Surgery;

public abstract partial class SharedSurgerySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private BodySystem _bodyCore = default!;
    [Dependency] private OrganRelationSystem _relation = default!;
    [Dependency] private DetachableOrganSystem _detachable = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<StackComponent> _stackQuery;

    /// <summary>Сущности-одиночки прототипов операций и шагов. Сбрасываются при перезагрузке прототипов.</summary>
    private readonly Dictionary<EntProtoId, EntityUid> _surgeries = new();

    private readonly List<EntProtoId> _allSurgeries = new();

    /// <summary>Все прототипы операций.</summary>
    public IReadOnlyList<EntProtoId> AllSurgeries => _allSurgeries;

    public override void Initialize()
    {
        base.Initialize();

        _bodyQuery = GetEntityQuery<BodyComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<SurgeryTargetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SurgeryTargetComponent, DoAfterAttemptEvent<SurgeryDoAfterEvent>>(OnBeforeTargetDoAfter);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryDoAfterEvent>(OnTargetDoAfter);

        SubscribeLocalEvent<SurgeryHasBodyConditionComponent, SurgeryValidEvent>(OnHasBodyConditionValid);
        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgeryPartPresentConditionComponent, SurgeryValidEvent>(OnPartPresentConditionValid);
        SubscribeLocalEvent<SurgeryPartRemovedConditionComponent, SurgeryValidEvent>(OnPartRemovedConditionValid);
        SubscribeLocalEvent<SurgeryOrganConditionComponent, SurgeryValidEvent>(OnOrganConditionValid);
        SubscribeLocalEvent<SurgeryOrganSlotConditionComponent, SurgeryValidEvent>(OnOrganSlotConditionValid);
        SubscribeLocalEvent<SurgeryTraumaPresentConditionComponent, SurgeryValidEvent>(OnTraumaPresentConditionValid);
        SubscribeLocalEvent<SurgeryBoneDamagedConditionComponent, SurgeryValidEvent>(OnBoneDamagedConditionValid);
        SubscribeLocalEvent<SurgeryOrganDamagedConditionComponent, SurgeryValidEvent>(OnOrganDamagedConditionValid);
        SubscribeLocalEvent<SurgeryBleedsPresentConditionComponent, SurgeryValidEvent>(OnBleedsPresentConditionValid);
        SubscribeLocalEvent<SurgeryWoundedConditionComponent, SurgeryValidEvent>(OnWoundedValid);
        SubscribeLocalEvent<SurgeryPartComponentConditionComponent, SurgeryValidEvent>(OnPartComponentConditionValid);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        InitializeSteps();
        InitializeStart();

        LoadPrototypes();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _surgeries.Clear();
    }

    private void OnMapInit(Entity<SurgeryTargetComponent> ent, ref MapInitEvent args)
    {
        _ui.SetUi(ent.Owner, SurgeryUIKey.Key, new InterfaceData("SurgeryBui"));
    }

    private void OnBeforeTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref DoAfterAttemptEvent<SurgeryDoAfterEvent> args)
    {
        // Повторяемые шаги: остановиться, когда шаг выполнен или операция больше не подходит
        if (_net.IsClient || !args.Event.Repeat)
            return;

        if (args.Event.Target is not { } target
            || !IsSurgeryValid(ent, target, args.Event.Surgery, args.Event.Step, args.Event.User, out var surgery, out var part, out _)
            || IsStepComplete(ent, part, args.Event.Step, surgery))
        {
            args.Cancel();
        }
    }

    private void OnTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (args.Cancelled)
        {
            var failEv = new SurgeryStepFailedEvent(args.User, ent, args.Surgery, args.Step);
            RaiseLocalEvent(args.User, ref failEv);
            return;
        }

        var tool = _hands.GetActiveItemOrSelf(args.User);
        if (args.Handled
            || args.Target is not { } target
            || !IsSurgeryValid(ent, target, args.Surgery, args.Step, args.User, out var surgery, out var part, out var step)
            || !PreviousStepsComplete(ent, part, surgery, args.Step, args.User)
            || !CanPerformStep(args.User, ent, part, step, tool, false))
        {
            return;
        }

        var complete = IsStepComplete(ent, part, args.Step, surgery);
        args.Repeat = HasComp<SurgeryRepeatableStepComponent>(step) && !complete;
        var ev = new SurgeryStepEvent(args.User, ent, part, tool, surgery, step, complete);
        RaiseLocalEvent(step, ref ev);
        RaiseLocalEvent(args.User, ref ev);

        // Одноразовый инструмент тратится
        if (args.ToolUsed)
        {
            if (_stackQuery.HasComp(tool))
                _stack.ReduceCount(tool, 1);
            else
                PredictedQueueDel(tool);
        }

        RefreshUI(ent);
    }

    // ===================== Условия =====================

    private void OnHasBodyConditionValid(Entity<SurgeryHasBodyConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (CompOrNull<BodyPartComponent>(args.Part)?.Body == null)
            args.Cancelled = true;
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.Part, out var part))
        {
            args.Cancelled = true;
            return;
        }

        var typeMatch = ent.Comp.Parts.Contains(part.PartType);
        var symmetryMatch = ent.Comp.Symmetry == null || part.Symmetry == ent.Comp.Symmetry;
        var valid = typeMatch && symmetryMatch;

        if (ent.Comp.Inverse ? valid : !valid)
            args.Cancelled = true;
    }

    private void OnPartPresentConditionValid(Entity<SurgeryPartPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Part == EntityUid.Invalid || !HasComp<BodyPartComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnPartRemovedConditionValid(Entity<SurgeryPartRemovedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        // У этой части по устройству тела должна быть такая дочерняя часть
        if (!PartExpects(args.Body, args.Part, ent.Comp.Connection))
        {
            args.Cancelled = true;
            return;
        }

        // Её нет — можно пришить; есть — только если её только что пришили и шов не закрыт
        if (ChildOfCategory(args.Part, ent.Comp.Connection) is { } child && !HasComp<BodyPartReattachedComponent>(child))
            args.Cancelled = true;
    }

    private void OnOrganConditionValid(Entity<SurgeryOrganConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.Part, out var partComp) || partComp.Body != args.Body)
        {
            args.Cancelled = true;
            return;
        }

        var organs = PartOrgans(args.Part, ent.Comp.Organs).ToList();
        if (organs.Count > 0)
        {
            // Вставка: орган уже есть — нечего вставлять (кроме только что вставленного, его надо закрепить)
            if (ent.Comp.Inverse && (!ent.Comp.Reattaching || !organs.Any(o => HasComp<OrganReattachedComponent>(o))))
                args.Cancelled = true;
        }
        else if (!ent.Comp.Inverse)
        {
            args.Cancelled = true;
        }
    }

    private void OnOrganSlotConditionValid(Entity<SurgeryOrganSlotConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!PartExpects(args.Body, args.Part, ent.Comp.OrganSlot))
            args.Cancelled = true;
    }

    private void OnTraumaPresentConditionValid(Entity<SurgeryTraumaPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled)
            return;

        if (HasTrauma(args.Part, ent.Comp.TraumaType) == ent.Comp.Inverted)
            args.Cancelled = true;
    }

    private void OnBoneDamagedConditionValid(Entity<SurgeryBoneDamagedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!BoneDamaged(args.Part))
            args.Cancelled = true;
    }

    private void OnOrganDamagedConditionValid(Entity<SurgeryOrganDamagedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!DamagedOrgans(args.Part, ent.Comp.Organs).Any())
            args.Cancelled = true;
    }

    private void OnBleedsPresentConditionValid(Entity<SurgeryBleedsPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<WoundableComponent>(args.Part))
        {
            args.Cancelled = true;
            return;
        }

        var bleeding = _bleeding.IsBleeding(args.Part);
        if (ent.Comp.Inverted)
        {
            if (bleeding && !HasComp<BleedersClampedComponent>(args.Part))
                args.Cancelled = true;
        }
        else if (!bleeding)
        {
            args.Cancelled = true;
        }
    }

    private void OnWoundedValid(Entity<SurgeryWoundedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<WoundableComponent>(args.Part))
        {
            args.Cancelled = true;
            return;
        }

        // Пока операция идёт (надрез открыт), она остаётся в списке и после того, как раны обработаны
        if (GroupSeverity(args.Part, ent.Comp.DamageGroup) <= 0f && !HasComp<IncisionOpenComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnPartComponentConditionValid(Entity<SurgeryPartComponentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var present = true;
        foreach (var reg in ent.Comp.Components.Values)
        {
            if (!HasComp(args.Part, reg.Component.GetType()))
                present = false;
        }

        args.Cancelled |= present == ent.Comp.Inverse;
    }

    // ===================== Общее =====================

    protected bool IsSurgeryValid(EntityUid body, EntityUid targetPart, EntProtoId surgery, EntProtoId stepId,
        EntityUid user, out Entity<SurgeryComponent> surgeryEnt, out EntityUid part, out EntityUid step)
    {
        surgeryEnt = default;
        part = default;
        step = default;

        if (!HasComp<SurgeryTargetComponent>(body)
            || !IsLyingDown(body, user)
            || GetSingleton(surgery) is not { } surgeryEntId
            || !TryComp(surgeryEntId, out SurgeryComponent? surgeryComp)
            || !surgeryComp.Steps.Contains(stepId)
            || GetSingleton(stepId) is not { } stepEnt
            || !HasComp<BodyPartComponent>(targetPart))
        {
            return false;
        }

        var ev = new SurgeryValidEvent(body, targetPart);
        if (_timing.IsFirstTimePredicted)
        {
            RaiseLocalEvent(stepEnt, ref ev);
            if (!ev.Cancelled)
                RaiseLocalEvent(surgeryEntId, ref ev);
        }

        if (ev.Cancelled)
            return false;

        surgeryEnt = (surgeryEntId, surgeryComp);
        part = targetPart;
        step = stepEnt;
        return true;
    }

    /// <summary>Подходит ли операция к части (для списка операций в окне).</summary>
    public bool IsSurgeryAvailable(EntityUid body, EntityUid part, EntProtoId surgery)
    {
        if (GetSingleton(surgery) is not { } surgeryEnt)
            return false;

        var ev = new SurgeryValidEvent(body, part);
        RaiseLocalEvent(surgeryEnt, ref ev);
        return !ev.Cancelled;
    }

    public EntityUid? GetSingleton(EntProtoId surgeryOrStep)
    {
        if (!_prototypes.HasIndex(surgeryOrStep))
            return null;

        // Данные операций одинаковы на клиенте и сервере — у каждого свои одиночки
        if (!_surgeries.TryGetValue(surgeryOrStep, out var ent) || TerminatingOrDeleted(ent))
        {
            ent = Spawn(surgeryOrStep, MapCoordinates.Nullspace);
            _surgeries[surgeryOrStep] = ent;
        }

        return ent;
    }

    /// <summary>Лежит ли пациент (или пристёгнут лёжа). Иначе — подсказка хирургу.</summary>
    public bool IsLyingDown(EntityUid entity, EntityUid user)
    {
        if (_standing.IsDown(entity))
            return true;

        if (!TryComp<BuckleComponent>(entity, out var buckle))
            return true;

        if (TryComp<StrapComponent>(buckle.BuckledTo, out var strap))
        {
            var rotation = strap.Rotation;
            if (rotation.GetCardinalDir() is Direction.West or Direction.East)
                return true;
        }

        _popup.PopupClient(Loc.GetString("surgery-error-laying"), user, user);
        return false;
    }

    protected virtual void RefreshUI(EntityUid body)
    {
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        foreach (var uid in _surgeries.Values)
        {
            Del(uid);
        }

        _surgeries.Clear();
        _allSurgeries.Clear();
        foreach (var entity in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!entity.Abstract && entity.HasComp<SurgeryComponent>(_compFactory))
                _allSurgeries.Add(new EntProtoId(entity.ID));
        }
    }

    /// <summary>Тяжесть ран части по видам урона группы (Brute, Burn...).</summary>
    private float GroupSeverity(EntityUid part, ProtoId<DamageGroupPrototype> group)
    {
        var types = GroupTypes(group);
        var total = 0f;
        foreach (var wound in _wounds.GetWoundableWounds(part))
        {
            if (types.Contains(wound.Comp.DamageType.Id))
                total += wound.Comp.WoundSeverityPoint.Float();
        }

        return total;
    }

    private HashSet<string> GroupTypes(ProtoId<DamageGroupPrototype> group)
    {
        var types = new HashSet<string>();
        foreach (var type in _prototypes.Index(group).DamageTypes)
        {
            types.Add(type.Id);
        }

        return types;
    }
}
