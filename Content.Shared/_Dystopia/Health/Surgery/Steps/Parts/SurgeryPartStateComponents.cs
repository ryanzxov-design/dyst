// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос Shitmed (Goob-Station, AGPL-3.0): состояния части тела во время операции (вешаются на часть тела).

using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Health.Surgery.Steps.Parts;

/// <summary>Сделан надрез.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IncisionOpenComponent : Component;

/// <summary>Края раны разведены.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkinRetractedComponent : Component;

/// <summary>Сосуды пережаты.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BleedersClampedComponent : Component;

/// <summary>Внутренние сосуды пережаты.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InternalBleedersClampedComponent : Component;

/// <summary>Кости распилены.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BonesSawedComponent : Component;

/// <summary>Кости раскрыты (рёбра, череп).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BonesOpenComponent : Component;

/// <summary>Часть распилена для ампутации или очистки культи.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BodyPartSawedComponent : Component;

/// <summary>Часть тела только что пришита — шов ещё не закрыт.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BodyPartReattachedComponent : Component;

/// <summary>Орган только что вставлен — ещё не закреплён.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganReattachedComponent : Component;

/// <summary>Кость вправлена костоправом — можно скреплять гелем.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BoneSetComponent : Component;
