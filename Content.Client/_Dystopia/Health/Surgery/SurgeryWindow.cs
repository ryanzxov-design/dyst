// SPDX-License-Identifier: AGPL-3.0-or-later
// Окно операции. Слева части тела, справа операции и шаги.

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Dystopia.Health.Surgery;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Health.Surgery;

public sealed class SurgeryWindow : FancyWindow
{
    public event Action<NetEntity, string, string>? OnStep;
    public event Action<NetEntity, NetEntity>? OnRemoveOrgan;
    public event Action<NetEntity>? OnInsertOrgan;
    public event Action<NetEntity>? OnAttachLimb;

    private static readonly Color DoneColor = Color.FromHex("#6FBF73");
    private static readonly Color CurrentColor = Color.FromHex("#E8D27A");
    private static readonly Color LockedColor = Color.FromHex("#8A8F98");
    private static readonly Color HintColor = Color.FromHex("#A9B4C2");

    private readonly Label _patient;
    private readonly Label _conditions;
    private readonly Label _held;
    private readonly BoxContainer _partsList;
    private readonly BoxContainer _details;

    private SurgeryBuiState? _state;
    private NetEntity? _selectedPart;

    public SurgeryWindow()
    {
        Title = Loc.GetString("surgery-window-title");
        MinSize = new Vector2(620, 440);
        SetSize = new Vector2(700, 520);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };

        _patient = new Label { StyleClasses = { "LabelHeading" } };
        _conditions = new Label { FontColorOverride = HintColor };
        _held = new Label { FontColorOverride = CurrentColor };
        root.AddChild(_patient);
        root.AddChild(_conditions);
        root.AddChild(_held);
        root.AddChild(new Control { MinHeight = 6 });

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, VerticalExpand = true };

        _partsList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 170 };
        body.AddChild(_partsList);
        body.AddChild(new Control { MinWidth = 8 });

        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        _details = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        scroll.AddChild(_details);
        body.AddChild(scroll);

        root.AddChild(body);
        ContentsContainer.AddChild(root);
    }

    public void UpdateState(SurgeryBuiState state)
    {
        _state = state;
        _patient.Text = Loc.GetString("surgery-window-patient", ("name", state.Patient));
        _conditions.Text = state.Conditions;
        _held.Text = state.Held;

        if (_selectedPart == null || state.Parts.TrueForAll(p => p.Part != _selectedPart))
            _selectedPart = state.Parts.Count > 0 ? state.Parts[0].Part : null;

        RebuildParts();
        RebuildDetails();
    }

    private void RebuildParts()
    {
        _partsList.RemoveAllChildren();
        if (_state == null)
            return;

        foreach (var part in _state.Parts)
        {
            var button = new Button
            {
                Text = $"{part.Name} — {part.Status}",
                ToggleMode = true,
                Pressed = part.Part == _selectedPart,
                HorizontalExpand = true,
                ClipText = true,
            };
            var id = part.Part;
            button.OnPressed += _ =>
            {
                _selectedPart = id;
                RebuildParts();
                RebuildDetails();
            };
            _partsList.AddChild(button);
        }
    }

    private void RebuildDetails()
    {
        _details.RemoveAllChildren();
        if (_state == null || _selectedPart is not { } selected)
            return;

        var part = _state.Parts.Find(p => p.Part == selected);
        if (part == null)
            return;

        foreach (var surgery in part.Surgeries)
        {
            _details.AddChild(new Label { Text = surgery.Name, StyleClasses = { "LabelHeading" } });
            foreach (var step in surgery.Steps)
            {
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(8, 1, 0, 1) };
                var mark = step.Done ? "✔" : step.Current ? "▶" : "·";
                var color = step.Done ? DoneColor : step.Current ? CurrentColor : LockedColor;
                row.AddChild(new Label { Text = $"{mark} {step.Name}", FontColorOverride = color, HorizontalExpand = true });
                row.AddChild(new Label { Text = step.Tools, FontColorOverride = HintColor, Margin = new Thickness(6, 0) });

                if (step.Current)
                {
                    var button = new Button { Text = Loc.GetString("surgery-window-do") };
                    var surgeryId = surgery.Id;
                    var stepId = step.Id;
                    button.OnPressed += _ => OnStep?.Invoke(selected, surgeryId, stepId);
                    row.AddChild(button);
                }

                _details.AddChild(row);
            }

            _details.AddChild(new Control { MinHeight = 6 });
        }

        // Пришивание конечностей: у вскрытой части тела не хватает руки, кисти, ноги или стопы
        if (part.Open && part.MissingLimbs.Count > 0)
        {
            _details.AddChild(new Label { Text = Loc.GetString("surgery-window-limbs"), StyleClasses = { "LabelHeading" } });
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(8, 1, 0, 1) };
            row.AddChild(new Label
            {
                Text = Loc.GetString("surgery-window-missing-limbs", ("limbs", string.Join(", ", part.MissingLimbs))),
                FontColorOverride = CurrentColor,
                HorizontalExpand = true,
            });
            var attach = new Button { Text = Loc.GetString("surgery-window-attach") };
            attach.OnPressed += _ => OnAttachLimb?.Invoke(selected);
            row.AddChild(attach);
            _details.AddChild(row);
            _details.AddChild(new Control { MinHeight = 6 });
        }

        if (!part.CavityOpen)
            return;

        _details.AddChild(new Label { Text = Loc.GetString("surgery-window-organs"), StyleClasses = { "LabelHeading" } });
        foreach (var organ in part.Organs)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(8, 1, 0, 1) };
            row.AddChild(new Label { Text = organ.Name, HorizontalExpand = true });
            var button = new Button { Text = Loc.GetString("surgery-window-remove") };
            var organId = organ.Organ;
            button.OnPressed += _ => OnRemoveOrgan?.Invoke(selected, organId);
            row.AddChild(button);
            _details.AddChild(row);
        }

        if (part.Missing.Count > 0)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(8, 4, 0, 1) };
            row.AddChild(new Label
            {
                Text = Loc.GetString("surgery-window-missing", ("organs", string.Join(", ", part.Missing))),
                FontColorOverride = CurrentColor,
                HorizontalExpand = true,
            });
            var button = new Button { Text = Loc.GetString("surgery-window-insert") };
            button.OnPressed += _ => OnInsertOrgan?.Invoke(selected);
            row.AddChild(button);
            _details.AddChild(row);
        }
    }
}
