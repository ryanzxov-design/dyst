// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню конвейерных устройств — связь окна с сервером.

using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

public sealed partial class DystopiaConveyorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaConveyorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DystopiaConveyorWindow>();
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.WindowTitle = meta.EntityName;

        _window.OnSetWhitelist += whitelist => SendMessage(new DystopiaConveyorSetWhitelistMessage(whitelist));
        _window.OnAdd += (list, key) => SendMessage(new DystopiaConveyorAddMessage(list, key));
        _window.OnRemove += (list, key) => SendMessage(new DystopiaConveyorRemoveMessage(list, key));
        _window.OnAddHeld += list => SendMessage(new DystopiaConveyorAddHeldMessage(list));
        _window.OnClear += list => SendMessage(new DystopiaConveyorClearMessage(list));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DystopiaConveyorUiState cast)
            _window?.UpdateState(cast);
    }
}
