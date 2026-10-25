// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: карта реактора на пульте — клетка на колонну (север сверху). Топливо — цвет по температуре (пустой канал
// тёмный), стержень — серый с красной полосой глубины и номером группы, графит — уголь, пароводяной — синий.
// Повреждённые колонны — красный крест. Клик — выбрать колонну. Размер клетки меняется кнопками масштаба.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaReactorMap : Control
{
    public const float MinCell = 12f;
    public const float MaxCell = 48f;

    private static readonly Color Empty = Color.FromHex("#1a2230");
    private static readonly Color Graphite = Color.FromHex("#2a2a30");
    private static readonly Color Coolant = Color.FromHex("#2c5a8c");
    private static readonly Color ControlBody = Color.FromHex("#5a6070");
    private static readonly Color RodColor = Color.FromHex("#d04030");
    private static readonly Color Scram = Color.FromHex("#ff2020");

    private List<DystopiaReactorCell> _cells = new();
    private int _width;
    private int _height;

    public float CellSize = 24f;
    public NetEntity? Selected;

    public event Action<DystopiaReactorCell>? OnSelect;

    public DystopiaReactorMap()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    public void SetCells(List<DystopiaReactorCell> cells, int width, int height)
    {
        _cells = cells;
        _width = width;
        _height = height;
        InvalidateMeasure();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(MathF.Max(1, _width) * CellSize, MathF.Max(1, _height) * CellSize);
    }

    /// <summary>Цвет топливного канала по температуре: синий (холодный) → зелёный (кипение) → жёлтый → красный.</summary>
    public static Color HeatColor(float t)
    {
        if (t < 285f)
            return Color.InterpolateBetween(Color.FromHex("#203858"), Color.FromHex("#2f8a58"), Math.Clamp(t / 285f, 0f, 1f));
        if (t < 600f)
            return Color.InterpolateBetween(Color.FromHex("#2f8a58"), Color.FromHex("#d8c040"), (t - 285f) / 315f);
        if (t < 1000f)
            return Color.InterpolateBetween(Color.FromHex("#d8c040"), Color.FromHex("#e04020"), (t - 600f) / 400f);
        return Color.InterpolateBetween(Color.FromHex("#e04020"), Color.FromHex("#ffffff"), Math.Clamp((t - 1000f) / 500f, 0f, 1f));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var cs = CellSize * UIScale;
        var pad = MathF.Max(1f, UIScale);
        handle.DrawRect(new UIBox2(Vector2.Zero, PixelSize), CityUi.Bg);

        foreach (var cell in _cells)
        {
            var x0 = cell.X * cs;
            var y0 = (_height - 1 - cell.Y) * cs;
            var box = new UIBox2(x0 + pad, y0 + pad, x0 + cs - pad, y0 + cs - pad);
            switch (cell.Kind)
            {
                case DystopiaReactorColumnKind.Fuel:
                    handle.DrawRect(box, cell.RodGrade > 0 ? HeatColor(cell.Temperature) : Empty);
                    if (cell.RodGrade > 0)
                    {
                        // выгорание — тёмная полоса снизу
                        var h = (box.Height - 2 * pad) * cell.Burnup;
                        handle.DrawRect(new UIBox2(box.Left + pad, box.Bottom - pad - h, box.Left + 3 * pad, box.Bottom - pad),
                            Color.Black.WithAlpha(0.6f));
                    }

                    break;
                case DystopiaReactorColumnKind.Control:
                    handle.DrawRect(box, ControlBody);
                    var depth = box.Height * cell.Insertion;
                    handle.DrawRect(new UIBox2(box.Left + box.Width * 0.35f, box.Top, box.Right - box.Width * 0.35f, box.Top + depth),
                        cell.Scrammed ? Scram : RodColor);
                    break;
                case DystopiaReactorColumnKind.Graphite:
                    handle.DrawRect(box, Graphite);
                    break;
                default:
                    handle.DrawRect(box, Color.InterpolateBetween(Coolant, HeatColor(cell.Temperature), 0.3f));
                    break;
            }

            if (cell.Health < 0.5f)
            {
                handle.DrawLine(box.TopLeft, box.BottomRight, CityUi.Danger);
                handle.DrawLine(box.TopRight, box.BottomLeft, CityUi.Danger);
            }

            if (Selected == cell.Entity)
                handle.DrawRect(new UIBox2(x0, y0, x0 + cs, y0 + cs), CityUi.Glow, false);
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var cs = CellSize * UIScale;
        var pos = args.RelativePixelPosition;
        var x = (int) MathF.Floor(pos.X / cs);
        var y = _height - 1 - (int) MathF.Floor(pos.Y / cs);
        foreach (var cell in _cells)
        {
            if (cell.X != x || cell.Y != y)
                continue;

            Selected = cell.Entity;
            OnSelect?.Invoke(cell);
            break;
        }

        args.Handle();
    }
}
