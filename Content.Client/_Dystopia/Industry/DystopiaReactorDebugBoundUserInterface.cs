// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: пульт отладки реактора — связь окна с сервером.

using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaReactorDebugBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaReactorDebugWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DystopiaReactorDebugWindow>();
        _window.OnAction += (action, value) => SendMessage(new DystopiaReactorDebugActionMessage(action, value));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DystopiaReactorDebugUiState cast)
            _window?.UpdateState(cast);
    }
}
