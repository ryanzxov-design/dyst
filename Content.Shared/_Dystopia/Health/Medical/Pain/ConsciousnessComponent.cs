// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Pain;

/// <summary>
/// Сознание: обмороки от кровопотери и боли. Обморок — принудительный сон (человек падает, ничего не видит
/// и не может действовать). Все числа настраиваются в YAML.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsciousnessComponent : Component
{
    /// <summary>Сейчас без сознания из-за потери крови или боли (для анализатора).</summary>
    [ViewVariables, AutoNetworkedField]
    public bool Unconscious;

    [ViewVariables]
    public TimeSpan NextCheck;

    [ViewVariables]
    public TimeSpan NextFaint;

    [ViewVariables]
    public TimeSpan NextDizzy;

    /// <summary>До какого времени длится обморок, который вызвали мы (потом человек приходит в себя сам).</summary>
    [ViewVariables]
    public TimeSpan? KnockedOutUntil;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>Ниже этой доли крови кружится голова (только сообщения).</summary>
    [DataField]
    public float DizzyBloodLevel = 0.85f;

    /// <summary>Ниже этой доли крови возможны обмороки.</summary>
    [DataField]
    public float FaintBloodLevel = 0.7f;

    /// <summary>Ниже этой доли крови человек без сознания постоянно.</summary>
    [DataField]
    public float UnconsciousBloodLevel = 0.5f;

    /// <summary>Шанс обморока за проверку у самой границы бессознательности (выше — пропорционально меньше).</summary>
    [DataField]
    public float MaxFaintChance = 0.25f;

    [DataField]
    public TimeSpan FaintDuration = TimeSpan.FromSeconds(6);

    /// <summary>Не чаще одного обморока за это время.</summary>
    [DataField]
    public TimeSpan FaintCooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan DizzyInterval = TimeSpan.FromSeconds(30);

    /// <summary>Статус-эффект, которым выражается потеря сознания.</summary>
    [DataField]
    public EntProtoId UnconsciousEffect = "StatusEffectForcedSleeping";
}
