// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос Shitmed (Goob-Station, AGPL-3.0): эффекты шагов операции.

using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Surgery.Effects;

/// <summary>Урон или лечение части при шаге (кровопотеря от надреза, прижигание...).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryDamageChangeEffectComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new();

    /// <summary>Множитель, если пациент спит.</summary>
    [DataField]
    public float SleepModifier = 0.5f;
}

/// <summary>Обработка ран: лечит раны группы урона части (мимо блокировок травм).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTendWoundsEffectComponent : Component
{
    [DataField]
    public ProtoId<DamageGroupPrototype> MainGroup = "Brute";

    /// <summary>Сколько тяжести ран снимает шаг.</summary>
    [DataField]
    public float Amount = 15f;

    /// <summary>Плюс эта доля от текущей тяжести ран группы.</summary>
    [DataField]
    public float HealMultiplier = 0.07f;
}

/// <summary>Пациент в сознании кричит на этом шаге.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepEmoteEffectComponent : Component
{
    [DataField]
    public ProtoId<EmotePrototype> Emote = "Scream";
}
