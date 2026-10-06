// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;

/// <summary>Кость части тела. Лежит в контейнере «Bone» части.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoneComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? BoneWoundable;

    [DataField, AutoNetworkedField]
    public FixedPoint2 IntegrityCap = 60f;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BoneIntegrity = 60f;

    [ViewVariables, AutoNetworkedField]
    public BoneSeverity BoneSeverity = BoneSeverity.Normal;

    /// <summary>Стадия кости наступает, когда целостность опускается ниже значения (наименьшая подходящая).</summary>
    [DataField]
    public Dictionary<BoneSeverity, FixedPoint2> Thresholds = new()
    {
        { BoneSeverity.Normal, 40 },
        { BoneSeverity.Damaged, 25 },
        { BoneSeverity.Cracked, 10 },
        { BoneSeverity.Broken, 0 },
    };

    /// <summary>Громкость хруста по стадиям.</summary>
    [DataField]
    public Dictionary<BoneSeverity, float> BreakVolume = new()
    {
        { BoneSeverity.Damaged, -8f },
        { BoneSeverity.Cracked, 1f },
        { BoneSeverity.Broken, 6f },
    };

    /// <summary>Пока кость повреждена, рана на части не заживает ниже этой тяжести.</summary>
    [DataField]
    public Dictionary<BoneSeverity, FixedPoint2> HealSeverityFloor = new()
    {
        { BoneSeverity.Normal, 0 },
        { BoneSeverity.Damaged, 5 },
        { BoneSeverity.Cracked, 10 },
        { BoneSeverity.Broken, 30 },
    };

    [DataField]
    public SoundSpecifier BoneBreakSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");
}
