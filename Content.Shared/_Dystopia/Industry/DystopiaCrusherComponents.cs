// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: дробилки руды — ступень ×2 переработки (кусок руды → горсти дроблёной руды, тигель плавит их 1:1).

using Content.Shared.DoAfter;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

/// <summary>
/// Дробилка: в неё закладывают руду, за цикл дробится до <see cref="OresPerCycle"/> кусков. Ручная крутится игроком
/// (каждый цикл — работа рукой), остальные работают сами, пока есть руда и ток. Что во что дробится — прототипы
/// <see cref="DystopiaCrushRecipePrototype"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaCrusherComponent : Component
{
    /// <summary>Контейнер с загруженной рудой.</summary>
    [DataField]
    public string Container = "dystopia_crusher_input";

    /// <summary>Сколько кусков руды влезает в загрузку.</summary>
    [DataField]
    public int Capacity = 30;

    /// <summary>Сколько кусков руды дробится за один цикл.</summary>
    [DataField]
    public int OresPerCycle = 1;

    /// <summary>Длительность цикла, секунды. У ручной — сколько крутить ворот на один цикл.</summary>
    [DataField]
    public float CycleTime = 4f;

    /// <summary>Ручная: цикл идёт, только пока игрок крутит ворот. Иначе — сама, от сети.</summary>
    [DataField]
    public bool Manual;

    /// <summary>Работает только прикрученной к полу.</summary>
    [DataField]
    public bool RequireAnchored = true;

    /// <summary>Звук каждого цикла.</summary>
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

/// <summary>Что получается при дроблении одного куска руды.</summary>
[Prototype]
public sealed partial class DystopiaCrushRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Стек руды, которая дробится.</summary>
    [DataField(required: true)]
    public ProtoId<StackPrototype> Input;

    /// <summary>Что выходит (одиночный предмет стека).</summary>
    [DataField(required: true)]
    public EntProtoId Output;

    /// <summary>Сколько штук выходит из одного куска.</summary>
    [DataField]
    public int Amount = 2;
}

[Serializable, NetSerializable]
public enum DystopiaCrusherVisuals : byte
{
    Running,
}

[Serializable, NetSerializable]
public sealed partial class DystopiaCrusherCrankDoAfterEvent : SimpleDoAfterEvent
{
}
