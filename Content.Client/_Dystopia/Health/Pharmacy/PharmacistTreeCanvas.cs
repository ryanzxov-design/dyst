// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: полотно древа синтеза — сетка, линии «реагент → продукт» и узлы-кнопки веществ.
// Полотно можно таскать мышью, как древо исследований, и масштабировать колесом.
// Координаты узлов и линий хранятся в масштабе 1, полотно умножает их на Zoom.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Dystopia.Health.Pharmacy;

public sealed class PharmacistTreeCanvas : LayoutContainer
{
    public const float NodeWidth = 204;
    public const float NodeHeight = 34;
    public const float ColumnGap = 56;
    public const float RowGap = 10;
    public const float Padding = 18;

    private const float GridStep = 24;
    private static readonly Color GridColor = CityUi.Line.WithAlpha(0.35f);

    public const float MinZoom = 0.4f;
    public const float MaxZoom = 1.6f;

    private readonly List<TreeEdge> _edges = new();
    private bool _dragging;

    /// <summary>
    /// Текущий масштаб полотна.
    /// </summary>
    public float Zoom = 1f;

    /// <summary>
    /// Колесо мыши над полотном: шаг (+1 — приблизить, -1 — отдалить) и точка под курсором на полотне.
    /// </summary>
    public event Action<int, Vector2>? Zoomed;

    /// <summary>
    /// Полотно тащат мышью: смещение мыши в виртуальных пикселях.
    /// </summary>
    public event Action<Vector2>? Dragged;

    public PharmacistTreeCanvas()
    {
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    public void SetEdges(IEnumerable<TreeEdge> edges)
    {
        _edges.Clear();
        _edges.AddRange(edges);
        // подсвеченные — поверх тусклых
        _edges.Sort((a, b) => a.Highlighted.CompareTo(b.Highlighted));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = UIScale * Zoom;
        var size = PixelSize;

        // сетка «чертежа»
        var step = GridStep * scale;
        for (var x = 0f; x < size.X; x += step)
        {
            handle.DrawRect(new UIBox2(x, 0, x + 1, size.Y), GridColor);
        }

        for (var y = 0f; y < size.Y; y += step)
        {
            handle.DrawRect(new UIBox2(0, y, size.X, y + 1), GridColor);
        }

        // линии: от правого края реагента до левого края продукта, с изломом в промежутке перед продуктом
        foreach (var edge in _edges)
        {
            var thickness = (edge.Highlighted ? 2f : 1f) * Math.Max(1f, UIScale);
            var color = edge.Highlighted ? CityUi.Accent : CityUi.Line;
            if (edge.Faded)
                color = color.WithAlpha(0.35f);

            var x1 = (edge.From.X + NodeWidth) * scale;
            var y1 = (edge.From.Y + NodeHeight / 2) * scale;
            var x2 = edge.To.X * scale;
            var y2 = (edge.To.Y + NodeHeight / 2) * scale;
            var xm = x2 - ColumnGap / 2 * scale;
            var half = thickness / 2;

            handle.DrawRect(new UIBox2(x1, y1 - half, xm + half, y1 + half), color);
            handle.DrawRect(new UIBox2(xm - half, MathF.Min(y1, y2) - half, xm + half, MathF.Max(y1, y2) + half), color);
            handle.DrawRect(new UIBox2(xm - half, y2 - half, x2, y2 + half), color);
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        // пока кнопка зажата, движение мыши приходит полотну, даже если курсор над узлом
        _dragging = true;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = false;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        // колесо масштабирует полотно; прокрутку списка-полотна не пускаем дальше
        args.Handle();
        if (args.Delta.Y != 0)
            Zoomed?.Invoke(args.Delta.Y > 0 ? 1 : -1, args.RelativePosition);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging)
            Dragged?.Invoke(args.Relative);
    }
}

public readonly record struct TreeEdge(Vector2 From, Vector2 To, bool Highlighted, bool Faded);

/// <summary>
/// Узел древа: вещество. Метка слева — сырьё из раздатчика, справа — выход реакции или номер рецепта.
/// </summary>
public sealed class PharmacistTreeNode : ContainerButton
{
    private readonly Label _name;
    private readonly Label _badge;
    private readonly PanelContainer _mark;
    private Color _fill = CityUi.Panel;
    private Color _border = CityUi.Line;
    private float _thickness = 1;

    public string ReagentId { get; }

    private readonly BoxContainer _row;
    private bool _bold;
    private float _zoom = 1f;

    public PharmacistTreeNode(string reagentId, string name)
    {
        ReagentId = reagentId;

        _row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(_row);

        _mark = new PanelContainer { VerticalAlignment = VAlignment.Center };
        _row.AddChild(_mark);

        _name = CityUi.MakeLabel(name, CityUi.Text, CityUi.Regular(11));
        _name.HorizontalExpand = true;
        _name.ClipText = true;
        _row.AddChild(_name);

        _badge = CityUi.MakeLabel(string.Empty, CityUi.Dim, CityUi.Mono(9));
        _row.AddChild(_badge);

        SetZoom(1f);
    }

    /// <summary>
    /// Размер узла, отступы и шрифты под масштаб полотна.
    /// </summary>
    public void SetZoom(float zoom)
    {
        _zoom = zoom;
        SetSize = new Vector2(PharmacistTreeCanvas.NodeWidth, PharmacistTreeCanvas.NodeHeight) * zoom;
        _row.SeparationOverride = (int) MathF.Round(7 * zoom);
        _row.Margin = new Thickness(8 * zoom, 0, 6 * zoom, 0);
        _mark.SetSize = new Vector2(6, 6) * zoom;
        UpdateFonts();
    }

    private void UpdateFonts()
    {
        var nameSize = Math.Max(6, (int) MathF.Round(11 * _zoom));
        _name.FontOverride = _bold ? CityUi.Bold(nameSize) : CityUi.Regular(nameSize);
        _badge.FontOverride = CityUi.Mono(Math.Max(6, (int) MathF.Round(9 * _zoom)));
    }

    public void SetLook(Color fill, Color border, Color text, float thickness, bool bold, Color mark, string badge, float alpha)
    {
        _fill = fill;
        _border = border;
        _thickness = thickness;
        UpdateBox();
        _name.FontColorOverride = text;
        _bold = bold;
        UpdateFonts();
        _mark.PanelOverride = new StyleBoxFlat { BackgroundColor = mark };
        _badge.Text = badge;
        Modulate = Color.White.WithAlpha(alpha);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateBox();
    }

    private void UpdateBox()
    {
        // вызывается и из конструктора базового класса — до создания полей
        var hover = DrawMode == DrawModeEnum.Hover;
        StyleBoxOverride = CityUi.Box(hover ? CityUi.HoverFill : _fill, hover ? CityUi.Accent : _border, _thickness);
    }
}
