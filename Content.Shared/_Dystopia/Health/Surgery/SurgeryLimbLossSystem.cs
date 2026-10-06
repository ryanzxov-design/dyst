// SPDX-License-Identifier: AGPL-3.0-or-later
// Хирургия Dystopia: тело без ног и с протезами.

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;

namespace Content.Shared._Dystopia.Health.Surgery;

/// <summary>
/// Без ноги (любой) встать нельзя — человек падает и ползёт, пока ему не пришьют ногу или протез.
/// Без стопы — хромает. Деревянные протезы медленнее живых ног.
/// Общая система (клиент и сервер), чтобы движение предсказывалось без рывков.
/// </summary>
public sealed partial class SurgeryLimbLossSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private SharedBodySystem _parts = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Настройки по умолчанию, если у тела их нет.</summary>
    private static readonly SurgeryLimbLossComponent Defaults = new();

    private SurgeryLimbLossComponent Settings(EntityUid body) => CompOrNull<SurgeryLimbLossComponent>(body) ?? Defaults;

    private bool Crippled(EntityUid body) => BoneSpeed(body) < Settings(body).CrippledSpeed;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryLimbLossComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SurgeryLimbLossComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<SurgeryLimbLossComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<SurgeryLimbLossComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SurgeryLimbLossComponent, StandUpAttemptEvent>(OnStandUpAttempt);
        // Встать можно не только из «сбит с ног»: после отстёгивания от стола/кровати, кнопкой и т.д.
        SubscribeLocalEvent<SurgeryLimbLossComponent, StandAttemptEvent>(OnStandAttempt);
        // Переломы: ноги — скорость и падение, руки — промахи при ударе и выстреле
        SubscribeLocalEvent<SurgeryLimbLossComponent, BoneStateChangedEvent>(OnBoneStateChanged);
        SubscribeLocalEvent<SurgeryLimbLossComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<SurgeryLimbLossComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnBoneStateChanged(EntityUid uid, SurgeryLimbLossComponent comp, BoneStateChangedEvent args)
    {
        Refresh(uid, Crippled(uid));
    }

    /// <summary>
    /// Скорость от костей ног (как в исходной системе): сломанная нога не несёт вес, треснувшая — вполсилы,
    /// повреждённые стопы замедляют. Итог — среднее по двум ногам.
    /// </summary>
    public float BoneSpeed(EntityUid body)
    {
        var total = 0f;
        foreach (var symmetry in new[] { BodyPartSymmetry.Left, BodyPartSymmetry.Right })
        {
            var leg = FirstPart(body, BodyPartType.Leg, symmetry);
            if (leg == null)
                continue; // нет ноги — это учитывает ползание

            var settings = Settings(body);
            var legFactor = settings.LegBoneSpeed.GetValueOrDefault(_trauma.GetBoneSeverity(leg.Value), 1f);

            var foot = FirstPart(body, BodyPartType.Foot, symmetry);
            var footFactor = foot == null ? 1f : settings.FootBoneSpeed.GetValueOrDefault(_trauma.GetBoneSeverity(foot.Value), 1f);

            total += legFactor * footFactor;
        }

        return total / 2f;
    }

    private EntityUid? FirstPart(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        foreach (var part in _parts.GetBodyChildrenOfType(body, type, symmetry: symmetry))
            return part.Id;

        return null;
    }

    /// <summary>Худшая кость рук и кистей.</summary>
    private BoneSeverity WorstArmBone(EntityUid body)
    {
        var worst = BoneSeverity.Normal;
        foreach (var type in new[] { BodyPartType.Arm, BodyPartType.Hand })
        {
            foreach (var part in _parts.GetBodyChildrenOfType(body, type))
            {
                var severity = _trauma.GetBoneSeverity(part.Id);
                if (severity > worst)
                    worst = severity;
            }
        }

        return worst;
    }

    private bool TryFumble(EntityUid body)
    {
        if (_net.IsClient)
            return false;

        var settings = Settings(body);
        var odds = settings.FumbleChance.GetValueOrDefault(WorstArmBone(body), 0f);

        if (odds <= 0f || !_random.Prob(odds))
            return false;

        _popup.PopupEntity(Loc.GetString("trauma-arm-fumble"), body, body, PopupType.SmallCaution);
        _audio.PlayPvs(settings.FumbleSound, body);
        return true;
    }

    private void OnAttackAttempt(EntityUid uid, SurgeryLimbLossComponent comp, AttackAttemptEvent args)
    {
        if (!args.Cancelled && TryFumble(uid))
            args.Cancel();
    }

    private void OnShotAttempted(Entity<SurgeryLimbLossComponent> ent, ref ShotAttemptedEvent args)
    {
        // Только в момент настоящего выстрела, а не на каждом кадре зажатого спуска
        if (args.Cancelled || args.Used.Comp.NextFire > _timing.CurTime)
            return;

        if (TryFumble(ent))
            args.Cancel();
    }

    private void OnStartup(Entity<SurgeryLimbLossComponent> ent, ref ComponentStartup args) => Refresh(ent, false);

    private void OnOrganInserted(Entity<SurgeryLimbLossComponent> ent, ref OrganInsertedIntoEvent args) => Refresh(ent, false);

    private void OnOrganRemoved(Entity<SurgeryLimbLossComponent> ent, ref OrganRemovedFromEvent args) => Refresh(ent, true);

    /// <param name="knockdown">Сбить с ног, если ноги нет. Только когда часть именно отняли:
    /// при появлении тела органы вставляются по одному, и ног в начале ещё нет.</param>
    private void Refresh(EntityUid uid, bool knockdown)
    {
        // Тело удаляется (органы вынимаются при удалении) — ничего не делаем
        if (TerminatingOrDeleted(uid))
            return;

        _movement.RefreshMovementSpeedModifiers(uid);

        // Нет ноги — человек падает и дальше только ползёт. Сбиваем и лежачего (например, пристёгнутого
        // к операционному столу): иначе после отстёгивания он просто встанет.
        if (knockdown && (!HasBothLegs(uid) || Crippled(uid)))
            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(1), refresh: true, autoStand: false, drop: false, force: true);
    }

    private void OnStandUpAttempt(Entity<SurgeryLimbLossComponent> ent, ref StandUpAttemptEvent args)
    {
        if (HasBothLegs(ent) && !Crippled(ent))
            return;

        args.Cancelled = true;
        args.Autostand = false;
        args.Message = (Loc.GetString("surgery-no-leg-stand"), PopupType.SmallCaution);
    }

    private void OnStandAttempt(EntityUid uid, SurgeryLimbLossComponent comp, StandAttemptEvent args)
    {
        if (!HasBothLegs(uid) || Crippled(uid))
            args.Cancel();
    }

    private Dictionary<string, float> Limbs(EntityUid uid)
    {
        var present = new Dictionary<string, float>();
        foreach (var organ in _body.EnumerateOrgans<OrganComponent>(uid))
        {
            if (organ.Comp1.Category is not { } cat)
                continue;

            present[cat.Id] = TryComp<SurgeryProstheticComponent>(organ, out var prosthetic) ? prosthetic.SpeedMultiplier : 1f;
        }

        return present;
    }

    public bool HasBothLegs(EntityUid uid)
    {
        var limbs = Limbs(uid);
        return limbs.ContainsKey("LegLeft") && limbs.ContainsKey("LegRight");
    }

    private void OnRefreshSpeed(Entity<SurgeryLimbLossComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var limbs = Limbs(ent);
        var speed = Side(limbs, "LegLeft", "FootLeft", ent.Comp.NoFootSpeed) * Side(limbs, "LegRight", "FootRight", ent.Comp.NoFootSpeed);
        // Переломы ног (стоя). Лёжа человек ползёт — кости уже не важны
        if (!_standing.IsDown(ent.Owner))
            speed *= MathF.Max(BoneSpeed(ent), ent.Comp.CrippledSpeed);
        speed = MathF.Max(ent.Comp.MinimumSpeed, speed);
        if (speed < 0.999f)
            args.ModifySpeed(speed, speed);
    }

    /// <summary>Нет ноги — модификатора нет (скорость задаёт ползание), нет стопы — хромота, протез — свой множитель.</summary>
    private static float Side(Dictionary<string, float> limbs, string leg, string foot, float noFoot)
    {
        if (!limbs.TryGetValue(leg, out var legMult))
            return 1f;

        return limbs.TryGetValue(foot, out var footMult) ? legMult * footMult : legMult * noFoot;
    }
}
