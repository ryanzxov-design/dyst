using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Economy;

public sealed partial class PaymentTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private PaymentTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaymentTerminalWindow>();
        _window.OnSetBill += (amount, description) => SendMessage(new PaymentTerminalSetBillMessage(amount, description));
        _window.OnCancelBill += () => SendMessage(new PaymentTerminalCancelBillMessage());
        _window.OnLink += () => SendMessage(new PaymentTerminalLinkMessage());
        _window.OnUnlink += () => SendMessage(new PaymentTerminalUnlinkMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PaymentTerminalUiState cast)
            _window?.UpdateState(cast);
    }
}
