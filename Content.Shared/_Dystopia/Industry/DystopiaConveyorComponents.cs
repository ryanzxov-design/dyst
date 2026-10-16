// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: конвейерные устройства — извлекатель, вставщик, сортировщик. Берут предмет с клетки (или из хранилища)
// позади себя и кладут вперёд. «Перед» — сторона, куда устройство повёрнуто (при повороте 0 — вниз).

using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

[Serializable, NetSerializable]
public enum DystopiaConveyorDeviceMode : byte
{
    /// <summary>Из хранилища позади — на клетку впереди.</summary>
    Extract,

    /// <summary>С клетки позади — в хранилище впереди.</summary>
    Insert,

    /// <summary>С клетки позади — на один из трёх выходов: зелёный (налево), синий (направо), жёлтый (вперёд).</summary>
    Sort,
}

[RegisterComponent]
public sealed partial class DystopiaConveyorDeviceComponent : Component
{
    [DataField(required: true)]
    public DystopiaConveyorDeviceMode Mode;

    /// <summary>Раз в столько секунд переносит один предмет (стопку).</summary>
    [DataField]
    public float Interval = 1f;

    /// <summary>Не класть на клетку, где уже лежит столько предметов (не заваливать конвейер).</summary>
    [DataField]
    public int MaxItemsOnTile = 8;

    [DataField]
    public bool RequireAnchored = true;

    /// <summary>Извлекатель и вставщик: фильтр — белый список (только эти) или чёрный (все, кроме этих).</summary>
    [DataField]
    public bool Whitelist;

    /// <summary>Фильтр извлекателя и вставщика: ключи предметов (тип стопки или прототип).</summary>
    [DataField]
    public HashSet<string> Filter = new();

    /// <summary>Сортировщик: что уходит в зелёный выход (налево).</summary>
    [DataField]
    public HashSet<string> SortGreen = new();

    /// <summary>Сортировщик: что уходит в синий выход (направо). Остальное — в жёлтый (вперёд).</summary>
    [DataField]
    public HashSet<string> SortBlue = new();

    [ViewVariables]
    public TimeSpan NextAction;
}

/// <summary>
/// Вставщик кладёт предмет в сущность. Системы машин (переработчики, тигли) отвечают, если берут;
/// ящики, шкафы, сумки и латы вставщик обрабатывает сам.
/// </summary>
[ByRefEvent]
public record struct DystopiaConveyorInsertEvent(EntityUid Item, bool Handled = false, bool Inserted = false);

/// <summary>
/// Извлекатель забирает предмет из сущности. Система машины отдаёт первый подходящий предмет (уже вынутый из её
/// контейнера) в <see cref="Item"/>.
/// </summary>
[ByRefEvent]
public record struct DystopiaConveyorExtractEvent(Predicate<EntityUid> Accept, bool Handled = false, EntityUid? Item = null);

[Serializable, NetSerializable]
public enum DystopiaConveyorDeviceVisuals : byte
{
    Working,
}

// ------------------------------------------------------------------ меню

[Serializable, NetSerializable]
public enum DystopiaConveyorUiKey : byte
{
    Key,
}

/// <summary>Какой список настраивается: фильтр извлекателя/вставщика или выходы сортировщика.</summary>
[Serializable, NetSerializable]
public enum DystopiaConveyorList : byte
{
    Filter,
    Green,
    Blue,
}

[Serializable, NetSerializable]
public sealed class DystopiaConveyorUiState(
    DystopiaConveyorDeviceMode mode,
    bool whitelist,
    List<string> filter,
    List<string> green,
    List<string> blue) : BoundUserInterfaceState
{
    public readonly DystopiaConveyorDeviceMode Mode = mode;
    public readonly bool Whitelist = whitelist;
    public readonly List<string> Filter = filter;
    public readonly List<string> Green = green;
    public readonly List<string> Blue = blue;
}

[Serializable, NetSerializable]
public sealed class DystopiaConveyorSetWhitelistMessage(bool whitelist) : BoundUserInterfaceMessage
{
    public readonly bool Whitelist = whitelist;
}

/// <summary>Добавить ключ в список (в сортировщике — убрав его из другого выхода).</summary>
[Serializable, NetSerializable]
public sealed class DystopiaConveyorAddMessage(DystopiaConveyorList list, string key) : BoundUserInterfaceMessage
{
    public readonly DystopiaConveyorList List = list;
    public readonly string Key = key;
}

[Serializable, NetSerializable]
public sealed class DystopiaConveyorRemoveMessage(DystopiaConveyorList list, string key) : BoundUserInterfaceMessage
{
    public readonly DystopiaConveyorList List = list;
    public readonly string Key = key;
}

/// <summary>Добавить в список предмет, который держит открывший меню.</summary>
[Serializable, NetSerializable]
public sealed class DystopiaConveyorAddHeldMessage(DystopiaConveyorList list) : BoundUserInterfaceMessage
{
    public readonly DystopiaConveyorList List = list;
}

[Serializable, NetSerializable]
public sealed class DystopiaConveyorClearMessage(DystopiaConveyorList list) : BoundUserInterfaceMessage
{
    public readonly DystopiaConveyorList List = list;
}

/// <summary>
/// Ключи фильтров: у стопок — тип стопки («stack:Steel» — все стопки стали сразу), у остального — прототип.
/// Общие для сервера (сверка предметов) и клиента (поиск и названия в меню).
/// </summary>
public static class DystopiaConveyorKeys
{
    public const string StackPrefix = "stack:";

    public static string KeyFor(EntityPrototype proto, IComponentFactory factory)
    {
        return proto.TryGetComponent<StackComponent>(out var stack, factory)
            ? StackPrefix + stack.StackTypeId.Id
            : proto.ID;
    }

    public static string Name(IPrototypeManager protoManager, string key)
    {
        if (key.StartsWith(StackPrefix, StringComparison.Ordinal) &&
            protoManager.TryIndex<StackPrototype>(key[StackPrefix.Length..], out var stack))
        {
            return Loc.GetString(stack.Name, ("amount", 1));
        }

        return protoManager.TryIndex<EntityPrototype>(key, out var proto) ? proto.Name : key;
    }
}
