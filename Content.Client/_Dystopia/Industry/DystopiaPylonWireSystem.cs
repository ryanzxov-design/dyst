// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: провода ЛЭП — провисающие ленты между изоляторами связанных опор (по проводу на фазу; каждый пролёт
// рисуется один раз). Опоры могут стоять на разных сетках — считаем в мировых координатах.
// Под туманом войны: провод за пределами обзора не виден.

using System.Numerics;
using Content.Shared._Dystopia.Industry;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Dystopia.Industry;

public sealed partial class DystopiaPylonWireSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new DystopiaPylonWireOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<DystopiaPylonWireOverlay>();
    }
}

public sealed class DystopiaPylonWireOverlay : Overlay
{
    private static readonly Color Wire = Color.FromHex("#18181f");
    private static readonly Color Shine = Color.FromHex("#7a8090");
    private const int Segments = 16;
    private const float SagPerTile = 0.035f;

    private readonly IEntityManager _ent;
    private readonly SharedTransformSystem _xform;
    private readonly Vector2[] _points = new Vector2[Segments + 1];
    private readonly Vector2[] _strip = new Vector2[(Segments + 1) * 2];

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public DystopiaPylonWireOverlay(IEntityManager ent)
    {
        _ent = ent;
        _xform = ent.System<SharedTransformSystem>();
        ZIndex = 4;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var bounds = args.WorldAABB.Enlarged(2f);
        var query = _ent.EntityQueryEnumerator<DystopiaPylonComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pylon, out var xform))
        {
            if (xform.MapID != args.MapId || pylon.Links.Count == 0)
                continue;

            var a = _xform.GetWorldPosition(xform) + pylon.Attach;
            foreach (var other in pylon.Links)
            {
                // пролёт рисует опора с меньшим номером
                if (other.Id < uid.Id ||
                    !_ent.TryGetComponent<TransformComponent>(other, out var otherXform) ||
                    !_ent.TryGetComponent<DystopiaPylonComponent>(other, out var otherPylon))
                {
                    continue;
                }

                var b = _xform.GetWorldPosition(otherXform) + otherPylon.Attach;
                if (!bounds.Contains(a) && !bounds.Contains(b) && !bounds.Contains((a + b) / 2f))
                    continue;

                var phases = Math.Min(pylon.Phases.Count, otherPylon.Phases.Count);
                if (phases == 0)
                {
                    DrawSpan(handle, a, b, pylon.WireWidth);
                    continue;
                }

                // провода фаз параллельны: изолятор i одной опоры — к изолятору i другой
                for (var i = 0; i < phases; i++)
                {
                    DrawSpan(handle, a + new Vector2(pylon.Phases[i], 0f), b + new Vector2(otherPylon.Phases[i], 0f),
                        pylon.WireWidth);
                }
            }
        }
    }

    private void DrawSpan(DrawingHandleWorld handle, Vector2 a, Vector2 b, float width)
    {
        var sag = (b - a).Length() * SagPerTile;
        for (var i = 0; i <= Segments; i++)
        {
            var t = i / (float) Segments;
            _points[i] = Vector2.Lerp(a, b, t) - new Vector2(0f, sag * 4f * t * (1f - t));
        }

        Strip(handle, width, 0f, Wire);
        Strip(handle, width * 0.3f, width * 0.25f, Shine.WithAlpha(0.6f));   // блик по верхней кромке
    }

    /// <summary>Лента вдоль ломаной: полоса толщиной width, сдвинутая на shift по нормали вверх.</summary>
    private void Strip(DrawingHandleWorld handle, float width, float shift, Color color)
    {
        var half = width / 2f;
        for (var i = 0; i <= Segments; i++)
        {
            var dir = _points[Math.Min(Segments, i + 1)] - _points[Math.Max(0, i - 1)];
            var n = dir.LengthSquared() > 1e-8f ? Vector2.Normalize(new Vector2(-dir.Y, dir.X)) : Vector2.UnitY;
            if (n.Y < 0f)
                n = -n;

            var c = _points[i] + n * shift;
            _strip[i * 2] = c + n * half;
            _strip[i * 2 + 1] = c - n * half;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _strip, color);
    }
}
