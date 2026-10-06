// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос хирургии Shitmed из Goob-Station (AGPL-3.0): выполнение шагов.
// Каждый особый шаг — компонент на сущности шага с парой обработчиков: «сделать» (SurgeryStepEvent)
// и «выполнен ли» (SurgeryStepCompleteCheckEvent). Изменения тела делает только сервер.

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Surgery.Conditions;
using Content.Shared._Dystopia.Health.Surgery.Effects;
using Content.Shared._Dystopia.Health.Surgery.Steps;
using Content.Shared._Dystopia.Health.Surgery.Steps.Parts;
using Content.Shared._Dystopia.Health.Surgery.Tools;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Surgery;

public abstract partial class SharedSurgerySystem
{
    private EntityQuery<BodyPartComponent> _partQuery;
    private EntityQuery<SurgeryIgnoreClothingComponent> _ignoreQuery;
    private EntityQuery<SurgeryStepComponent> _stepQuery;
    private EntityQuery<SurgeryToolComponent> _toolQuery;

    private readonly List<EntityUid> _nextStepList = new();

    /// <summary>Нестерильный шаг: заражение части.</summary>
    private static readonly ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> SepsisDamage = "Poison";

    private const float SepsisAmount = 5f;

    private void InitializeSteps()
    {
        _partQuery = GetEntityQuery<BodyPartComponent>();
        _ignoreQuery = GetEntityQuery<SurgeryIgnoreClothingComponent>();
        _stepQuery = GetEntityQuery<SurgeryStepComponent>();
        _toolQuery = GetEntityQuery<SurgeryToolComponent>();

        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepEvent>(OnToolStep);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepCompleteCheckEvent>(OnToolCheck);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryCanPerformStepEvent>(OnToolCanPerform);
        SubscribeLocalEvent<SurgeryOperatingTableConditionComponent, SurgeryCanPerformStepEvent>(OnTableCanPerform);
        SubscribeLocalEvent<SurgeryAddPartStepComponent, SurgeryCanPerformStepEvent>(OnAddPartCanPerform);
        SubscribeLocalEvent<SurgeryAddOrganStepComponent, SurgeryCanPerformStepEvent>(OnAddOrganCanPerform);

        SubSurgery<SurgeryTendWoundsEffectComponent>(OnTendWoundsStep, OnTendWoundsCheck);
        SubSurgery<SurgeryAddPartStepComponent>(OnAddPartStep, OnAddPartCheck);
        SubSurgery<SurgeryAffixPartStepComponent>(OnAffixPartStep, OnAffixPartCheck);
        SubSurgery<SurgeryRemovePartStepComponent>(OnRemovePartStep, OnRemovePartCheck);
        SubSurgery<SurgeryAddOrganStepComponent>(OnAddOrganStep, OnAddOrganCheck);
        SubSurgery<SurgeryAffixOrganStepComponent>(OnAffixOrganStep, OnAffixOrganCheck);
        SubSurgery<SurgeryRemoveOrganStepComponent>(OnRemoveOrganStep, OnRemoveOrganCheck);
        SubSurgery<SurgeryTraumaTreatmentStepComponent>(OnTraumaTreatmentStep, OnTraumaTreatmentCheck);
        SubSurgery<SurgeryBleedsTreatmentStepComponent>(OnBleedsTreatmentStep, OnBleedsTreatmentCheck);
        SubscribeLocalEvent<SurgeryStepPainInflicterComponent, SurgeryStepEvent>(OnPainInflicterStep);
        SubscribeLocalEvent<SurgeryDamageChangeEffectComponent, SurgeryStepEvent>(OnDamageChangeStep);

        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs =>
        {
            subs.Event<SurgeryStepChosenBuiMsg>(OnSurgeryTargetStepChosen);
        });
    }

    private void SubSurgery<TComp>(EntityEventRefHandler<TComp, SurgeryStepEvent> onStep,
        EntityEventRefHandler<TComp, SurgeryStepCompleteCheckEvent> onComplete) where TComp : IComponent
    {
        SubscribeLocalEvent(onStep);
        SubscribeLocalEvent(onComplete);
    }

    // ===================== Общий шаг =====================

    private void OnToolStep(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryToolAudio(ent, args))
            return;

        AddOrRemoveComponents(args.Part, ent.Comp.Add);
        AddOrRemoveComponents(args.Part, ent.Comp.Remove, true);
        AddOrRemoveComponents(args.Body, ent.Comp.BodyAdd);
        AddOrRemoveComponents(args.Body, ent.Comp.BodyRemove, true);

        HandleSanitization(args);
    }

    private void OnToolCheck(Entity<SurgeryStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (HasMismatch(ent.Comp.Add, args.Part)
            || HasMismatch(ent.Comp.Remove, args.Part, checkMissing: false)
            || HasMismatch(ent.Comp.BodyAdd, args.Body)
            || HasMismatch(ent.Comp.BodyRemove, args.Body, checkMissing: false))
        {
            args.Cancelled = true;
        }
    }

    private void OnToolCanPerform(Entity<SurgeryStepComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (args.IsInvalid)
            return;

        // Через одежду и броню не оперируют
        if (!_ignoreQuery.HasComp(args.User)
            && !_ignoreQuery.HasComp(args.Tool)
            && _inventory.TryGetContainerSlotEnumerator(args.Body, out var slots, args.TargetSlots))
        {
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is not { } worn)
                    continue;

                args.Invalid = StepInvalidReason.Armor;
                args.Popup = Loc.GetString("surgery-ui-window-steps-error-armor", ("item", worn));
                return;
            }
        }

        if (ent.Comp.Tool == null)
            return;

        foreach (var reg in ent.Comp.Tool.Values)
        {
            if (GetSurgeryComp(args.Tool, reg.Component) is { } data)
            {
                args.ValidTool = data;
                return;
            }

            args.Invalid = StepInvalidReason.MissingTool;
            if (reg.Component is ISurgeryToolComponent required)
                args.Popup = Loc.GetString("surgery-ui-window-steps-error-missing-tool", ("tool", Loc.GetString(required.ToolName)));

            return;
        }
    }

    private void OnTableCanPerform(Entity<SurgeryOperatingTableConditionComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (args.IsInvalid)
            return;

        if (!TryComp(args.Body, out BuckleComponent? buckle) || !HasComp<OperatingTableComponent>(buckle.BuckledTo))
        {
            args.Invalid = StepInvalidReason.NeedsOperatingTable;
            args.Popup = Loc.GetString("surgery-ui-window-steps-error-table");
        }
    }

    // ===================== Части тела =====================

    private void OnAddPartCanPerform(Entity<SurgeryAddPartStepComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (args.IsInvalid)
            return;

        if (LimbConnection(args.Body, args.Part, args.Tool) == null)
        {
            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = Loc.GetString("surgery-ui-window-steps-error-missing-limb");
        }
    }

    /// <summary>Какую из недостающих частей можно пришить предметом из рук (или null).</summary>
    private string? LimbConnection(EntityUid body, EntityUid part, EntityUid held)
    {
        foreach (var surgeryId in AllSurgeries)
        {
            if (GetSingleton(surgeryId) is not { } surgery
                || !TryComp<SurgeryPartRemovedConditionComponent>(surgery, out var removed)
                || !PartExpects(body, part, removed.Connection)
                || ChildOfCategory(part, removed.Connection) != null
                || FindLimbRoot(held, removed.Connection) == null)
            {
                continue;
            }

            return removed.Connection;
        }

        return null;
    }

    private void OnAddPartStep(Entity<SurgeryAddPartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (_net.IsClient || !TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removed))
            return;

        if (AttachLimb(args.Body, args.Part, args.Tool, removed.Connection))
            _popup.PopupEntity(Loc.GetString("surgery-limb-attached", ("user", Identity.Entity(args.User, EntityManager)),
                ("patient", Identity.Entity(args.Body, EntityManager))), args.Body, PopupType.Medium);
    }

    private void OnAddPartCheck(Entity<SurgeryAddPartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removed)
            || ChildOfCategory(args.Part, removed.Connection) == null)
        {
            args.Cancelled = true;
        }
    }

    private void OnAffixPartStep(Entity<SurgeryAffixPartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removed)
            || ChildOfCategory(args.Part, removed.Connection) is not { } attached)
        {
            return;
        }

        RemComp<BodyPartReattachedComponent>(attached);
        // Хорошо закреплённая часть немного заживает
        if (!_net.IsClient)
            _wounds.TendWounds(attached, 12);
    }

    private void OnAffixPartCheck(Entity<SurgeryAffixPartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removed)
            && ChildOfCategory(args.Part, removed.Connection) is { } attached
            && HasComp<BodyPartReattachedComponent>(attached))
        {
            args.Cancelled = true;
        }
    }

    private void OnRemovePartStep(Entity<SurgeryRemovePartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (_net.IsClient || !_partQuery.TryComp(args.Part, out var partComp) || partComp.Body != args.Body)
            return;

        EntityUid? parent = TryComp<ChildOrganComponent>(args.Part, out var child) ? child.Parent : null;
        if (Amputate(args.User, args.Body, args.Part) == null)
            return;

        // Рана на месте отрезанной конечности остаётся открытой — её закрывают «Закрыть»
        if (parent is { } stump && Exists(stump))
        {
            EnsureComp<IncisionOpenComponent>(stump);
            EnsureComp<SkinRetractedComponent>(stump);
            EnsureComp<BleedersClampedComponent>(stump);
        }

        _popup.PopupEntity(Loc.GetString("surgery-amputated", ("user", Identity.Entity(args.User, EntityManager)),
            ("patient", Identity.Entity(args.Body, EntityManager))), args.Body, PopupType.MediumCaution);
    }

    private void OnRemovePartCheck(Entity<SurgeryRemovePartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!_partQuery.TryComp(args.Part, out var partComp) || partComp.Body == args.Body)
            args.Cancelled = true;
    }

    // ===================== Органы =====================

    private void OnAddOrganCanPerform(Entity<SurgeryAddOrganStepComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (args.IsInvalid)
            return;

        if (Category(args.Tool) is not { } category
            || !HasComp<InternalChildOrganComponent>(args.Tool)
            || !PartExpects(args.Body, args.Part, category))
        {
            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = Loc.GetString("surgery-ui-window-steps-error-missing-organ");
        }
    }

    private void OnAddOrganStep(Entity<SurgeryAddOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (_net.IsClient
            || !TryComp(args.Surgery, out SurgeryOrganConditionComponent? condition)
            || Category(args.Tool) is not { } category
            || !condition.Organs.Contains(category)
            || ChildOfCategory(args.Part, category) != null)
        {
            return;
        }

        if (InsertOrgan(args.Body, args.Part, args.Tool))
            EnsureComp<OrganReattachedComponent>(args.Tool);
    }

    private void OnAddOrganCheck(Entity<SurgeryAddOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp(args.Surgery, out SurgeryOrganConditionComponent? condition) && !PartOrgans(args.Part, condition.Organs).Any())
            args.Cancelled = true;
    }

    private void OnAffixOrganStep(Entity<SurgeryAffixOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryOrganConditionComponent? condition))
            return;

        foreach (var organ in PartOrgans(args.Part, condition.Organs).ToList())
        {
            RemComp<OrganReattachedComponent>(organ);
        }
    }

    private void OnAffixOrganCheck(Entity<SurgeryAffixOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp(args.Surgery, out SurgeryOrganConditionComponent? condition)
            && PartOrgans(args.Part, condition.Organs).Any(o => HasComp<OrganReattachedComponent>(o)))
        {
            args.Cancelled = true;
        }
    }

    private void OnRemoveOrganStep(Entity<SurgeryRemoveOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (_net.IsClient || !TryComp(args.Surgery, out SurgeryOrganConditionComponent? condition))
            return;

        foreach (var organ in PartOrgans(args.Part, condition.Organs).ToList())
        {
            if (RemoveOrgan(args.Body, organ))
                _hands.PickupOrDrop(args.User, organ);
            else
                _popup.PopupEntity(Loc.GetString("surgery-popup-step-SurgeryStepRemoveOrgan-failed"), args.User, args.User);

            break;
        }
    }

    private void OnRemoveOrganCheck(Entity<SurgeryRemoveOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp(args.Surgery, out SurgeryOrganConditionComponent? condition) && PartOrgans(args.Part, condition.Organs).Any())
            args.Cancelled = true;
    }

    // ===================== Лечение =====================

    private void OnTendWoundsStep(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (_net.IsClient)
            return;

        var current = GroupSeverity(args.Part, ent.Comp.MainGroup);
        if (current <= 0f)
            return;

        var amount = ent.Comp.Amount + ent.Comp.HealMultiplier * current;
        if (_mobState.IsDead(args.Body))
            amount *= 0.2f;

        _wounds.TendWounds(args.Part, amount, GroupTypes(ent.Comp.MainGroup));
    }

    private void OnTendWoundsCheck(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (GroupSeverity(args.Part, ent.Comp.MainGroup) > 0f)
            args.Cancelled = true;
    }

    private void OnTraumaTreatmentStep(Entity<SurgeryTraumaTreatmentStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (_net.IsClient)
            return;

        var type = ent.Comp.TraumaType;
        if (type == TraumaSystem.BoneDamage)
        {
            if (!TryComp<WoundableComponent>(args.Part, out var woundable)
                || _trauma.GetBone(woundable) is not { } bone
                || !TryComp<BoneComponent>(bone, out var boneComp))
            {
                return;
            }

            _trauma.ApplyDamageToBone(bone, -ent.Comp.Amount, boneComp);
            if (boneComp.BoneIntegrity >= boneComp.IntegrityCap)
            {
                RemoveTraumas(args.Part, TraumaSystem.BoneDamage);
                RemComp<BoneSplintedComponent>(args.Part);
            }
        }
        else if (type == TraumaSystem.OrganDamage)
        {
            foreach (var (organ, integrity) in DamagedOrgans(args.Part, ent.Comp.Organs).ToList())
            {
                _trauma.RecoverOrgan(organ, ent.Comp.Amount, integrity, allowDestroyed: true);
            }

            if (!DamagedOrgans(args.Part).Any())
                RemoveTraumas(args.Part, TraumaSystem.OrganDamage);
        }
        else
        {
            // Вены, нервы, культя: травма снимается целиком
            RemoveTraumas(args.Part, type);
            if (type == TraumaSystem.VeinsDamage)
                _bleeding.StopBleeding(args.Part);
        }
    }

    private void OnTraumaTreatmentCheck(Entity<SurgeryTraumaTreatmentStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        var type = ent.Comp.TraumaType;
        if (type == TraumaSystem.BoneDamage)
            args.Cancelled |= BoneDamaged(args.Part);
        else if (type == TraumaSystem.OrganDamage)
            args.Cancelled |= DamagedOrgans(args.Part, ent.Comp.Organs).Any();
        else
            args.Cancelled |= HasTrauma(args.Part, type);
    }

    private void OnBleedsTreatmentStep(Entity<SurgeryBleedsTreatmentStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsClient)
            _bleeding.StopBleeding(args.Part);
    }

    private void OnBleedsTreatmentCheck(Entity<SurgeryBleedsTreatmentStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_bleeding.IsBleeding(args.Part))
            args.Cancelled = true;
    }

    // ===================== Боль и урон шага =====================

    private void OnPainInflicterStep(Entity<SurgeryStepPainInflicterComponent> ent, ref SurgeryStepEvent args)
    {
        if (_net.IsClient)
            return;

        var amount = ent.Comp.Amount;
        if (HasComp<SleepingComponent>(args.Body))
            amount *= ent.Comp.SleepModifier;

        _pain.AddSurgeryPain(args.Body, args.Part, amount, ent.Comp.PainDuration);
    }

    private void OnDamageChangeStep(Entity<SurgeryDamageChangeEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var damage = ent.Comp.Damage;
        if (HasComp<SleepingComponent>(args.Body))
            damage = damage * ent.Comp.SleepModifier;

        var ev = new SurgeryStepDamageEvent(args.User, args.Body, args.Part, args.Surgery, damage);
        RaiseLocalEvent(args.Body, ref ev);
    }

    private void OnSurgeryTargetStepChosen(Entity<SurgeryTargetComponent> ent, ref SurgeryStepChosenBuiMsg args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (GetEntity(args.Part) is { } targetPart && targetPart.IsValid())
            TryDoSurgeryStep(ent, targetPart, args.Actor, args.Surgery, args.Step, out _);
    }

    // ===================== Вспомогательное =====================

    /// <summary>Нестерильно (без перчаток и маски или стерильной вещи) — заражение части.</summary>
    private void HandleSanitization(SurgeryStepEvent args)
    {
        if (_inventory.TryGetSlotEntity(args.User, "gloves", out _) && _inventory.TryGetSlotEntity(args.User, "mask", out _))
            return;

        if (TryComp<SurgeryTargetComponent>(args.Body, out var target) && target.SepsisImmune)
            return;

        foreach (var held in _hands.EnumerateHeld(args.User))
        {
            if (TryComp<SanitizedComponent>(held, out var sanitized) && sanitized.WorksInHands)
                return;
        }

        if (_inventory.TryGetContainerSlotEnumerator(args.User, out var slots))
        {
            while (slots.NextItem(out var item))
            {
                if (HasComp<SanitizedComponent>(item))
                    return;
            }
        }

        var sepsis = new DamageSpecifier(_prototypes.Index(SepsisDamage), SepsisAmount);
        var ev = new SurgeryStepDamageEvent(args.User, args.Body, args.Part, args.Surgery, sepsis);
        RaiseLocalEvent(args.Body, ref ev);
    }

    private bool TryToolAudio(Entity<SurgeryStepComponent> ent, SurgeryStepEvent args)
    {
        if (ent.Comp.Tool == null)
            return true;

        foreach (var reg in ent.Comp.Tool.Values)
        {
            if (GetSurgeryComp(args.Tool, reg.Component) == null)
                return false;

            if (_toolQuery.CompOrNull(args.Tool)?.EndSound is not { } sound)
                continue;

            _audio.PlayPredicted(sound, args.Tool, args.User);
            break;
        }

        return true;
    }

    private void AddOrRemoveComponents(EntityUid ent, ComponentRegistry? registry, bool remove = false)
    {
        if (registry == null)
            return;

        foreach (var reg in registry.Values)
        {
            var compType = reg.Component.GetType();
            if (remove)
            {
                RemComp(ent, compType);
            }
            else if (!HasComp(ent, compType))
            {
                AddComp(ent, _compFactory.GetComponent(compType));
            }
        }
    }

    /// <summary>true — состояние части не совпадает с ожидаемым (компонента нет, а должен быть, или наоборот).</summary>
    private bool HasMismatch(ComponentRegistry? components, EntityUid target, bool checkMissing = true)
    {
        if (components == null)
            return false;

        foreach (var (_, entry) in components)
        {
            if (checkMissing != HasComp(target, entry.Component.GetType()))
                return true;
        }

        return false;
    }

    /// <summary>Сделать шаг операции, если можно. true — шаг начат.</summary>
    public bool TryDoSurgeryStep(EntityUid body, EntityUid targetPart, EntityUid user, EntProtoId surgeryId, EntProtoId stepId,
        out StepInvalidReason error)
    {
        error = StepInvalidReason.None;
        if (!IsSurgeryValid(body, targetPart, surgeryId, stepId, user, out var surgery, out var part, out var step))
        {
            error = StepInvalidReason.SurgeryInvalid;
            return false;
        }

        if (!PreviousStepsComplete(body, part, surgery, stepId, user))
        {
            error = StepInvalidReason.MissingPreviousSteps;
            return false;
        }

        if (IsStepComplete(body, part, stepId, surgery))
        {
            error = StepInvalidReason.StepCompleted;
            return false;
        }

        var tool = _hands.GetActiveItemOrSelf(user);
        if (!CanPerformStep(user, body, part, step, tool, true, out _, out error, out var data))
            return false;

        var toolComp = _toolQuery.CompOrNull(tool);
        var usedEv = new SurgeryToolUsedEvent(user, body, toolComp?.IgnoreToggle ?? false);
        RaiseLocalEvent(tool, ref usedEv);
        if (usedEv.Cancelled)
        {
            error = StepInvalidReason.ToolInvalid;
            return false;
        }

        if (toolComp?.StartSound is { } sound)
            _audio.PlayPredicted(sound, tool, user);

        _rotateToFace.TryFaceCoordinates(user, _transform.GetMapCoordinates(body).Position);

        var speed = data?.Speed ?? 1f;
        var toolUsed = data?.Used ?? false;
        var ev = new SurgeryDoAfterEvent(surgeryId, stepId, toolUsed);
        var duration = GetSurgeryDuration(step, user, body, speed);

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(duration), ev, body, part)
        {
            BreakOnMove = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            NeedHand = true,
            BreakOnHandChange = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            DistanceThreshold = null,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            error = StepInvalidReason.DoAfterFailed;
            return false;
        }

        var userName = Identity.Entity(user, EntityManager);
        var targetName = Identity.Entity(body, EntityManager);
        var partName = Name(part);
        var locName = $"surgery-popup-step-{stepId}";
        var message = Loc.TryGetString(locName, out var text, ("user", userName), ("target", targetName), ("part", partName))
            ? text
            : Loc.GetString("surgery-popup-step-generic", ("user", userName), ("target", targetName), ("part", partName),
                ("step", Name(step)));

        _popup.PopupPredicted(message, body, user);
        return true;
    }

    private float GetSurgeryDuration(EntityUid surgeryStep, EntityUid user, EntityUid target, float toolSpeed)
    {
        if (!_stepQuery.TryComp(surgeryStep, out var stepComp))
            return 2f;

        var speed = toolSpeed;
        if (TryComp<BuckleComponent>(target, out var buckle) && TryComp<OperatingTableComponent>(buckle.BuckledTo, out var table))
            speed *= table.SpeedModifier;

        if (TryComp(user, out SurgerySpeedModifierComponent? modifier))
            speed *= modifier.SpeedModifier;

        return stepComp.Duration / MathF.Max(0.05f, speed);
    }

    private (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part,
        Entity<SurgeryComponent?> surgery, List<EntityUid> requirements, EntityUid user)
    {
        if (!Resolve(surgery, ref surgery.Comp))
            return null;

        if (requirements.Contains(surgery))
            throw new ArgumentException($"Surgery {surgery} has a requirement loop: {string.Join(", ", requirements)}");

        var ev = new SurgeryIgnorePreviousStepsEvent();
        RaiseLocalEvent(user, ev);
        if (ev.Handled)
        {
            for (var i = surgery.Comp.Steps.Count - 1; i >= 0; i--)
            {
                if (!IsStepComplete(body, part, surgery.Comp.Steps[i], surgery))
                    return ((surgery, surgery.Comp), -i - 1);
            }

            return null;
        }

        requirements.Add(surgery);

        if (surgery.Comp.Requirement is { } requirementId
            && GetSingleton(requirementId) is { } requirement
            && GetNextStep(body, part, requirement, requirements, user) is { } requiredNext)
        {
            return requiredNext;
        }

        for (var i = 0; i < surgery.Comp.Steps.Count; i++)
        {
            if (!IsStepComplete(body, part, surgery.Comp.Steps[i], surgery))
                return ((surgery, surgery.Comp), i);
        }

        return null;
    }

    /// <summary>Следующий шаг операции (с учётом обязательной предыдущей операции) или null — всё сделано.</summary>
    public (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, EntityUid surgery, EntityUid user)
    {
        _nextStepList.Clear();
        return GetNextStep(body, part, surgery, _nextStepList, user);
    }

    private bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step, EntityUid user)
    {
        var ev = new SurgeryIgnorePreviousStepsEvent();
        RaiseLocalEvent(user, ev);
        if (ev.Handled)
            return true;

        if (surgery.Comp.Requirement is { } requirement)
        {
            if (GetSingleton(requirement) is not { } requiredEnt
                || !TryComp(requiredEnt, out SurgeryComponent? requiredComp)
                || !PreviousStepsComplete(body, part, (requiredEnt, requiredComp), step, user))
            {
                return false;
            }
        }

        foreach (var surgeryStep in surgery.Comp.Steps)
        {
            if (surgeryStep == step)
                break;

            if (!IsStepComplete(body, part, surgeryStep, surgery))
                return false;
        }

        return true;
    }

    private bool CanPerformStep(EntityUid user, EntityUid body, EntityUid part, EntityUid step, EntityUid tool, bool doPopup,
        out string? popup, out StepInvalidReason reason, out ISurgeryToolComponent? data)
    {
        var type = _partQuery.CompOrNull(part)?.PartType ?? BodyPartType.Other;
        var slot = type switch
        {
            BodyPartType.Head => SlotFlags.HEAD,
            BodyPartType.Chest => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Groin => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Arm => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Hand => SlotFlags.GLOVES,
            BodyPartType.Leg => SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            BodyPartType.Foot => SlotFlags.FEET,
            _ => SlotFlags.NONE,
        };

        var check = new SurgeryCanPerformStepEvent(user, body, part, tool, slot);
        RaiseLocalEvent(step, ref check);
        if (check.IsValid)
            RaiseLocalEvent(body, ref check);

        popup = check.Popup;
        reason = check.Invalid;
        data = check.ValidTool;

        if (check.IsValid)
            return true;

        if (doPopup && check.Popup != null)
            _popup.PopupClient(check.Popup, user, user, PopupType.SmallCaution);

        return false;
    }

    private bool CanPerformStep(EntityUid user, EntityUid body, EntityUid part, EntityUid step, EntityUid tool, bool doPopup)
    {
        return CanPerformStep(user, body, part, step, tool, doPopup, out _, out _, out _);
    }

    /// <summary>Можно ли сделать шаг тем, что сейчас в руке (для подсказки в окне).</summary>
    public bool CanPerformStepWithHeld(EntityUid user, EntityUid body, EntityUid part, EntityUid step, bool doPopup, out string? popup)
    {
        var tool = _hands.GetActiveItemOrSelf(user);
        return CanPerformStep(user, body, part, step, tool, doPopup, out popup, out _, out _);
    }

    private bool IsStepComplete(EntityUid body, EntityUid part, EntProtoId step, EntityUid surgery)
    {
        if (GetSingleton(step) is not { } stepEnt)
            return false;

        var ev = new SurgeryStepCompleteCheckEvent(body, part, surgery);
        RaiseLocalEvent(stepEnt, ref ev);
        return !ev.Cancelled;
    }

    private ISurgeryToolComponent? GetSurgeryComp(EntityUid tool, IComponent component)
    {
        if (EntityManager.TryGetComponent(tool, component.GetType(), out var found) && found is ISurgeryToolComponent data)
            return data;

        return null;
    }
}
