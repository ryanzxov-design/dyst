// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: приборы пульта реактора. Вертикальная шкала (мощность, температура, вода, давление) с порогами и меткой
// уставки и общая шкала стержней: колонка на каждую группу и на все стержни — красный поглотитель опускается сверху
// на глубину погружения, белая черта — куда он идёт.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

/// <summary>Вертикальный прибор: подпись сверху, столбик, значение снизу.</summary>
public sealed class DystopiaBarGauge : Control
{
    public string Title = string.Empty;
    public string Value = string.Empty;
    public float Fraction;
    public Color BarColor = CityUi.Accent;

    /// <summary>Пороги: доля шкалы и цвет черты.</summary>
    public readonly List<(float Fraction, Color Color)> Marks = new();

    /// <summary>Метка уставки (доля шкалы) — треугольник сбоку.</summary>
    public float? Marker;

    public DystopiaBarGauge()
    {
        MinSize = new Vector2(52, 160);
        VerticalExpand = true;
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var title = CityUi.Bold(10);
        var mono = CityUi.Mono(10);
        var w = PixelSize.X;
        var h = PixelSize.Y;
        var line = 16 * s;

        DrawCentered(handle, title, Title, 0, w, 0, CityUi.Dim);
        DrawCentered(handle, mono, Value, 0, w, h - line, CityUi.Glow);

        var barW = MathF.Min(26 * s, w - 16 * s);
        var x0 = (w - barW) / 2f;
        var top = line + 4 * s;
        var bottom = h - line - 4 * s;
        var box = new UIBox2(x0, top, x0 + barW, bottom);
        handle.DrawRect(box, CityUi.Input);
        var fill = Math.Clamp(Fraction, 0f, 1f) * box.Height;
        if (fill > 0f)
            handle.DrawRect(new UIBox2(box.Left, box.Bottom - fill, box.Right, box.Bottom), BarColor);

        // деления по четвертям
        for (var i = 1; i < 4; i++)
        {
            var y = box.Bottom - box.Height * i / 4f;
            handle.DrawLine(new Vector2(box.Left, y), new Vector2(box.Left + 4 * s, y), CityUi.Line);
            handle.DrawLine(new Vector2(box.Right - 4 * s, y), new Vector2(box.Right, y), CityUi.Line);
        }

        foreach (var (frac, color) in Marks)
        {
            var y = box.Bottom - Math.Clamp(frac, 0f, 1f) * box.Height;
            handle.DrawRect(new UIBox2(box.Left - 3 * s, y - s, box.Right + 3 * s, y + s), color);
        }

        if (Marker is { } marker)
        {
            var y = box.Bottom - Math.Clamp(marker, 0f, 1f) * box.Height;
            var tip = new Vector2(box.Left - 2 * s, y);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[]
            {
                tip, tip + new Vector2(-6 * s, -4 * s), tip + new Vector2(-6 * s, 4 * s),
            }, CityUi.Glow);
        }

        handle.DrawRect(box, CityUi.Frame, false);
    }

    private void DrawCentered(DrawingHandleScreen handle, Font font, string text, float x0, float x1, float y, Color color)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
            width += font.GetCharMetrics(rune, UIScale)?.Advance ?? 0;

        handle.DrawString(font, new Vector2(MathF.Max(x0, (x0 + x1 - width) / 2f), y), text, UIScale, color);
    }
}

/// <summary>Общая шкала стержней: колонка на группу (и на все стержни сразу).</summary>
public sealed class DystopiaRodScale : Control
{
    public readonly record struct Column(string Label, string Sub, float Insertion, float Target, bool Scrammed, bool Empty);

    private static readonly Color Rod = Color.FromHex("#c84a3c");
    private static readonly Color RodScram = Color.FromHex("#ff3020");
    private static readonly Color Channel = Color.FromHex("#18283c");

    private List<Column> _columns = new();

    public DystopiaRodScale()
    {
        MinSize = new Vector2(240, 200);
        HorizontalExpand = true;
    }

    public void SetColumns(List<Column> columns)
    {
        _columns = columns;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (_columns.Count == 0)
            return;

        var s = UIScale;
        var mono = CityUi.Mono(10);
        var bold = CityUi.Bold(11);
        var left = 34 * s;
        var top = 6 * s;
        var bottom = PixelSize.Y - 34 * s;
        var height = bottom - top;
        var colW = (PixelSize.X - left) / _columns.Count;

        // шкала погружения: 0% — стержень вынут (сверху пусто), 100% — опущен до дна
        for (var i = 0; i <= 4; i++)
        {
            var y = top + height * i / 4f;
            handle.DrawLine(new Vector2(left - 4 * s, y), new Vector2(PixelSize.X, y), CityUi.Line.WithAlpha(0.6f));
            handle.DrawString(mono, new Vector2(0, y - 7 * s), $"{i * 25}", s, CityUi.Muted);
        }

        for (var c = 0; c < _columns.Count; c++)
        {
            var col = _columns[c];
            var cx = left + colW * c + colW / 2f;
            var half = MathF.Min(12 * s, colW / 2f - 6 * s);
            var track = new UIBox2(cx - half, top, cx + half, bottom);
            handle.DrawRect(track, Channel);
            if (!col.Empty)
            {
                var depth = height * Math.Clamp(col.Insertion, 0f, 1f);
                handle.DrawRect(new UIBox2(track.Left + 2 * s, top, track.Right - 2 * s, top + depth),
                    col.Scrammed ? RodScram : Rod);
                var ty = top + height * Math.Clamp(col.Target, 0f, 1f);
                handle.DrawRect(new UIBox2(track.Left - 4 * s, ty - s, track.Right + 4 * s, ty + s), CityUi.Glow);
            }

            handle.DrawRect(track, c == _columns.Count - 1 ? CityUi.Accent : CityUi.Frame, false);
            DrawCentered(handle, bold, col.Label, cx, bottom + 3 * s, col.Empty ? CityUi.Muted : CityUi.Accent);
            DrawCentered(handle, mono, col.Sub, cx, bottom + 18 * s, CityUi.Dim);
        }
    }

    private void DrawCentered(DrawingHandleScreen handle, Font font, string text, float cx, float y, Color color)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
            width += font.GetCharMetrics(rune, UIScale)?.Advance ?? 0;

        handle.DrawString(font, new Vector2(cx - width / 2f, y), text, UIScale, color);
    }
}
