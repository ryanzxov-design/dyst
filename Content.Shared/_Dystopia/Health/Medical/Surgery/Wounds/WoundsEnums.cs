// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds;

// Перечисления системы ран. События и данные ран добавляются в Ф3 вместе с самими ранами.

[Serializable, NetSerializable]
public enum WoundType
{
    External,
    Internal,
}

[Serializable, NetSerializable]
public enum WoundSeverity
{
    Healed,
    Minor,
    Moderate,
    Severe,
    Critical,
    Loss,
}

[Serializable, NetSerializable]
public enum BleedingSeverity
{
    Minor,
    Severe,
}

[Serializable, NetSerializable]
public enum WoundableSeverity : byte
{
    Healthy,
    Minor,
    Moderate,
    Severe,
    Critical,
    Mangled,
    Severed,
}

[Serializable, NetSerializable]
public enum WoundVisibility
{
    Always,
    HandScanner,
    AdvancedScanner,
}
