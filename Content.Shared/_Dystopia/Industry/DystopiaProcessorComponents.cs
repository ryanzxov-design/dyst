// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: машины переработки руды — дробилки (×2), мельницы и обогатители (×3). Одна машина — одна группа
// рецептов; числа — в полях компонента и в прототипах рецептов.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

/// <summary>
/// Машина переработки: в загрузку кладут сырьё, за цикл выполняется до <see cref="BatchesPerCycle"/> партий рецептов
/// своей группы (<see cref="DystopiaProcessRecipePrototype"/>). Ручная работает, пока игрок крутит/качает (каждый
/// цикл — работа рукой), остальные — сами, пока есть сырьё, ток и место для продукции.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaProcessorComponent : Component
{
    /// <summary>Группа рецептов: Crusher, Mill, Concentrator...</summary>
    [DataField(required: true)]
    public string Group = string.Empty;

    /// <summary>Контейнер загрузки.</summary>
    [DataField]
    public string Container = "dystopia_processor_input";

    /// <summary>
    /// Контейнер готовой продукции (его разгружает конвейерный извлекатель). Нет — продукция падает на пол.
    /// </summary>
    [DataField]
    public string? OutputContainer;

    /// <summary>Сколько единиц сырья влезает в загрузку. 0 — загрузки нет, машина работает только на веществах бака.</summary>
    [DataField]
    public int Capacity = 30;

    /// <summary>Сколько единиц продукции держит машина; полна — встаёт.</summary>
    [DataField]
    public int OutputCapacity = 60;

    /// <summary>Сколько партий рецепта выполняется за цикл.</summary>
    [DataField]
    public int BatchesPerCycle = 1;

    /// <summary>Длительность цикла, секунды. У ручной — сколько работать рукой на один цикл.</summary>
    [DataField]
    public float CycleTime = 4f;

    /// <summary>Ручная: цикл идёт, только пока игрок работает. Иначе — сама, от сети.</summary>
    [DataField]
    public bool Manual;

    /// <summary>Работает только прикрученной к полу.</summary>
    [DataField]
    public bool RequireAnchored = true;

    /// <summary>Бак (раствор) для реагентов рецепта — например, воды у обогатителя.</summary>
    [DataField]
    public string? Tank;

    /// <summary>
    /// Отдельный бак продукции: вещества рецептов (produces) идут сюда, а не в <see cref="Tank"/>, чтобы не
    /// мешаться с тем, что залили. Нет — в <see cref="Tank"/>.
    /// </summary>
    [DataField]
    public string? OutputTank;

    /// <summary>Подпись действия ручной работы («Крутить ворот», «Качать лоток»).</summary>
    [DataField]
    public LocId WorkVerb = "dystopia-processor-work-verb";

    [DataField]
    public SoundSpecifier? CycleSound;

    [DataField]
    public SoundSpecifier? InsertSound;

    // --- состояние ---

    [ViewVariables]
    public bool Running;

    [ViewVariables]
    public TimeSpan CycleEnd;
}

/// <summary>
/// Рецепт переработки: <see cref="InputCount"/> единиц стека <see cref="Input"/> (и реагенты из бака) →
/// <see cref="Amount"/> штук <see cref="Output"/> и побочные продукты.
/// </summary>
[Prototype]
public sealed partial class DystopiaProcessRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Группа машин, которые это делают.</summary>
    [DataField(required: true)]
    public string Group = string.Empty;

    /// <summary>
    /// Основное сырьё. Нет — рецепт только из веществ бака (контактный аппарат, хлор-щелочной электролиз...):
    /// партия идёт, как только в баке хватает реагентов.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype>? Input;

    /// <summary>Сколько единиц сырья на одну партию.</summary>
    [DataField]
    public int InputCount = 1;

    /// <summary>Ещё сырьё на партию, кроме основного (песок и щебень к цементу...).</summary>
    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> ExtraInputs = new();

    /// <summary>Основная продукция (одиночный предмет стека). Нет — рецепт даёт только вещества в бак.</summary>
    [DataField]
    public EntProtoId? Output;

    [DataField]
    public int Amount = 1;

    /// <summary>Побочная продукция на партию.</summary>
    [DataField]
    public Dictionary<EntProtoId, int> Byproducts = new();

    /// <summary>Реагенты из бака машины на партию.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    /// <summary>Вещества, которые партия даёт в бак машины (ртуть из обжига, натрий и хлор из электролиза...).</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Produces = new();
}

[Serializable, NetSerializable]
public enum DystopiaProcessorVisuals : byte
{
    Running,
}

[Serializable, NetSerializable]
public sealed partial class DystopiaProcessorWorkDoAfterEvent : SimpleDoAfterEvent
{
}
