// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: дуговая сталеплавильная печь (ДСП) — большая (3×3) электрическая печь с меню. Плавит загрузку как тигель
// (DystopiaCrucible на той же сущности), сама снимает шлак в стопку внутри и разливает в подведённый литейный
// желоб ТОЛЬКО выбранный сплав.

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

[RegisterComponent]
public sealed partial class DystopiaArcFurnaceComponent : Component
{
    /// <summary>Предел жара, K.</summary>
    [DataField]
    public float MaxTemperature = 2073.15f;

    /// <summary>Скорость нагрева, K в секунду.</summary>
    [DataField]
    public float HeatRate = 30f;

    /// <summary>Потребление включённой и выключенной печи, Вт.</summary>
    [DataField]
    public float PowerLoad = 15000f;

    [DataField]
    public float IdlePowerLoad = 200f;

    /// <summary>Как часто печь греет, снимает шлак и разливает, секунды.</summary>
    [DataField]
    public float UpdateInterval = 1f;

    /// <summary>Сколько расплава разливать за один раз (не больше).</summary>
    [DataField]
    public float PourAmount = 100f;

    /// <summary>Контейнер со снятым шлаком (его разгружает извлекатель).</summary>
    [DataField]
    public string SlagContainer = "dystopia_arc_slag";

    /// <summary>Сколько кусков шлака держит печь; полна — шлак остаётся в расплаве.</summary>
    [DataField]
    public int SlagCapacity = 120;

    /// <summary>Полуразмер печи в клетках (1 — печь 3×3): желоб ищется вокруг всего корпуса.</summary>
    [DataField]
    public int Footprint = 1;

    /// <summary>Что можно выбрать для разлива.</summary>
    [DataField(required: true)]
    public List<ProtoId<ReagentPrototype>> Castable = new();

    // --- состояние ---

    [DataField]
    public bool On;

    [DataField]
    public ProtoId<ReagentPrototype>? Selected;

    [ViewVariables]
    public TimeSpan NextUpdate;
}

[Serializable, NetSerializable]
public enum DystopiaArcFurnaceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DystopiaArcFurnaceVisuals : byte
{
    Working,
}

[Serializable, NetSerializable]
public sealed class DystopiaArcFurnaceReagent(string id, float amount)
{
    public readonly string Id = id;
    public readonly float Amount = amount;
}

[Serializable, NetSerializable]
public sealed class DystopiaArcFurnaceUiState : BoundUserInterfaceState
{
    public bool On;
    public bool Powered;
    public bool ChannelAttached;
    public float Temperature;
    public float MaxTemperature;
    public int Charge;
    public int MaxCharge;
    public float Volume;
    public float MaxVolume;
    public List<DystopiaArcFurnaceReagent> Contents = new();
    public string? Selected;
    public List<string> Castable = new();
    public int Slag;
    public int SlagCapacity;
}

[Serializable, NetSerializable]
public sealed class DystopiaArcFurnaceToggleMessage : BoundUserInterfaceMessage;

/// <summary>Выбрать сплав для разлива (null — не разливать).</summary>
[Serializable, NetSerializable]
public sealed class DystopiaArcFurnaceSelectMessage(string? reagent) : BoundUserInterfaceMessage
{
    public readonly string? Reagent = reagent;
}

[Serializable, NetSerializable]
public sealed class DystopiaArcFurnaceEjectSlagMessage : BoundUserInterfaceMessage;
