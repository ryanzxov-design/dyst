using Content.Client.UserInterface.Fragments;
using Content.Shared._Dystopia.Laws;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Laws;

/// <summary>
/// Программа КПК «Свод законов» (только чтение): вкладки «Статьи» и «Санкции».
/// </summary>
public sealed partial class CityLawsUi : UIFragment
{
    private CityLawsUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new CityLawsUiFragment();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CityLawsUiState cast)
            _fragment?.UpdateState(cast);
    }
}

public sealed class CityLawsUiFragment : BoxContainer
{
    private static readonly Color AccentColor = Color.FromHex("#78BEE1");
    private static readonly Color DimColor = Color.FromHex("#607A92");

    private readonly Button _articlesTab;
    private readonly Button _sanctionsTab;
    private readonly BoxContainer _content;
    private CityLawsUiState? _state;
    private bool _showSanctions;

    public CityLawsUiFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 4;

        var tabs = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
        _articlesTab = new Button { Text = Loc.GetString("dystopia-laws-tab-articles"), ToggleMode = true, HorizontalExpand = true };
        _sanctionsTab = new Button { Text = Loc.GetString("dystopia-laws-tab-sanctions"), ToggleMode = true, HorizontalExpand = true };
        _articlesTab.OnPressed += _ => { _showSanctions = false; Render(); };
        _sanctionsTab.OnPressed += _ => { _showSanctions = true; Render(); };
        tabs.AddChild(_articlesTab);
        tabs.AddChild(_sanctionsTab);
        AddChild(tabs);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        _content = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 2 };
        scroll.AddChild(_content);
        AddChild(scroll);

        Render();
    }

    public void UpdateState(CityLawsUiState state)
    {
        _state = state;
        Render();
    }

    private void Render()
    {
        _articlesTab.Pressed = !_showSanctions;
        _sanctionsTab.Pressed = _showSanctions;
        _content.RemoveAllChildren();

        if (_state == null)
            return;

        if (_showSanctions)
            RenderSanctions(_state);
        else
            RenderArticles(_state);
    }

    private void RenderArticles(CityLawsUiState state)
    {
        if (state.Laws.Count == 0)
        {
            _content.AddChild(new Label { Text = Loc.GetString("dystopia-laws-empty"), FontColorOverride = DimColor });
            return;
        }

        foreach (var law in state.Laws)
        {
            _content.AddChild(new Label
            {
                Text = Loc.GetString("dystopia-laws-article-header", ("number", law.Number), ("title", law.Title)),
                FontColorOverride = AccentColor,
                Margin = new Thickness(0, 6, 0, 0),
            });

            if (law.Text.Length > 0)
                _content.AddChild(MakeText(law.Text));

            if (law.Sanction.Length > 0)
                _content.AddChild(MakeText(Loc.GetString("dystopia-laws-article-sanction", ("sanction", law.Sanction))));

            _content.AddChild(new Label
            {
                Text = Loc.GetString("dystopia-laws-article-enacted", ("time", FormatTime(law.EnactedAt))),
                FontColorOverride = DimColor,
            });
        }
    }

    private void RenderSanctions(CityLawsUiState state)
    {
        foreach (var sanction in state.Sanctions)
        {
            _content.AddChild(new Label
            {
                Text = Loc.GetString("dystopia-laws-sanction-class", ("class", sanction.Class)),
                FontColorOverride = AccentColor,
                Margin = new Thickness(0, 6, 0, 0),
            });
            _content.AddChild(MakeText(Loc.GetString("dystopia-laws-sanction-legal", ("text", sanction.Legal))));
            _content.AddChild(MakeText(Loc.GetString("dystopia-laws-sanction-disciplinary", ("text", sanction.Disciplinary))));
        }

        if (state.GeneralProvision.Length > 0)
        {
            _content.AddChild(new Label
            {
                Text = Loc.GetString("dystopia-laws-general-provision"),
                FontColorOverride = AccentColor,
                Margin = new Thickness(0, 8, 0, 0),
            });
            _content.AddChild(MakeText(state.GeneralProvision));
        }
    }

    private static RichTextLabel MakeText(string text)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        return label;
    }

    public static string FormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}";
    }
}
