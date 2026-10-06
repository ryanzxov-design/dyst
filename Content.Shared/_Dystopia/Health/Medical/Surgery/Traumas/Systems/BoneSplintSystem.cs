// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: шина — лечение переломов без операции. Кость под шиной медленно срастается.

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems;

public sealed partial class BoneSplintSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Medical.Chemistry.BloodChemistrySystem _chemistry = default!;
    [Dependency] private Content.Shared.Body.Systems.SharedBodySystem _body = default!;

    private TimeSpan _nextHeal;
    private static readonly TimeSpan HealTick = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SplintComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SplintComponent, SplintDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<SplintComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (_wounds.GetAimedPart(target, args.User) is not { } part)
            return;

        args.Handled = true;
        var partName = Name(part);
        if (HasComp<BoneSplintedComponent>(part))
        {
            _popup.PopupClient(Loc.GetString("splint-already", ("part", partName)), target, args.User);
            return;
        }

        if (_trauma.GetBoneSeverity(part, effective: false) == BoneSeverity.Normal)
        {
            _popup.PopupClient(Loc.GetString("splint-bone-fine", ("part", partName)), target, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ApplyTime,
            new SplintDoAfterEvent(GetNetEntity(part)), ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnDoAfter(Entity<SplintComponent> ent, ref SplintDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        var part = GetEntity(args.Part);
        if (!Exists(part) || !TryComp<BodyPartComponent>(part, out var bodyPart) || bodyPart.Body != target
            || HasComp<BoneSplintedComponent>(part))
            return;

        var splinted = EnsureComp<BoneSplintedComponent>(part);
        splinted.HealPerSecond = ent.Comp.HealPerSecond;
        splinted.StabilizedSeverity = ent.Comp.StabilizedSeverity;
        Dirty(part, splinted);

        _popup.PopupClient(Loc.GetString("splint-applied", ("part", Name(part))), target, args.User);
        PredictedQueueDel(ent.Owner);

        // Под шиной меняется скорость и способность стоять
        RaiseLocalEvent(target, new BoneStateChangedEvent());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient || _timing.CurTime < _nextHeal)
            return;

        _nextHeal = _timing.CurTime + HealTick;
        HealWithMedicine();
        var query = EntityQueryEnumerator<BoneSplintedComponent, WoundableComponent>();
        while (query.MoveNext(out var part, out var splint, out var woundable))
        {
            if (_trauma.GetBone(woundable) is not { } bone || !TryComp<BoneComponent>(bone, out var boneComp))
            {
                RemCompDeferred<BoneSplintedComponent>(part);
                continue;
            }

            var boost = 1f;
            if (TryComp<BodyPartComponent>(part, out var splintedPart) && splintedPart.Body is { } splintBody
                && TryComp<Content.Shared._Dystopia.Health.Medical.Organs.OrganFunctionComponent>(splintBody, out var meds))
            {
                boost = MathF.Min(meds.MaxBoneHealMultiplier, 1f + _chemistry.Weighted(splintBody, meds.BoneHealBoost));
            }

            _trauma.SetBoneIntegrity(bone, boneComp.BoneIntegrity + splint.HealPerSecond * boost * (float) HealTick.TotalSeconds, boneComp);
            if (boneComp.BoneIntegrity < boneComp.IntegrityCap)
                continue;

            // Кость срослась — шина больше не нужна
            RemCompDeferred<BoneSplintedComponent>(part);
            if (TryComp<BodyPartComponent>(part, out var bodyPart) && bodyPart.Body is { } body)
                _popup.PopupEntity(Loc.GetString("splint-healed", ("part", Name(part))), body, body);
        }
    }

    /// <summary>Лекарство костей в крови: повреждённые кости без шины тоже понемногу срастаются.</summary>
    private void HealWithMedicine()
    {
        var bodies = EntityQueryEnumerator<Content.Shared._Dystopia.Health.Medical.Organs.OrganFunctionComponent>();
        while (bodies.MoveNext(out var body, out var meds))
        {
            if (meds.BoneHealWithoutSplint <= 0)
                continue;

            var amount = 0f;
            foreach (var reagent in meds.BoneHealBoost.Keys)
            {
                amount += _chemistry.GetQuantity(body, reagent);
            }

            if (amount < meds.BoneHealMinimum)
                continue;

            foreach (var (part, _) in _body.GetBodyChildren(body))
            {
                if (HasComp<BoneSplintedComponent>(part) || !TryComp<WoundableComponent>(part, out var woundable)
                    || _trauma.GetBone(woundable) is not { } bone || !TryComp<BoneComponent>(bone, out var boneComp)
                    || boneComp.BoneIntegrity >= boneComp.IntegrityCap)
                    continue;

                _trauma.SetBoneIntegrity(bone, boneComp.BoneIntegrity + meds.BoneHealWithoutSplint * (float) HealTick.TotalSeconds, boneComp);
            }
        }
    }
}
