// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: фосфорные боеприпасы — цель горит заданное время, потушить её нельзя.

using Content.Server.Atmos.EntitySystems;
using Content.Shared._Dystopia.Weapons.Ammunition;
using Content.Shared.Atmos.Components;
using Content.Shared.Projectiles;
using Content.Shared.Rejuvenate;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Weapons.Ammunition;

public sealed partial class PhosphorusSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FlammableSystem _flammable = default!;

    /// <summary>
    /// Как часто фосфор подновляет огонь: вода и катание по полу сбивают его не дольше чем на этот срок.
    /// </summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PhosphorusOnHitComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<PhosphorusBurningComponent, ComponentShutdown>(OnBurningShutdown);
        // Админское исцеление гасит и фосфор (снимаем до того, как его попробует потушить FlammableSystem)
        SubscribeLocalEvent<PhosphorusBurningComponent, RejuvenateEvent>(OnRejuvenate, before: [typeof(FlammableSystem)]);
    }

    private void OnProjectileHit(Entity<PhosphorusOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        Apply(args.Target, ent.Comp.Duration, ent.Comp.FireStacks, ent.Owner, args.Shooter);
    }

    /// <summary>
    /// Поджечь цель фосфором. Повторное попадание продлевает горение.
    /// </summary>
    public void Apply(EntityUid target, TimeSpan duration, float fireStacks, EntityUid source, EntityUid? shooter = null)
    {
        if (!TryComp<FlammableComponent>(target, out var flammable))
            return;

        // Возвращает true, если фосфор на цели уже горел
        if (!EnsureComp<PhosphorusBurningComponent>(target, out var burning))
        {
            burning.CouldExtinguish = flammable.CanExtinguish;
            burning.FireStacks = fireStacks;
        }

        var end = _timing.CurTime + duration;
        if (end > burning.EndTime)
            burning.EndTime = end;
        burning.FireStacks = MathF.Max(burning.FireStacks, fireStacks);
        burning.NextUpdate = _timing.CurTime;

        flammable.CanExtinguish = false;
        // Сначала огонь, потом поджог — так попадание пишется в лог с именем стрелка
        _flammable.SetFireStacks(target, MathF.Max(flammable.FireStacks, burning.FireStacks), flammable);
        _flammable.Ignite(target, source, flammable, shooter);
    }

    private void OnRejuvenate(EntityUid uid, PhosphorusBurningComponent component, RejuvenateEvent args)
    {
        RemComp<PhosphorusBurningComponent>(uid);
    }

    private void OnBurningShutdown(Entity<PhosphorusBurningComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<FlammableComponent>(ent, out var flammable))
            flammable.CanExtinguish = ent.Comp.CouldExtinguish;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PhosphorusBurningComponent, FlammableComponent>();
        while (query.MoveNext(out var uid, out var burning, out var flammable))
        {
            if (now >= burning.EndTime)
            {
                // Догорел: дальше это обычный огонь, его снова можно потушить
                RemCompDeferred<PhosphorusBurningComponent>(uid);
                continue;
            }

            if (now < burning.NextUpdate)
                continue;

            burning.NextUpdate = now + UpdateInterval;
            Keep(uid, burning, flammable);
        }
    }

    /// <summary>
    /// Огонь не ниже заданного и горит.
    /// </summary>
    private void Keep(EntityUid uid, PhosphorusBurningComponent burning, FlammableComponent flammable)
    {
        if (flammable.FireStacks < burning.FireStacks || !flammable.OnFire)
            _flammable.SetFireStacks(uid, MathF.Max(flammable.FireStacks, burning.FireStacks), flammable, ignite: true);
    }
}
