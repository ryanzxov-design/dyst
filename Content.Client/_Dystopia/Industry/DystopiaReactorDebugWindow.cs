// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: пульт отладки реактора — сводка и кнопки действий (DystopiaReactorDebugActions).

using System.Globalization;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaReactorDebugWindow : CityWindow
{
    public event Action<string, float>? OnAction;

    private readonly RichTextLabel _summary;
    private readonly LineEdit _value;
    private readonly Label _speed;

    public DystopiaReactorDebugWindow()
    {
        WindowTitle = Loc.GetString("dystopia-reactor-debug-title");
        Subtitle = Loc.GetString("dystopia-industry-ui-sub");
        Slogan = Loc.GetString("dystopia-industry-ui-slogan");
        MinSize = new Vector2(720, 560);
        SetSize = new Vector2(760, 600);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        _summary = new RichTextLabel { HorizontalExpand = true };
        root.AddChild(_summary);

        var valueRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        valueRow.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-reactor-debug-value"), CityUi.Dim));
        _value = CityUi.Field("0", 120);
        valueRow.AddChild(_value);
        _speed = CityUi.MakeLabel(string.Empty, CityUi.Accent, CityUi.Bold(12));
        _speed.HorizontalExpand = true;
        _speed.Align = Label.AlignMode.Right;
        valueRow.AddChild(_speed);
        root.AddChild(valueRow);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        scroll.AddChild(rows);
        root.AddChild(scroll);

        const string t = DystopiaReactorDebugActions.Temperature;
        Row(rows, "temperature", ("20 °C", t, 20f), ("300 °C", t, 300f), ("800 °C", t, 800f), ("1300 °C", t, 1300f),
            (Loc.GetString("dystopia-reactor-debug-set"), t, float.NaN));
        Row(rows, "xenon", (Loc.GetString("dystopia-reactor-debug-clear"), DystopiaReactorDebugActions.ClearXenon, 0f),
            (Loc.GetString("dystopia-reactor-debug-max"), DystopiaReactorDebugActions.MaxXenon, 0f));
        const string r = DystopiaReactorDebugActions.Rods;
        Row(rows, "rods", ("0%", r, 0f), ("25%", r, 0.25f), ("50%", r, 0.5f), ("100%", r, 1f),
            (Loc.GetString("dystopia-reactor-ui-reset-scram"), DystopiaReactorDebugActions.ResetScram, 0f));
        const string b = DystopiaReactorDebugActions.Burnup;
        Row(rows, "burnup", ("0%", b, 0f), ("50%", b, 0.5f), ("95%", b, 0.95f), ("100%", b, 1f));
        const string a = DystopiaReactorDebugActions.Activity;
        Row(rows, "activity", ("0", a, 0f), ("1", a, 1f), ("10", a, 10f), ("30", a, 30f),
            (Loc.GetString("dystopia-reactor-debug-set"), a, float.NaN));
        Row(rows, "water", (Loc.GetString("dystopia-reactor-debug-fill"), DystopiaReactorDebugActions.FillWater, 0f),
            (Loc.GetString("dystopia-reactor-debug-drain"), DystopiaReactorDebugActions.DrainWater, 0f));
        const string p = DystopiaReactorDebugActions.Pressure;
        Row(rows, "pressure", ("0", p, 0f), ("4000", p, 4000f), ("7000", p, 7000f), ("9000", p, 9000f),
            (Loc.GetString("dystopia-reactor-debug-set"), p, float.NaN));
        Row(rows, "kick", ("+100", DystopiaReactorDebugActions.Kick, 100f), ("+1000", DystopiaReactorDebugActions.Kick, 1000f));
        const string s = DystopiaReactorDebugActions.Speed;
        Row(rows, "speed", ("×1", s, 1f), ("×5", s, 5f), ("×20", s, 20f), ("×50", s, 50f));
        Row(rows, "accident",
            (Loc.GetString("dystopia-reactor-debug-explosion"), DystopiaReactorDebugActions.Explosion, 0f),
            (Loc.GetString("dystopia-reactor-debug-meltdown"), DystopiaReactorDebugActions.Meltdown, 0f),
            (Loc.GetString("dystopia-reactor-debug-break"), DystopiaReactorDebugActions.BreakChannel, 0f),
            (Loc.GetString("dystopia-reactor-debug-runaway"), DystopiaReactorDebugActions.Runaway, 0f));
        Row(rows, "aftermath",
            (Loc.GetString("dystopia-reactor-debug-corium"), DystopiaReactorDebugActions.Corium, 0f),
            (Loc.GetString("dystopia-reactor-debug-fire"), DystopiaReactorDebugActions.GraphiteFire, 0f),
            (Loc.GetString("dystopia-reactor-debug-dust"), DystopiaReactorDebugActions.Contamination, 0f));
    }

    /// <summary>Строка: подпись и кнопки. Значение NaN — взять из поля ввода.</summary>
    private void Row(BoxContainer parent, string key, params (string Text, string Action, float Value)[] buttons)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        var label = CityUi.MakeLabel(Loc.GetString($"dystopia-reactor-debug-row-{key}"), CityUi.Dim);
        label.MinWidth = 170;
        row.AddChild(label);
        foreach (var (text, action, value) in buttons)
        {
            var button = CityUi.MakeButton(text);
            var act = action;
            var val = value;
            button.OnPressed += _ => OnAction?.Invoke(act, float.IsNaN(val) ? FieldValue() : val);
            row.AddChild(button);
        }

        parent.AddChild(row);
    }

    private float FieldValue()
    {
        return float.TryParse(_value.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0f;
    }

    public void UpdateState(DystopiaReactorDebugUiState state)
    {
        var color = state.Linked ? CityUi.Text : CityUi.Danger;
        _summary.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={color.ToHex()}]{FormattedMessage.EscapeText(state.Summary)}[/color]"));
        _speed.Text = Loc.GetString("dystopia-reactor-debug-speed", ("speed", state.Speed));
    }
}
