// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню парового котла (стиль Города): топка (топливо, погасить), режим (температура пара), прогрев,
// давление в барабане (тревога, предел), пар в трубу, стравливание, вода и повреждение.

using System.Globalization;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaBoilerWindow : CityWindow
{
    public event Action<int>? OnSetMode;
    public event Action? OnToggleVent;
    public event Action? OnExtinguish;

    private static readonly Color Good = Color.FromHex("#78E070");
    private static readonly Color Warn = Color.FromHex("#E0C040");
    private static readonly Color Fire = Color.FromHex("#FF9A50");

    private readonly Label _status;
    private readonly CityButton _extinguish;
    private readonly CityWrapLabel _fuel;
    private readonly GridContainer _modes;
    private readonly List<CityButton> _modeButtons = new();
    private readonly Label _modeInfo;
    private readonly Label _heat;
    private readonly Label _pressure;
    private readonly Label _steam;
    private readonly CityButton _vent;
    private readonly Label _water;
    private readonly Label _wear;

    private int _modeCount = -1;

    public DystopiaBoilerWindow()
    {
        WindowTitle = Loc.GetString("dystopia-boiler-ui-title");
        Subtitle = Loc.GetString("dystopia-industry-ui-sub");
        Slogan = Loc.GetString("dystopia-industry-ui-slogan");
        MinSize = new Vector2(520, 540);
        SetSize = new Vector2(560, 600);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _status = CityUi.MakeLabel(string.Empty, CityUi.Accent, CityUi.Bold(13));
        _status.HorizontalExpand = true;
        _extinguish = CityUi.MakeButton(Loc.GetString("dystopia-boiler-ui-extinguish"), CityButtonStyle.Danger);
        _extinguish.OnPressed += _ => OnExtinguish?.Invoke();
        top.AddChild(_status);
        top.AddChild(_extinguish);
        root.AddChild(top);

        // Топка
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-boiler-ui-firebox-header")));
        _fuel = CityUi.Hint(string.Empty, CityUi.Text);
        root.AddChild(_fuel);
        root.AddChild(CityUi.Hint(Loc.GetString("dystopia-boiler-ui-fuel-hint"), CityUi.Muted));

        // Режим
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-boiler-ui-mode-header")));
        _modes = new GridContainer { Columns = 3, HorizontalExpand = true };
        root.AddChild(_modes);
        _modeInfo = CityUi.MakeLabel(string.Empty, CityUi.Dim);
        root.AddChild(_modeInfo);
        root.AddChild(CityUi.Hint(Loc.GetString("dystopia-boiler-ui-mode-hint"), CityUi.Muted));

        // Барабан
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-boiler-ui-drum-header")));
        _heat = CityUi.MakeLabel(string.Empty);
        _pressure = CityUi.MakeLabel(string.Empty, null, CityUi.Bold(13));
        _steam = CityUi.MakeLabel(string.Empty);
        root.AddChild(_heat);
        root.AddChild(_pressure);
        root.AddChild(_steam);
        var ventRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var ventHint = CityUi.MakeLabel(Loc.GetString("dystopia-boiler-ui-vent-hint"), CityUi.Muted);
        ventHint.HorizontalExpand = true;
        ventHint.ClipText = true;
        _vent = CityUi.MakeButton(Loc.GetString("dystopia-boiler-ui-vent-open"));
        _vent.OnPressed += _ => OnToggleVent?.Invoke();
        ventRow.AddChild(ventHint);
        ventRow.AddChild(_vent);
        root.AddChild(ventRow);

        // Вода и состояние
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-boiler-ui-water-header")));
        _water = CityUi.MakeLabel(string.Empty);
        _wear = CityUi.MakeLabel(string.Empty);
        root.AddChild(_water);
        root.AddChild(_wear);
        root.AddChild(CityUi.Hint(Loc.GetString("dystopia-boiler-ui-water-hint"), CityUi.Muted));
    }

    private static string Fmt(float value, string format = "0.#")
    {
        return Math.Round(value, 2).ToString(format, CultureInfo.InvariantCulture);
    }

    private void RebuildModes(DystopiaBoilerUiState state)
    {
        _modes.RemoveAllChildren();
        _modeButtons.Clear();
        _modes.Columns = Math.Max(1, state.Modes.Count);
        for (var i = 0; i < state.Modes.Count; i++)
        {
            var index = i;
            var button = CityUi.MakeButton($"{Fmt(state.Modes[i].Temperature, "0")} °C", CityButtonStyle.Tab);
            button.HorizontalExpand = true;
            button.OnPressed += _ => OnSetMode?.Invoke(index);
            _modeButtons.Add(button);
            _modes.AddChild(button);
        }

        _modeCount = state.Modes.Count;
    }

    public void UpdateState(DystopiaBoilerUiState state)
    {
        if (_modeCount != state.Modes.Count)
            RebuildModes(state);

        (string key, Color color) = state.Status switch
        {
            DystopiaBoilerStatus.Producing => ("dystopia-boiler-ui-status-producing", Good),
            DystopiaBoilerStatus.Warming => ("dystopia-boiler-ui-status-warming", Fire),
            DystopiaBoilerStatus.NoWater => ("dystopia-boiler-ui-status-nowater", CityUi.Danger),
            DystopiaBoilerStatus.Danger => ("dystopia-boiler-ui-status-danger", CityUi.Danger),
            _ => ("dystopia-boiler-ui-status-cold", CityUi.Dim),
        };
        _status.Text = Loc.GetString(key);
        _status.FontColorOverride = color;
        _extinguish.Disabled = !state.Lit;

        _fuel.Text = Loc.GetString("dystopia-boiler-ui-fuel", ("fuel", Fmt(state.Fuel, "0")),
            ("max", Fmt(state.MaxFuel, "0")), ("rate", Fmt(state.FuelRate, "0.##")),
            ("minutes", state.FuelRate > 0f ? Fmt(state.Fuel / state.FuelRate / 60f, "0") : "—"));
        _fuel.FontColorOverride = state.Fuel > 0f ? CityUi.Text : CityUi.Danger;

        for (var i = 0; i < _modeButtons.Count; i++)
            _modeButtons[i].Active = i == state.Mode;

        if (state.Mode >= 0 && state.Mode < state.Modes.Count)
        {
            var mode = state.Modes[state.Mode];
            _modeInfo.Text = Loc.GetString("dystopia-boiler-ui-mode-info", ("steam", Fmt(mode.Steam)),
                ("fuel", Fmt(mode.Fuel, "0.##")));
        }

        _heat.Text = Loc.GetString("dystopia-boiler-ui-heat", ("heat", Fmt(state.Heat * 100f, "0")));
        _heat.FontColorOverride = state.Heat >= 1f ? Good : CityUi.Text;
        _pressure.Text = Loc.GetString("dystopia-boiler-ui-pressure", ("pressure", Fmt(state.Pressure, "0")),
            ("warn", Fmt(state.WarnPressure, "0")), ("max", Fmt(state.DamagePressure, "0")));
        _pressure.FontColorOverride = state.Pressure > state.DamagePressure ? CityUi.Danger
            : state.Pressure > state.WarnPressure ? Warn : CityUi.Glow;
        _steam.Text = Loc.GetString(state.SteamConnected ? "dystopia-boiler-ui-steam" : "dystopia-boiler-ui-steam-disconnected",
            ("produced", Fmt(state.ProducedRate, "0.##")), ("out", Fmt(state.SteamRate, "0.##")),
            ("pipe", Fmt(state.PipePressure, "0")), ("drum", Fmt(state.Steam, "0")));
        _steam.FontColorOverride = state.SteamConnected ? CityUi.Text : Warn;
        _vent.Text = Loc.GetString(state.Venting ? "dystopia-boiler-ui-vent-close" : "dystopia-boiler-ui-vent-open").ToUpperInvariant();
        _vent.Active = state.Venting;

        _water.Text = Loc.GetString("dystopia-boiler-ui-water", ("amount", Fmt(state.Water, "0")),
            ("max", Fmt(state.WaterMax, "0")), ("rate", Fmt(state.WaterRate, "0.#")));
        _water.FontColorOverride = state.Water > 0f ? CityUi.Text : CityUi.Danger;
        _wear.Text = Loc.GetString("dystopia-boiler-ui-wear", ("wear", Fmt(state.Wear * 100f, "0")));
        _wear.FontColorOverride = state.Wear > 0.6f ? CityUi.Danger : state.Wear > 0.3f ? Warn : CityUi.Text;
    }
}
