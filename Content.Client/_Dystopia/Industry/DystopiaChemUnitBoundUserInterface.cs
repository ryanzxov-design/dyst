// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню химической установки — связь окна с сервером.

using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaChemUnitBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaChemUnitWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DystopiaChemUnitWindow>();
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.WindowTitle = meta.EntityName;

        _window.OnToggle += () => SendMessage(new DystopiaChemUnitToggleMessage());
        _window.OnSelect += reaction => SendMessage(new DystopiaChemUnitSelectMessage(reaction));
        _window.OnTransfer += (from, to, reagent, amount) =>
            SendMessage(new DystopiaChemUnitTransferMessage(from, to, reagent, amount));
        _window.OnEject += () => SendMessage(new DystopiaChemUnitEjectMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DystopiaChemUnitUiState cast)
            _window?.UpdateState(cast);
    }
}
