// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;

[Serializable, NetSerializable]
public enum OrganSeverity : byte
{
    Normal = 0,
    Damaged = 1,
    Destroyed = 2,
}

[Serializable, NetSerializable]
public enum BoneSeverity : byte
{
    Normal = 0,
    Damaged = 1,
    Cracked = 2,
    Broken = 3,
}

[ByRefEvent]
public record struct ApplyTraumaEvent(
    ProtoId<TraumaTypePrototype> TraumaType,
    Entity<WoundableComponent> Woundable,
    Entity<TraumaInflicterComponent> Inflicter,
    EntityUid Target,
    FixedPoint2 Severity,
    bool Handled = false);

[ByRefEvent]
public record struct BeforeTraumaInducedEvent(FixedPoint2 TraumaSeverity, EntityUid TraumaTarget, ProtoId<TraumaTypePrototype> TraumaType, bool Cancelled = false);

[ByRefEvent]
public record struct TraumaInducedEvent(Entity<TraumaComponent> Trauma, EntityUid TraumaTarget, FixedPoint2 TraumaSeverity, ProtoId<TraumaTypePrototype> TraumaType);

[ByRefEvent]
public record struct TraumaBeingRemovedEvent(Entity<TraumaComponent> Trauma, EntityUid TraumaTarget, FixedPoint2 TraumaSeverity, ProtoId<TraumaTypePrototype> TraumaType);

[ByRefEvent]
public record struct BoneIntegrityChangedEvent(Entity<BoneComponent> Bone, FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct BoneSeverityChangedEvent(Entity<BoneComponent> Bone, BoneSeverity OldSeverity, BoneSeverity NewSeverity);

[ByRefEvent]
public record struct OrganIntegrityChangedEvent(FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct OrganDamageSeverityChanged(OrganSeverity OldSeverity, OrganSeverity NewSeverity);

/// <summary>
/// Оторвать конечность (травма «отрыв»). Обрабатывает сервер: часть отделяется и падает рядом.
/// Поднимается широковещательно.
/// </summary>
[ByRefEvent]
public record struct DismemberRequestEvent(EntityUid Body, EntityUid Part, bool Handled = false);
