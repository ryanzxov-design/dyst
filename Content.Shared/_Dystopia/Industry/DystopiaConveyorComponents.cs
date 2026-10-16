// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: конвейерные устройства — извлекатель, вставщик, сортировщик. Берут предмет с клетки (или из хранилища)
// позади себя и кладут вперёд. «Перед» — сторона, куда устройство повёрнуто (при повороте 0 — вниз).

using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

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
