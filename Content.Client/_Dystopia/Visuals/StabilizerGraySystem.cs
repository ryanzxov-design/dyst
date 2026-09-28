using Content.Shared._Dystopia.FleshCult;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Dystopia.Visuals;

/// <summary>
/// Чем ближе игрок к работающему стабилизатору, тем серее экран. Начинает сереть с GrayRadius клеток от края
/// стабилизатора, вплотную — полностью серый. Зерно и помехи — так же, но с вдвое меньшего расстояния.
/// Работает ли стабилизатор, клиент узнаёт по его внешнему виду.
/// </summary>
public sealed partial class StabilizerGraySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private AppearanceSystem _appearance = default!;

    private StabilizerGrayOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new StabilizerGrayOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var target = 0f;
        var noiseTarget = 0f;
        if (_player.LocalEntity is { } player && TryComp<TransformComponent>(player, out var playerXform))
        {
            var playerPos = _transform.GetWorldPosition(playerXform);
            var query = EntityQueryEnumerator<DystopiaStabilizerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var comp, out var xform))
            {
                if (xform.MapID != playerXform.MapID || !IsWorking(uid))
                    continue;

                // расстояние от края стабилизатора; вплотную (≤0.5 клетки) — эффект максимальный
                var distance = (_transform.GetWorldPosition(xform) - playerPos).Length() - comp.HalfSize;
                if (distance >= comp.GrayRadius)
                    continue;

                target = MathF.Max(target, Closeness(distance, comp.GrayRadius));
                noiseTarget = MathF.Max(noiseTarget, Closeness(distance, comp.GrayRadius * 0.5f));
            }
        }

        // плавно, без рывков
        var lerp = MathF.Min(1f, frameTime * 4f);
        _overlay.Amount += (target - _overlay.Amount) * lerp;
        _overlay.Noise += (noiseTarget - _overlay.Noise) * lerp;
    }

    /// <summary>1 — вплотную, 0 — на границе радиуса и дальше.</summary>
    private static float Closeness(float distance, float radius)
    {
        if (distance >= radius)
            return 0f;

        return 1f - Math.Clamp((distance - 0.5f) / MathF.Max(0.1f, radius - 0.5f), 0f, 1f);
    }

    private bool IsWorking(EntityUid uid)
    {
        return _appearance.TryGetData<string>(uid, DystopiaStabilizerVisuals.State, out var state) &&
               state is "on" or "battery";
    }
}
