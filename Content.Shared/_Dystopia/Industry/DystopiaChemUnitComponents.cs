// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: химическая установка. Вещества в буфере не смешиваются и не реагируют (как в химмастере). Игрок выбирает,
// что производить (ванильную реакцию); установка берёт из буфера только нужные вещества в нужных пропорциях
// (катализаторы — не тратит), а готовое складывает в бак продукции. Остальное в буфере не трогает.
// Вещества заливают и забирают через слот «Контейнер» (любая ёмкость с раствором) или жидкостными экспортером
// (в буфер) и импортером (из бака продукции); напрямую ведром — нельзя.

using Content.Shared.Chemistry.Reaction;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

[RegisterComponent]
public sealed partial class DystopiaChemUnitComponent : Component
{
    /// <summary>Буфер: сюда заливают (ведром, экспортером из трубы). Вещества в нём не реагируют.</summary>
    [DataField]
    public string Buffer = "buffer";

    /// <summary>Бак продукции: отсюда забирают в контейнер или импортером.</summary>
    [DataField]
    public string Output = "output";

    /// <summary>Слот для ёмкости (мензурка, ведро, канистра — всё, у чего есть раствор).</summary>
    [DataField]
    public string ContainerSlot = "dystopia_chem_container";

    /// <summary>Что производить (ванильная реакция). Пусто — ничего.</summary>
    [DataField]
    public ProtoId<ReactionPrototype>? Reaction;

    [DataField]
    public bool On;

    /// <summary>Сколько единиц сырья (без катализаторов) перерабатывает за цикл.</summary>
    [DataField]
    public float UnitsPerCycle = 20f;

    [DataField]
    public float CycleTime = 2f;

    [ViewVariables]
    public TimeSpan NextCycle;

    [ViewVariables]
    public bool Working;
}

public static class DystopiaChemUnit
{
    /// <summary>
    /// Какие реакции установка умеет вести: с продуктами, без особых эффектов (взрывы, дым, пена) и без особого
    /// смесителя (центрифуга, электролиз, святая вода). Температура не важна — у установки свой нагрев и охлаждение.
    /// </summary>
    public static bool Allowed(ReactionPrototype reaction)
    {
        return reaction.Products.Count > 0 &&
               reaction.Effects.Length == 0 &&
               (reaction.MixingCategories == null || reaction.MixingCategories.Count == 0) &&
               reaction.Reactants.Count > 0;
    }

    /// <summary>Сколько «порций» реакции хватает в буфере (катализаторов должно быть не меньше нормы).</summary>
    public static float Portions(ReactionPrototype reaction, Func<string, FixedPoint2> amountOf)
    {
        var portions = float.MaxValue;
        foreach (var (id, info) in reaction.Reactants)
        {
            if (info.Amount <= FixedPoint2.Zero)
                continue;

            var have = amountOf(id.Id);
            if (info.Catalyst)
            {
                if (have < info.Amount)
                    return 0f;

                continue;
            }

            portions = MathF.Min(portions, have.Float() / info.Amount.Float());
        }

        return portions == float.MaxValue ? 0f : portions;
    }
}

[Serializable, NetSerializable]
public enum DystopiaChemUnitUiKey : byte
{
    Key,
}

/// <summary>Вещество в баке (для меню).</summary>
[Serializable, NetSerializable]
public sealed class DystopiaChemUnitReagent(string id, float amount)
{
    public readonly string Id = id;
    public readonly float Amount = amount;
}

[Serializable, NetSerializable]
public sealed class DystopiaChemUnitUiState(
    bool on,
    bool powered,
    bool working,
    string? reaction,
    float unitsPerCycle,
    float cycleTime,
    List<DystopiaChemUnitReagent> buffer,
    float bufferMax,
    List<DystopiaChemUnitReagent> output,
    float outputMax,
    string? container,
    List<DystopiaChemUnitReagent> containerReagents,
    float containerMax) : BoundUserInterfaceState
{
    public readonly bool On = on;
    public readonly bool Powered = powered;
    public readonly bool Working = working;
    public readonly string? Reaction = reaction;
    public readonly float UnitsPerCycle = unitsPerCycle;
    public readonly float CycleTime = cycleTime;
    public readonly List<DystopiaChemUnitReagent> Buffer = buffer;
    public readonly float BufferMax = bufferMax;
    public readonly List<DystopiaChemUnitReagent> Output = output;
    public readonly float OutputMax = outputMax;

    /// <summary>Название ёмкости в слоте (null — слот пуст).</summary>
    public readonly string? Container = container;
    public readonly List<DystopiaChemUnitReagent> ContainerReagents = containerReagents;
    public readonly float ContainerMax = containerMax;
}

[Serializable, NetSerializable]
public sealed class DystopiaChemUnitToggleMessage : BoundUserInterfaceMessage;

/// <summary>Что производить (null — ничего).</summary>
[Serializable, NetSerializable]
public sealed class DystopiaChemUnitSelectMessage(string? reaction) : BoundUserInterfaceMessage
{
    public readonly string? Reaction = reaction;
}

/// <summary>Откуда и куда переливать в меню.</summary>
[Serializable, NetSerializable]
public enum DystopiaChemUnitTank : byte
{
    Container,
    Buffer,
    Output,

    /// <summary>Только как «куда»: вылить (уничтожить).</summary>
    Discard,
}

/// <summary>Перелить вещество (amount ≤ 0 — всё) из одного бака в другой или вылить.</summary>
[Serializable, NetSerializable]
public sealed class DystopiaChemUnitTransferMessage(DystopiaChemUnitTank from, DystopiaChemUnitTank to, string reagent,
    float amount) : BoundUserInterfaceMessage
{
    public readonly DystopiaChemUnitTank From = from;
    public readonly DystopiaChemUnitTank To = to;
    public readonly string Reagent = reagent;
    public readonly float Amount = amount;
}

/// <summary>Достать ёмкость из слота в руки.</summary>
[Serializable, NetSerializable]
public sealed class DystopiaChemUnitEjectMessage : BoundUserInterfaceMessage;
