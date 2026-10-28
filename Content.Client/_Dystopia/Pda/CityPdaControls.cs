// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: детали КПК Города — клавиши (на экране у стали, на корпусе у планшета), строка списка (программа,
// настройка), мигающий курсор, строки развёртки поверх экрана, огонёк и сам корпус.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Pda;

/// <summary>Клавиша КПК: текущий экран, включённое (фонарь), нечего делать (нет ручки, нет карты).</summary>
public abstract class CityPdaKeyBase : ContainerButton
{
    private bool _current;
    private bool _toggled;
    private bool _inactive;

    /// <summary>Этот экран сейчас открыт.</summary>
    public bool Current
    {
        get => _current;
        set
        {
            _current = value;
            Refresh();
        }
    }

    /// <summary>Включено (фонарь).</summary>
    public bool Toggled
    {
        get => _toggled;
        set
        {
            _toggled = value;
            Refresh();
        }
    }

    /// <summary>Нечего делать (нет ручки, нет карты) — тусклая, но нажимается.</summary>
    public bool Inactive
    {
        get => _inactive;
        set
        {
            _inactive = value;
            Refresh();
        }
    }

    protected bool Hovered => DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed;

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        Refresh();
    }

    /// <summary>Перекрасить. Базовый конструктор зовёт это раньше, чем созданы надписи наследника.</summary>
    protected abstract void Refresh();
}

/// <summary>
/// Клавиша нижней строки экрана (казённая сталь): номер на янтарной плашке и подпись. Текущий экран и включённое —
/// подпись инверсией, нечего вынимать — тусклая, наведение — ярче.
/// </summary>
public sealed class CityPdaKey : CityPdaKeyBase
{
    private readonly PanelContainer? _numberPanel;
    private readonly PanelContainer? _captionPanel;
    private readonly Label? _caption;

    public CityPdaKey(string number, string caption)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        _numberPanel = new PanelContainer();
        _numberPanel.AddChild(CityPdaUi.Text(number, CityPdaUi.Ink));
        _captionPanel = new PanelContainer();
        _caption = CityPdaUi.Text(caption.ToUpperInvariant());
        _captionPanel.AddChild(_caption);
        row.AddChild(_numberPanel);
        row.AddChild(_captionPanel);
        AddChild(row);
        Refresh();
    }

    protected override void Refresh()
    {
        if (_numberPanel == null || _captionPanel == null || _caption == null)
            return;

        var inverse = Current || Toggled;
        _numberPanel.PanelOverride = CityPdaUi.Fill(Inactive ? CityPdaUi.Faint : CityPdaUi.Amber, 3);
        _captionPanel.PanelOverride = CityPdaUi.Fill(inverse ? CityPdaUi.Amber : Hovered ? CityPdaUi.Faint : Color.Transparent, 3);
        _caption.FontColorOverride = inverse ? CityPdaUi.Ink
            : Hovered ? CityPdaUi.Bright
            : Inactive ? CityPdaUi.Dim
            : CityPdaUi.Amber;
    }
}

/// <summary>
/// Клавиша на корпусе планшета: колпачок с цифрой (нажатие — колпачок проседает), под ним трафаретная подпись.
/// Текущий экран и включённое — на колпачке горит янтарная риска, нечего делать — подпись бледнее.
/// </summary>
public sealed class CityPdaHardKey : CityPdaKeyBase
{
    private readonly PanelContainer? _cap;
    private readonly Label? _number;
    private readonly PanelContainer? _lamp;
    private readonly Label? _caption;
    private readonly Color _keycap;
    private readonly Color _stencil;

    public CityPdaHardKey(string number, string caption, Color keycap, Color stencil)
    {
        _keycap = keycap;
        _stencil = stencil;
        HorizontalExpand = true;

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };
        _cap = new PanelContainer { HorizontalAlignment = HAlignment.Center, MinWidth = 44 };
        var capBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 1 };
        _number = CityPdaUi.Text(number, null, 15);
        _number.HorizontalAlignment = HAlignment.Center;
        _lamp = new PanelContainer { MinSize = new Vector2(14, 2), HorizontalAlignment = HAlignment.Center };
        capBox.AddChild(_number);
        capBox.AddChild(_lamp);
        _cap.AddChild(capBox);
        _caption = CityPdaUi.Text(caption.ToUpperInvariant(), null, 11);
        _caption.HorizontalAlignment = HAlignment.Center;
        box.AddChild(_cap);
        box.AddChild(_caption);
        AddChild(box);
        Refresh();
    }

    protected override void Refresh()
    {
        if (_cap == null || _number == null || _lamp == null || _caption == null)
            return;

        var pressed = DrawMode == DrawModeEnum.Pressed;
        var fill = pressed ? Color.InterpolateBetween(_keycap, Color.Black, 0.25f)
            : Hovered ? Color.InterpolateBetween(_keycap, Color.White, 0.12f)
            : _keycap;

        // колпачок с тёмной «юбкой» снизу; нажатый проседает — юбка тоньше, отступ сверху
        var box = new StyleBoxFlat
        {
            BackgroundColor = fill,
            BorderColor = Color.InterpolateBetween(_keycap, Color.Black, 0.6f),
            BorderThickness = new Thickness(1, 1, 1, pressed ? 1 : 4),
        };
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);
        box.SetContentMarginOverride(StyleBox.Margin.Top, pressed ? 5 : 2);
        box.SetContentMarginOverride(StyleBox.Margin.Bottom, 2);
        _cap.PanelOverride = box;

        var lit = Current || Toggled;
        _lamp.PanelOverride = CityPdaUi.Fill(lit ? CityPdaUi.Amber : Color.InterpolateBetween(_keycap, Color.Black, 0.45f));
        _number.FontColorOverride = Color.InterpolateBetween(_keycap, Color.White, 0.75f);
        _caption.FontColorOverride = Inactive ? _stencil.WithAlpha(0.45f) : _stencil;
    }
}

/// <summary>Строка списка: при наведении — инверсия всей строки.</summary>
public sealed class CityPdaRow : ContainerButton
{
    private readonly List<(Label Label, Color Color)> _labels = new();

    public CityPdaRow()
    {
        HorizontalExpand = true;
    }

    /// <summary>Запомнить надпись: при наведении она станет тёмной, потом вернёт свой цвет.</summary>
    public Label Track(Label label)
    {
        _labels.Add((label, label.FontColorOverride ?? CityPdaUi.Amber));
        return label;
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        var hover = DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed;
        StyleBoxOverride = CityPdaUi.Fill(hover ? CityPdaUi.Amber : Color.Transparent, 4, 1);

        // базовый конструктор зовёт это раньше, чем создан список
        if (_labels == null)
            return;

        foreach (var (label, color) in _labels)
        {
            label.FontColorOverride = hover ? CityPdaUi.Ink : color;
        }
    }
}

/// <summary>Мигающий курсор-прямоугольник.</summary>
public sealed class CityPdaCursor : Control
{
    [Dependency] private IGameTiming _timing = default!;

    public CityPdaCursor()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(8, 14);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if ((int) (_timing.RealTime.TotalSeconds * 2) % 2 == 0)
            handle.DrawRect(PixelSizeBox, CityPdaUi.Amber);
    }
}

/// <summary>Мигание: надпись то видна, то нет (розыск, тревога).</summary>
public sealed class CityPdaBlink : Control
{
    [Dependency] private IGameTiming _timing = default!;

    public CityPdaBlink()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Modulate = (int) (_timing.RealTime.TotalSeconds * 1.5) % 2 == 0 ? Color.White : Color.Transparent;
    }
}

/// <summary>Строки развёртки поверх экрана: тёмная полоса через каждые три пикселя. Мышь пропускает.</summary>
public sealed class CityCrtScanlines : Control
{
    private static readonly Color Line = Color.Black.WithAlpha(0.22f);

    public CityCrtScanlines()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var width = PixelSize.X;
        for (var y = 0; y < PixelSize.Y; y += 3)
        {
            handle.DrawRect(new UIBox2(0, y, width, y + 1), Line);
        }
    }
}

/// <summary>Полоса из делений (как на старом терминале) с отметкой нормы.</summary>
public sealed class CityPdaBar : Control
{
    private const int Segments = 25;

    private int _value;
    private int _max = 1;
    private int? _norm;

    public CityPdaBar()
    {
        MinHeight = 12;
        HorizontalExpand = true;
    }

    public void Set(int value, int max, int? norm)
    {
        _value = value;
        _max = Math.Max(1, max);
        _norm = norm;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var w = PixelSize.X;
        var h = PixelSize.Y;
        var step = w / (float) Segments;
        var lit = (int) MathF.Round(Segments * Math.Clamp(_value / (float) _max, 0f, 1f));

        for (var i = 0; i < Segments; i++)
        {
            var x = i * step;
            handle.DrawRect(new UIBox2(x + 1, 2, x + step - 1, h - 2), i < lit ? CityPdaUi.Amber : CityPdaUi.Faint);
        }

        if (_norm is { } norm)
        {
            var x = MathF.Round(w * Math.Clamp(norm / (float) _max, 0f, 1f));
            handle.DrawRect(new UIBox2(x - 1, 0, x + 1, h), CityPdaUi.Bright);
        }
    }
}

/// <summary>Огонёк на корпусе: квадратик цвета со спрайта, с тёмной окантовкой.</summary>
public sealed class CityPdaLight : Control
{
    private readonly Color _color;

    public CityPdaLight(Color color)
    {
        _color = color;
        MinSize = new Vector2(7, 7);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(PixelSizeBox, Color.Black.WithAlpha(0.7f));
        var t = MathF.Max(1f, MathF.Round(UIScale));
        handle.DrawRect(new UIBox2(t, t, PixelSize.X - t, PixelSize.Y - t), _color);
    }
}

/// <summary>
/// Корпус КПК: рисуется под содержимым окна. Сталь — крашеный металл с кромкой, планка цвета службы слева
/// (с полосой и метками начальника), заклёпки по углам. Планшет — тёмная резина, уголки-бамперы цвета службы,
/// полоса по верху и низу, метки начальника на нижнем левом уголке.
/// </summary>
public sealed class CityPdaCaseControl : Control
{
    /// <summary>Ширина планки стали и уголка планшета (до масштаба интерфейса).</summary>
    public const float TrimSize = 14f;
    public const float BumperSize = 22f;

    private readonly Content.Shared._Dystopia.Pda.CityPdaCaseComponent _case;

    public CityPdaCaseControl(Content.Shared._Dystopia.Pda.CityPdaCaseComponent data)
    {
        _case = data;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var w = PixelSize.X;
        var h = PixelSize.Y;
        var s = UIScale;
        var edge = MathF.Max(1f, MathF.Round(2 * s));

        handle.DrawRect(PixelSizeBox, _case.Edge);
        handle.DrawRect(new UIBox2(edge, edge, w - edge, h - edge), _case.Body);

        if (_case.Style == Content.Shared._Dystopia.Pda.CityPdaCaseStyle.Tablet)
            DrawTablet(handle, w, h, s, edge);
        else
            DrawSteel(handle, w, h, s, edge);
    }

    private void DrawSteel(DrawingHandleScreen handle, float w, float h, float s, float edge)
    {
        // фаска: светлая кромка сверху-слева, тёмная снизу-справа
        var light = Color.InterpolateBetween(_case.Body, Color.White, 0.18f);
        var dark = Color.InterpolateBetween(_case.Body, Color.Black, 0.3f);
        handle.DrawRect(new UIBox2(edge, edge, w - edge, edge * 2), light);
        handle.DrawRect(new UIBox2(edge, h - edge * 2, w - edge, h - edge), dark);
        handle.DrawRect(new UIBox2(w - edge * 2, edge, w - edge, h - edge), dark);

        // планка слева
        var trim = TrimSize * s;
        handle.DrawRect(new UIBox2(edge, edge, edge + trim, h - edge), _case.Trim);
        handle.DrawRect(new UIBox2(edge + trim, edge, edge * 2 + trim, h - edge), _case.Edge);
        if (_case.Stripe is { } stripe)
            handle.DrawRect(new UIBox2(edge + trim - 4 * s, edge, edge + trim, h - edge), stripe);

        // метки начальника — риски на планке под верхней заклёпкой
        var y = 20 * s;
        foreach (var mark in _case.Marks)
        {
            handle.DrawRect(new UIBox2(edge + 2 * s, y, edge + trim - 5 * s, y + 4 * s), mark);
            y += 6 * s;
        }

        // заклёпки
        var r = 2.5f * s;
        var rivet = Color.InterpolateBetween(_case.Edge, Color.Black, 0.2f);
        var cx = edge + trim / 2f;
        handle.DrawCircle(new Vector2(cx, 9 * s), r, rivet);
        handle.DrawCircle(new Vector2(cx, h - 9 * s), r, rivet);
        handle.DrawCircle(new Vector2(w - 8 * s, 8 * s), r, rivet);
        handle.DrawCircle(new Vector2(w - 8 * s, h - 8 * s), r, rivet);
    }

    private void DrawTablet(DrawingHandleScreen handle, float w, float h, float s, float edge)
    {
        var b = BumperSize * s;
        var bumperEdge = Color.InterpolateBetween(_case.Trim, Color.Black, 0.35f);

        // полоса по верху и низу между уголками
        if (_case.Stripe is { } stripe)
        {
            handle.DrawRect(new UIBox2(b, edge, w - b, edge + 3 * s), stripe);
            handle.DrawRect(new UIBox2(b, h - edge - 3 * s, w - b, h - edge), stripe);
        }

        // уголки-бамперы
        foreach (var (x, y) in new[] { (0f, 0f), (w - b, 0f), (0f, h - b), (w - b, h - b) })
        {
            handle.DrawRect(new UIBox2(x, y, x + b, y + b), bumperEdge);
            handle.DrawRect(new UIBox2(x + edge, y + edge, x + b - edge, y + b - edge), _case.Trim);
        }

        // метки начальника — на нижнем левом уголке
        var mx = 5 * s;
        foreach (var mark in _case.Marks)
        {
            handle.DrawRect(new UIBox2(mx, h - b + 6 * s, mx + 4 * s, h - 6 * s), mark);
            mx += 6 * s;
        }
    }
}
