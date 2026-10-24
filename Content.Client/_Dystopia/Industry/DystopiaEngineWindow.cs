// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню пульта промышленного двигателя (стиль Города): состояние и пуск, ряд (цилиндры, радиаторы),
// мощность и подача (больше 100% — перегрузка), температура и износ, баки (топливо, масло, вода, пар) и расход.

using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaEngineWindow : CityWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action? OnToggle;
    public event Action<float>? OnSetThrottle;

    private static readonly Color Good = Color.FromHex("#78E070");
    private static readonly Color Warn = Color.FromHex("#E0C040");

    /// <summary>Готовые значения подачи; больше 100% — перегрузка.</summary>
    private static readonly float[] Presets = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f };

    private readonly Label _status;
    private readonly CityButton _toggle;
    private readonly Label _row;
    private readonly Label _power;
    private readonly Label _throttle;
    private readonly List<(float Value, CityButton Button)> _presetButtons = new();
    private readonly Label _temperature;
    private readonly Label _wear;
    private readonly BoxContainer _tanks;

    private DystopiaEngineUiState? _state;

    public DystopiaEngineWindow()
    {
        IoCManager.InjectDependencies(this);

        WindowTitle = Loc.GetString("dystopia-engine-ui-title");
        Subtitle = Loc.GetString("dystopia-industry-ui-sub");
        Slogan = Loc.GetString("dystopia-industry-ui-slogan");
        MinSize = new Vector2(520, 560);
        SetSize = new Vector2(560, 640);

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
        _toggle = CityUi.MakeButton(Loc.GetString("dystopia-engine-ui-start"), CityButtonStyle.Primary);
        _toggle.OnPressed += _ => OnToggle?.Invoke();
        top.AddChild(_status);
        top.AddChild(_toggle);
        root.AddChild(top);
        _row = CityUi.MakeLabel(string.Empty, CityUi.Dim);
        root.AddChild(_row);

        // Мощность и подача
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-engine-ui-power-header")));
        _power = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Bold(13));
        root.AddChild(_power);
        var throttleRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _throttle = CityUi.MakeLabel(string.Empty);
        _throttle.HorizontalExpand = true;
        var minus = CityUi.MakeButton("−10%");
        minus.OnPressed += _ => Step(-0.1f);
        var plus = CityUi.MakeButton("+10%");
        plus.OnPressed += _ => Step(0.1f);
        throttleRow.AddChild(_throttle);
        throttleRow.AddChild(minus);
        throttleRow.AddChild(plus);
        root.AddChild(throttleRow);
        var presets = new GridContainer { Columns = Presets.Length, HorizontalExpand = true };
        foreach (var value in Presets)
        {
            var button = CityUi.MakeButton($"{value * 100:0}%", value > 1f ? CityButtonStyle.Danger : CityButtonStyle.Tab);
            button.HorizontalExpand = true;
            var v = value;
            button.OnPressed += _ => OnSetThrottle?.Invoke(v);
            _presetButtons.Add((value, button));
            presets.AddChild(button);
        }

        root.AddChild(presets);
        root.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-engine-ui-overload-hint"), CityUi.Muted));

        // Состояние
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-engine-ui-condition-header")));
        _temperature = CityUi.MakeLabel(string.Empty);
        _wear = CityUi.MakeLabel(string.Empty);
        root.AddChild(_temperature);
        root.AddChild(_wear);

        // Баки
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _tanks = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        scroll.AddChild(_tanks);
        root.AddChild(scroll);
    }

    private void Step(float delta)
    {
        if (_state == null)
            return;

        OnSetThrottle?.Invoke(Math.Clamp(MathF.Round((_state.Throttle + delta) * 20f) / 20f, 0f, _state.MaxThrottle));
    }

    private static string Fmt(float value, string format = "0.#")
    {
        return Math.Round(value, 2).ToString(format, CultureInfo.InvariantCulture);
    }

    private string ReagentName(string id)
    {
        return _proto.TryIndex<ReagentPrototype>(id, out var proto) ? proto.LocalizedName : id;
    }

    public void UpdateState(DystopiaEngineUiState state)
    {
        _state = state;

        (string key, Color color) = state.Status switch
        {
            DystopiaEngineStatus.Running => ("dystopia-engine-ui-status-running", Good),
            DystopiaEngineStatus.NoFuel => ("dystopia-engine-ui-status-nofuel", CityUi.Danger),
            DystopiaEngineStatus.ShortRow => ("dystopia-engine-ui-status-shortrow", CityUi.Danger),
            DystopiaEngineStatus.Seized => ("dystopia-engine-ui-status-seized", CityUi.Danger),
            _ => ("dystopia-engine-ui-status-off", CityUi.Dim),
        };
        _status.Text = Loc.GetString(key);
        _status.FontColorOverride = color;
        _toggle.Text = Loc.GetString(state.On ? "dystopia-engine-ui-stop" : "dystopia-engine-ui-start").ToUpperInvariant();

        _row.Text = Loc.GetString("dystopia-engine-ui-row", ("cylinders", state.Cylinders),
            ("min", state.MinCylinders), ("max", state.MaxCylinders), ("radiators", state.Radiators));
        _power.Text = Loc.GetString("dystopia-engine-ui-power",
            ("power", Fmt(state.Power / 1000f)), ("max", Fmt(state.MaxPower / 1000f)));
        _throttle.Text = Loc.GetString("dystopia-engine-ui-throttle", ("throttle", Fmt(state.Throttle * 100f, "0")));
        _throttle.FontColorOverride = state.Throttle > 1f ? CityUi.Danger : CityUi.Text;
        foreach (var (value, button) in _presetButtons)
            button.Active = MathF.Abs(value - state.Throttle) < 0.01f;

        _temperature.Text = Loc.GetString("dystopia-engine-ui-temperature", ("temperature", Fmt(state.Temperature, "0")),
            ("warn", Fmt(state.WarnTemperature, "0")), ("overheat", Fmt(state.OverheatTemperature, "0")));
        _temperature.FontColorOverride = state.Temperature > state.OverheatTemperature ? CityUi.Danger
            : state.Temperature > state.WarnTemperature ? Warn : Good;
        _wear.Text = Loc.GetString("dystopia-engine-ui-wear", ("wear", Fmt(state.Wear * 100f, "0")));
        _wear.FontColorOverride = state.Wear > 0.75f ? CityUi.Danger : state.Wear > 0.4f ? Warn : CityUi.Text;

        RebuildTanks(state);
    }

    private void RebuildTanks(DystopiaEngineUiState state)
    {
        _tanks.RemoveAllChildren();
        _tanks.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-engine-ui-tanks-header")));

        var fuelTotal = state.Fuel.Sum(f => f.Amount);
        _tanks.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-engine-ui-fuel",
            ("amount", Fmt(fuelTotal)), ("max", Fmt(state.FuelMax)), ("rate", Fmt(state.FuelRate, "0.##"))),
            fuelTotal > 0f ? CityUi.Text : CityUi.Danger));
        foreach (var fuel in state.Fuel.GroupBy(f => f.Id))
        {
            _tanks.AddChild(CityUi.MakeLabel($"    {ReagentName(fuel.Key)} — {Fmt(fuel.Sum(f => f.Amount))}", CityUi.Dim));
        }

        _tanks.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-engine-ui-oil",
            ("amount", Fmt(state.Oil)), ("max", Fmt(state.OilMax)), ("rate", Fmt(state.OilRate, "0.###"))),
            state.Oil > 0f ? CityUi.Text : CityUi.Danger));
        _tanks.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-engine-ui-water",
            ("amount", Fmt(state.Water)), ("max", Fmt(state.WaterMax)), ("rate", Fmt(state.WaterRate, "0.##")))));
        _tanks.AddChild(CityUi.MakeLabel(Loc.GetString(state.SteamConnected
                ? "dystopia-engine-ui-steam"
                : "dystopia-engine-ui-steam-disconnected",
            ("amount", Fmt(state.Steam)), ("max", Fmt(state.SteamMax)), ("pressure", Fmt(state.SteamPressure, "0"))),
            state.SteamConnected ? CityUi.Text : Warn));
        _tanks.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-engine-ui-tanks-hint"), CityUi.Muted));
    }
}
