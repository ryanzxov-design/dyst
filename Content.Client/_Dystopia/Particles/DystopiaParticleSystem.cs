using System.Numerics;
using Content.Shared._Dystopia.Particles;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Client._Dystopia.Particles;

/// <summary>
/// Пиксельные частицы Dystopia: огонь, искры, дым, споры и т.п.
/// Частицы живут только на клиенте. Источник — сущности с DystopiaParticleEmitterComponent.
/// </summary>
public sealed partial class DystopiaParticleSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    /// <summary>Предел живых частиц — защита от просадки кадров.</summary>
    public const int MaxParticles = 4000;

    internal readonly List<Particle> Particles = new();

    // Возраст источников (для расширения облака)
    private readonly Dictionary<EntityUid, float> _ages = new();
    private readonly HashSet<EntityUid> _seen = new();
    private DystopiaParticleOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new DystopiaParticleOverlay(this);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
        Particles.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // 1. Рождение новых частиц
        _seen.Clear();
        var query = EntityQueryEnumerator<DystopiaParticleEmitterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Enabled || xform.MapID == MapId.Nullspace)
                continue;

            _seen.Add(uid);
            var age = _ages.GetValueOrDefault(uid) + frameTime;
            _ages[uid] = age;

            var position = _transform.GetWorldPosition(xform);
            var velocity = _physics.GetMapLinearVelocity(uid, xform: xform);

            foreach (var emitter in comp.Emitters)
            {
                emitter.Accumulator += emitter.Rate * frameTime;
                var count = (int) emitter.Accumulator;
                emitter.Accumulator -= count;

                var ramp = emitter.RampTime > 0f ? Math.Clamp(age / emitter.RampTime, 0f, 1f) : 0f;
                for (var i = 0; i < count && Particles.Count < MaxParticles; i++)
                {
                    Particles.Add(Spawn(emitter, xform.MapID, position, velocity, ramp));
                }
            }
        }

        // Забываем возраст исчезнувших источников
        if (_ages.Count > _seen.Count)
        {
            foreach (var key in new List<EntityUid>(_ages.Keys))
            {
                if (!_seen.Contains(key))
                    _ages.Remove(key);
            }
        }

        // 2. Жизнь частиц
        for (var i = Particles.Count - 1; i >= 0; i--)
        {
            var p = Particles[i];
            p.Age += frameTime;
            if (p.Age >= p.Lifetime)
            {
                // быстрое удаление: переставляем последнюю на место умершей
                Particles[i] = Particles[^1];
                Particles.RemoveAt(Particles.Count - 1);
                continue;
            }

            p.Velocity += p.Emitter.Drift * frameTime;
            p.Velocity *= MathF.Max(0f, 1f - p.Emitter.Drag * frameTime);
            p.Position += p.Velocity * frameTime;
            Particles[i] = p;
        }
    }

    private Particle Spawn(DystopiaParticleEmitter emitter, MapId map, Vector2 position, Vector2 sourceVelocity, float ramp)
    {
        // Направление: вдоль движения источника или от севера
        var baseAngle = emitter.AlignToVelocity && sourceVelocity.LengthSquared() > 0.0001f
            ? MathF.Atan2(sourceVelocity.Y, sourceVelocity.X)
            : MathF.PI / 2f;
        baseAngle += MathHelper.DegreesToRadians(emitter.Direction);
        var spreadDeg = emitter.SpreadEnd >= 0f ? emitter.Spread + (emitter.SpreadEnd - emitter.Spread) * ramp : emitter.Spread;
        var spread = MathHelper.DegreesToRadians(spreadDeg) * 0.5f;
        var angle = baseAngle + _random.NextFloat(-spread, spread);

        var speed = _random.NextFloat(emitter.Speed.X, MathF.Max(emitter.Speed.X, emitter.Speed.Y));
        var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed
                       + sourceVelocity * emitter.InheritVelocity;

        var jitterAngle = _random.NextFloat(0f, MathF.PI * 2f);
        var jitterRadius = emitter.JitterEnd >= 0f ? emitter.Jitter + (emitter.JitterEnd - emitter.Jitter) * ramp : emitter.Jitter;
        var jitter = new Vector2(MathF.Cos(jitterAngle), MathF.Sin(jitterAngle)) * _random.NextFloat(0f, jitterRadius);

        return new Particle
        {
            Emitter = emitter,
            Map = map,
            Position = position + jitter,
            Velocity = velocity,
            Lifetime = _random.NextFloat(emitter.Lifetime.X, MathF.Max(emitter.Lifetime.X, emitter.Lifetime.Y)),
            Seed = _random.NextFloat(),
            SizeScale = 1f + (emitter.SizeMultiplierEnd - 1f) * ramp,
        };
    }

    internal struct Particle
    {
        public DystopiaParticleEmitter Emitter;
        public MapId Map;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public float Seed;
        public float SizeScale;
    }
}
