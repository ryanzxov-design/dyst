// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню пульта промышленного двигателя — связь окна с сервером.

using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaEngineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaEngineWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DystopiaEngineWindow>();
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.WindowTitle = meta.EntityName;

        _window.OnToggle += () => SendMessage(new DystopiaEngineToggleMessage());
        _window.OnSetThrottle += throttle => SendMessage(new DystopiaEngineSetThrottleMessage(throttle));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DystopiaEngineUiState cast)
            _window?.UpdateState(cast);
    }
}
