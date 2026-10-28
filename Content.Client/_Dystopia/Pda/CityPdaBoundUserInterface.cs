// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: интерфейс КПК Города — то же, что ванильный PdaBoundUserInterface (те же сообщения серверу), но окно —
// старый янтарный терминал CityPdaWindow в корпусе своей службы. Подключается в pdas.yml у КПК Города.

using Content.Client.CartridgeLoader;
using Content.Shared._Dystopia.Pda;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Pda;

[UsedImplicitly]
public sealed class CityPdaBoundUserInterface : CartridgeLoaderBoundUserInterface
{
    [ViewVariables]
    private CityPdaWindow? _menu;

    public CityPdaBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (_menu == null)
            CreateMenu();
    }

    private void CreateMenu()
    {
        _menu = this.CreateWindowCenteredLeft<CityPdaWindow>();

        _menu.OnLight += () => SendMessage(new PdaToggleFlashlightMessage());
        _menu.OnEjectId += () => SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaIdSlotId));
        _menu.OnEjectPen += () => SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPenSlotId));
        _menu.OnEjectPai += () => SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPaiSlotId));

        _menu.OnMusic += () => SendMessage(new PdaShowMusicMessage());
        _menu.OnRingtone += () => SendMessage(new PdaShowRingtoneMessage());
        _menu.OnShowUplink += () => SendMessage(new PdaShowUplinkMessage());
        _menu.OnLockUplink += () => SendMessage(new PdaLockUplinkMessage());

        _menu.OnProgramItemPressed += ActivateCartridge;
        _menu.OnInstallButtonPressed += InstallCartridge;
        _menu.OnUninstallButtonPressed += UninstallCartridge;
        _menu.OnExitProgram += DeactivateActiveCartridge;

        // корпус своей службы (pdas.yml); без него — казённая сталь по умолчанию
        if (EntMan.TryGetComponent<CityPdaCaseComponent>(Owner, out var pdaCase))
            _menu.ApplyCase(pdaCase);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PdaUpdateState update)
            _menu?.UpdateState(update);
    }

    protected override void AttachCartridgeUI(Control cartridgeUIFragment, string? title)
    {
        _menu?.ProgramView.AddChild(cartridgeUIFragment);
        _menu?.ToProgramView(title ?? Loc.GetString("comp-pda-io-program-fallback-title"));
    }

    protected override void DetachCartridgeUI(Control cartridgeUIFragment)
    {
        if (_menu is null)
            return;

        _menu.HideProgramHeader();
        _menu.ToHomeScreen();
        _menu.ProgramView.RemoveChild(cartridgeUIFragment);
    }

    protected override void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
    {
        _menu?.UpdateAvailablePrograms(programs);
    }
}
