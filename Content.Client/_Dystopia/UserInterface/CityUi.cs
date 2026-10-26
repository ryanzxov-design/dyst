using Content.Client._Dystopia.Lobby;
using Content.Client.Resources;
using Content.Shared._Dystopia.Lobby;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.IoC;
using System.Text;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.UserInterface;

/// <summary>
/// Единый стиль интерфейса городского оборудования: палитра, шрифты и готовые элементы.
/// Основа — очень тёмный синий фон и безжизненный холодный голубой для элементов.
/// Терминальный вид (как у лобби): моноширинный PT Mono (запасные — Noto), заголовок окна пиксельным
/// Press Start 2P, кнопки тонкой рамкой с заливкой при наведении, заголовки разделов — как кромка рамки.
/// </summary>
public static class CityUi
{
    // Палитра
    public static readonly Color Bg = Color.FromHex("#080D18");
    public static readonly Color Bg2 = Color.FromHex("#0B1220");
    public static readonly Color Panel = Color.FromHex("#0E1728");
    public static readonly Color Panel2 = Color.FromHex("#121E34");
    public static readonly Color PanelAlt = Color.FromHex("#101A2D");
    public static readonly Color Input = Color.FromHex("#090F1B");
    public static readonly Color Line = Color.FromHex("#223854");
    public static readonly Color Frame = Color.FromHex("#3E6E96");
    public static readonly Color Accent = Color.FromHex("#78BEE1");
    public static readonly Color Glow = Color.FromHex("#C8E4F0");
    public static readonly Color Text = Color.FromHex("#B0CCDE");
    public static readonly Color Dim = Color.FromHex("#607A92");
    public static readonly Color Muted = Color.FromHex("#40546A");
    public static readonly Color Danger = Color.FromHex("#C85A54");
    public static readonly Color ActiveFill = Color.FromHex("#14283F");
    public static readonly Color HoverFill = Color.FromHex("#18304C");

    public const string EmblemPath = "/Textures/_Dystopia/Interface/Emblem/city_emblem_64.png";

    /// <summary>Тёмный цвет текста на залитой (наведённой) кнопке.</summary>
    public static readonly Color Ink = Color.FromHex("#06101C");

    private const string MonoPath = "/Fonts/_Dystopia/PTMono/PTMono-Regular.ttf";
    private const string PixelPath = "/Fonts/_Dystopia/PressStart2P/PressStart2P-Regular.ttf";
    private const string NotoRegular = "/Fonts/NotoSans/NotoSans-Regular.ttf";
    private const string NotoBold = "/Fonts/NotoSans/NotoSans-Bold.ttf";
    private const string NotoSymbols = "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf";
    private const string NotoSymbols2 = "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf";

    private static readonly Dictionary<(string, int), Font> Fonts = new();
    private static Stylesheet? _sheet;

    private static IResourceCache Res => IoCManager.Resolve<IResourceCache>();

    // у PT Mono нет жирного начертания и части значков — за ним стоят шрифты Noto
    public static Font Regular(int size = 12) => Stack(MonoPath, NotoRegular, size);
    public static Font Bold(int size = 12) => Stack(MonoPath, NotoBold, size);
    public static Font Title(int size = 16) => Stack(PixelPath, NotoBold, size);
    public static Font Mono(int size = 11) => Stack(MonoPath, NotoRegular, size);

    private static Font Stack(string main, string fallback, int size)
    {
        if (Fonts.TryGetValue((main + fallback, size), out var font))
            return font;

        font = Res.GetFont(new[] { main, fallback, NotoSymbols, NotoSymbols2 }, size);
        Fonts[(main + fallback, size)] = font;
        return font;
    }

    /// <summary>
    /// Лист стилей окон Города: ванильные надписи, поля и прокрутки внутри окна — тем же шрифтом и цветами
    /// (тот же терминальный лист, что у настройки персонажа, но с палитрой Города).
    /// </summary>
    public static Stylesheet Sheet => _sheet ??= DystopiaTerminalStylesheet.Build(new DystopiaLobbyThemePrototype
    {
        Foreground = Text,
        Dim = Frame,
        Glow = Accent.WithAlpha(0.55f),
        Background = Bg,
        Alert = Danger,
    }, 12);

    public static Texture Emblem() => Res.GetTexture(EmblemPath);

    /// <summary>Светлая полоса развёртки (та же, что у лобби-терминала).</summary>
    public static Texture Rollbar() => Res.GetTexture("/Textures/_Dystopia/Interface/Lobby/rollbar.png");

    public static StyleBoxFlat Box(Color background, Color border, float thickness = 1, float padH = 0, float padV = 0)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(thickness),
        };
        if (padH > 0)
            box.SetContentMarginOverride(StyleBox.Margin.Horizontal, padH);
        if (padV > 0)
            box.SetContentMarginOverride(StyleBox.Margin.Vertical, padV);
        return box;
    }

    public static Label MakeLabel(string text, Color? color = null, Font? font = null)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = color ?? Text,
            FontOverride = font ?? Regular(),
        };
    }

    /// <summary>Панель с рамкой (для группировки содержимого).</summary>
    public static PanelContainer MakePanel(Color? background = null, Color? border = null, float pad = 6)
    {
        return new PanelContainer
        {
            PanelOverride = Box(background ?? Panel, border ?? Line, 1, pad, pad),
        };
    }

    /// <summary>Горизонтальная линия-разделитель.</summary>
    public static Control Separator(Color? color = null, float height = 1)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = color ?? Line },
            MinHeight = height,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    /// <summary>Заголовок раздела: «─ ЗАГОЛОВОК ───────» — как верхняя кромка рамки с врезанным названием.</summary>
    public static Control SectionHeader(string text)
    {
        return new CitySectionHeader(text.ToUpperInvariant()) { Margin = new Thickness(0, 6, 0, 2) };
    }

    /// <summary>Поле ввода в стиле Города: линия снизу, как строка терминала.</summary>
    public static LineEdit Field(string placeholder = "", float minWidth = 0)
    {
        var box = Box(Input.WithAlpha(0.6f), Frame, 0, 6, 3);
        box.BorderThickness = new Thickness(0, 0, 0, 1);
        var edit = new LineEdit
        {
            PlaceHolder = placeholder,
            MinWidth = minWidth,
            StyleBoxOverride = box,
        };
        return edit;
    }

    /// <summary>Надпись с переносом строк по ширине окна (длинные подсказки и строки — не уходят за край).</summary>
    public static CityWrapLabel Hint(string text, Color? color = null)
    {
        return new CityWrapLabel(text, color ?? Dim);
    }

    public static CityButton MakeButton(string text, CityButtonStyle style = CityButtonStyle.Normal)
    {
        return new CityButton(text.ToUpperInvariant(), style);
    }
}

/// <summary>
/// Надпись с переносом строк: те же Text и FontColorOverride, что у Label, но длинный текст переносится по ширине,
/// а не уходит за край окна (моноширинный шрифт шире прежнего).
/// </summary>
public sealed class CityWrapLabel : Control
{
    private readonly RichTextLabel _label = new() { HorizontalExpand = true };
    private string _text = string.Empty;
    private Color? _color;

    public CityWrapLabel() : this(string.Empty)
    {
    }

    public CityWrapLabel(string text, Color? color = null)
    {
        HorizontalExpand = true;
        AddChild(_label);
        _color = color;
        Text = text;
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Refresh();
        }
    }

    public Color? FontColorOverride
    {
        get => _color;
        set
        {
            _color = value;
            Refresh();
        }
    }

    private void Refresh()
    {
        _label.SetMessage(FormattedMessage.FromUnformatted(_text), defaultColor: _color ?? CityUi.Text);
    }
}

public enum CityButtonStyle : byte
{
    Normal,
    Primary,
    Danger,
    Tab,
}

/// <summary>
/// Кнопка Города: своя рамка и заливка для обычного, наведённого, нажатого/активного и выключенного состояний.
/// </summary>
public sealed class CityButton : Button
{
    private readonly CityButtonStyle _style;
    private bool _active;

    public CityButton(string text, CityButtonStyle style)
    {
        _style = style;
        Text = text;
        Label.FontOverride = CityUi.Bold(12);
        // цвет кнопки — только своя рамка и заливка: ванильная палитра кнопок её не подкрашивает
        ModulateSelfOverride = Color.White;
        MinHeight = style == CityButtonStyle.Tab ? 34 : 24;
        if (style == CityButtonStyle.Tab)
        {
            // Текст вкладки прижат влево и обрезается справа. Класс стиля "button" снимаем:
            // стандартная таблица стилей принудительно центрирует текст кнопок.
            Label.RemoveStyleClass(StyleClassButton);
            TextAlign = Robust.Client.UserInterface.Controls.Label.AlignMode.Left;
            Label.ClipText = true;
            Label.HorizontalExpand = true;
            Label.HorizontalAlignment = HAlignment.Stretch;
        }

        _fullText = text;
        UpdateLook();
    }

    private Color? _textColor;

    // Бегущая строка: если текст не влезает, при наведении он прокручивается.
    private string _fullText = string.Empty;
    private float _marqueeTimer;
    private int _marqueeOffset;
    private bool _marqueeRunning;

    /// <summary>Прокручивать не влезающий текст бегущей строкой при наведении.</summary>
    public bool Marquee { get; set; }

    private float _lastWidth = -1;
    private string _truncated = string.Empty;

    /// <summary>Задать полный текст для кнопки с бегущей строкой.</summary>
    public void SetFullText(string text)
    {
        _fullText = text;
        _lastWidth = -1;
        Text = text;
    }

    /// <summary>Ширина текста в пикселях экрана (тем же шрифтом, что у надписи).</summary>
    private float Measure(string text)
    {
        var font = Label.FontOverride;
        if (font == null)
            return 0;

        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            width += font.GetCharMetrics(rune, UIScale)?.Advance ?? 0;
        }

        return width;
    }

    /// <summary>Самая длинная часть текста с «…», которая влезает в ширину.</summary>
    private string Truncate(string text, float available)
    {
        var ellipsis = Measure("…");
        var sb = new StringBuilder();
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var w = Measure(rune.ToString());
            if (width + w + ellipsis > available)
                break;
            sb.Append(rune.ToString());
            width += w;
        }

        return sb.ToString().TrimEnd() + "…";
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!Marquee || _fullText.Length == 0)
            return;

        var padding = (_style == CityButtonStyle.Tab ? 14 : 10) * 2 + 6;
        var available = PixelSize.X - padding * UIScale;
        if (available <= 0)
            return;

        var fits = Measure(_fullText) <= available;
        var hovered = DrawMode == DrawModeEnum.Hover;

        if (hovered && !fits)
        {
            if (!_marqueeRunning)
            {
                _marqueeRunning = true;
                _marqueeOffset = 0;
                _marqueeTimer = -0.5f; // пауза перед стартом
                Text = _fullText;
            }

            _marqueeTimer += args.DeltaSeconds;
            if (_marqueeTimer < 0.12f)
                return;

            _marqueeTimer = 0;
            var loop = _fullText + "     ";
            _marqueeOffset = (_marqueeOffset + 1) % loop.Length;
            Text = loop[_marqueeOffset..] + loop[.._marqueeOffset];
            return;
        }

        if (_marqueeRunning)
        {
            _marqueeRunning = false;
            _lastWidth = -1;
        }

        // Без наведения: полный текст, если влезает, иначе — начало с «…» (номер статьи всегда виден).
        if (Math.Abs(_lastWidth - available) < 0.5f)
            return;

        _lastWidth = available;
        _truncated = fits ? _fullText : Truncate(_fullText, available);
        Text = _truncated;
    }

    /// <summary>Свой цвет текста (например, цвет положения Города). null — цвет по стилю.</summary>
    public Color? TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            UpdateLook();
        }
    }

    /// <summary>Выбрана ли вкладка (для стиля Tab).</summary>
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            UpdateLook();
        }
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateLook();
    }

    private void UpdateLook()
    {
        // DrawModeChanged вызывается ещё из конструктора базового класса, когда Label ещё не создан.
        if (Label is null)
            return;

        var disabled = Disabled;
        var hover = DrawMode == DrawModeEnum.Hover;
        var pressed = DrawMode == DrawModeEnum.Pressed || _active;

        // терминальный вид: прозрачная заливка и рамка; наведённая — залита цветом рамки, текст тёмный
        Color fill, border, text;
        switch (_style)
        {
            case CityButtonStyle.Danger:
                fill = hover ? CityUi.Danger : Color.Transparent;
                border = CityUi.Danger;
                text = hover ? CityUi.Ink : CityUi.Danger;
                break;
            case CityButtonStyle.Primary:
                fill = hover ? CityUi.Accent : pressed ? CityUi.HoverFill : CityUi.ActiveFill;
                border = CityUi.Accent;
                text = hover ? CityUi.Ink : CityUi.Glow;
                break;
            case CityButtonStyle.Tab:
                fill = hover ? CityUi.Accent : pressed ? CityUi.ActiveFill : CityUi.Panel.WithAlpha(0.6f);
                border = pressed ? CityUi.Accent : CityUi.Line;
                text = hover ? CityUi.Ink : pressed ? CityUi.Glow : CityUi.Text;
                break;
            default:
                fill = hover ? CityUi.Accent : pressed ? CityUi.HoverFill : Color.Transparent;
                border = hover ? CityUi.Accent : CityUi.Frame;
                text = hover ? CityUi.Ink : CityUi.Text;
                break;
        }

        if (disabled)
        {
            fill = Color.Transparent;
            border = CityUi.Line;
            text = CityUi.Muted;
        }

        var box = CityUi.Box(fill, border, 1, _style == CityButtonStyle.Tab ? 14 : 10, 3);
        if (_style == CityButtonStyle.Tab && _active)
            box.BorderThickness = new Thickness(4, 1, 1, 1);   // голубая полоса слева у активной вкладки
        if (_style == CityButtonStyle.Tab && _active)
            box.BorderColor = CityUi.Accent;

        StyleBoxOverride = box;
        // свой цвет текста (например, цвет положения Города) — кроме наведения: на заливке текст тёмный
        Label.FontColorOverride = disabled || hover ? text : _textColor ?? text;
    }
}

/// <summary>Карточка-показатель с «уголками» по углам, как на мониторах наблюдения.</summary>
public sealed class CityCard : PanelContainer
{
    public CityCard()
    {
        PanelOverride = CityUi.Box(CityUi.Panel, CityUi.Line, 1, 10, 6);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        CityDraw.Corners(handle, PixelSize, UIScale, CityUi.Frame, 6);
    }
}

public static class CityDraw
{
    /// <summary>Строка со свечением цветом glow (копии со сдвигом на пиксель), буквы — по целым пикселям.
    /// Возвращает ширину.</summary>
    public static float GlowText(DrawingHandleScreen handle, Font font, System.Numerics.Vector2 pos, string text, float scale,
        Color color, Color glow, float spacing = 0f)
    {
        var halo = glow.WithAlpha(0.18f * color.A);
        var d = MathF.Max(1f, MathF.Round(scale));
        DystopiaTerm.Plain(handle, font, pos + new System.Numerics.Vector2(-d, 0f), text, scale, halo, spacing);
        DystopiaTerm.Plain(handle, font, pos + new System.Numerics.Vector2(d, 0f), text, scale, halo, spacing);
        DystopiaTerm.Plain(handle, font, pos + new System.Numerics.Vector2(0f, -d), text, scale, halo, spacing);
        DystopiaTerm.Plain(handle, font, pos + new System.Numerics.Vector2(0f, d), text, scale, halo, spacing);
        return DystopiaTerm.Plain(handle, font, pos, text, scale, color, spacing);
    }

    /// <summary>Уголки по четырём углам прямоугольника размера size.</summary>
    public static void Corners(DrawingHandleScreen handle, Vector2i size, float scale, Color color, float length)
    {
        var n = length * scale;
        var t = Math.Max(1f, scale);
        var w = size.X;
        var h = size.Y;
        // верхний левый
        handle.DrawRect(new UIBox2(0, 0, n, t), color);
        handle.DrawRect(new UIBox2(0, 0, t, n), color);
        // верхний правый
        handle.DrawRect(new UIBox2(w - n, 0, w, t), color);
        handle.DrawRect(new UIBox2(w - t, 0, w, n), color);
        // нижний левый
        handle.DrawRect(new UIBox2(0, h - t, n, h), color);
        handle.DrawRect(new UIBox2(0, h - n, t, h), color);
        // нижний правый
        handle.DrawRect(new UIBox2(w - n, h - t, w, h), color);
        handle.DrawRect(new UIBox2(w - t, h - n, w, h), color);
    }
}

/// <summary>
/// Выпадающий список в стиле Города (замена ванильной OptionButton).
/// </summary>
public sealed class CityDropdown : ContainerButton
{
    private readonly Label _label;
    private readonly Popup _popup;
    private readonly ScrollContainer _scroll;
    private readonly BoxContainer _list;
    private readonly List<(int Id, string Text)> _items = new();

    public int SelectedId { get; private set; } = int.MinValue;

    public event Action<int>? OnItemSelected;

    public CityDropdown()
    {
        StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 8, 3);
        MinHeight = 24;

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _label = CityUi.MakeLabel(string.Empty, CityUi.Text, CityUi.Regular(12));
        _label.HorizontalExpand = true;
        _label.ClipText = true;
        row.AddChild(_label);
        row.AddChild(CityUi.MakeLabel("▼", CityUi.Accent, CityUi.Regular(10)));
        AddChild(row);

        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 1 };
        _scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            ReturnMeasure = true,
            MaxHeight = 300,
            Children = { _list },
        };
        _popup = new Popup
        {
            Children =
            {
                new PanelContainer { PanelOverride = CityUi.Box(CityUi.Panel2, CityUi.Frame, 1, 2, 2) },
                _scroll,
            },
        };
        _popup.OnPopupHide += () => _popup.Orphan();

        OnPressed += _ => OpenPopup();
    }

    public void Clear()
    {
        _items.Clear();
        _label.Text = string.Empty;
        SelectedId = int.MinValue;
    }

    public void AddItem(string text, int id)
    {
        _items.Add((id, text));
        if (_items.Count == 1)
            SelectId(id);
    }

    public void SelectId(int id)
    {
        foreach (var (itemId, text) in _items)
        {
            if (itemId != id)
                continue;

            SelectedId = id;
            _label.Text = text;
            return;
        }
    }

    private void OpenPopup()
    {
        if (Root == null)
            return;

        _list.RemoveAllChildren();
        foreach (var (id, text) in _items)
        {
            var itemId = id;
            var button = new CityButton(text, CityButtonStyle.Tab)
            {
                Active = id == SelectedId,
                HorizontalExpand = true,
                Marquee = true,
                MinHeight = 26,
            };
            button.OnPressed += _ =>
            {
                _popup.Close();
                SelectId(itemId);
                OnItemSelected?.Invoke(itemId);
            };
            _list.AddChild(button);
        }

        var position = GlobalPosition + new System.Numerics.Vector2(0, Size.Y + 1);
        _scroll.Measure(new System.Numerics.Vector2(Width, float.PositiveInfinity));
        var height = Math.Min(_scroll.DesiredSize.Y, 300);
        var box = UIBox2.FromDimensions(position, new System.Numerics.Vector2(Width, height));
        Root.ModalRoot.AddChild(_popup);
        _popup.Open(box);
    }
}
