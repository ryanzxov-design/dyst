// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: терминал нефтепровода — связь окна с сервером.

using Content.Shared._Dystopia.Industry;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaPipelineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DystopiaPipelineWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DystopiaPipelineWindow>();
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.WindowTitle = meta.EntityName;

        _window.OnMode += mode => SendMessage(new DystopiaPipelineModeMessage(mode));
        _window.OnRate += rate => SendMessage(new DystopiaPipelineRateMessage(rate));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DystopiaPipelineUiState cast)
            _window?.UpdateState(cast);
    }
}
