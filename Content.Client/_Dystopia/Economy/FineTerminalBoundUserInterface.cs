using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Economy;

public sealed partial class FineTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private FineTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FineTerminalWindow>();
        _window.OnPrepare += (lawId, amount, reason) => SendMessage(new FineTerminalPrepareMessage(lawId, amount, reason));
        _window.OnClear += () => SendMessage(new FineTerminalClearMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FineTerminalUiState cast)
            _window?.UpdateState(cast);
    }
}
