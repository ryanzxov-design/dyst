using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Economy;

public sealed partial class CityVendorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CityVendorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CityVendorWindow>();
        _window.OnBuy += index => SendMessage(new CityVendorBuyMessage(index));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CityVendorUiState cast)
            _window?.UpdateState(cast);
    }
}
