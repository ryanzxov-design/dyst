// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;

[ImplicitDataDefinitionForInheritors]
public abstract partial class TraumaCause
{
    [DataField]
    public List<BodyPartType>? AllowedParts;

    public bool PartAllowed(BodyPartType partType) => AllowedParts == null || AllowedParts.Contains(partType);
}

/// <summary>Взрыв (осколки). Обработка взрывов по частям — позже.</summary>
public sealed partial class ExplosionCause : TraumaCause
{
    [DataField]
    public FixedPoint2 MinDamage = 40;

    [DataField]
    public float Chance = 0.25f;

    [DataField]
    public bool ScaleWithDamage = true;
}

/// <summary>Рана резко потяжелела.</summary>
public sealed partial class WoundSeverityCause : TraumaCause
{
    [DataField]
    public List<ProtoId<DamageTypePrototype>>? DamageTypes;

    [DataField]
    public TraumaChance? Chance;
}
