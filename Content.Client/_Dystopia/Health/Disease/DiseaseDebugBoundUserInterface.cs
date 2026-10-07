// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: окно отладочного инструмента «Болезни».

using Content.Shared._Dystopia.Health.Disease;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Health.Disease;

public sealed partial class DiseaseDebugBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DiseaseDebugWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DiseaseDebugWindow>();
        _window.OnInfect += (proto, force) => SendMessage(new DiseaseDebugInfectMessage(proto, force));
        _window.OnInfectRandom += complexity => SendMessage(new DiseaseDebugInfectRandomMessage(complexity));
        _window.OnCure += disease => SendMessage(new DiseaseDebugCureMessage(disease));
        _window.OnCureAll += () => SendMessage(new DiseaseDebugCureAllMessage());
        _window.OnProgress += (disease, infection, immunity) => SendMessage(new DiseaseDebugProgressMessage(disease, infection, immunity));
        _window.OnMutate += disease => SendMessage(new DiseaseDebugMutateMessage(disease));
        _window.OnClearImmunity += () => SendMessage(new DiseaseDebugClearImmunityMessage());
        _window.OnSpread += () => SendMessage(new DiseaseDebugSpreadMessage());
        _window.OnRefresh += () => SendMessage(new DiseaseDebugRefreshMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DiseaseDebugBuiState cast)
            _window?.UpdateState(cast);
    }
}
