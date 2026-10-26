// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню паровой турбины (стиль Города): пуск, клапан (расход пара), обороты и мощность, пар на входе и
// выхлопе, подключения (патрубки, клемма), масло и износ.

using System.Globalization;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaSteamTurbineWindow : CityWindow
{
    public event Action? OnToggle;
    public event Action<float>? OnSetValve;

    private static readonly Color Good = Color.FromHex("#78E070");
    private static readonly Color Warn = Color.FromHex("#E0C040");

    private static readonly float[] Presets = { 0f, 0.25f, 0.5f, 0.75f, 1f };

    private readonly Label _status;
    private readonly CityButton _toggle;
    private readonly Label _power;
    private readonly Label _speed;
    private readonly Label _valve;
    private readonly List<(float Value, CityButton Button)> _presetButtons = new();
    private readonly Label _inlet;
    private readonly Label _exhaust;
    private readonly Label _links;
    private readonly Label _oil;
    private readonly Label _wear;

    private DystopiaSteamTurbineUiState? _state;

    public DystopiaSteamTurbineWindow()
    {
        WindowTitle = Loc.GetString("dystopia-turbine-ui-title");
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
        _toggle = CityUi.MakeButton(Loc.GetString("dystopia-turbine-ui-start"), CityButtonStyle.Primary);
        _toggle.OnPressed += _ => OnToggle?.Invoke();
        top.AddChild(_status);
        top.AddChild(_toggle);
        root.AddChild(top);

        // Мощность и клапан
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-turbine-ui-power-header")));
        _power = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Bold(13));
        _speed = CityUi.MakeLabel(string.Empty);
        root.AddChild(_power);
        root.AddChild(_speed);
        var valveRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _valve = CityUi.MakeLabel(string.Empty);
        _valve.HorizontalExpand = true;
        var minus = CityUi.MakeButton("−5%");
        minus.OnPressed += _ => Step(-0.05f);
        var plus = CityUi.MakeButton("+5%");
        plus.OnPressed += _ => Step(0.05f);
        valveRow.AddChild(_valve);
        valveRow.AddChild(minus);
        valveRow.AddChild(plus);
        root.AddChild(valveRow);
        var presets = new GridContainer { Columns = Presets.Length, HorizontalExpand = true };
        foreach (var value in Presets)
        {
            var button = CityUi.MakeButton($"{value * 100:0}%", CityButtonStyle.Tab);
            button.HorizontalExpand = true;
            var v = value;
            button.OnPressed += _ => OnSetValve?.Invoke(v);
            _presetButtons.Add((value, button));
            presets.AddChild(button);
        }

        root.AddChild(presets);
        root.AddChild(CityUi.Hint(Loc.GetString("dystopia-turbine-ui-valve-hint"), CityUi.Muted));

        // Пар
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-turbine-ui-steam-header")));
        _inlet = CityUi.MakeLabel(string.Empty);
        _exhaust = CityUi.MakeLabel(string.Empty);
        _links = CityUi.MakeLabel(string.Empty, CityUi.Dim);
        root.AddChild(_inlet);
        root.AddChild(_exhaust);
        root.AddChild(_links);

        // Масло и износ
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-turbine-ui-condition-header")));
        _oil = CityUi.MakeLabel(string.Empty);
        _wear = CityUi.MakeLabel(string.Empty);
        root.AddChild(_oil);
        root.AddChild(_wear);
        root.AddChild(CityUi.Hint(Loc.GetString("dystopia-turbine-ui-oil-hint"), CityUi.Muted));
    }

    private void Step(float delta)
    {
        if (_state == null)
            return;

        OnSetValve?.Invoke(Math.Clamp(MathF.Round((_state.Valve + delta) * 20f) / 20f, 0f, 1f));
    }

    private static string Fmt(float value, string format = "0.#")
    {
        return Math.Round(value, 2).ToString(format, CultureInfo.InvariantCulture);
    }

    private static string Celsius(float kelvin)
    {
        return Fmt(kelvin - Atmospherics.T0C, "0");
    }

    private static string Link(bool connected)
    {
        return Loc.GetString(connected ? "dystopia-turbine-ui-link-yes" : "dystopia-turbine-ui-link-no");
    }

    public void UpdateState(DystopiaSteamTurbineUiState state)
    {
        _state = state;

        (string key, Color color) = state.Status switch
        {
            DystopiaSteamTurbineStatus.Running => ("dystopia-turbine-ui-status-running", Good),
            DystopiaSteamTurbineStatus.NoSteam => ("dystopia-turbine-ui-status-nosteam", Warn),
            DystopiaSteamTurbineStatus.Blocked => ("dystopia-turbine-ui-status-blocked", CityUi.Danger),
            DystopiaSteamTurbineStatus.Seized => ("dystopia-turbine-ui-status-seized", CityUi.Danger),
            _ => ("dystopia-turbine-ui-status-off", CityUi.Dim),
        };
        _status.Text = Loc.GetString(key);
        _status.FontColorOverride = color;
        _toggle.Text = Loc.GetString(state.On ? "dystopia-turbine-ui-stop" : "dystopia-turbine-ui-start").ToUpperInvariant();

        _power.Text = Loc.GetString("dystopia-turbine-ui-power",
            ("power", Fmt(state.Power / 1000f)), ("max", Fmt(state.MaxPower / 1000f, "0")));
        _speed.Text = Loc.GetString("dystopia-turbine-ui-speed", ("speed", Fmt(state.Speed * 100f, "0")),
            ("flow", Fmt(state.Flow, "0.##")), ("max", Fmt(state.MaxFlow, "0.#")));
        _valve.Text = Loc.GetString("dystopia-turbine-ui-valve", ("valve", Fmt(state.Valve * 100f, "0")));
        foreach (var (value, button) in _presetButtons)
            button.Active = MathF.Abs(value - state.Valve) < 0.01f;

        _inlet.Text = Loc.GetString("dystopia-turbine-ui-inlet", ("temperature", Celsius(state.InletTemperature)),
            ("pressure", Fmt(state.InletPressure, "0")), ("max", Celsius(state.MaxInletTemperature)));
        _inlet.FontColorOverride = state.InletTemperature > state.MaxInletTemperature ? CityUi.Danger : CityUi.Text;
        _exhaust.Text = Loc.GetString("dystopia-turbine-ui-exhaust", ("temperature", Celsius(state.ExhaustTemperature)),
            ("pressure", Fmt(state.ExhaustPressure, "0")), ("max", Fmt(state.MaxExhaustPressure, "0")));
        _exhaust.FontColorOverride = state.ExhaustPressure >= state.MaxExhaustPressure ? CityUi.Danger : CityUi.Text;
        _links.Text = Loc.GetString("dystopia-turbine-ui-links", ("inlet", Link(state.InletConnected)),
            ("outlet", Link(state.OutletConnected)), ("power", Link(state.PowerConnected)));

        _oil.Text = Loc.GetString("dystopia-turbine-ui-oil", ("amount", Fmt(state.Oil, "0.#")),
            ("max", Fmt(state.OilMax, "0")), ("rate", Fmt(state.OilRate, "0.###")));
        _oil.FontColorOverride = state.Oil > 0f ? CityUi.Text : CityUi.Danger;
        _wear.Text = Loc.GetString("dystopia-turbine-ui-wear", ("wear", Fmt(state.Wear * 100f, "0")));
        _wear.FontColorOverride = state.Wear > 0.75f ? CityUi.Danger : state.Wear > 0.4f ? Warn : CityUi.Text;
    }
}
