// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: консоль Гражданской Инспекции в оформлении Города — связь окна с ванильной консолью ID-карт.

using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewManifest;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using static Content.Shared.Access.Components.IdCardConsoleComponent;

namespace Content.Client._Dystopia.Registry;

public sealed partial class CityIdConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CityIdConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        var levels = EntMan.TryGetComponent<IdCardConsoleComponent>(Owner, out var console)
            ? console.AccessLevels
            : new List<ProtoId<AccessLevelPrototype>>();

        _window = this.CreateWindow<CityIdConsoleWindow>();
        _window.SetAccessLevels(levels);
        _window.OnPrivilegedSlot += () => SendMessage(new ItemSlotButtonPressedEvent(PrivilegedIdCardSlotId));
        _window.OnTargetSlot += () => SendMessage(new ItemSlotButtonPressedEvent(TargetIdCardSlotId));
        _window.OnManifest += () => SendMessage(new CrewManifestOpenUiMessage());
        _window.OnSubmit += (name, title, access, job) =>
            SendMessage(new WriteToTargetIdMessage(name, title, access, job));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IdCardConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
