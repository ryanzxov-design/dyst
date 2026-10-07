// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goobstation.Disease;

namespace Content.Server._Dystopia.Health.Disease;

/// <summary>
/// Dystopia: на предмете (или игле шприца) осталась зараза. Хранит ссылки на болезни-источники:
/// заражение копирует источник, а когда источник вылечен — запись пропадает.
/// </summary>
[RegisterComponent, Access(typeof(DiseaseTransmissionSystem))]
public sealed partial class DiseaseContaminatedComponent : Component
{
    [ViewVariables]
    public List<DiseaseContamination> Entries = new();
}

public sealed class DiseaseContamination(EntityUid disease, DiseaseSpreadSpecifier spread, TimeSpan expires)
{
    /// <summary>
    /// Болезнь-источник (в теле больного).
    /// </summary>
    public EntityUid Disease = disease;

    /// <summary>
    /// Путь, сила и шанс передачи.
    /// </summary>
    public DiseaseSpreadSpecifier Spread = spread;

    /// <summary>
    /// Когда зараза выветрится.
    /// </summary>
    public TimeSpan Expires = expires;
}
