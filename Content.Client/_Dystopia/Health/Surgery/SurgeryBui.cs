// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос Shitmed (Goob-Station, AGPL-3.0): окно операции. Слева направо: часть тела → операция → шаги.
// Выполненные шаги зелёные, доступен только следующий; у шага иконка нужного инструмента, а внизу —
// подсказка, почему следующий шаг сейчас сделать нельзя (нет инструмента, мешает одежда...).

using Content.Client.UserInterface.Controls;
using Content.Shared._Dystopia.Health.Surgery;
using Content.Shared.Body.Part;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Health.Surgery;

[UsedImplicitly]
public sealed partial class SurgeryBui : BoundUserInterface
{
    private readonly SurgerySystem _system;
    private readonly SpriteSystem _sprite;

    [ViewVariables]
    private SurgeryWindow? _window;

    private EntityUid? _part;
    private (EntityUid Ent, EntProtoId Proto)? _surgery;
    private readonly List<EntProtoId> _previousSurgeries = new();

    public SurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = EntMan.System<SurgerySystem>();
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window is null || message is not SurgeryBuiRefreshMessage)
            return;

        RefreshUI();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is SurgeryBuiState s)
            Update(s);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    private void CreateWindow()
    {
        _window = new SurgeryWindow();
        _window.OnClose += Close;
        _window.Title = Loc.GetString("surgery-ui-window-title");

        _window.PartsButton.OnPressed += _ =>
        {
            _part = null;
            _surgery = null;
            _previousSurgeries.Clear();
            View(ViewType.Parts);
        };

        _window.SurgeriesButton.OnPressed += _ =>
        {
            _surgery = null;
            _previousSurgeries.Clear();

            if (!EntMan.TryGetNetEntity(_part, out var netPart)
                || State is not SurgeryBuiState s
                || !s.Choices.TryGetValue(netPart.Value, out var surgeries))
            {
                return;
            }

            OnPartPressed(netPart.Value, surgeries);
        };

        _window.StepsButton.OnPressed += _ =>
        {
            if (!EntMan.TryGetNetEntity(_part, out var netPart) || _previousSurgeries.Count == 0)
                return;

            var last = _previousSurgeries[^1];
            _previousSurgeries.RemoveAt(_previousSurgeries.Count - 1);

            if (_system.GetSingleton(last) is not { } previousId
                || !EntMan.TryGetComponent(previousId, out SurgeryComponent? previous))
            {
                return;
            }

            OnSurgeryPressed((previousId, previous), netPart.Value, last);
        };
    }

    private void Update(SurgeryBuiState state)
    {
        if (_window == null)
            CreateWindow();

        var window = _window!;
        window.Surgeries.DisposeAllChildren();
        window.Steps.DisposeAllChildren();
        window.Parts.DisposeAllChildren();
        View(ViewType.Parts);

        var oldSurgery = _surgery;
        var oldPart = _part;
        _part = null;
        _surgery = null;

        var options = new List<(NetEntity NetEntity, EntityUid Entity, string Name, BodyPartType PartType)>();
        foreach (var choice in state.Choices.Keys)
        {
            if (!EntMan.TryGetEntity(choice, out var ent) || !EntMan.TryGetComponent(ent, out BodyPartComponent? part))
                continue;

            options.Add((choice, ent.Value, EntMan.GetComponent<MetaDataComponent>(ent.Value).EntityName, part.PartType));
        }

        options.Sort((a, b) => PartScore(a.PartType).CompareTo(PartScore(b.PartType)));

        foreach (var (netEntity, entity, partName, _) in options)
        {
            var surgeries = state.Choices[netEntity];
            var partButton = new ChoiceControl();

            var label = new FormattedMessage();
            label.AddText(partName);
            if (surgeries.Count > 0)
                label.AddMarkupOrThrow($" [color=gray]({surgeries.Count})[/color]");

            partButton.Set(label, null);
            partButton.Button.OnPressed += _ => OnPartPressed(netEntity, surgeries);
            window.Parts.AddChild(partButton);

            if (oldPart != entity)
                continue;

            var restored = false;
            if (oldSurgery != null)
            {
                foreach (var surgeryId in surgeries)
                {
                    if (oldSurgery.Value.Proto != surgeryId
                        || _system.GetSingleton(surgeryId) is not { } surgery
                        || !EntMan.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
                    {
                        continue;
                    }

                    OnSurgeryPressed((surgery, surgeryComp), netEntity, surgeryId);
                    restored = true;
                    break;
                }
            }

            if (!restored)
                OnPartPressed(netEntity, surgeries);
        }

        if (!window.IsOpen)
            window.OpenCentered();
    }

    private static int PartScore(BodyPartType type)
    {
        return type switch
        {
            BodyPartType.Head => 1,
            BodyPartType.Chest => 2,
            BodyPartType.Groin => 3,
            BodyPartType.Arm => 4,
            BodyPartType.Hand => 5,
            BodyPartType.Leg => 6,
            BodyPartType.Foot => 7,
            _ => 9,
        };
    }

    private void AddStep(EntProtoId stepId, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null || _system.GetSingleton(stepId) is not { } step)
            return;

        var stepButton = new SurgeryStepButton { Step = step, StepId = stepId };
        stepButton.Button.OnPressed += _ => SendPredictedMessage(new SurgeryStepChosenBuiMsg(netPart, surgeryId, stepId));
        _window.Steps.AddChild(stepButton);
    }

    private void OnSurgeryPressed(Entity<SurgeryComponent> surgery, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null)
            return;

        _part = EntMan.GetEntity(netPart);
        _surgery = (surgery, surgeryId);

        _window.Steps.DisposeAllChildren();

        if (surgery.Comp.Requirement is { } requirementId && _system.GetSingleton(requirementId) is { } requirement)
        {
            var label = new ChoiceControl();
            label.Button.OnPressed += _ =>
            {
                _previousSurgeries.Add(surgeryId);

                if (EntMan.TryGetComponent(requirement, out SurgeryComponent? requirementComp))
                    OnSurgeryPressed((requirement, requirementComp), netPart, requirementId);
            };

            var msg = new FormattedMessage();
            var surgeryName = EntMan.GetComponent<MetaDataComponent>(requirement).EntityName;
            msg.AddMarkupOrThrow($"[bold]{Loc.GetString("surgery-ui-window-require")}: {FormattedMessage.EscapeText(surgeryName)}[/bold]");
            label.Set(msg, null);

            _window.Steps.AddChild(label);
            _window.Steps.AddChild(new Separator { StyleClasses = { "LowDivider" }, Margin = new Thickness(0, 2) });
        }

        foreach (var stepId in surgery.Comp.Steps)
        {
            AddStep(stepId, netPart, surgeryId);
        }

        View(ViewType.Steps);
        RefreshUI();
    }

    private void OnPartPressed(NetEntity netPart, List<EntProtoId> surgeryIds)
    {
        if (_window == null)
            return;

        _part = EntMan.GetEntity(netPart);
        _window.Surgeries.DisposeAllChildren();

        var surgeries = new List<(Entity<SurgeryComponent> Ent, EntProtoId Id, string Name)>();
        foreach (var surgeryId in surgeryIds)
        {
            if (_system.GetSingleton(surgeryId) is not { } surgery
                || !EntMan.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
            {
                continue;
            }

            var name = EntMan.GetComponent<MetaDataComponent>(surgery).EntityName;
            surgeries.Add(((surgery, surgeryComp), surgeryId, name));
        }

        surgeries.Sort((a, b) =>
        {
            var priority = a.Ent.Comp.Priority.CompareTo(b.Ent.Comp.Priority);
            return priority != 0 ? priority : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });

        if (surgeries.Count == 0)
        {
            var empty = new ChoiceControl();
            empty.Set(Loc.GetString("surgery-ui-window-no-surgeries"), null);
            empty.Button.Disabled = true;
            _window.Surgeries.AddChild(empty);
        }

        foreach (var surgery in surgeries)
        {
            var surgeryButton = new ChoiceControl();
            surgeryButton.Set(surgery.Name, null);
            surgeryButton.Button.OnPressed += _ => OnSurgeryPressed(surgery.Ent, netPart, surgery.Id);
            _window.Surgeries.AddChild(surgeryButton);
        }

        RefreshUI();
        View(ViewType.Surgeries);
    }

    private void RefreshUI()
    {
        if (_window == null
            || !_window.IsOpen
            || _part == null
            || _surgery == null
            || !EntMan.HasComponent<SurgeryComponent>(_surgery.Value.Ent)
            || PlayerManager.LocalEntity is not { } user)
        {
            return;
        }

        var next = _system.GetNextStep(Owner, _part.Value, _surgery.Value.Ent, user);
        string? hint = null;
        var i = 0;
        foreach (var child in _window.Steps.Children)
        {
            if (child is not SurgeryStepButton stepButton)
                continue;

            var status = StepStatus.Incomplete;
            if (next == null)
                status = StepStatus.Complete;
            else if (next.Value.Step < 0 && i > -next.Value.Step - 1)
                status = StepStatus.Complete;
            else if (next.Value.Step < 0 && i <= -next.Value.Step - 1)
                status = StepStatus.Next;
            else if (next.Value.Surgery.Owner != _surgery.Value.Ent)
                status = StepStatus.Incomplete;
            else if (next.Value.Step == i)
                status = StepStatus.Next;
            else if (i < next.Value.Step)
                status = StepStatus.Complete;

            stepButton.Button.Disabled = status != StepStatus.Next;
            stepButton.ToolTip = null;

            var stepName = new FormattedMessage();
            stepName.AddText(EntMan.GetComponent<MetaDataComponent>(stepButton.Step).EntityName);

            if (status == StepStatus.Complete)
            {
                stepButton.Button.Modulate = Color.FromHex("#7fdc7f");
            }
            else
            {
                stepButton.Button.Modulate = Color.White;
                if (status == StepStatus.Next
                    && !_system.CanPerformStepWithHeld(user, Owner, _part.Value, stepButton.Step, false, out var popup))
                {
                    stepButton.ToolTip = popup;
                    hint ??= popup;
                }
            }

            stepButton.Set(stepName, _sprite.GetPrototypeIcon(stepButton.StepId.Id).Default);
            i++;
        }

        if (next == null)
            hint = Loc.GetString("surgery-ui-window-done");
        else if (next.Value.Surgery.Owner != _surgery.Value.Ent)
            hint = Loc.GetString("surgery-ui-window-requirement-first");

        _window.HintLabel.SetMessage(hint ?? string.Empty);
    }

    private void View(ViewType type)
    {
        if (_window == null)
            return;

        _window.Parts.Visible = type == ViewType.Parts;
        _window.PartsButton.Disabled = type == ViewType.Parts;

        _window.Surgeries.Visible = type == ViewType.Surgeries;
        _window.SurgeriesButton.Disabled = type != ViewType.Steps;

        _window.Steps.Visible = type == ViewType.Steps;
        _window.StepsButton.Disabled = type != ViewType.Steps || _previousSurgeries.Count == 0;

        if (type != ViewType.Steps)
            _window.HintLabel.SetMessage(string.Empty);

        var title = Loc.GetString("surgery-ui-window-title");
        if (EntMan.TryGetComponent(_part, out MetaDataComponent? partMeta)
            && EntMan.TryGetComponent(_surgery?.Ent, out MetaDataComponent? surgeryMeta))
        {
            _window.Title = $"{title} — {partMeta.EntityName}, {surgeryMeta.EntityName}";
        }
        else if (partMeta != null)
        {
            _window.Title = $"{title} — {partMeta.EntityName}";
        }
        else
        {
            _window.Title = title;
        }
    }

    private enum ViewType
    {
        Parts,
        Surgeries,
        Steps,
    }

    private enum StepStatus
    {
        Next,
        Complete,
        Incomplete,
    }
}
