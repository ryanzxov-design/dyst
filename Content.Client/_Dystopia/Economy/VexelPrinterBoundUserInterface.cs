using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Economy;

public sealed partial class VexelPrinterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private VexelPrinterWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<VexelPrinterWindow>();
        _window.OnPrint += (amount, reason) => SendMessage(new VexelPrinterPrintMessage(amount, reason));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is VexelPrinterUiState cast)
            _window?.UpdateState(cast);
    }
}
