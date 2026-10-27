// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: консоль социоучёта — связь окна с сервером.

using Content.Client.Eye;
using Content.Shared._Dystopia.Registry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Registry;

public sealed partial class CityRegistryConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CityRegistryConsoleWindow? _window;

    /// <summary>Окно «Смотреть» — картинка с камеры.</summary>
    [ViewVariables]
    private CityCameraViewWindow? _cameraWindow;

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
        _window.OnIdentify += (sighting, number) => SendMessage(new CityRegistryIdentifyMessage(sighting, number));
        _window.OnWatch += camera => SendMessage(new CityRegistryWatchMessage(camera));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CityRegistryConsoleUiState cast)
            _window?.UpdateState(cast);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is not CityRegistryWatchingMessage watching)
            return;

        if (EntMan.GetEntity(watching.Camera) is not { } camera)
        {
            CloseCamera(false);
            return;
        }

        if (_cameraWindow == null || _cameraWindow.Disposed)
        {
            _cameraWindow = new CityCameraViewWindow();
            _cameraWindow.OnClose += OnCameraWindowClosed;
            _cameraWindow.OpenCentered();
        }

        // глаз камеры подключает само окно, когда камера дойдёт до клиента; старый — отключаем
        if (_cameraWindow.Camera is { } old && old != camera)
            EntMan.System<EyeLerpingSystem>().RemoveEye(old);

        _cameraWindow.SetCamera(camera, watching.Title);
    }

    /// <summary>Окно камеры закрыли руками — сервер перестаёт показывать камеру.</summary>
    private void OnCameraWindowClosed()
    {
        if (_cameraWindow?.Camera is { } camera)
            EntMan.System<EyeLerpingSystem>().RemoveEye(camera);
        _cameraWindow = null;
        SendMessage(new CityRegistryWatchMessage(null));
    }

    private void CloseCamera(bool notifyServer)
    {
        if (_cameraWindow == null)
            return;

        var window = _cameraWindow;
        _cameraWindow = null;
        window.OnClose -= OnCameraWindowClosed;
        if (window.Camera is { } camera)
            EntMan.System<EyeLerpingSystem>().RemoveEye(camera);
        window.Close();

        if (notifyServer)
            SendMessage(new CityRegistryWatchMessage(null));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        CloseCamera(false);
    }
}
