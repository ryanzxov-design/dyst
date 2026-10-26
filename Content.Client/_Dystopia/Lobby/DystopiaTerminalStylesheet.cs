// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: стиль терминала для ванильных элементов интерфейса (настройка персонажа и всё, что из неё открывается:
// выпадающие списки, окно снаряжения, окно «сохранить?»). Это тот же стиль Nanotrasen, но палитры собраны из цветов
// темы лобби (люминофор, тусклый, фон, тревога), а поверх добавлены правила терминала: кнопки — тонкая рамка
// без скруглений (нажатая — залита, текст тёмный), поля ввода — линия снизу, вкладки — рамкой, текст — PT Mono
// цветом люминофора. Свои правила идут в конце с той же точностью селектора, что у ванильных, поэтому побеждают их.

using System.Linq;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.Stylesheets;
using Content.Shared._Dystopia.Lobby;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Dystopia.Lobby;

public sealed class DystopiaTerminalStylesheet : NanotrasenStylesheet
{

    private static readonly Color Black = Color.FromHex("#020302");

    // тема для строящегося листа: базовый конструктор уже читает палитры, а поля наследника, заданные инициализаторами,
    // заполняются раньше него — так тема попадает в палитры
    private static DystopiaLobbyThemePrototype? _building;
    private static int _buildingSize = 13;

    private readonly DystopiaLobbyThemePrototype _theme = _building ?? new DystopiaLobbyThemePrototype();
    private readonly int _textSize = _buildingSize;

    public override string StylesheetName => "DystopiaTerminal";

    public override ColorPalette PrimaryPalette => Make(_theme.Dim, _theme.Foreground, _theme.Foreground,
        _theme.Dim.WithAlpha(0.45f), _theme.Foreground);

    public override ColorPalette SecondaryPalette => Make(Mix(0.18f), Mix(0.28f), _theme.Dim, Mix(0.1f), _theme.Foreground);

    public override ColorPalette PositivePalette => PrimaryPalette;

    public override ColorPalette NegativePalette => Make(_theme.Alert.WithAlpha(0.8f), _theme.Alert, _theme.Alert,
        _theme.Alert.WithAlpha(0.4f), _theme.Alert);

    public override ColorPalette HighlightPalette => Make(_theme.Foreground, _theme.Foreground, _theme.Foreground,
        _theme.Dim, _theme.Foreground);

    private DystopiaTerminalStylesheet(StylesheetManager man) : base(new NoConfig(), man)
    {
        Stylesheet = new Stylesheet(base.Stylesheet.Rules.Concat(TerminalRules()).ToArray());
    }

    /// <summary>Лист для темы (строится заново на каждый вызов — кэширует вызывающий). Окна Города берут его же
    /// со своей палитрой и кеглем 12 (CityUi.Sheet).</summary>
    public static Stylesheet Build(DystopiaLobbyThemePrototype theme, int textSize = 13)
    {
        _building = theme;
        _buildingSize = textSize;
        try
        {
            var man = (StylesheetManager) IoCManager.Resolve<IStylesheetManager>();
            return new DystopiaTerminalStylesheet(man).Stylesheet;
        }
        finally
        {
            _building = null;
            _buildingSize = 13;
        }
    }

    private Color Mix(float amount)
    {
        return Color.InterpolateBetween(Black, _theme.Foreground, amount);
    }

    private ColorPalette Make(Color element, Color hovered, Color pressed, Color disabled, Color text)
    {
        return new ColorPalette(
            Base: element,
            LightnessShift: 0f,
            ChromaShift: 0f,
            Element: element,
            HoveredElement: hovered,
            PressedElement: pressed,
            DisabledElement: disabled,
            Background: Mix(0.08f),
            BackgroundLight: Mix(0.14f),
            BackgroundDark: Mix(0.04f),
            Text: text,
            TextDark: _theme.Dim);
    }

    private static StyleBoxFlat Frame(float fill, float horizontal = 10f, float vertical = 3f)
    {
        // белая рамка и заливка — цвет даёт modulate-self из палитры кнопки (обычная — тусклая, наведённая — яркая)
        var box = new StyleBoxFlat
        {
            BackgroundColor = Color.White.WithAlpha(fill),
            BorderColor = Color.White,
            BorderThickness = new Thickness(1f),
        };
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, horizontal);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, vertical);
        return box;
    }

    private IEnumerable<StyleRule> TerminalRules()
    {
        var fg = _theme.Foreground;
        var dim = _theme.Dim;
        var ink = DystopiaLobbyThemeController.Ink;
        var font = ResCache.GetFont(new[]
        {
            DystopiaLobbyThemeController.MonoFontPath,
            "/Fonts/NotoSans/NotoSans-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
        }, _textSize);

        var normal = Frame(0.08f);
        var small = Frame(0.08f, 6f, 1f);
        var hover = Frame(0.22f);
        var pressed = Frame(0.85f);

        var line = new StyleBoxFlat
        {
            BackgroundColor = fg.WithAlpha(0.04f),
            BorderColor = dim,
            BorderThickness = new Thickness(0f, 0f, 0f, 1f),
        };
        line.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5f);
        line.SetContentMarginOverride(StyleBox.Margin.Vertical, 3f);

        var tabPanel = new StyleBoxFlat
        {
            BackgroundColor = Black.WithAlpha(0.35f),
            BorderColor = dim,
            BorderThickness = new Thickness(1f),
        };
        tabPanel.SetContentMarginOverride(StyleBox.Margin.All, 8f);
        var tabOn = new StyleBoxFlat
        {
            BackgroundColor = fg.WithAlpha(0.16f),
            BorderColor = fg,
            BorderThickness = new Thickness(1f, 1f, 1f, 0f),
        };
        tabOn.SetContentMarginOverride(StyleBox.Margin.Horizontal, 12f);
        tabOn.SetContentMarginOverride(StyleBox.Margin.Vertical, 4f);
        var tabOff = new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            BorderColor = dim.WithAlpha(0.6f),
            BorderThickness = new Thickness(1f, 1f, 1f, 0f),
        };
        tabOff.SetContentMarginOverride(StyleBox.Margin.Horizontal, 12f);
        tabOff.SetContentMarginOverride(StyleBox.Margin.Vertical, 4f);

        return new StyleRule[]
        {
            // шрифт и цвет текста по умолчанию
            E().Prop(Label.StylePropertyFont, font),
            E<Label>().FontColor(fg),

            // кнопки: рамка вместо скруглённой текстуры, в том числе у сегментов и маленьких
            Btn().Box(normal),
            Btn().Class(StyleClass.ButtonOpenLeft).Box(normal),
            Btn().Class(StyleClass.ButtonOpenRight).Box(normal),
            Btn().Class(StyleClass.ButtonOpenBoth).Box(normal),
            Btn().Class(StyleClass.ButtonSquare).Box(normal),
            Btn().Class(StyleClass.ButtonSmall).Box(small),
            Btn().PseudoHovered().Box(hover),
            Btn().PseudoPressed().Box(pressed),

            // нажатая (выбранная) — залита, текст тёмный; выключенная — тусклая
            Btn().PseudoPressed().ParentOf(E<Label>()).FontColor(ink),
            Btn().PseudoPressed().ParentOf(E()).ParentOf(E<Label>()).FontColor(ink),
            Btn().PseudoDisabled().ParentOf(E<Label>()).FontColor(dim.WithAlpha(0.6f)),
            Btn().PseudoDisabled().ParentOf(E()).ParentOf(E<Label>()).FontColor(dim.WithAlpha(0.6f)),

            // поля ввода — линия снизу
            E<LineEdit>().Prop(LineEdit.StylePropertyStyleBox, line).Prop("font-color", fg),
            E<LineEdit>().Pseudo(LineEdit.StylePseudoClassPlaceholder).Prop("font-color", dim),
            E<LineEdit>().Class(LineEdit.StyleClassLineEditNotEditable).Prop("font-color", dim),
            E<TextEdit>().Pseudo(TextEdit.StylePseudoClassPlaceholder).Prop("font-color", dim),

            // вкладки — рамкой, выбранная подсвечена
            E<TabContainer>()
                .Prop(TabContainer.StylePropertyPanelStyleBox, tabPanel)
                .Prop(TabContainer.StylePropertyTabStyleBox, tabOn)
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, tabOff)
                .Prop(TabContainer.stylePropertyTabFontColor, fg)
                .Prop(TabContainer.StylePropertyTabFontColorInactive, dim),
        };
    }

    private static MutableSelectorElement Btn()
    {
        return E<ContainerButton>().Class(ContainerButton.StyleClassButton);
    }
}
