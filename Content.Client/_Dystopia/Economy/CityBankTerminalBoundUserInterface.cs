using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Economy;

public sealed partial class CityBankTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CityBankTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CityBankTerminalWindow>();
        _window.OnSetFrozen += (id, frozen) => SendMessage(new CityBankTerminalSetFrozenMessage(id, frozen));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CityBankTerminalUiState cast)
            _window?.UpdateState(cast);
    }
}
