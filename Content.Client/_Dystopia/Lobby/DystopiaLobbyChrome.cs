// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — обвязка экрана: фон кинескопа, пунктир под шапкой, бегущая строка сводки, кнопки ссылок
// сервера (правила, вики, дискорд, руководство, изменения) и слой эффектов поверх всего (развёртка, шум, бегущая
// полоса, затемнение к краям, блик, мерцание, включение кинескопа; у повреждённого терминала — разрывы, статика,
// подёргивание картинки).

using System.Numerics;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.Info;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Lobby;

/// <summary>Корень терминала: тёмный экран с пятном люминофора в центре. Подёргивает содержимое у повреждённого.</summary>
public sealed class DystopiaTerminalScreen : Control
{
    private DystopiaLobbyThemeController? _theme;
    private bool _lagging;

    /// <summary>Содержимое, которое подёргивается у повреждённого терминала.</summary>
    public Control? Content { get; set; }

    /// <summary>Поля содержимого в обычном состоянии.</summary>
    public Thickness ContentMargin { get; set; } = new(44f, 30f, 44f, 0f);

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Content == null)
            return;

        // подтормаживание: раз в 7 с картинку на миг сдёргивает
        var offset = Vector2.Zero;
        if (Term.Corrupted)
        {
            var p = Term.Time % 7f / 7f;
            offset = p switch
            {
                >= 0.47f and < 0.48f => new Vector2(4f, -2f),
                >= 0.48f and < 0.49f => new Vector2(-3f, 1f),
                >= 0.82f and < 0.83f => new Vector2(-7f, 0f),
                >= 0.83f and < 0.84f => new Vector2(2f, 0f),
                _ => Vector2.Zero,
            };
        }

        var lagging = offset != Vector2.Zero;
        if (!lagging && !_lagging)
            return;

        _lagging = lagging;
        var m = ContentMargin;
        Content.Margin = new Thickness(m.Left + offset.X, m.Top + offset.Y, m.Right - offset.X, m.Bottom);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var size = PixelSize;
        DystopiaTerm.Rect(handle, 0f, 0f, size.X, size.Y, Color.FromHex("#020302"));
        var glow = Term.Texture("glow");
        handle.DrawTextureRect(glow, new UIBox2(-size.X * 0.1f, -size.Y * 0.15f, size.X * 1.1f, size.Y * 1.05f), Term.Bg);
    }
}

/// <summary>Пунктирная линия цветом рамок.</summary>
public sealed class DystopiaDashedLine : Control
{
    private DystopiaLobbyThemeController? _theme;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaDashedLine()
    {
        MinHeight = 3f;
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var t = MathF.Max(1f, MathF.Round(s));
        var y = PixelSize.Y / 2f;
        for (var x = 0f; x < PixelSize.X; x += 8f * s)
        {
            DystopiaTerm.Rect(handle, x, y, x + 4f * s, y + t, Term.Dim);
        }
    }
}

/// <summary>Бегущая строка сводки внизу экрана.</summary>
public sealed class DystopiaLobbyTicker : Control
{
    private const int TextSize = 15;
    private const float Speed = 60f;
    private DystopiaLobbyThemeController? _theme;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaLobbyTicker()
    {
        MinHeight = 40f;
        HorizontalExpand = true;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        var s = UIScale;
        var size = PixelSize;
        var t = MathF.Max(1f, MathF.Round(s));
        DystopiaTerm.Rect(handle, 0f, 0f, size.X, size.Y, theme.Fg.WithAlpha(0.08f));
        DystopiaTerm.Rect(handle, 0f, 0f, size.X, t, theme.Dim);

        var font = theme.Font(false, TextSize);
        var text = theme.Corrupt(theme.Text("ticker")) + "      ";
        var width = DystopiaTerm.Measure(font, text, s, 2f);
        if (width <= 0f)
            return;

        var y = (size.Y - DystopiaTerm.LineHeight(font, s)) / 2f;
        var x = -(theme.Time * Speed * s % width);
        for (; x < size.X; x += width)
        {
            DystopiaTerm.Glowing(handle, theme, font, new Vector2(x, y), text, s, theme.Fg, 2f);
        }
    }
}

/// <summary>Кнопки ссылок сервера — сеткой в два столбца (колонка лобби узкая).</summary>
public sealed class DystopiaLobbyLinks : GridContainer
{
    private readonly IConfigurationManager _cfg;
    private readonly List<(CVarDef<string> CVar, DystopiaTerminalButton Button)> _links = new();

    public DystopiaLobbyLinks()
    {
        Columns = 2;
        HSeparationOverride = 8;
        VSeparationOverride = 8;
        _cfg = IoCManager.Resolve<IConfigurationManager>();
        var uri = IoCManager.Resolve<IUriOpener>();

        var rules = new DystopiaTerminalButton { TextKey = "link-rules", HorizontalExpand = true };
        rules.OnPressed += _ => new RulesAndInfoWindow().Open();
        AddChild(rules);

        AddLink("link-discord", CCVars.InfoLinksDiscord, uri);
        AddLink("link-wiki", CCVars.InfoLinksWiki, uri);

        var guide = new DystopiaTerminalButton { TextKey = "link-guide", HorizontalExpand = true };
        guide.OnPressed += _ => UserInterfaceManager.GetUIController<GuidebookUIController>().ToggleGuidebook();
        AddChild(guide);

        var changes = new DystopiaTerminalButton { TextKey = "link-changelog", HorizontalExpand = true };
        changes.OnPressed += _ => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
        AddChild(changes);
    }

    private void AddLink(string key, CVarDef<string> cvar, IUriOpener uri)
    {
        var button = new DystopiaTerminalButton { TextKey = key, HorizontalExpand = true };
        button.OnPressed += _ => uri.OpenUri(_cfg.GetCVar(cvar));
        AddChild(button);
        _links.Add((cvar, button));
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        foreach (var (cvar, button) in _links)
        {
            button.Visible = _cfg.GetCVar(cvar) != string.Empty;
        }
    }
}

/// <summary>Эффекты кинескопа поверх всего терминала. Мышь пропускает.</summary>
public sealed class DystopiaCrtOverlay : Control
{
    private const float PowerOn = 1.4f;
    private DystopiaLobbyThemeController? _theme;
    private readonly IRobustRandom _random;
    private float _entered = -1f;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaCrtOverlay()
    {
        MouseFilter = MouseFilterMode.Ignore;
        _random = IoCManager.Resolve<IRobustRandom>();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _entered = -1f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        if (_entered < 0f)
            _entered = theme.Time;

        var s = UIScale;
        var size = PixelSize;
        var time = theme.Time;
        var full = new UIBox2(0f, 0f, size.X, size.Y);

        // мерцание всего экрана
        var flicker = (time % 5f / 5f) switch
        {
            >= 0.39f and < 0.40f => 0.14f,
            >= 0.71f and < 0.72f => 0.05f,
            >= 0.88f and < 0.89f => 0.03f,
            _ => 0f,
        };

        if (theme.Corrupted && time % 2.4f / 2.4f is >= 0.39f and < 0.41f)
            flicker = 0.2f;

        if (flicker > 0f)
            handle.DrawRect(full, Color.Black.WithAlpha(flicker));

        // шум: плитка случайно сдвигается каждый кадр
        var noise = theme.Texture("noise");
        var tile = 256f * s;
        var ox = -_random.NextFloat() * tile;
        var oy = -_random.NextFloat() * tile;
        // зелёные (по теме) искры, а не серая пелена: белый шум поверх чёрного экрана его выцвечивает
        var noiseColor = theme.Fg.WithAlpha(0.022f);
        for (var x = ox; x < size.X; x += tile)
        {
            for (var y = oy; y < size.Y; y += tile)
            {
                handle.DrawTextureRect(noise, UIBox2.FromDimensions(x, y, tile, tile), noiseColor);
            }
        }

        // строки развёртки
        var line = MathF.Max(1f, MathF.Round(s));
        var scan = Color.Black.WithAlpha(0.2f);
        for (var y = 0f; y < size.Y; y += 3f * line)
        {
            DystopiaTerm.Rect(handle, 0f, y, size.X, y + line, scan);
        }

        // бегущая светлая полоса
        var roll = theme.Texture("rollbar");
        var rollY = (time % 7f / 7f) * (size.Y + 300f * s) - 160f * s;
        handle.DrawTextureRect(roll, new UIBox2(0f, rollY, size.X, rollY + 140f * s), theme.Fg.WithAlpha(0.03f));

        if (theme.Corrupted)
            DrawDamage(handle, theme, size, s, time, noise);

        handle.DrawTextureRect(theme.Texture("vignette"), full);
        DrawBezel(handle, theme, size, s);

        DrawPowerOn(handle, size, time - _entered, theme);
    }

    /// <summary>Корпус кинескопа: тёмная кромка с ободком и скруглённые углы стекла.</summary>
    private static void DrawBezel(DrawingHandleScreen handle, DystopiaLobbyThemeController theme, Vector2i size, float s)
    {
        var body = Color.FromHex("#050605");
        var rim = Color.FromHex("#0D0F0D");
        var b = MathF.Round(10f * s);
        var r = MathF.Round(2f * s);
        var w = size.X;
        var h = size.Y;

        DystopiaTerm.Rect(handle, 0f, 0f, w, b, body);
        DystopiaTerm.Rect(handle, 0f, h - b, w, h, body);
        DystopiaTerm.Rect(handle, 0f, b, b, h - b, body);
        DystopiaTerm.Rect(handle, w - b, b, w, h - b, body);

        var i = b + r;
        DystopiaTerm.Rect(handle, b, b, w - b, i, rim);
        DystopiaTerm.Rect(handle, b, h - i, w - b, h - b, rim);
        DystopiaTerm.Rect(handle, b, i, i, h - i, rim);
        DystopiaTerm.Rect(handle, w - i, i, w - b, h - i, rim);

        // скругление: снаружи дуги — цвет ободка
        var c = MathF.Round(28f * s);
        if (w <= 2f * (i + c) || h <= 2f * (i + c))
            return;

        handle.DrawTextureRect(theme.Texture("corner_tl"), UIBox2.FromDimensions(i, i, c, c), rim);
        handle.DrawTextureRect(theme.Texture("corner_tr"), UIBox2.FromDimensions(w - i - c, i, c, c), rim);
        handle.DrawTextureRect(theme.Texture("corner_bl"), UIBox2.FromDimensions(i, h - i - c, c, c), rim);
        handle.DrawTextureRect(theme.Texture("corner_br"), UIBox2.FromDimensions(w - i - c, h - i - c, c, c), rim);
    }

    private void DrawDamage(DrawingHandleScreen handle, DystopiaLobbyThemeController theme, Vector2i size, float s, float time, Texture noise)
    {
        // разрывы: полосы на миг вспыхивают и сдвигаются
        var a = time % 5.3f / 5.3f;
        var b = time % 8.1f / 8.1f;
        var band = theme.Fg.WithAlpha(0.3f);
        if (a is >= 0.22f and < 0.24f)
            Tear(handle, (a < 0.23f ? 190f : 205f) * s, 16f * s, (a < 0.23f ? -24f : 18f) * s, size, band);
        else if (a is >= 0.61f and < 0.62f)
            Tear(handle, 610f * s, 16f * s, 30f * s, size, band);

        if (b is >= 0.37f and < 0.39f)
            Tear(handle, (b < 0.38f ? 420f : 432f) * s, 34f * s, (b < 0.38f ? 16f : -12f) * s, size, band.WithAlpha(0.25f));
        else if (b is >= 0.74f and < 0.75f)
            Tear(handle, 96f * s, 34f * s, -20f * s, size, band.WithAlpha(0.2f));

        // мёртвая строка
        var dead = 611f * s;
        DystopiaTerm.Rect(handle, 0f, dead, size.X, dead + MathF.Max(1f, s), theme.Fg.WithAlpha(0.4f));

        // всплеск статики
        var c = time % 9f / 9f;
        var burst = c switch
        {
            >= 0.64f and < 0.65f => 0.38f,
            >= 0.65f and < 0.66f => 0.12f,
            >= 0.66f and < 0.67f => 0.3f,
            _ => 0f,
        };

        if (burst <= 0f)
            return;

        var tile = 128f * s;
        var color = Color.FromHex("#FF4030").WithAlpha(burst);
        for (var x = -_random.NextFloat() * tile; x < size.X; x += tile)
        {
            for (var y = -_random.NextFloat() * tile; y < size.Y; y += tile)
            {
                handle.DrawTextureRect(noise, UIBox2.FromDimensions(x, y, tile, tile), color);
            }
        }
    }

    private static void Tear(DrawingHandleScreen handle, float y, float height, float shift, Vector2i size, Color color)
    {
        DystopiaTerm.Rect(handle, shift, y, size.X * 0.3f + shift, y + height, color);
        DystopiaTerm.Rect(handle, size.X * 0.45f + shift, y + height * 0.3f, size.X * 0.8f + shift, y + height * 0.7f, color);
    }

    private static void DrawPowerOn(DrawingHandleScreen handle, Vector2i size, float t, DystopiaLobbyThemeController theme)
    {
        if (t >= PowerOn)
            return;

        // экран раскрывается из яркой полосы по центру
        var p = t / PowerOn;
        float open;
        if (p < 0.3f)
            open = 0.004f;
        else if (p < 0.55f)
            open = 0.004f + (p - 0.3f) / 0.25f * 0.996f;
        else
            open = 1f;

        var half = size.Y * open / 2f;
        var mid = size.Y / 2f;
        DystopiaTerm.Rect(handle, 0f, 0f, size.X, mid - half, Color.Black);
        DystopiaTerm.Rect(handle, 0f, mid + half, size.X, size.Y, Color.Black);

        var flash = p < 0.55f ? 0.8f : 0.8f * (1f - (p - 0.55f) / 0.45f);
        DystopiaTerm.Rect(handle, 0f, mid - half, size.X, mid + half, Color.White.WithAlpha(flash * 0.35f));
        if (p < 0.55f)
            DystopiaTerm.Rect(handle, 0f, mid - 2f, size.X, mid + 2f, theme.Fg);
    }
}
