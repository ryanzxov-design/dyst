// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: фосфорные боеприпасы. Белый фосфор горит сам по себе: цель пылает заданное время,
// вода и катание по полу огонь не сбивают. Система — Content.Server/_Dystopia/Weapons/Ammunition/PhosphorusSystem.cs

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Dystopia.Weapons.Ammunition;

/// <summary>
/// Снаряд с белым фосфором: при попадании поджигает цель, и та горит не меньше <see cref="Duration"/>.
/// </summary>
[RegisterComponent]
public sealed partial class PhosphorusOnHitComponent : Component
{
    /// <summary>
    /// Сколько горит фосфор.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Сколько огня держит фосфор на цели (ниже не опускается, пока горит).
    /// </summary>
    [DataField]
    public float FireStacks = 3f;
}

/// <summary>
/// На цели горит фосфор: огонь не гаснет до <see cref="EndTime"/>.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PhosphorusBurningComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan EndTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField]
    public float FireStacks = 3f;

    /// <summary>
    /// Можно ли было потушить цель до фосфора — вернём, когда он догорит.
    /// </summary>
    [DataField]
    public bool CouldExtinguish = true;
}
