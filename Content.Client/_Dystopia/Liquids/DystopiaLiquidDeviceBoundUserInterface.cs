// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню жидкостных устройств (насос, фильтр, экспортер, импортер, качалка) — связь окна с сервером.

using Content.Shared._Dystopia.Liquids;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Liquids;

public sealed class DystopiaLiquidDeviceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaLiquidDeviceWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DystopiaLiquidDeviceWindow>();
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.Title = meta.EntityName;

        _window.OnToggle += on => SendMessage(new DystopiaLiquidDeviceToggleMessage(on));
        _window.OnSetRate += rate => SendMessage(new DystopiaLiquidDeviceSetRateMessage(rate));
        _window.OnSetFilter += reagent => SendMessage(new DystopiaLiquidDeviceSetFilterMessage(reagent));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DystopiaLiquidDeviceUiState cast)
            _window?.UpdateState(cast);
    }
}
