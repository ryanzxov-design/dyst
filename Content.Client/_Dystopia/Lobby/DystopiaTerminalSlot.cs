// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — обёртки над обычными кнопками лобби (готов, наблюдать, голосование и т.д.). Кнопка
// остаётся собой и делает своё дело, но невидима: её рисует обёртка по её состоянию (наведение, нажатие, выключена,
// непрочитанное у жалобы). Здесь же — небольшая кнопка-ячейка ряда второстепенных команд под досье.

using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Lobby;

/// <summary>Обёртка над кнопкой: кнопка растянута на всю обёртку, её собственный вид спрятан.</summary>
public abstract class DystopiaButtonShell : Control
{
    private DystopiaLobbyThemeController? _theme;

    protected DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    protected BaseButton? Button
    {
        get
        {
            foreach (var child in Children)
            {
                if (child is BaseButton button)
                    return button;
            }

            return null;
        }
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);
        if (newChild is not ContainerButton container)
            return;

        container.StyleBoxOverride = new StyleBoxEmpty();
        if (container is Button button)
            button.Label.Visible = false;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        // ширину отдаёт раскладка (ячейки ряда — поровну), высота — своя
        return new Vector2(0f, MinHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
        }

        return finalSize;
    }

    protected static bool IsDisabled(BaseButton button)
    {
        return button.DrawMode == BaseButton.DrawModeEnum.Disabled || !button.Visible;
    }

    /// <summary>Рамка толщиной t.</summary>
    protected static void Border(DrawingHandleScreen handle, Vector2 size, float t, Color color)
    {
        DystopiaTerm.Rect(handle, 0f, 0f, size.X, t, color);
        DystopiaTerm.Rect(handle, 0f, size.Y - t, size.X, size.Y, color);
        DystopiaTerm.Rect(handle, 0f, t, t, size.Y - t, color);
        DystopiaTerm.Rect(handle, size.X - t, t, size.X, size.Y - t, color);
    }
}

/// <summary>Ячейка ряда команд под досье: «НАБЛЮДАТЬ», «ГОЛОСОВАНИЕ», «НАСТРОЙКИ», «АДМИН», «ВЫХОД».</summary>
public sealed class DystopiaTerminalSlot : DystopiaButtonShell
{
    private const int TextSize = 15;

    /// <summary>Что за команда: observe, vote, options, ahelp, leave — надпись берётся из темы (slot-&lt;роль&gt;).</summary>
    public string Role { get; set; } = "observe";

    public DystopiaTerminalSlot()
    {
        MinHeight = 38f;
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Button is not { } button)
            return;

        var theme = Term;
        var s = UIScale;
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        var t = MathF.Max(1f, MathF.Round(s));
        var disabled = IsDisabled(button);
        var active = !disabled && (button.IsHovered || button.DrawMode == BaseButton.DrawModeEnum.Pressed);
        var unread = button.StyleClasses.Contains(StyleClass.Negative);

        Color text;
        if (active)
        {
            DystopiaTerm.Rect(handle, 0f, 0f, size.X, size.Y, theme.Fg);
            text = DystopiaLobbyThemeController.Ink;
        }
        else
        {
            text = disabled ? theme.Dim.WithAlpha(0.6f) : unread ? theme.Alert.WithAlpha(theme.Pulse) : theme.Fg;
            Border(handle, size, t, disabled ? theme.Dim.WithAlpha(0.5f) : unread ? text : theme.Dim);
        }

        var font = theme.Font(false, TextSize);
        var label = DystopiaTerm.Fit(font, theme.Corrupt(theme.Text($"slot-{Role}")), s, size.X - 12f * s, 1f);
        var w = DystopiaTerm.Measure(font, label, s, 1f);
        var pos = new Vector2((size.X - w) / 2f, (size.Y - DystopiaTerm.LineHeight(font, s)) / 2f);
        DystopiaTerm.Glowing(handle, theme, font, pos, label, s, text, 1f, !active);
    }
}
