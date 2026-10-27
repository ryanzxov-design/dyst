// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: консоль социоучёта — связь окна с сервером.

using Content.Shared._Dystopia.Registry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Registry;

public sealed partial class CityRegistryConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CityRegistryConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CityRegistryConsoleWindow>();
        _window.OnSelect += number => SendMessage(new CityRegistrySelectMessage(number));
        _window.OnSetStatus += (number, status, reason) => SendMessage(new CityRegistrySetStatusMessage(number, status, reason));
        _window.OnViolation += (number, law, cls, loyalty, details) =>
            SendMessage(new CityRegistryViolationMessage(number, law, cls, loyalty, details));
        _window.OnReward += (number, loyalty, text) => SendMessage(new CityRegistryRewardMessage(number, loyalty, text));
        _window.OnNotes += (number, notes) => SendMessage(new CityRegistryNotesMessage(number, notes));
        _window.OnPrint += number => SendMessage(new CityRegistryPrintMessage(number));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CityRegistryConsoleUiState cast)
            _window?.UpdateState(cast);
    }
}
