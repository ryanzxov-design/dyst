// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: датчик Стражи в маске стражника. Раз в секунду: кто носит маску, в каком он состоянии (норма, ранен,
// тяжёлое, мёртв) и где — с коротким следом для карты «Дозора». Маска снята — данных нет, на карте остаётся последняя
// точка. Стражник впал в тяжёлое состояние или погиб — тревога в ленту соционадзора и на канал Стражи. «Сигнал
// бедствия» (действие маски) — тоже тревога, метка стражника на карте мигает.

using Content.Shared._Dystopia.Registry;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityGuardSensorSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CityRegistrySystem _registry = default!;
    [Dependency] private CityCameraSystem _cameras = default!;

    /// <summary>Как долго метка стражника мигает после сигнала бедствия.</summary>
    private static readonly TimeSpan DistressShown = TimeSpan.FromMinutes(1);

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityGuardSensorComponent, CityGuardDistressEvent>(OnDistress);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;
        _accumulator = 0f;

        if (!_registry.TryGetRegistry(out var registry))
            return;

        var now = _ticker.RoundDuration();
        var query = EntityQueryEnumerator<CityGuardSensorComponent>();
        while (query.MoveNext(out var uid, out var sensor))
        {
            UpdateSensor(registry, (uid, sensor), now);
        }
    }

    private void UpdateSensor(Entity<CityRegistryComponent> registry, Entity<CityGuardSensorComponent> ent, TimeSpan now)
    {
        var sensor = ent.Comp;
        sensor.Wearer = WearerOf(ent);

        // старые точки следа уходят
        sensor.Trail.RemoveAll(p => now - p.Time > sensor.TrailLength);

        if (sensor.Wearer is not { } wearer)
        {
            sensor.Health = CityGuardHealth.NoData;
            return;
        }

        if (_registry.NumberOf(wearer) is { } number)
            sensor.LastWearerNumber = number;

        var health = HealthOf(wearer, sensor);
        var old = sensor.Health;
        sensor.Health = health;
        sensor.LastSeenAt = now;

        if (_cameras.TryGridPosition(ent, out var grid, out var position))
        {
            sensor.LastGrid = grid;
            sensor.LastPosition = position;
            sensor.Trail.Add((now, position));
        }

        // тревога — только на перемену (а не каждую секунду, пока он лежит)
        if (health == old)
            return;

        if (health == CityGuardHealth.Critical && old != CityGuardHealth.Dead)
            Raise(registry, ent, CityAlertKind.GuardCritical, "dystopia-guard-alert-critical");
        else if (health == CityGuardHealth.Dead)
            Raise(registry, ent, CityAlertKind.GuardDead, "dystopia-guard-alert-dead");
    }

    /// <summary>Кто носит маску (надетая на лицо, приспущенная тоже считается).</summary>
    private EntityUid? WearerOf(EntityUid mask)
    {
        if (!TryComp<ClothingComponent>(mask, out var clothing) ||
            clothing.InSlotFlag is not { } slot || (slot & SlotFlags.MASK) == 0 ||
            !_container.TryGetContainingContainer((mask, null, null), out var container))
        {
            return null;
        }

        return container.Owner;
    }

    private CityGuardHealth HealthOf(EntityUid wearer, CityGuardSensorComponent sensor)
    {
        if (_mobState.IsDead(wearer))
            return CityGuardHealth.Dead;
        if (_mobState.IsCritical(wearer))
            return CityGuardHealth.Critical;
        // урон — через систему повреждений: напрямую поле компонента читать нельзя
#pragma warning disable CS0618
        var damage = _damageable.GetTotalDamage(wearer);
#pragma warning restore CS0618
        if (damage >= sensor.HurtDamage)
            return CityGuardHealth.Hurt;
        return CityGuardHealth.Ok;
    }

    private void OnDistress(Entity<CityGuardSensorComponent> ent, ref CityGuardDistressEvent args)
    {
        if (args.Handled || !_registry.TryGetRegistry(out var registry))
            return;
        args.Handled = true;

        var now = _ticker.RoundDuration();
        if (now < ent.Comp.NextDistress)
            return;
        ent.Comp.NextDistress = now + ent.Comp.DistressCooldown;

        Raise(registry, ent, CityAlertKind.Distress, "dystopia-guard-alert-distress");
    }

    /// <summary>Подавал ли стражник сигнал бедствия совсем недавно (метка мигает).</summary>
    public bool RecentDistress(CityGuardSensorComponent sensor)
    {
        return sensor.NextDistress > TimeSpan.Zero &&
               _ticker.RoundDuration() < sensor.NextDistress - sensor.DistressCooldown + DistressShown;
    }

    private void Raise(Entity<CityRegistryComponent> registry, Entity<CityGuardSensorComponent> ent, CityAlertKind kind, string key)
    {
        var number = ent.Comp.LastWearerNumber;
        var who = number is { } n ? n.ToString() : "—";
        var place = _cameras.PlaceOf(ent);
        _cameras.AddAlert(registry, kind, Loc.GetString(key, ("number", who), ("place", place)), place, ent.Owner, number);
        _cameras.Broadcast(registry, ent, Loc.GetString(key + "-radio",
            ("number", who), ("place", FormattedMessage.EscapeText(place))));
    }
}
