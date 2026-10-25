// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: пульт реактора — связь окна с сервером.

using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaReactorConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaReactorConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DystopiaReactorConsoleWindow>();
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.WindowTitle = meta.EntityName;

        _window.OnSetRod += (rod, target) => SendMessage(new DystopiaReactorSetRodMessage(rod, target));
        _window.OnSetGroup += (group, target) => SendMessage(new DystopiaReactorSetGroupMessage(group, target));
        _window.OnStepGroup += (group, delta) => SendMessage(new DystopiaReactorStepGroupMessage(group, delta));
        _window.OnAssignGroup += (rod, group) => SendMessage(new DystopiaReactorAssignGroupMessage(rod, group));
        _window.OnScram += () => SendMessage(new DystopiaReactorScramMessage());
        _window.OnResetScram += () => SendMessage(new DystopiaReactorResetScramMessage());
        _window.OnAutoRegulator += (on, setpoint) => SendMessage(new DystopiaReactorAutoRegulatorMessage(on, setpoint));
        _window.OnAutoScram += on => SendMessage(new DystopiaReactorAutoScramMessage(on));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DystopiaReactorConsoleUiState cast)
            _window?.UpdateState(cast);
    }
}
