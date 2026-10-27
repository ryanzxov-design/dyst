// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: «Паспортный стол» — связь окна с сервером.

using Content.Shared._Dystopia.Registry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Registry;

public sealed partial class CityPassportConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CityPassportConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CityPassportConsoleWindow>();
        _window.OnSelect += number => SendMessage(new CityPassportSelectMessage(number));
        _window.OnAnnul += (number, card, reason) => SendMessage(new CityPassportAnnulMessage(number, card, reason));
        _window.OnIssue += number => SendMessage(new CityPassportIssueMessage(number));
        _window.OnReinstate += number => SendMessage(new CityPassportReinstateMessage(number));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CityPassportConsoleUiState cast)
            _window?.UpdateState(cast);
    }
}
