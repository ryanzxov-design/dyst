// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню дуговой сталеплавильной печи — связь окна с сервером.

using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

public sealed partial class DystopiaArcFurnaceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaArcFurnaceWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DystopiaArcFurnaceWindow>();
        _window.OnToggle += () => SendMessage(new DystopiaArcFurnaceToggleMessage());
        _window.OnSelect += reagent => SendMessage(new DystopiaArcFurnaceSelectMessage(reagent));
        _window.OnEjectSlag += () => SendMessage(new DystopiaArcFurnaceEjectSlagMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DystopiaArcFurnaceUiState cast)
            _window?.UpdateState(cast);
    }
}
