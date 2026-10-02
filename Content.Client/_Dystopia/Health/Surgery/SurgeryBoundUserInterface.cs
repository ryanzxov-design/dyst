// SPDX-License-Identifier: AGPL-3.0-or-later
// Хирургия Dystopia.

using Content.Shared._Dystopia.Health.Surgery;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Health.Surgery;

public sealed partial class SurgeryBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private SurgeryWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SurgeryWindow>();
        _window.OnStep += (part, surgery, step) => SendMessage(new SurgeryStepMessage(part, surgery, step));
        _window.OnRemoveOrgan += (part, organ) => SendMessage(new SurgeryRemoveOrganMessage(part, organ));
        _window.OnInsertOrgan += part => SendMessage(new SurgeryInsertOrganMessage(part));
        _window.OnAttachLimb += part => SendMessage(new SurgeryAttachLimbMessage(part));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is SurgeryBuiState cast)
            _window?.UpdateState(cast);
    }
}
