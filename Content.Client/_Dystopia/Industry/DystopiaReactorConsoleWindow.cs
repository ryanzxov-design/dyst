// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: пульт реактора (стиль Города). Три колонки фиксированной ширины, чтобы ничего не наезжало:
// слева приборы (мощность с уставкой, самая горячая колонна, вода, давление барабанов), посередине картограмма
// с масштабом и выбранная колонна, справа общая шкала стержней (группы 1–4 и все) с кнопками и автоматика.
// Сверху — состояние, «Снять АЗ» и АЗ-5. Пояснения — во всплывающих подсказках.

using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaReactorConsoleWindow : CityWindow
{
    public event Action<NetEntity, float>? OnSetRod;
    public event Action<int, float>? OnSetGroup;
    public event Action<int, float>? OnStepGroup;
    public event Action<NetEntity, int>? OnAssignGroup;
    public event Action? OnScram;
    public event Action? OnResetScram;
    public event Action<bool, float>? OnAutoRegulator;
    public event Action<bool>? OnAutoScram;

    private const float SideWidth = 300f;
    private const float GaugeWidth = 250f;
    private const float SetpointStep = 250f;

    private static readonly Color Good = Color.FromHex("#78E070");
    private static readonly Color Warn = Color.FromHex("#E0C040");
    private static readonly Color PowerColor = Color.FromHex("#58C070");
    private static readonly Color WaterColor = Color.FromHex("#3C78C8");
    private static readonly Color SteamColor = Color.FromHex("#9CC8DC");

    private readonly Label _status;
    private readonly DystopiaBarGauge _power;
    private readonly DystopiaBarGauge _temperature;
    private readonly DystopiaBarGauge _water;
    private readonly DystopiaBarGauge _pressure;
    private readonly Label _steam;
    private readonly Label _xenon;
    private readonly DystopiaReactorMap _map;
    private readonly Label _selTitle;
    private readonly Label _selLine;
    private readonly BoxContainer _rodRow;
    private readonly List<CityButton> _groupButtons = new();
    private readonly DystopiaRodScale _rods;
    private readonly CityButton _arToggle;
    private readonly Label _arSetpoint;
    private readonly CityButton _autoScram;

    private DystopiaReactorConsoleUiState? _state;

    public DystopiaReactorConsoleWindow()
    {
        WindowTitle = Loc.GetString("dystopia-reactor-ui-title");
        Subtitle = Loc.GetString("dystopia-industry-ui-sub");
        Slogan = Loc.GetString("dystopia-industry-ui-slogan");
        MinSize = new Vector2(1000, 640);
        SetSize = new Vector2(1080, 700);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        Contents.AddChild(root);

        // ---------------------------------------------------------------- шапка
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _status = CityUi.MakeLabel(string.Empty, CityUi.Accent, CityUi.Bold(14));
        _status.HorizontalExpand = true;
        _status.ClipText = true;
        var reset = CityUi.MakeButton(Loc.GetString("dystopia-reactor-ui-reset-scram"));
        reset.ToolTip = Loc.GetString("dystopia-reactor-ui-reset-scram-tip");
        reset.OnPressed += _ => OnResetScram?.Invoke();
        var scram = CityUi.MakeButton(Loc.GetString("dystopia-reactor-ui-scram"), CityButtonStyle.Danger);
        scram.ToolTip = Loc.GetString("dystopia-reactor-ui-scram-tip");
        scram.MinWidth = 90;
        scram.OnPressed += _ => OnScram?.Invoke();
        top.AddChild(_status);
        top.AddChild(reset);
        top.AddChild(scram);
        root.AddChild(top);
        root.AddChild(CityUi.Separator());

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            SeparationOverride = 10,
        };
        root.AddChild(body);

        // ---------------------------------------------------------------- приборы
        var gauges = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SetWidth = GaugeWidth,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        gauges.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-reactor-ui-gauges")));
        var bars = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            SeparationOverride = 4,
        };
        _power = new DystopiaBarGauge { Title = Loc.GetString("dystopia-reactor-ui-gauge-power"), BarColor = PowerColor };
        _power.ToolTip = Loc.GetString("dystopia-reactor-ui-gauge-power-tip");
        _temperature = new DystopiaBarGauge { Title = Loc.GetString("dystopia-reactor-ui-gauge-temperature") };
        _temperature.ToolTip = Loc.GetString("dystopia-reactor-ui-gauge-temperature-tip");
        _water = new DystopiaBarGauge { Title = Loc.GetString("dystopia-reactor-ui-gauge-water"), BarColor = WaterColor };
        _water.ToolTip = Loc.GetString("dystopia-reactor-ui-gauge-water-tip");
        _pressure = new DystopiaBarGauge { Title = Loc.GetString("dystopia-reactor-ui-gauge-pressure"), BarColor = SteamColor };
        _pressure.ToolTip = Loc.GetString("dystopia-reactor-ui-gauge-pressure-tip");
        bars.AddChild(_power);
        bars.AddChild(_temperature);
        bars.AddChild(_water);
        bars.AddChild(_pressure);
        gauges.AddChild(bars);
        _steam = CityUi.MakeLabel(string.Empty);
        _steam.ClipText = true;
        _xenon = CityUi.MakeLabel(string.Empty, CityUi.Dim);
        _xenon.ClipText = true;
        _xenon.ToolTip = Loc.GetString("dystopia-reactor-ui-xenon-tip");
        gauges.AddChild(_steam);
        gauges.AddChild(_xenon);
        body.AddChild(gauges);

        // ---------------------------------------------------------------- картограмма
        var center = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        var mapHead = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        var mapTitle = new Control { HorizontalExpand = true };
        mapTitle.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-reactor-ui-map")));
        mapHead.AddChild(mapTitle);
        var zoomOut = CityUi.MakeButton("−");
        zoomOut.MinWidth = 28;
        zoomOut.ToolTip = Loc.GetString("dystopia-reactor-ui-zoom-out");
        var zoomIn = CityUi.MakeButton("+");
        zoomIn.MinWidth = 28;
        zoomIn.ToolTip = Loc.GetString("dystopia-reactor-ui-zoom-in");
        zoomOut.OnPressed += _ => Zoom(-6f);
        zoomIn.OnPressed += _ => Zoom(6f);
        mapHead.AddChild(zoomOut);
        mapHead.AddChild(zoomIn);
        center.AddChild(mapHead);

        var mapFrame = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 4);
        mapFrame.VerticalExpand = true;
        mapFrame.HorizontalExpand = true;
        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        _map = new DystopiaReactorMap { CellSize = 32f };
        _map.ToolTip = Loc.GetString("dystopia-reactor-ui-legend");
        _map.OnSelect += _ => UpdateSelected();
        scroll.AddChild(_map);
        mapFrame.AddChild(scroll);
        center.AddChild(mapFrame);

        // выбранная колонна
        var selPanel = CityUi.MakePanel(CityUi.Panel, CityUi.Line, 6);
        var sel = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        _selTitle = CityUi.MakeLabel(string.Empty, CityUi.Accent, CityUi.Bold(12));
        _selTitle.ClipText = true;
        _selLine = CityUi.MakeLabel(string.Empty);
        _selLine.ClipText = true;
        sel.AddChild(_selTitle);
        sel.AddChild(_selLine);
        _rodRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4, Visible = false };
        foreach (var (text, delta) in new[] { ("▲ 10%", -0.1f), ("▼ 10%", 0.1f) })
        {
            var b = CityUi.MakeButton(text);
            var d = delta;
            b.OnPressed += _ =>
            {
                if (SelectedCell() is { } cell)
                    OnSetRod?.Invoke(cell.Entity, Math.Clamp(cell.Target + d, 0f, 1f));
            };
            _rodRow.AddChild(b);
        }

        _rodRow.AddChild(new Control { MinWidth = 12 });
        _rodRow.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-reactor-ui-cell-group"), CityUi.Dim));
        for (var g = 1; g <= 4; g++)
        {
            var group = g;
            var b = CityUi.MakeButton(g.ToString());
            b.MinWidth = 30;
            b.OnPressed += _ =>
            {
                if (SelectedCell() is { } cell)
                    OnAssignGroup?.Invoke(cell.Entity, group);
            };
            _groupButtons.Add(b);
            _rodRow.AddChild(b);
        }

        sel.AddChild(_rodRow);
        selPanel.AddChild(sel);
        center.AddChild(selPanel);
        body.AddChild(center);

        // ---------------------------------------------------------------- стержни и автоматика
        var side = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SetWidth = SideWidth,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        side.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-reactor-ui-rods")));
        _rods = new DystopiaRodScale { VerticalExpand = true };
        _rods.ToolTip = Loc.GetString("dystopia-reactor-ui-rods-tip");
        side.AddChild(_rods);

        // кнопки под колонками шкалы: ▲ поднять / ▼ опустить на 10% (группа 0 — все)
        var steps = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 0 };
        steps.AddChild(new Control { MinWidth = 34 });
        foreach (var group in new[] { 1, 2, 3, 4, 0 })
        {
            var cell = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                SeparationOverride = 2,
            };
            foreach (var (text, delta) in new[] { ("▲", -0.1f), ("▼", 0.1f) })
            {
                var b = CityUi.MakeButton(text);
                b.HorizontalAlignment = HAlignment.Center;
                b.MinWidth = 40;
                var d = delta;
                var g = group;
                b.OnPressed += _ => OnStepGroup?.Invoke(g, d);
                cell.AddChild(b);
            }

            steps.AddChild(cell);
        }

        side.AddChild(steps);
        var all = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        var allOut = CityUi.MakeButton(Loc.GetString("dystopia-reactor-ui-all-out"));
        allOut.HorizontalExpand = true;
        allOut.OnPressed += _ => OnSetGroup?.Invoke(0, 0f);
        var allIn = CityUi.MakeButton(Loc.GetString("dystopia-reactor-ui-all-in"));
        allIn.HorizontalExpand = true;
        allIn.OnPressed += _ => OnSetGroup?.Invoke(0, 1f);
        all.AddChild(allOut);
        all.AddChild(allIn);
        side.AddChild(all);

        side.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-reactor-ui-auto-header")));
        _arToggle = CityUi.MakeButton(string.Empty, CityButtonStyle.Tab);
        _arToggle.ToolTip = Loc.GetString("dystopia-reactor-ui-ar-tip");
        _arToggle.OnPressed += _ =>
        {
            if (_state != null)
                OnAutoRegulator?.Invoke(!_state.AutoRegulator, _state.Setpoint);
        };
        side.AddChild(_arToggle);
        var arRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        var arMinus = CityUi.MakeButton("−");
        arMinus.MinWidth = 36;
        arMinus.OnPressed += _ =>
        {
            if (_state != null)
                OnAutoRegulator?.Invoke(_state.AutoRegulator, MathF.Max(0f, _state.Setpoint - SetpointStep));
        };
        _arSetpoint = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Bold(12));
        _arSetpoint.HorizontalExpand = true;
        _arSetpoint.Align = Label.AlignMode.Center;
        var arPlus = CityUi.MakeButton("+");
        arPlus.MinWidth = 36;
        arPlus.OnPressed += _ =>
        {
            if (_state != null)
                OnAutoRegulator?.Invoke(_state.AutoRegulator, _state.Setpoint + SetpointStep);
        };
        arRow.AddChild(arMinus);
        arRow.AddChild(_arSetpoint);
        arRow.AddChild(arPlus);
        side.AddChild(arRow);
        _autoScram = CityUi.MakeButton(string.Empty, CityButtonStyle.Tab);
        _autoScram.ToolTip = Loc.GetString("dystopia-reactor-ui-autoscram-tip");
        _autoScram.OnPressed += _ =>
        {
            if (_state != null)
                OnAutoScram?.Invoke(!_state.AutoScram);
        };
        side.AddChild(_autoScram);
        body.AddChild(side);
    }

    private void Zoom(float delta)
    {
        _map.CellSize = Math.Clamp(_map.CellSize + delta, DystopiaReactorMap.MinCell, DystopiaReactorMap.MaxCell);
        _map.InvalidateMeasure();
    }

    private static string Fmt(float value, string format = "0.#")
    {
        return Math.Round(value, 2).ToString(format, CultureInfo.InvariantCulture);
    }

    private DystopiaReactorCell? SelectedCell()
    {
        return _state == null || _map.Selected is not { } selected
            ? null
            : _state.Cells.FirstOrDefault(c => c.Entity == selected);
    }

    public void UpdateState(DystopiaReactorConsoleUiState state)
    {
        _state = state;
        if (!state.Connected)
        {
            _status.Text = Loc.GetString("dystopia-reactor-ui-disconnected");
            _status.FontColorOverride = CityUi.Danger;
            _map.SetCells(new List<DystopiaReactorCell>(), 0, 0);
            _rods.SetColumns(new List<DystopiaRodScale.Column>());
            _steam.Text = _xenon.Text = string.Empty;
            foreach (var gauge in new[] { _power, _temperature, _water, _pressure })
            {
                gauge.Fraction = 0f;
                gauge.Value = "—";
                gauge.Marks.Clear();
                gauge.Marker = null;
            }

            UpdateSelected();
            return;
        }

        var controls = state.Cells.Where(c => c.Kind == DystopiaReactorColumnKind.Control).ToList();
        var scrammed = controls.Any(c => c.Scrammed);
        _status.Text = Loc.GetString(scrammed ? "dystopia-reactor-ui-status-scram"
            : state.Alarm ? "dystopia-reactor-ui-status-alarm" : "dystopia-reactor-ui-status-normal",
            ("columns", state.Cells.Count),
            ("fuel", state.Cells.Count(c => c.Kind == DystopiaReactorColumnKind.Fuel && c.RodGrade > 0)),
            ("rods", controls.Count));
        _status.FontColorOverride = scrammed || state.Alarm ? CityUi.Danger : Good;

        // приборы
        var powerScale = MathF.Max(state.PowerScale, state.Setpoint) * 1.25f;
        _power.Fraction = state.ThermalPower / powerScale;
        _power.Value = Loc.GetString("dystopia-reactor-ui-mw", ("value", Fmt(state.ThermalPower / 1000f, "0.00")));
        _power.Marker = state.AutoRegulator ? state.Setpoint / powerScale : null;
        _power.Marks.Clear();
        _power.Marks.Add((state.PowerScale / powerScale, Warn));

        var tScale = state.DamageTemperature * 1.1f;
        _temperature.Fraction = state.MaxTemperature / tScale;
        _temperature.Value = $"{Fmt(state.MaxTemperature, "0")} °C";
        _temperature.BarColor = DystopiaReactorMap.HeatColor(state.MaxTemperature);
        _temperature.Marks.Clear();
        _temperature.Marks.Add((state.AlarmTemperature / tScale, Warn));
        _temperature.Marks.Add((state.DamageTemperature / tScale, CityUi.Danger));
        _temperature.Marker = state.AutoScram ? state.AutoScramTemperature / tScale : null;

        _water.Fraction = state.WaterMax > 0f ? state.Water / state.WaterMax : 0f;
        _water.Value = Fmt(state.Water, "0");
        _water.BarColor = _water.Fraction < 0.2f ? CityUi.Danger : WaterColor;

        var pScale = state.DamagePressure * 1.1f;
        _pressure.Fraction = state.SeparatorPressure / pScale;
        _pressure.Value = Loc.GetString("dystopia-reactor-ui-kpa", ("value", Fmt(state.SeparatorPressure, "0")));
        _pressure.Marks.Clear();
        _pressure.Marks.Add((state.WarnPressure / pScale, Warn));
        _pressure.Marks.Add((state.DamagePressure / pScale, CityUi.Danger));

        _steam.Text = Loc.GetString("dystopia-reactor-ui-steam", ("steam", Fmt(state.SteamRate, "0.0")),
            ("separators", state.Separators));
        _steam.FontColorOverride = state.Separators == 0 || state.Water <= 0f ? CityUi.Danger : CityUi.Text;
        _xenon.Text = Loc.GetString("dystopia-reactor-ui-xenon", ("xenon", Fmt(state.AverageXenon * 100f, "0")));
        _xenon.FontColorOverride = state.AverageXenon > 0.1f ? Warn : CityUi.Dim;

        _map.SetCells(state.Cells, state.Width, state.Height);

        // шкала стержней
        var columns = new List<DystopiaRodScale.Column>();
        for (var g = 1; g <= 4; g++)
            columns.Add(RodColumn(g.ToString(), controls.Where(c => c.Group == g).ToList()));
        columns.Add(RodColumn(Loc.GetString("dystopia-reactor-ui-rods-all"), controls));
        _rods.SetColumns(columns);

        _arToggle.SetFullText(Loc.GetString(state.AutoRegulator ? "dystopia-reactor-ui-ar-on" : "dystopia-reactor-ui-ar-off"));
        _arToggle.Active = state.AutoRegulator;
        _arSetpoint.Text = Loc.GetString("dystopia-reactor-ui-setpoint", ("setpoint", Fmt(state.Setpoint / 1000f, "0.00")));
        _autoScram.SetFullText(Loc.GetString(state.AutoScram ? "dystopia-reactor-ui-autoscram-on" : "dystopia-reactor-ui-autoscram-off",
            ("temperature", Fmt(state.AutoScramTemperature, "0"))));
        _autoScram.Active = state.AutoScram;

        UpdateSelected();
    }

    private static DystopiaRodScale.Column RodColumn(string label, List<DystopiaReactorCell> rods)
    {
        if (rods.Count == 0)
            return new DystopiaRodScale.Column(label, "—", 0f, 0f, false, true);

        var insertion = rods.Average(r => r.Insertion);
        return new DystopiaRodScale.Column(label, $"{Fmt(insertion * 100f, "0")}%", insertion,
            rods.Average(r => r.Scrammed ? 1f : r.Target), rods.Any(r => r.Scrammed), false);
    }

    private void UpdateSelected()
    {
        if (SelectedCell() is not { } cell)
        {
            _selTitle.Text = Loc.GetString("dystopia-reactor-ui-selected-header");
            _selLine.Text = Loc.GetString("dystopia-reactor-ui-select-hint");
            _selLine.FontColorOverride = CityUi.Muted;
            _rodRow.Visible = false;
            return;
        }

        var kind = Loc.GetString($"dystopia-reactor-kind-{cell.Kind.ToString().ToLowerInvariant()}");
        _selTitle.Text = Loc.GetString("dystopia-reactor-ui-cell-title", ("kind", kind), ("x", cell.X + 1), ("y", cell.Y + 1),
            ("temperature", Fmt(cell.Temperature, "0")), ("health", Fmt(cell.Health * 100f, "0")));
        _selLine.FontColorOverride = CityUi.Text;
        _rodRow.Visible = cell.Kind == DystopiaReactorColumnKind.Control;
        _selLine.Text = cell.Kind switch
        {
            DystopiaReactorColumnKind.Fuel when cell.RodGrade > 0 => Loc.GetString("dystopia-reactor-ui-cell-fuel",
                ("power", Fmt(cell.Power, "0")), ("burnup", Fmt(cell.Burnup * 100f, "0")), ("xenon", Fmt(cell.Xenon, "0.00"))),
            DystopiaReactorColumnKind.Fuel => Loc.GetString("dystopia-reactor-ui-cell-empty"),
            DystopiaReactorColumnKind.Control => Loc.GetString(cell.Scrammed
                    ? "dystopia-reactor-ui-cell-scrammed"
                    : "dystopia-reactor-ui-cell-control",
                ("insertion", Fmt(cell.Insertion * 100f, "0")), ("target", Fmt(cell.Target * 100f, "0")),
                ("group", cell.Group)),
            DystopiaReactorColumnKind.Graphite => Loc.GetString("dystopia-reactor-ui-cell-graphite"),
            _ => Loc.GetString("dystopia-reactor-ui-cell-coolant"),
        };

        for (var i = 0; i < _groupButtons.Count; i++)
            _groupButtons[i].Disabled = cell.Group == i + 1;
    }
}
