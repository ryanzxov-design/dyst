// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню парового котла и паровой турбины — связь окон с сервером.

using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaBoilerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaBoilerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DystopiaBoilerWindow>();
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.WindowTitle = meta.EntityName;

        _window.OnSetMode += mode => SendMessage(new DystopiaBoilerSetModeMessage(mode));
        _window.OnToggleVent += () => SendMessage(new DystopiaBoilerToggleVentMessage());
        _window.OnExtinguish += () => SendMessage(new DystopiaBoilerExtinguishMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DystopiaBoilerUiState cast)
            _window?.UpdateState(cast);
    }
}

public sealed class DystopiaSteamTurbineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaSteamTurbineWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DystopiaSteamTurbineWindow>();
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.WindowTitle = meta.EntityName;

        _window.OnToggle += () => SendMessage(new DystopiaSteamTurbineToggleMessage());
        _window.OnSetValve += valve => SendMessage(new DystopiaSteamTurbineSetValveMessage(valve));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DystopiaSteamTurbineUiState cast)
            _window?.UpdateState(cast);
    }
}
