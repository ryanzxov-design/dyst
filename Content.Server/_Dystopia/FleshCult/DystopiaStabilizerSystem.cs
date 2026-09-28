using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared._Dystopia.FleshCult;
using Content.Shared.Audio;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Station.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.FleshCult;

/// <summary>
/// Стабилизаторы Города: зона без кист, замедление наростов, аккумулятор, поломка и ремонт,
/// защита Омега-Стабилизатора. Город получает объявления о поломках и разрядке.
/// </summary>
public sealed partial class DystopiaStabilizerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaStabilizerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaStabilizerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaStabilizerComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<DystopiaFleshSeedAttemptEvent>(OnSeedAttempt);
        SubscribeLocalEvent<DystopiaFleshGrowthSpeedEvent>(OnGrowthSpeed);
    }

    private void OnMapInit(Entity<DystopiaStabilizerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Battery < 0)
            ent.Comp.Battery = ent.Comp.BatteryCapacity;

        UpdateState(ent, announce: false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        // Сначала обычные стабилизаторы, потом Омега (ему нужно знать, сколько обычных работает)
        var working = 0;
        var query = EntityQueryEnumerator<DystopiaStabilizerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsOmega || now < comp.NextUpdate)
            {
                if (!comp.IsOmega && comp.Working)
                    working++;
                continue;
            }

            comp.NextUpdate = now + TimeSpan.FromSeconds(1);
            Tick((uid, comp));
            if (comp.Working)
                working++;
        }

        var omegas = EntityQueryEnumerator<DystopiaStabilizerComponent>();
        while (omegas.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsOmega || now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + TimeSpan.FromSeconds(1);

            var shielded = !comp.Broken && working >= comp.OmegaMinWorking;
            _appearance.SetData(uid, DystopiaStabilizerVisuals.Shielded, shielded);
            if (shielded != comp.Shielded || !comp.ShieldKnown)
            {
                // Первое вычисление (начало раунда) не объявляем
                if (comp.ShieldKnown && !comp.Broken)
                    Announce(uid, shielded ? "dystopia-omega-shielded" : "dystopia-omega-vulnerable");

                comp.Shielded = shielded;
                comp.ShieldKnown = true;
            }

            Tick((uid, comp));
        }
    }

    private void Tick(Entity<DystopiaStabilizerComponent> ent)
    {
        var comp = ent.Comp;
        comp.Powered = TryComp<ApcPowerReceiverComponent>(ent, out var receiver) && receiver.Powered;

        if (comp.Powered)
            comp.Battery = MathF.Min(comp.BatteryCapacity, comp.Battery + comp.RechargeRate);
        else if (comp.Working)
            comp.Battery = MathF.Max(0f, comp.Battery - 1f);

        UpdateState(ent, announce: true);
    }

    private void UpdateState(Entity<DystopiaStabilizerComponent> ent, bool announce)
    {
        var comp = ent.Comp;
        var wasBroken = comp.Broken;
        var wasWorking = comp.Working;
        var wasPowered = comp.Powered;

        comp.Broken = _damageable.GetTotalDamage(ent.Owner).Float() >= comp.BreakDamage;
        comp.Working = !comp.Broken && (comp.Powered || comp.Battery > 0f);

        string state;
        if (comp.Broken)
            state = comp.IsOmega ? "destroyed" : "broken";
        else if (!comp.Working)
            state = "off";
        else if (comp.Powered)
            state = "on";
        else
            state = "battery";

        _appearance.SetData(ent, DystopiaStabilizerVisuals.State, state);
        _light.SetEnabled(ent, comp.Working);
        _ambient.SetAmbience(ent, comp.Working); // гудит, только пока работает

        if (!announce)
            return;

        if (comp.Broken && !wasBroken)
        {
            if (comp.IsOmega)
            {
                Announce(ent, "dystopia-omega-destroyed");
                var ev = new DystopiaOmegaStabilizerDestroyedEvent(ent);
                RaiseLocalEvent(ref ev);
            }
            else
            {
                Announce(ent, "dystopia-stabilizer-broken");
            }
        }
        else if (!comp.Broken && wasBroken)
        {
            Announce(ent, "dystopia-stabilizer-repaired");
        }
        else if (!comp.Broken && wasWorking && !comp.Working)
        {
            Announce(ent, "dystopia-stabilizer-depleted");
        }
        else if (comp.Working && wasPowered && !comp.Powered && wasWorking)
        {
            Announce(ent, "dystopia-stabilizer-on-battery");
        }
    }

    private void Announce(EntityUid uid, string locId)
    {
        if (_station.GetOwningStation(uid) is not { } station)
            return;

        var message = Loc.GetString(locId, ("name", Name(uid)));
        _chat.DispatchStationAnnouncement(station, message,
            sender: Loc.GetString("dystopia-stabilizer-sender"),
            playDefaultSound: false,
            colorOverride: Color.FromHex("#78BEE1"));
    }

    /// <summary>Омега под защитой не получает урона.</summary>
    private void OnDamageModify(Entity<DystopiaStabilizerComponent> ent, ref DamageModifyEvent args)
    {
        if (!ent.Comp.IsOmega || !ent.Comp.Shielded)
            return;

        args.Damage = new DamageSpecifier();
        if (args.Origin is { } origin)
            _popup.PopupEntity(Loc.GetString("dystopia-omega-shield-hit"), ent, origin, PopupType.SmallCaution);
    }

    /// <summary>Работающий стабилизатор в радиусе — Семя не приживается, киста не появляется.</summary>
    private void OnSeedAttempt(ref DystopiaFleshSeedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var mapCoords = _transform.ToMapCoordinates(args.Coordinates);
        var query = EntityQueryEnumerator<DystopiaStabilizerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var comp, out var xform))
        {
            if (!comp.Working || xform.MapID != mapCoords.MapId)
                continue;

            if ((_transform.GetWorldPosition(xform) - mapCoords.Position).Length() <= comp.Radius)
            {
                args.Cancelled = true;
                args.Reason = Loc.GetString("dystopia-flesh-seed-suppressed");
                return;
            }
        }
    }

    /// <summary>Кисты у зоны стабилизатора растят наросты медленнее.</summary>
    private void OnGrowthSpeed(ref DystopiaFleshGrowthSpeedEvent args)
    {
        var cystXform = Transform(args.Cyst);
        var cystPos = _transform.GetWorldPosition(cystXform);
        var query = EntityQueryEnumerator<DystopiaStabilizerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var comp, out var xform))
        {
            if (!comp.Working || xform.MapID != cystXform.MapID)
                continue;

            if ((_transform.GetWorldPosition(xform) - cystPos).Length() <= comp.Radius + comp.SlowdownReach)
                args.Multiplier = MathF.Max(args.Multiplier, comp.GrowthSlowdown);
        }
    }

    private void OnExamined(Entity<DystopiaStabilizerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;
        var minutes = (int) MathF.Ceiling(comp.Battery / 60f);
        string status;
        if (comp.Broken)
            status = Loc.GetString(comp.IsOmega ? "dystopia-stabilizer-examine-destroyed" : "dystopia-stabilizer-examine-broken");
        else if (!comp.Working)
            status = Loc.GetString("dystopia-stabilizer-examine-off");
        else if (comp.Powered)
            status = Loc.GetString("dystopia-stabilizer-examine-on", ("minutes", minutes));
        else
            status = Loc.GetString("dystopia-stabilizer-examine-battery", ("minutes", minutes));

        args.PushMarkup(status);
        args.PushMarkup(Loc.GetString("dystopia-stabilizer-examine-radius", ("radius", (int) comp.Radius)));

        if (comp.IsOmega && !comp.Broken)
            args.PushMarkup(Loc.GetString(comp.Shielded ? "dystopia-omega-examine-shielded" : "dystopia-omega-examine-vulnerable",
                ("min", comp.OmegaMinWorking)));
    }
}
