// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: КПК Города — дешёвый старый терминал гражданина, старше терминалов Стражи: один янтарный цвет люминофора
// на почти чёрном экране, консольный шрифт Terminus, выделение инверсией (тёмный текст на янтарной плашке) и
// миганием, а не цветом. Здесь палитра, шрифты, лист стилей (им окрашены и ванильные программы внутри КПК) и
// мелкие помощники для экранов и программ.

using Content.Client._Dystopia.Lobby;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Lobby;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Pda;

public static class CityPdaUi
{
    /// <summary>Экран.</summary>
    public static readonly Color Screen = Color.FromHex("#140C04");

    /// <summary>Люминофор: обычный текст, плашки инверсии.</summary>
    public static readonly Color Amber = Color.FromHex("#FFB347");

    /// <summary>Яркий люминофор: наведение.</summary>
    public static readonly Color Bright = Color.FromHex("#FFD49A");

    /// <summary>Тусклый: подписи, точки-заполнители.</summary>
    public static readonly Color Dim = Color.FromHex("#9A6526");

    /// <summary>Еле видный: линии, рамки, неактивное.</summary>
    public static readonly Color Faint = Color.FromHex("#5A3A14");

    /// <summary>Текст на янтарной плашке.</summary>
    public static readonly Color Ink = Color.FromHex("#140C04");

    /// <summary>Корпус КПК без цвета службы.</summary>
    public static readonly Color Case = Color.FromHex("#3A3428");

    public const string TerminalPath = "/Fonts/_Dystopia/Terminus/Terminess-Regular.ttf";

    private static Stylesheet? _sheet;

    public static Font Font(int size = 13) => CityUi.Custom(TerminalPath, size);

    /// <summary>Ванильные надписи, кнопки и поля внутри КПК — янтарём и шрифтом Terminus.</summary>
    public static Stylesheet Sheet => _sheet ??= DystopiaTerminalStylesheet.Build(new DystopiaLobbyThemePrototype
    {
        Foreground = Amber,
        Dim = Dim,
        Glow = Amber.WithAlpha(0.4f),
        Background = Screen,
        Alert = Amber,
    }, 13, TerminalPath);

    public static Label Text(string text, Color? color = null, int size = 13)
    {
        return new Label
        {
            Text = text,
            FontOverride = Font(size),
            FontColorOverride = color ?? Amber,
        };
    }

    /// <summary>Надпись с переносом строк (шрифт — из листа стилей КПК).</summary>
    public static RichTextLabel Wrap(string text, Color? color = null)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(Robust.Shared.Utility.FormattedMessage.FromUnformatted(text), defaultColor: color ?? Amber);
        return label;
    }

    /// <summary>Инверсия: тёмный текст на янтарной плашке.</summary>
    public static PanelContainer Inverse(string text, int size = 13, Color? fill = null)
    {
        var panel = new PanelContainer
        {
            PanelOverride = Fill(fill ?? Amber, 3, 0),
            HorizontalAlignment = Control.HAlignment.Left,
        };
        panel.AddChild(Text(text, Ink, size));
        return panel;
    }

    public static StyleBoxFlat Fill(Color color, float padH = 0, float padV = 0)
    {
        var box = new StyleBoxFlat { BackgroundColor = color };
        if (padH > 0)
            box.SetContentMarginOverride(StyleBox.Margin.Horizontal, padH);
        if (padV > 0)
            box.SetContentMarginOverride(StyleBox.Margin.Vertical, padV);
        return box;
    }

    /// <summary>Тонкая линия-разделитель.</summary>
    public static Control Rule()
    {
        return new PanelContainer
        {
            PanelOverride = Fill(Faint),
            MinHeight = 1,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4),
        };
    }

    /// <summary>Заголовок раздела: «─ ЛОЯЛЬНОСТЬ ─────».</summary>
    public static Control Section(string text)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(0, 8, 0, 2),
        };
        row.AddChild(new PanelContainer { PanelOverride = Fill(Faint), MinWidth = 10, MinHeight = 1, VerticalAlignment = Control.VAlignment.Center });
        row.AddChild(Text(text.ToUpperInvariant(), Dim));
        row.AddChild(new PanelContainer { PanelOverride = Fill(Faint), HorizontalExpand = true, MinHeight = 1, VerticalAlignment = Control.VAlignment.Center });
        return row;
    }

    /// <summary>Ключ с точками-заполнителями: «ВЛАДЕЛЕЦ .....» — ширина выровнена моноширинным шрифтом.</summary>
    public static string Leader(string key, int width = 14)
    {
        var dots = Math.Max(2, width - key.Length - 1);
        return key.ToUpperInvariant() + " " + new string('.', dots);
    }

    /// <summary>Строка «КЛЮЧ ....... значение».</summary>
    public static BoxContainer Field(string key, Control value, int width = 14)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(Text(Leader(key, width), Dim));
        value.HorizontalExpand = true;
        row.AddChild(value);
        return row;
    }

    public static BoxContainer Field(string key, string value, int width = 14)
    {
        return Field(key, Text(value), width);
    }

    public static string Clock(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}";
    }
}
