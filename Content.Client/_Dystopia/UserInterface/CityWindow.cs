using System.Numerics;
using Content.Client._Dystopia.Lobby;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Analyzers;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.UserInterface;

/// <summary>
/// Окно городского оборудования — терминал Городской сети: своя рамка, шапка с гербом, названием пиксельным
/// шрифтом (с редким двоением) и строкой терминала с мигающим курсором, индикатор сети, своя кнопка закрытия,
/// пунктирные разделители, подвал с лозунгом. Поверх — слой кинескопа: развёртка, бегущая полоса, редкое мерцание,
/// уголки и «включение» при открытии. Ванильные надписи и поля внутри — стилем Города (<see cref="CityUi.Sheet"/>).
/// Содержимое окна добавляется в <see cref="Contents"/>.
/// </summary>
[Virtual]
public class CityWindow : BaseWindow
{
    private const float HeaderHeight = 66;
    private const float DragMargin = 6;

    private readonly CityCaption _title;
    private readonly CityPrompt _subtitle;
    private readonly Label _slogan;
    private readonly Label _footer;

    /// <summary>Сюда добавляется содержимое окна.</summary>
    public BoxContainer Contents { get; }

    public CityWindow()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(360, 240);
        Stylesheet = CityUi.Sheet;

        var root = new PanelContainer
        {
            PanelOverride = CityUi.Box(CityUi.Bg, CityUi.Frame, 2),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        AddChild(root);

        var layout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(layout);

        // --- Шапка ---
        var header = new PanelContainer
        {
            PanelOverride = CityUi.Box(CityUi.Bg2, CityUi.Bg2, 0, 12, 8),
            MinHeight = HeaderHeight,
        };
        layout.AddChild(header);

        var headerRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            VerticalAlignment = VAlignment.Center,
        };
        header.AddChild(headerRow);

        headerRow.AddChild(new TextureRect
        {
            Texture = CityUi.Emblem(),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(48, 48),
            VerticalAlignment = VAlignment.Center,
        });

        var titles = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        _title = new CityCaption { HorizontalExpand = true };
        _subtitle = new CityPrompt { HorizontalExpand = true };
        titles.AddChild(_title);
        titles.AddChild(_subtitle);
        headerRow.AddChild(titles);

        headerRow.AddChild(new CityPulse(Loc.GetString("dystopia-city-ui-net")) { VerticalAlignment = VAlignment.Center });

        var close = CityUi.MakeButton("×");
        close.SetSize = new Vector2(28, 28);
        close.VerticalAlignment = VAlignment.Center;
        close.OnPressed += _ => Close();
        headerRow.AddChild(close);

        layout.AddChild(new CityDashedLine(CityUi.Frame));

        // --- Содержимое ---
        var body = new PanelContainer
        {
            PanelOverride = CityUi.Box(CityUi.Bg, CityUi.Bg, 0, 12, 10),
            VerticalExpand = true,
            HorizontalExpand = true,
            RectClipContent = true,
        };
        layout.AddChild(body);

        Contents = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        body.AddChild(Contents);

        // --- Подвал ---
        layout.AddChild(new CityDashedLine(CityUi.Line));
        var footer = new PanelContainer
        {
            PanelOverride = CityUi.Box(CityUi.Bg2, CityUi.Bg2, 0, 12, 4),
        };
        layout.AddChild(footer);
        var footerRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        footerRow.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CityUi.Accent },
            SetSize = new Vector2(6, 6),
            VerticalAlignment = VAlignment.Center,
        });
        _footer = CityUi.MakeLabel(Loc.GetString("dystopia-city-ui-footer").ToUpperInvariant(), CityUi.Muted, CityUi.Regular(10));
        _footer.HorizontalExpand = true;
        _footer.ClipText = true;
        _slogan = CityUi.MakeLabel(Loc.GetString("dystopia-city-ui-slogan"), CityUi.Accent, CityUi.Bold(10));
        footerRow.AddChild(_footer);
        footerRow.AddChild(_slogan);
        footer.AddChild(footerRow);

        // Слой кинескопа поверх всего окна (мышь пропускает).
        AddChild(new CityScanlines());
    }

    /// <summary>Название прибора (пишется капсом).</summary>
    public string WindowTitle
    {
        get => _title.Text;
        set => _title.Text = value.ToUpperInvariant();
    }

    /// <summary>Строка под названием: «ведомство · терминал · доступ» — печатается строкой терминала.</summary>
    public string Subtitle
    {
        get => _subtitle.Text;
        set => _subtitle.Text = value;
    }

    /// <summary>Лозунг в правом нижнем углу.</summary>
    public string Slogan
    {
        get => _slogan.Text ?? string.Empty;
        set => _slogan.Text = value.ToUpperInvariant();
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        var mode = DragMode.None;

        if (Resizable)
        {
            if (relativeMousePos.Y < DragMargin)
                mode = DragMode.Top;
            else if (relativeMousePos.Y > Size.Y - DragMargin)
                mode = DragMode.Bottom;

            if (relativeMousePos.X < DragMargin)
                mode |= DragMode.Left;
            else if (relativeMousePos.X > Size.X - DragMargin)
                mode |= DragMode.Right;
        }

        if (mode == DragMode.None && relativeMousePos.Y < HeaderHeight)
            mode = DragMode.Move;

        return mode;
    }
}

/// <summary>Время для анимаций окон Города (секунды реального времени).</summary>
internal static class CityClock
{
    public static float Now => (float) IoCManager.Resolve<IGameTiming>().RealTime.TotalSeconds;
}

/// <summary>Название окна пиксельным шрифтом со свечением; кегль уменьшается, пока строка не влезет; изредка
/// красная и голубая копии на миг разъезжаются.</summary>
public sealed class CityCaption : Control
{
    private const int MaxFont = 15;
    private const int MinFont = 8;
    private const float Spacing = 2f;

    private static readonly Color GhostRed = Color.FromHex("#FF3B30");
    private static readonly Color GhostCyan = Color.FromHex("#44E8FF");

    public string Text { get; set; } = string.Empty;

    public CityCaption()
    {
        MinHeight = 22;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Text.Length == 0 || PixelSize.X <= 0)
            return;

        var s = UIScale;
        var size = MaxFont;
        var font = CityUi.Title(size);
        while (size > MinFont && DystopiaTerm.Measure(font, Text, s, Spacing) > PixelSize.X)
        {
            size--;
            font = CityUi.Title(size);
        }

        var text = DystopiaTerm.Fit(font, Text, s, PixelSize.X, Spacing);
        var pos = new Vector2(0f, MathF.Round((PixelSize.Y - DystopiaTerm.LineHeight(font, s)) / 2f));

        var time = CityClock.Now;
        var a = time % 5.2f / 5.2f;
        var b = time % 7.1f / 7.1f;
        if (a is >= 0.93f and < 0.97f)
            DystopiaTerm.Plain(handle, font, pos + new Vector2((a < 0.95f ? -4f : 3f) * s, 0f), text, s, GhostRed.WithAlpha(0.6f), Spacing);
        if (b is >= 0.89f and < 0.91f)
            DystopiaTerm.Plain(handle, font, pos + new Vector2(4f * s, 0f), text, s, GhostCyan.WithAlpha(0.5f), Spacing);

        CityDraw.GlowText(handle, font, pos, text, s, CityUi.Glow, CityUi.Accent, Spacing);
    }
}

/// <summary>Строка терминала: «ГТС:\ГОРОД&gt; ведомство · терминал · доступ █» с мигающим курсором.</summary>
public sealed class CityPrompt : Control
{
    public string Text { get; set; } = string.Empty;

    public CityPrompt()
    {
        MinHeight = 16;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var font = CityUi.Regular(11);
        var width = PixelSize.X;
        if (width <= 0)
            return;

        var y = MathF.Round((PixelSize.Y - DystopiaTerm.LineHeight(font, s)) / 2f);
        var prompt = Loc.GetString("dystopia-city-ui-prompt");
        var x = DystopiaTerm.Plain(handle, font, new Vector2(0f, y), prompt, s, CityUi.Accent);
        var cursor = DystopiaTerm.Measure(font, "█", s);
        var room = width - x - cursor - 4f * s;
        if (room > 0f)
            x += DystopiaTerm.Plain(handle, font, new Vector2(x, y), DystopiaTerm.Fit(font, Text, s, room), s, CityUi.Dim);

        if (CityClock.Now % 1f < 0.5f)
            DystopiaTerm.Plain(handle, font, new Vector2(x + 2f * s, y), "█", s, CityUi.Accent);
    }
}

/// <summary>Пульсирующий индикатор «● СЕТЬ».</summary>
public sealed class CityPulse : Control
{
    private readonly string _text;

    public CityPulse(string text)
    {
        _text = text;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var font = CityUi.Regular(11);
        return new Vector2(DystopiaTerm.Measure(font, _text, 1f, 2f), DystopiaTerm.LineHeight(font, 1f));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var font = CityUi.Regular(11);
        var alpha = 0.725f + 0.275f * MathF.Cos(CityClock.Now * MathF.PI);
        var y = MathF.Round((PixelSize.Y - DystopiaTerm.LineHeight(font, s)) / 2f);
        DystopiaTerm.Plain(handle, font, new Vector2(0f, y), _text, s, CityUi.Accent.WithAlpha(alpha), 2f);
    }
}

/// <summary>Пунктирная линия на всю ширину.</summary>
public sealed class CityDashedLine : Control
{
    private readonly Color _color;

    public CityDashedLine(Color color)
    {
        _color = color;
        MinHeight = 1;
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var t = MathF.Max(1f, MathF.Round(s));
        for (var x = 0f; x < PixelSize.X; x += 8f * s)
        {
            DystopiaTerm.Rect(handle, x, 0f, MathF.Min(x + 4f * s, PixelSize.X), t, _color);
        }
    }
}

/// <summary>Заголовок раздела «─ ЗАГОЛОВОК ─────────» — как кромка рамки с врезанным названием.</summary>
public sealed class CitySectionHeader : Control
{
    private const float Lead = 10f;
    private const float Gap = 6f;
    private readonly string _text;

    public CitySectionHeader(string text)
    {
        _text = text;
        HorizontalExpand = true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var font = CityUi.Bold(12);
        return new Vector2(DystopiaTerm.Measure(font, _text, 1f, 1f) + Lead + Gap * 2f + 10f, DystopiaTerm.LineHeight(font, 1f));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var font = CityUi.Bold(12);
        var t = MathF.Max(1f, MathF.Round(s));
        var mid = MathF.Round(PixelSize.Y / 2f);
        DystopiaTerm.Rect(handle, 0f, mid, Lead * s, mid + t, CityUi.Frame);

        var x = (Lead + Gap) * s;
        var room = PixelSize.X - x - Gap * s;
        if (room <= 0f)
            return;

        var text = DystopiaTerm.Fit(font, _text, s, room, 1f);
        var y = MathF.Round((PixelSize.Y - DystopiaTerm.LineHeight(font, s)) / 2f);
        x += CityDraw.GlowText(handle, font, new Vector2(x, y), text, s, CityUi.Accent, CityUi.Accent, 1f) + Gap * s;
        DystopiaTerm.Rect(handle, x, mid, PixelSize.X, mid + t, CityUi.Line);
    }
}

/// <summary>Слой кинескопа поверх окна: развёртка, бегущая светлая полоса, редкое мерцание, уголки и «включение»
/// при открытии (картинка раскрывается из яркой полосы). Зерна и затемнения к краям нет.</summary>
public sealed class CityScanlines : Control
{
    private const float PowerOn = 0.5f;
    private static readonly Color LineColor = new(0f, 0f, 0f, 0.14f);

    private float _opened = -1f;

    public CityScanlines()
    {
        MouseFilter = MouseFilterMode.Ignore;
        // всё рисуется только внутри окна: полоса развёртки длиннее окна и без обрезки вылезает за его края
        RectClipContent = true;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _opened = -1f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var now = CityClock.Now;
        if (_opened < 0f)
            _opened = now;

        var size = PixelSize;
        var s = UIScale;
        var step = Math.Max(3, (int) (3 * s));
        for (var y = 0; y < size.Y; y += step)
        {
            handle.DrawRect(new UIBox2(0, y, size.X, y + 1), LineColor);
        }

        // бегущая светлая полоса
        var roll = CityUi.Rollbar();
        var rollY = now % 8f / 8f * (size.Y + 240f * s) - 120f * s;
        handle.DrawTextureRect(roll, new UIBox2(0f, rollY, size.X, rollY + 110f * s), CityUi.Accent.WithAlpha(0.05f));

        // редкое мерцание
        if (now % 6f / 6f is >= 0.41f and < 0.43f)
            handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), Color.Black.WithAlpha(0.08f));

        CityDraw.Corners(handle, size, s, CityUi.Glow, 14);

        DrawPowerOn(handle, size, now - _opened);
    }

    private static void DrawPowerOn(DrawingHandleScreen handle, Vector2i size, float t)
    {
        if (t >= PowerOn)
            return;

        var p = t / PowerOn;
        var open = p < 0.3f ? 0.006f : p < 0.7f ? 0.006f + (p - 0.3f) / 0.4f * 0.994f : 1f;
        var half = size.Y * open / 2f;
        var mid = size.Y / 2f;
        DystopiaTerm.Rect(handle, 0f, 0f, size.X, mid - half, CityUi.Bg);
        DystopiaTerm.Rect(handle, 0f, mid + half, size.X, size.Y, CityUi.Bg);

        var flash = p < 0.7f ? 0.5f : 0.5f * (1f - (p - 0.7f) / 0.3f);
        DystopiaTerm.Rect(handle, 0f, mid - half, size.X, mid + half, CityUi.Glow.WithAlpha(flash * 0.3f));
        if (p < 0.7f)
            DystopiaTerm.Rect(handle, 0f, mid - 1f, size.X, mid + 1f, CityUi.Glow);
    }
}
