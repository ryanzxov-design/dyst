// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: карта «Дозора» — стражники по датчикам масок поверх навигационной карты Города. Метка-ромб в цвете
// состояния вспыхивает ореолом на каждом ударе сердца стражника (по его кардиограмме в списке); маска снята — пустой
// пунктирный ромб на последней точке; следы за последние секунды; цели — кольца разыскиваемых и «вне закона» на их
// последней отметке камер. Щелчок по стражнику — выбрать его.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Client.Pinpointer.UI;
using Content.Client.Resources;
using Content.Shared._Dystopia.Registry;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;

namespace Content.Client._Dystopia.Registry;

public sealed class CityDozorMapControl : CityNavMapControl
{
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IEntityManager _entMan = default!;

    public static readonly Color TargetColor = Color.FromHex("#B5A3DA");
    public static readonly Color DistressColor = Color.FromHex("#FF5A4E");

    private readonly Texture _diamond;
    private readonly Dictionary<NetEntity, int> _numbers = new();
    private List<CityPatrolEntry> _patrols = new();
    private List<CityDozorTarget> _targets = new();

    public bool ShowTargets = true;
    public bool ShowTrails = true;
    public int? Selected;

    /// <summary>Вспышка пульса стражника по номеру (0..1) — от его кардиограммы.</summary>
    public Func<int, float>? FlashOf;

    public event Action<int?>? GuardSelected;

    public CityDozorMapControl()
    {
        IoCManager.InjectDependencies(this);
        _diamond = _resources.GetTexture("/Textures/Interface/NavMap/beveled_diamond.png");
        PostWallDrawingAction += DrawDozor;
        TrackedEntitySelectedAction += net =>
            GuardSelected?.Invoke(net is { } n && _numbers.TryGetValue(n, out var number) ? number : null);
    }

    public void SetData(List<CityPatrolEntry> patrols, List<CityDozorTarget> targets)
    {
        _patrols = patrols;
        _targets = targets;

        // невидимые метки — только чтобы по стражнику можно было щёлкнуть (рисуем их сами)
        TrackedEntities.Clear();
        _numbers.Clear();
        if (MapUid is not { } grid)
            return;

        foreach (var patrol in patrols)
        {
            if (!patrol.HasPosition || !patrol.Sensor.IsValid() || _entMan.GetEntity(patrol.Grid) != grid)
                continue;

            TrackedEntities[patrol.Sensor] = new NavMapBlip(new EntityCoordinates(grid, patrol.Position),
                _diamond, Color.Transparent, false, true);
            _numbers[patrol.Sensor] = patrol.Number;
        }
    }

    private Vector2 ToScreen(Vector2 local)
    {
        var position = local - GetOffset();
        return ScalePosition(new Vector2(position.X, -position.Y));
    }

    private void DrawDozor(DrawingHandleScreen handle)
    {
        if (MapUid is not { } grid)
            return;

        var font = CityUi.Regular(10);
        var r = 6f * UIScale;
        var blink = (float) (Timing.RealTime.TotalSeconds % 1.0) < 0.5f;

        // цели
        if (ShowTargets)
        {
            foreach (var target in _targets)
            {
                if (_entMan.GetEntity(target.Grid) != grid)
                    continue;

                var p = ToScreen(target.Position);
                handle.DrawCircle(p, r * 1.2f, TargetColor, false);
                handle.DrawCircle(p, r * 1.2f + 1, TargetColor, false);
                handle.DrawString(font, p + new Vector2(r * 1.6f, -r), $"№{target.Number}", TargetColor);
            }
        }

        foreach (var patrol in _patrols)
        {
            if (!patrol.HasPosition || _entMan.GetEntity(patrol.Grid) != grid)
                continue;

            var color = CityHeartbeatControl.ColorOf(patrol.Health);
            var p = ToScreen(patrol.Position);

            // след
            if (ShowTrails && patrol.Trail.Count > 1)
            {
                for (var i = 0; i < patrol.Trail.Count; i++)
                {
                    var alpha = 0.2f + 0.5f * i / patrol.Trail.Count;
                    handle.DrawCircle(ToScreen(patrol.Trail[i]), 1.5f * UIScale, color.WithAlpha(alpha));
                }
            }

            if (patrol.Health == CityGuardHealth.NoData)
            {
                // маска снята: пустой пунктирный ромб на последней точке
                Diamond(handle, p, r, CityUi.Dim, true);
            }
            else
            {
                var flash = FlashOf?.Invoke(patrol.Number) ?? 0f;
                if (flash > 0.05f && patrol.Health != CityGuardHealth.Dead)
                    handle.DrawCircle(p, r + 6f * flash * UIScale, color.WithAlpha(0.45f * flash));

                var size = r + 2f * flash * UIScale;
                handle.DrawTextureRect(_diamond, new UIBox2(p - new Vector2(size, size), p + new Vector2(size, size)), color);
            }

            if (patrol.Distress && blink)
                handle.DrawCircle(p, r * 2.2f, DistressColor, false);

            if (patrol.Number == Selected)
            {
                var s = r + 5f * UIScale;
                handle.DrawRect(new UIBox2(p - new Vector2(s, s), p + new Vector2(s, s)), CityUi.Glow, false);
            }

            var label = patrol.Health == CityGuardHealth.NoData && patrol.DarkFor is { } dark
                ? Loc.GetString("dystopia-dozor-map-dark", ("number", patrol.Number), ("minutes", (int) dark.TotalMinutes))
                : patrol.Number.ToString();
            handle.DrawString(font, p + new Vector2(r + 4f, -r), label,
                patrol.Health == CityGuardHealth.NoData ? CityUi.Dim : color);
        }
    }

    /// <summary>Ромб контуром (пунктиром — нет данных).</summary>
    private static void Diamond(DrawingHandleScreen handle, Vector2 center, float r, Color color, bool dashed)
    {
        var top = center + new Vector2(0, -r);
        var right = center + new Vector2(r, 0);
        var bottom = center + new Vector2(0, r);
        var left = center + new Vector2(-r, 0);
        foreach (var (from, to) in new[] { (top, right), (right, bottom), (bottom, left), (left, top) })
        {
            if (!dashed)
            {
                handle.DrawLine(from, to, color);
                continue;
            }

            const int dashes = 3;
            for (var i = 0; i < dashes; i++)
            {
                var a = Vector2.Lerp(from, to, i / (float) dashes);
                var b = Vector2.Lerp(from, to, (i + 0.5f) / dashes);
                handle.DrawLine(a, b, color);
            }
        }
    }
}
