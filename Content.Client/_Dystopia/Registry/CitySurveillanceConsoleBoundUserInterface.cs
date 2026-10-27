// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: консоль соционадзора — связь окна с сервером.

using Content.Shared._Dystopia.Registry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Registry;

public sealed partial class CitySurveillanceConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CitySurveillanceConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CitySurveillanceConsoleWindow>();
        _window.SetGrid(EntMan.GetComponentOrNull<TransformComponent>(Owner)?.GridUid);
        _window.OnSelect += camera => SendMessage(new CitySurveillanceSelectMessage(camera));
        _window.OnAccept += alert => SendMessage(new CitySurveillanceAcceptMessage(alert));
        _window.OnDismiss += alert => SendMessage(new CitySurveillanceDismissMessage(alert));
        _window.OnTrack += number => SendMessage(new CitySurveillanceTrackMessage(number));
        _window.OnKeyword += (word, add) => SendMessage(new CitySurveillanceKeywordMessage(word, add));
        _window.OnVoice += (text, personal) => SendMessage(new CitySurveillanceVoiceMessage(text, personal));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CitySurveillanceUiState cast)
            _window?.UpdateState(cast);
    }
}
