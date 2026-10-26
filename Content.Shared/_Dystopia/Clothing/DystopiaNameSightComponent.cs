// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Clothing;

/// <summary>
/// Dystopia: надетая вещь (гарнитура в ушах, маска на лице), с которой видны настоящие имена тех, кто говорит из-под
/// маски Стражи (а не их номера гражданина). Маска считается, только пока не приспущена.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DystopiaNameSightComponent : Component;
