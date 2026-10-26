// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — силуэт Города (стена с воротами, заводы с трубами, кварталы, башня Консулата, мачта
// стабилизатора) пиксельными прямоугольниками: дым из труб, окна гаснут и зажигаются, сигнальные огни, кольца поля
// стабилизатора. Под силуэтом — заголовок темы пиксельным шрифтом (с редким сбоем-двоением; кегль уменьшается, если
// не влезает) и подзаголовок, справа — лозунги (если включены). Размер клетки силуэта задаётся — в узкой колонке он
// мельче. У повреждённого терминала трубы оплетены пульсирующей плотью, дым красный и гуще, а заголовок по кругу
// рассыпается в другой и обратно.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Lobby;

public sealed class DystopiaLobbyHero : Control
{
    private const float SkyTop = 8f;        // силуэт начинается с y = −8 клеток
    private const float SkyHeight = 30f;    // от −8 до низа стены (22) клеток
    private const float SkyWidth = 140f;
    private const int MinCaptionSize = 10;
    private const int SubSize = 13;
    private const int SloganSize = 14;

    private static readonly Color GhostRed = Color.FromHex("#FF3B30");
    private static readonly Color GhostCyan = Color.FromHex("#44E8FF");
    private static readonly Color Flesh = Color.FromHex("#3A0505");

    // x, y, w, h — в клетках силуэта
    private static readonly float[,] Buildings =
    {
        { 2, 12, 24, 6 }, { 6, 3, 2, 9 }, { 12, 5, 2, 7 }, { 30, 11, 6, 7 }, { 37, 8, 5, 10 }, { 43, 12, 7, 6 },
        { 51, 9, 4, 9 }, { 58, 5, 12, 13 }, { 61, 2, 6, 3 }, { 63.5f, -2, 1, 4 }, { 73, 10, 6, 8 }, { 80, 12, 5, 6 },
        { 89, 4, 2, 14 }, { 86, 3, 8, 1 }, { 89, 2, 2, 1 }, { 96, 11, 20, 7 }, { 110, 2, 3, 9 }, { 101, 6, 2, 5 },
        { 126, 12, 6, 6 }, { 133, 10, 5, 8 },
    };

    // x, y, вид (0 — горит, 1 — гаснет с 36% до 64% из 6 с, 2 — горит с 21% до 78% из 9 с), сдвиг фазы
    private static readonly float[,] Windows =
    {
        { 4, 14, 0, 0 }, { 7, 14, 2, .37f }, { 10, 14, 0, 0 }, { 13, 14, 1, 1.11f }, { 16, 14, 1, 1.48f }, { 19, 14, 1, 1.85f },
        { 22, 14, 2, 2.22f }, { 4, 16, 0, 0 }, { 7, 16, 2, 2.96f }, { 10, 16, 0, 0 }, { 13, 16, 1, .7f }, { 16, 16, 1, 1.07f },
        { 19, 16, 1, 1.44f }, { 22, 16, 2, 1.81f }, { 31, 13, 0, 0 }, { 33, 13, 2, 2.55f }, { 31, 15, 0, 0 }, { 33, 15, 1, .29f },
        { 38, 10, 1, .66f }, { 40, 10, 1, 1.03f }, { 38, 12, 2, 1.4f }, { 40, 12, 0, 0 }, { 38, 14, 2, 2.14f }, { 40, 14, 0, 0 },
        { 38, 16, 1, 2.88f }, { 40, 16, 1, .25f }, { 44, 14, 1, .62f }, { 46, 14, 2, .99f }, { 48, 14, 0, 0 }, { 44, 16, 2, 1.73f },
        { 46, 16, 0, 0 }, { 48, 16, 1, 2.47f }, { 52, 11, 1, 2.84f }, { 52, 13, 1, .21f }, { 52, 15, 2, .58f },
        { 60.5f, 7, 2, 2.35f }, { 63.5f, 7, 0, 0 }, { 66.5f, 7, 2, .09f }, { 60.5f, 9, 0, 0 }, { 63.5f, 9, 1, .83f },
        { 66.5f, 9, 1, 1.2f }, { 60.5f, 11, 1, 1.57f }, { 63.5f, 11, 2, 1.94f }, { 66.5f, 11, 0, 0 }, { 60.5f, 13, 2, 2.68f },
        { 63.5f, 13, 0, 0 }, { 66.5f, 13, 1, .42f }, { 60.5f, 15, 1, .79f }, { 63.5f, 15, 1, 1.16f }, { 66.5f, 15, 2, 1.53f },
        { 74, 12, 0, 0 }, { 76, 12, 2, 1.32f }, { 74, 14, 0, 0 }, { 76, 14, 1, 2.06f }, { 74, 16, 1, 2.43f }, { 76, 16, 1, 2.8f },
        { 81, 14, 2, .17f }, { 83, 14, 0, 0 }, { 81, 16, 2, .91f }, { 83, 16, 0, 0 }, { 98, 14, 0, 0 }, { 101, 14, 2, 2.27f },
        { 104, 14, 0, 0 }, { 107, 14, 1, .01f }, { 110, 14, 1, .38f }, { 113, 14, 1, .75f }, { 127, 14, 1, 1.65f },
        { 129, 14, 1, 2.02f }, { 127, 16, 1, 2.39f }, { 129, 16, 2, 2.76f }, { 134, 12, 0, 0 }, { 136, 12, 2, .5f },
        { 134, 14, 0, 0 }, { 136, 14, 1, 1.24f }, { 134, 16, 1, 1.61f }, { 136, 16, 1, 1.98f },
    };

    // x, y, w, сдвиг фазы
    private static readonly float[,] Beacons = { { 6, 2, 2, 0 }, { 12, 4, 2, .5f }, { 63.5f, -3, 1, .9f }, { 110.5f, 1, 2, 1.2f } };

    // устья труб: x, y, сдвиг фазы первого клуба
    private static readonly float[,] Chimneys = { { 7, 2, 0 }, { 13, 4, .7f }, { 111.5f, 1, .3f }, { 102, 5, 1f } };

    // наросты: x, y, r, сдвиг фазы
    private static readonly float[,] Blobs =
    {
        { 7, 11.5f, 1.8f, 0 }, { 7, 8, 1.1f, .4f }, { 7, 5.2f, .8f, .9f }, { 13, 10, 1.3f, 1.3f }, { 13, 7, .8f, .2f },
        { 20, 18, 2.2f, 1.7f }, { 45, 18, 1.8f, .6f }, { 58.5f, 16.5f, 1.6f, 1.1f }, { 69.5f, 17, 1.4f, 1.9f },
        { 90, 12, 1.1f, .3f }, { 90, 8, .8f, 1.5f }, { 111.5f, 9, 1.5f, .8f }, { 111.5f, 5, 1, 2 }, { 102, 10, 1, 1.2f },
        { 98, 18, 1.6f, .5f }, { 128, 18, 2, 1.6f },
    };

    // щупальца: кубические кривые x0 y0 c1x c1y c2x c2y x1 y1
    private static readonly float[,] Tendrils =
    {
        { 7, 12, 5, 9, 9, 7, 7, 4 }, { 13, 12, 15, 9, 11, 8, 13, 6 }, { 111.5f, 11, 109, 8, 114, 6, 111.5f, 3 },
        { 90, 18, 88, 13, 92, 11, 90, 6 }, { 58, 18, 55, 14, 61, 12, 58.5f, 8 }, { 18, 21, 24, 16, 28, 20, 31, 14 },
        { 124, 21, 126, 17, 130, 19, 131, 13 },
    };

    private readonly Vector2[] _roof = new Vector2[18];
    private DystopiaLobbyThemeController? _theme;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    private float _unit = 4f;
    private int _captionSize = 24;

    /// <summary>Точек интерфейса на клетку силуэта.</summary>
    public float Unit
    {
        get => _unit;
        set
        {
            _unit = value;
            UpdateMinSize();
        }
    }

    /// <summary>Кегль заголовка (наибольший; уменьшается, если строка не влезает в ширину).</summary>
    public int CaptionSize
    {
        get => _captionSize;
        set
        {
            _captionSize = value;
            UpdateMinSize();
        }
    }

    /// <summary>Лозунги справа от силуэта.</summary>
    public bool Slogans { get; set; } = true;

    public DystopiaLobbyHero()
    {
        RectClipContent = true;
        UpdateMinSize();
    }

    private float CaptionTop => SkyHeight * _unit + 8f;

    private void UpdateMinSize()
    {
        // заголовок пиксельным шрифтом (строка выше кегля) и подзаголовок под ним
        MinSize = new Vector2(SkyWidth * _unit, CaptionTop + _captionSize * 1.5f + 24f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        var s = UIScale;
        var u = Unit * s;
        var time = theme.Time;

        Vector2 P(float x, float y) => new(x * u, (y + SkyTop) * u);
        UIBox2 R(float x, float y, float w, float h) => UIBox2.FromDimensions(P(x, y), new Vector2(w * u, h * u));

        // постройки
        var building = theme.Dim.WithAlpha(0.8f);

        // мягкий ореол вокруг силуэта и стены — свечение люминофора
        var halo = theme.Glow.WithAlpha(theme.Glow.A * 0.18f);
        var g = 2f * s;
        for (var i = 0; i < Buildings.GetLength(0); i++)
        {
            var box = R(Buildings[i, 0], Buildings[i, 1], Buildings[i, 2], Buildings[i, 3]);
            DystopiaTerm.Rect(handle, box.Left - g, box.Top - g, box.Right + g, box.Bottom, halo);
        }

        var wallBox = R(0f, 17f, 140f, 5f);
        DystopiaTerm.Rect(handle, wallBox.Left, wallBox.Top - g, wallBox.Right, wallBox.Bottom, halo);

        for (var i = 0; i < Buildings.GetLength(0); i++)
        {
            handle.DrawRect(R(Buildings[i, 0], Buildings[i, 1], Buildings[i, 2], Buildings[i, 3]), building);
        }

        // пилообразная крыша первого завода
        var tri = _roof;   // без stackalloc: песочница клиента не пропускает непроверяемый IL
        for (var k = 0; k < 6; k++)
        {
            var x0 = 2f + 4f * k;
            tri[k * 3] = P(x0, 12f);
            tri[k * 3 + 1] = P(x0 + 4f, 9f);
            tri[k * 3 + 2] = P(x0 + 4f, 12f);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, tri, building);

        // стена с зубцами и воротами
        var wall = theme.Fg.WithAlpha(0.85f);
        handle.DrawRect(R(0f, 18f, 140f, 4f), wall);
        for (var x = 0f; x < 138f; x += 5f)
        {
            handle.DrawRect(R(x, 17f, 3f, 1f), wall);
        }

        handle.DrawRect(R(118f, 19f, 6f, 3f), theme.Bg);

        // окна
        for (var i = 0; i < Windows.GetLength(0); i++)
        {
            var kind = (int) Windows[i, 2];
            var lit = kind switch
            {
                1 => !InWindow(time + Windows[i, 3], 6f, 0.36f, 0.64f),
                2 => InWindow(time + Windows[i, 3], 9f, 0.21f, 0.78f),
                _ => true,
            };

            var box = R(Windows[i, 0], Windows[i, 1], 1f, 1f);
            if (lit)
            {
                DystopiaTerm.Rect(handle, box.TopLeft - new Vector2(s, s), box.BottomRight + new Vector2(s, s), theme.Glow.WithAlpha(theme.Glow.A * 0.35f));
                handle.DrawRect(box, theme.Fg);
            }
            else
            {
                handle.DrawRect(box, theme.Fg.WithAlpha(0.12f));
            }
        }

        // сигнальные огни
        for (var i = 0; i < Beacons.GetLength(0); i++)
        {
            var on = (time + Beacons[i, 3]) % 1.6f / 1.6f >= 0.56f;
            handle.DrawRect(R(Beacons[i, 0], Beacons[i, 1], Beacons[i, 2], 1f), theme.Alert.WithAlpha(on ? 1f : 0.15f));
        }

        // поле стабилизатора: расходящиеся кольца
        for (var k = 0; k < 2; k++)
        {
            var p = (time + k * 1.3f) % 2.6f / 2.6f;
            var eased = 1f - (1f - p) * (1f - p);
            var radius = 1.4f * (1f + 6f * eased) * u;
            handle.DrawCircle(P(90f, 2.5f), radius, theme.Fg.WithAlpha(0.9f * (1f - eased)), false);
        }

        // дым
        var smoke = theme.Theme.Smoke;
        var puff = theme.Corrupted ? 1.6f : 1.1f;
        for (var i = 0; i < Chimneys.GetLength(0); i++)
        {
            for (var k = 0; k < 3; k++)
            {
                var p = (time + Chimneys[i, 2] + k * 1.4f) % 4.2f / 4.2f;
                var alpha = p < 0.12f ? p / 0.12f * 0.6f : 0.6f * (1f - (p - 0.12f) / 0.88f);
                var pos = P(Chimneys[i, 0] + 7f * p, Chimneys[i, 1] - 12f * p);
                handle.DrawCircle(pos, puff * (0.6f + 2f * p) * u, smoke.WithAlpha(alpha * 0.55f));
            }
        }

        if (theme.Corrupted)
            DrawFlesh(handle, theme, u, time);

        DrawCaption(handle, theme, s, time);
    }

    private void DrawFlesh(DrawingHandleScreen handle, DystopiaLobbyThemeController theme, float u, float time)
    {
        Vector2 P(float x, float y) => new(x * u, (y + SkyTop) * u);

        var tendril = theme.Fg.WithAlpha(0.75f);
        for (var i = 0; i < Tendrils.GetLength(0); i++)
        {
            var prev = P(Tendrils[i, 0], Tendrils[i, 1]);
            for (var k = 1; k <= 10; k++)
            {
                var t = k / 10f;
                var a = 1f - t;
                var x = a * a * a * Tendrils[i, 0] + 3 * a * a * t * Tendrils[i, 2] + 3 * a * t * t * Tendrils[i, 4] + t * t * t * Tendrils[i, 6];
                var y = a * a * a * Tendrils[i, 1] + 3 * a * a * t * Tendrils[i, 3] + 3 * a * t * t * Tendrils[i, 5] + t * t * t * Tendrils[i, 7];
                var next = P(x, y);
                handle.DrawLine(prev, next, tendril);
                handle.DrawLine(prev + new Vector2(1f, 0f), next + new Vector2(1f, 0f), tendril);
                prev = next;
            }
        }

        for (var i = 0; i < Blobs.GetLength(0); i++)
        {
            var pulse = 1f + 0.09f * (1f - MathF.Cos((time + Blobs[i, 3]) / 2.2f * MathF.Tau));
            var r = Blobs[i, 2] * pulse * u;
            var c = P(Blobs[i, 0], Blobs[i, 1]);
            handle.DrawCircle(c, r, Flesh);
            handle.DrawCircle(c, r, theme.Fg, false);
        }
    }

    private void DrawCaption(DrawingHandleScreen handle, DystopiaLobbyThemeController theme, float s, float time)
    {
        var caption = theme.Text("title");
        if (theme.Corrupted)
        {
            // заголовок по кругу: свой (5 шагов) → сбой → другой (5 шагов) → сбой → свой
            var phase = theme.Tick % 12;
            var alt = theme.Text("title-alt");
            caption = phase switch
            {
                < 5 => theme.Corrupt(caption),
                5 => theme.Corrupt(alt, 3),
                11 => theme.Corrupt(caption, 3),
                _ => theme.Corrupt(alt),
            };
        }

        var spacing = 3f;
        var size = _captionSize;
        var font = theme.Font(true, size);
        while (size > MinCaptionSize && DystopiaTerm.Measure(font, caption, s, spacing) > PixelSize.X)
        {
            size -= 2;
            font = theme.Font(true, size);
        }

        var pos = new Vector2(0f, MathF.Round(CaptionTop * s));

        // двоение: красная и голубая копии ненадолго разъезжаются
        var periodA = theme.Corrupted ? 2.3f : 4.2f;
        var periodB = theme.Corrupted ? 3.1f : 5.7f;
        var a = time % periodA / periodA;
        var b = time % periodB / periodB;
        if (a is >= 0.91f and < 0.97f)
        {
            var dx = a < 0.93f ? -7f : a < 0.95f ? 6f : -3f;
            DystopiaTerm.Plain(handle, font, pos + new Vector2(dx * s, 0f), caption, s, GhostRed.WithAlpha(0.7f), spacing);
        }

        if (b is >= 0.87f and < 0.91f)
        {
            var dx = b < 0.89f ? 8f : -5f;
            DystopiaTerm.Plain(handle, font, pos + new Vector2(dx * s, 0f), caption, s, GhostCyan.WithAlpha(0.6f), spacing);
        }

        DystopiaTerm.Glowing(handle, theme, font, pos, caption, s, theme.Fg, spacing);

        var sub = theme.Corrupt(theme.Text("subtitle"));
        // подзаголовок — под заголовком, по высоте его строки (у пиксельного шрифта она больше кегля)
        var subY = pos.Y + DystopiaTerm.LineHeight(font, s) + 4f * s;
        DystopiaTerm.Glowing(handle, theme, theme.Font(false, SubSize), new Vector2(2f * s, subY), sub, s, theme.Dim, 4f);

        if (!Slogans)
            return;

        // лозунги справа от силуэта
        var slogan = theme.Font(false, SloganSize);
        for (var i = 1; i <= 4; i++)
        {
            var text = theme.Corrupt(theme.Text($"slogan-{i}"));
            var color = i == 4 ? theme.Fg : theme.Dim;
            DystopiaTerm.Glowing(handle, theme, slogan, new Vector2((SkyWidth * _unit + 14f) * s, (14f + (i - 1) * 24f) * s), text, s, color, 2f);
        }
    }

    private static bool InWindow(float time, float period, float from, float to)
    {
        var p = time % period / period;
        return p >= from && p < to;
    }
}
