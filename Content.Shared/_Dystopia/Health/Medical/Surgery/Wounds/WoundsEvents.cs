// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Wounds;

[Serializable, NetSerializable]
public enum WoundableVisualizerKeys
{
    Wounds,
}

[ByRefEvent]
public record struct WoundAddedEvent(WoundComponent Component, WoundableComponent Woundable, WoundableComponent RootWoundable);

[ByRefEvent]
public record struct WoundRemovedEvent(WoundComponent Component, WoundableComponent OldWoundable, WoundableComponent OldRootWoundable);

[ByRefEvent]
public record struct WoundSeverityPointChangedEvent(WoundComponent Component, FixedPoint2 OldSeverity, FixedPoint2 NewSeverity, FixedPoint2? Overflow = null);

[ByRefEvent]
public record struct WoundSeverityChangedEvent(WoundSeverity OldSeverity, WoundSeverity NewSeverity);

[ByRefEvent]
public record struct WoundableIntegrityChangedEvent(FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct WoundableIntegrityChangedOnBodyEvent(Entity<WoundableComponent> Woundable, FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct WoundableSeverityChangedEvent(WoundableSeverity OldSeverity, WoundableSeverity NewSeverity);

[ByRefEvent]
public record struct WoundHealAttemptEvent(Entity<WoundableComponent> Woundable, bool IgnoreBlockers = false, bool Cancelled = false, FixedPoint2 SeverityFloor = default);

[ByRefEvent]
public record struct WoundHealAttemptOnWoundableEvent(Entity<WoundComponent> Wound, bool Cancelled = false);

[Serializable, DataRecord]
public partial record struct WoundableSeverityMultiplier(FixedPoint2 Change, string Identifier = "Unspecified");

[Serializable, DataRecord]
public partial record struct WoundableHealingMultiplier(FixedPoint2 Change, string Identifier = "Unspecified");

