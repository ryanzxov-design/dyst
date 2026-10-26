// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: терминал нефтепровода — трасса, режим (отправка / приём / стоп), расход насоса, бак, течи.

using System.Globalization;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaPipelineWindow : CityWindow
{
    public event Action<DystopiaPipelineMode>? OnMode;
    public event Action<float>? OnRate;

    private static readonly Color Good = Color.FromHex("#78E070");

    private readonly CityWrapLabel _route;
    private readonly Label _state;
    private readonly Label _tank;
    private readonly Label _contents;
    private readonly Label _flow;
    private readonly Label _rate;
    private readonly Dictionary<DystopiaPipelineMode, CityButton> _modes = new();
    private DystopiaPipelineUiState? _last;

    public DystopiaPipelineWindow()
    {
        WindowTitle = Loc.GetString("dystopia-pipeline-ui-title");
        Subtitle = Loc.GetString("dystopia-industry-ui-sub");
        Slogan = Loc.GetString("dystopia-industry-ui-slogan");
        MinSize = new Vector2(460, 340);
        SetSize = new Vector2(480, 360);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        Contents.AddChild(root);

        _route = CityUi.Hint(string.Empty, CityUi.Accent);
        _state = CityUi.MakeLabel(string.Empty);
        root.AddChild(_route);
        root.AddChild(_state);

        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-pipeline-ui-mode")));
        var modes = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        foreach (var mode in new[] { DystopiaPipelineMode.Send, DystopiaPipelineMode.Receive, DystopiaPipelineMode.Stop })
        {
            var b = CityUi.MakeButton(Loc.GetString($"dystopia-pipeline-mode-{mode.ToString().ToLowerInvariant()}"),
                CityButtonStyle.Tab);
            b.HorizontalExpand = true;
            var m = mode;
            b.OnPressed += _ => OnMode?.Invoke(m);
            _modes[mode] = b;
            modes.AddChild(b);
        }

        root.AddChild(modes);

        var rateRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        var minus = CityUi.MakeButton("−5");
        minus.OnPressed += _ => { if (_last != null) OnRate?.Invoke(MathF.Max(0f, _last.Rate - 5f)); };
        _rate = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Bold(12));
        _rate.HorizontalExpand = true;
        _rate.Align = Label.AlignMode.Center;
        var plus = CityUi.MakeButton("+5");
        plus.OnPressed += _ => { if (_last != null) OnRate?.Invoke(MathF.Min(_last.MaxRate, _last.Rate + 5f)); };
        rateRow.AddChild(minus);
        rateRow.AddChild(_rate);
        rateRow.AddChild(plus);
        root.AddChild(rateRow);

        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-pipeline-ui-tank")));
        _tank = CityUi.MakeLabel(string.Empty);
        _contents = CityUi.MakeLabel(string.Empty, CityUi.Dim);
        _contents.ClipText = true;
        _flow = CityUi.MakeLabel(string.Empty);
        root.AddChild(_tank);
        root.AddChild(_contents);
        root.AddChild(_flow);
    }

    private static string Fmt(float v, string f = "0")
    {
        return v.ToString(f, CultureInfo.InvariantCulture);
    }

    public void UpdateState(DystopiaPipelineUiState state)
    {
        _last = state;
        _route.Text = state.Linked
            ? Loc.GetString("dystopia-pipeline-ui-linked", ("length", state.Length))
            : Loc.GetString("dystopia-pipeline-ui-unlinked");
        _route.FontColorOverride = state.Linked ? CityUi.Accent : CityUi.Danger;

        string key;
        var danger = true;
        if (state.Broken)
            key = "dystopia-pipeline-ui-broken";
        else if (state.Leaks > 0)
            key = "dystopia-pipeline-ui-leaks";
        else if (!state.Powered)
            key = "dystopia-pipeline-ui-no-power";
        else
        {
            key = "dystopia-pipeline-ui-ok";
            danger = false;
        }

        _state.Text = Loc.GetString(key, ("leaks", state.Leaks));
        _state.FontColorOverride = danger ? CityUi.Danger : Good;

        foreach (var (mode, button) in _modes)
        {
            button.Active = mode == state.Mode;
            button.Disabled = !state.Linked;
        }

        _rate.Text = Loc.GetString("dystopia-pipeline-ui-rate", ("rate", Fmt(state.Rate)), ("max", Fmt(state.MaxRate)));
        _tank.Text = Loc.GetString("dystopia-pipeline-ui-volume", ("volume", Fmt(state.Volume)), ("max", Fmt(state.MaxVolume)));
        _contents.Text = state.Contents.Length > 0 ? state.Contents : Loc.GetString("dystopia-pipeline-ui-empty");
        _flow.Text = Loc.GetString("dystopia-pipeline-ui-flow", ("flow", Fmt(state.Flow, "0.#")));
    }
}
