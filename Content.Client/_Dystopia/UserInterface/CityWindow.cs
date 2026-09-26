using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Analyzers;

namespace Content.Client._Dystopia.UserInterface;

/// <summary>
/// Окно городского оборудования: своя рамка, шапка с гербом, названием и строкой терминала,
/// индикатор сети, своя кнопка закрытия, подвал с лозунгом, уголки и лёгкие сканлайны.
/// Содержимое окна добавляется в <see cref="Contents"/>.
/// </summary>
[Virtual]
public class CityWindow : BaseWindow
{
    private const float HeaderHeight = 66;
    private const float DragMargin = 6;

    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _slogan;
    private readonly Label _footer;

    /// <summary>Сюда добавляется содержимое окна.</summary>
    public BoxContainer Contents { get; }

    public CityWindow()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(360, 240);

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
        };
        _title = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Title(17));
        _title.ClipText = true;
        _subtitle = CityUi.MakeLabel(string.Empty, CityUi.Dim, CityUi.Regular(10));
        _subtitle.ClipText = true;
        titles.AddChild(_title);
        titles.AddChild(_subtitle);
        headerRow.AddChild(titles);

        var close = CityUi.MakeButton("×");
        close.SetSize = new Vector2(28, 28);
        close.VerticalAlignment = VAlignment.Center;
        close.OnPressed += _ => Close();
        headerRow.AddChild(close);

        layout.AddChild(CityUi.Separator(CityUi.Frame));

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
        layout.AddChild(CityUi.Separator());
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
        _slogan = CityUi.MakeLabel(Loc.GetString("dystopia-city-ui-slogan"), CityUi.Frame, CityUi.Bold(10));
        footerRow.AddChild(_footer);
        footerRow.AddChild(_slogan);
        footer.AddChild(footerRow);

        // Сканлайны поверх всего окна (мышь пропускают).
        AddChild(new CityScanlines());
    }

    /// <summary>Название прибора (пишется капсом).</summary>
    public string WindowTitle
    {
        get => _title.Text ?? string.Empty;
        set => _title.Text = value.ToUpperInvariant();
    }

    /// <summary>Строка под названием: «ведомство · терминал · доступ».</summary>
    public string Subtitle
    {
        get => _subtitle.Text ?? string.Empty;
        set => _subtitle.Text = value.ToUpperInvariant();
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

/// <summary>Лёгкие горизонтальные линии и уголки поверх окна — «монитор наблюдения».</summary>
public sealed class CityScanlines : Control
{
    private static readonly Color LineColor = new(0f, 0f, 0f, 0.09f);

    public CityScanlines()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var step = Math.Max(3, (int) (3 * UIScale));
        for (var y = 0; y < size.Y; y += step)
        {
            handle.DrawRect(new UIBox2(0, y, size.X, y + 1), LineColor);
        }

        CityDraw.Corners(handle, size, UIScale, CityUi.Glow, 14);
    }
}
