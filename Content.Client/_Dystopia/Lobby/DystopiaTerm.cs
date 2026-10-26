// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — рисование текста люминофором: свечение цветом самих букв (у повреждённого терминала —
// разъехавшиеся каналы), разрядка между буквами, замер ширины.

using System.Numerics;
using System.Text;
using Robust.Client.Graphics;

namespace Content.Client._Dystopia.Lobby;

public static class DystopiaTerm
{
    private static readonly Color AberrationCyan = Color.FromHex("#00E6FF");

    /// <summary>Ширина строки в пикселях экрана.</summary>
    public static float Measure(Font font, string text, float scale, float spacing = 0f)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            width += (font.GetCharMetrics(rune, scale)?.Advance ?? 0) + spacing * scale;
        }

        return width;
    }

    public static float LineHeight(Font font, float scale)
    {
        return font.GetLineHeight(scale);
    }

    /// <summary>Строка без эффектов. Возвращает ширину.</summary>
    public static float Plain(DrawingHandleScreen handle, Font font, Vector2 pos, string text, float scale, Color color,
        float spacing = 0f)
    {
        // буквы — по целым пикселям: на дробных координатах глиф размывается и выглядит грязным
        var baseline = MathF.Round(pos.Y + font.GetAscent(scale));
        var x = pos.X;
        foreach (var rune in text.EnumerateRunes())
        {
            var advance = font.DrawChar(handle, rune, new Vector2(MathF.Round(x), baseline), scale, color);
            x += advance + spacing * scale;
        }

        return x - pos.X;
    }

    /// <summary>Строка люминофором: сначала свечение, потом сами буквы. Свечение — цветом самого текста (красная
    /// надпись на зелёном терминале светится красным, а не грязно-жёлтым); широкий ореол — только у крупного кегля,
    /// у мелкого чёткие копии со сдвигом читаются как грязь вокруг букв.</summary>
    public static float Glowing(DrawingHandleScreen handle, DystopiaLobbyThemeController theme, Font font, Vector2 pos,
        string text, float scale, Color color, float spacing = 0f, bool glow = true)
    {
        if (glow && color.A > 0.05f)
        {
            var strength = theme.Glow.A * color.A;
            if (theme.Corrupted)
            {
                // разъехавшиеся каналы: слабая голубая копия слева, копия цвета текста справа
                var shift = MathF.Max(1f, MathF.Round(1.5f * scale));
                Plain(handle, font, pos - new Vector2(shift, 0f), text, scale, AberrationCyan.WithAlpha(0.3f * strength), spacing);
                Plain(handle, font, pos + new Vector2(shift, 0f), text, scale, color.WithAlpha(0.5f * strength), spacing);
            }
            else
            {
                if (LineHeight(font, scale) >= 26f * scale)
                {
                    var wide = color.WithAlpha(0.1f * strength);
                    var far = MathF.Round(2f * MathF.Max(1f, scale));
                    Plain(handle, font, pos + new Vector2(-far, -far), text, scale, wide, spacing);
                    Plain(handle, font, pos + new Vector2(far, -far), text, scale, wide, spacing);
                    Plain(handle, font, pos + new Vector2(-far, far), text, scale, wide, spacing);
                    Plain(handle, font, pos + new Vector2(far, far), text, scale, wide, spacing);
                }

                var halo = color.WithAlpha(0.2f * strength);
                var d = MathF.Max(1f, MathF.Round(scale));
                Plain(handle, font, pos + new Vector2(-d, 0f), text, scale, halo, spacing);
                Plain(handle, font, pos + new Vector2(d, 0f), text, scale, halo, spacing);
                Plain(handle, font, pos + new Vector2(0f, -d), text, scale, halo, spacing);
                Plain(handle, font, pos + new Vector2(0f, d), text, scale, halo, spacing);
            }
        }

        return Plain(handle, font, pos, text, scale, color, spacing);
    }

    /// <summary>Обрезает строку многоточием под ширину.</summary>
    public static string Fit(Font font, string text, float scale, float maxWidth, float spacing = 0f)
    {
        if (Measure(font, text, scale, spacing) <= maxWidth)
            return text;

        var sb = new StringBuilder();
        var width = Measure(font, "…", scale, spacing);
        foreach (var rune in text.EnumerateRunes())
        {
            var w = (font.GetCharMetrics(rune, scale)?.Advance ?? 0) + spacing * scale;
            if (width + w > maxWidth)
                break;

            width += w;
            sb.Append(rune.ToString());
        }

        return sb.Append('…').ToString();
    }

    /// <summary>Залитый прямоугольник; пустой или вывернутый (до раскладки размер ещё 0) пропускается —
    /// иначе UIBox2 падает на проверке «левый край правее правого».</summary>
    public static void Rect(DrawingHandleScreen handle, float left, float top, float right, float bottom, Color color)
    {
        if (right <= left || bottom <= top)
            return;

        handle.DrawRect(new UIBox2(left, top, right, bottom), color);
    }

    public static void Rect(DrawingHandleScreen handle, Vector2 topLeft, Vector2 bottomRight, Color color)
    {
        Rect(handle, topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y, color);
    }

    /// <summary>Прямоугольник в пикселях экрана по координатам в точках интерфейса.</summary>
    public static UIBox2 Box(float x, float y, float w, float h, float scale)
    {
        return UIBox2.FromDimensions(x * scale, y * scale, w * scale, h * scale);
    }
}
