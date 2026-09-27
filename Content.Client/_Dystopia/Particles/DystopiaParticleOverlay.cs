using System.Numerics;
using Content.Shared._Dystopia.Particles;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Particles;

/// <summary>
/// Рисует частицы квадратиками ровно в пиксель спрайта (1/32 клетки), привязанными к пиксельной сетке мира —
/// чтобы частицы не выбивались из пиксель-арта. Светящиеся частицы — аддитивно, дым — обычным смешиванием.
/// Рисуется под туманом войны: частицы за пределами обзора не видны.
/// </summary>
public sealed partial class DystopiaParticleOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> AdditiveShader = "DystopiaParticleAdditive";
    private const float Pixel = 1f / 32f;

    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly DystopiaParticleSystem _system;
    private readonly ShaderInstance _additive;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public DystopiaParticleOverlay(DystopiaParticleSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _additive = _prototypeManager.Index(AdditiveShader).Instance();
        ZIndex = 5;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var particles = _system.Particles;
        if (particles.Count == 0)
            return;

        var handle = args.WorldHandle;
        var bounds = args.WorldAABB.Enlarged(1f);
        var time = (float) _timing.RealTime.TotalSeconds;

        // Сначала дым (обычное смешивание), потом свечение (аддитивно) поверх
        DrawPass(handle, particles, args.MapId, bounds, time, false);
        handle.UseShader(_additive);
        DrawPass(handle, particles, args.MapId, bounds, time, true);
        handle.UseShader(null);
    }

    private static void DrawPass(DrawingHandleWorld handle, List<DystopiaParticleSystem.Particle> particles,
        Robust.Shared.Map.MapId map, Box2 bounds, float time, bool additive)
    {
        foreach (var p in particles)
        {
            if (p.Emitter.Additive != additive || p.Map != map || !bounds.Contains(p.Position))
                continue;

            var t = Math.Clamp(p.Age / p.Lifetime, 0f, 1f);
            var color = Ramp(p.Emitter.Colors, t);

            if (p.Emitter.Flicker > 0f)
            {
                var f = 1f - p.Emitter.Flicker * (0.5f + 0.5f * MathF.Sin(time * 40f + p.Seed * 100f));
                color = color.WithAlpha(color.A * f);
            }

            var size = MathF.Max(1f, MathF.Round((p.Emitter.Size.X + (p.Emitter.Size.Y - p.Emitter.Size.X) * t) * p.SizeScale)) * Pixel;

            // привязка к пиксельной сетке мира
            var x = MathF.Floor(p.Position.X / Pixel) * Pixel;
            var y = MathF.Floor(p.Position.Y / Pixel) * Pixel;
            var half = MathF.Floor(size / Pixel / 2f) * Pixel;
            var box = new Box2(x - half, y - half, x - half + size, y - half + size);

            handle.DrawRect(box, color);
        }
    }

    private static Color Ramp(List<Color> colors, float t)
    {
        if (colors.Count == 0)
            return Color.White;
        if (colors.Count == 1)
            return colors[0];

        var scaled = t * (colors.Count - 1);
        var i = Math.Min((int) scaled, colors.Count - 2);
        return Color.InterpolateBetween(colors[i], colors[i + 1], scaled - i);
    }
}
