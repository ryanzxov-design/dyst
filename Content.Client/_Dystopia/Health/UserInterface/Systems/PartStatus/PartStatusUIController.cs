// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Gameplay;
using Content.Client._Dystopia.Health.UserInterface.Systems.PartStatus.Widgets;
using Content.Shared._Dystopia.Health.PartStatus.Events;
using Content.Shared._Dystopia.Health.Targeting;
using Content.Client._Dystopia.Health.Targeting;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.Player;
using Robust.Shared.Utility;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Health.UserInterface.Systems.PartStatus;

public sealed partial class PartStatusUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<TargetingSystem>
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    private SpriteSystem _spriteSystem = default!;
    private TargetingComponent? _targetingComponent;
    private PartStatusControl? PartStatusControl => UIManager.GetActiveUIWidgetOrNull<PartStatusControl>();

    public void OnSystemLoaded(TargetingSystem system)
    {
        system.PartStatusStartup += AddPartStatusControl;
        system.PartStatusShutdown += RemovePartStatusControl;
        system.PartStatusUpdate += UpdatePartStatusControl;
    }

    public void OnSystemUnloaded(TargetingSystem system)
    {
        system.PartStatusStartup -= AddPartStatusControl;
        system.PartStatusShutdown -= RemovePartStatusControl;
        system.PartStatusUpdate -= UpdatePartStatusControl;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (PartStatusControl != null)
        {
            PartStatusControl.SetVisible(_targetingComponent != null);

            if (_targetingComponent != null)
                PartStatusControl.SetTextures(_targetingComponent.BodyStatus);
        }
    }

    public void AddPartStatusControl(TargetingComponent component)
    {
        _targetingComponent = component;

        if (PartStatusControl != null)
        {
            PartStatusControl.SetVisible(_targetingComponent != null);
            if (_targetingComponent != null)
                PartStatusControl.SetTextures(_targetingComponent.BodyStatus);
        }

    }

    public void RemovePartStatusControl()
    {
        if (PartStatusControl != null)
            PartStatusControl.SetVisible(false);

        _targetingComponent = null;
    }

    public void UpdatePartStatusControl(TargetingComponent component)
    {
        if (PartStatusControl != null && _targetingComponent != null)
            PartStatusControl.SetTextures(_targetingComponent.BodyStatus);
    }

    public Texture GetTexture(SpriteSpecifier specifier)
    {
        if (_spriteSystem == null)
            _spriteSystem = _entManager.System<SpriteSystem>();

        return _spriteSystem.Frame0(specifier);
    }

    public void GetPartStatusMessage()
    {
        if (_playerManager.LocalEntity is not { } user
            || _entManager.GetComponent<TargetingComponent>(user) is not { } targetingComponent
            || PartStatusControl == null
            || !_timing.IsFirstTimePredicted)
            return;

        var player = _entManager.GetNetEntity(user);
        _net.SendSystemNetworkMessage(new GetPartStatusEvent(player));
    }
}
