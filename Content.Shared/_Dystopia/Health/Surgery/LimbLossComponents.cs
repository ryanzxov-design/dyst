// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: потеря конечностей и протезы (скорость, ползание). Не зависит от хирургии.

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Health.Surgery;

/// <summary>Протез: скорость части ниже, чем у живой конечности.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryProstheticComponent : Component
{
    [DataField]
    public float SpeedMultiplier = 1f;
}

/// <summary>
/// Тело, у которого менялись конечности: скорость зависит от ног и стоп (нет ноги — ползёт, протез — медленнее).
/// Есть у всех видов (BaseSpeciesMob), так что работает при любой потере ноги, не только после операции.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryLimbLossComponent : Component
{
    /// <summary>Скорость стороны без стопы (хромота).</summary>
    [DataField]
    public float NoFootSpeed = 0.7f;

    /// <summary>Нижний предел скорости от конечностей и переломов.</summary>
    [DataField]
    public float MinimumSpeed = 0.15f;

    /// <summary>Ниже этой доли скорости (из-за переломов ног) не устоять на ногах.</summary>
    [DataField]
    public float CrippledSpeed = 1f / 3.4f;

    /// <summary>Доля, которую несёт нога с повреждённой костью (нет в списке — 1).</summary>
    [DataField]
    public Dictionary<BoneSeverity, float> LegBoneSpeed = new()
    {
        { BoneSeverity.Damaged, 0.625f },
        { BoneSeverity.Cracked, 0.5f },
        { BoneSeverity.Broken, 0f },
    };

    /// <summary>Множитель стопы с повреждённой костью.</summary>
    [DataField]
    public Dictionary<BoneSeverity, float> FootBoneSpeed = new()
    {
        { BoneSeverity.Damaged, 0.77f },
        { BoneSeverity.Cracked, 0.66f },
        { BoneSeverity.Broken, 0.55f },
    };

    /// <summary>Шанс, что рука дрогнет при ударе или выстреле, по худшей кости рук и кистей.</summary>
    [DataField]
    public Dictionary<BoneSeverity, float> FumbleChance = new()
    {
        { BoneSeverity.Cracked, 0.10f },
        { BoneSeverity.Broken, 0.25f },
    };

    [DataField]
    public SoundSpecifier FumbleSound = new SoundPathSpecifier("/Audio/Effects/slip.ogg");
}
