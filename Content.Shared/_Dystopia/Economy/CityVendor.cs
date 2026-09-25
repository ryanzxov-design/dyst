using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>Товар городского автомата.</summary>
[DataDefinition]
public sealed partial class CityVendorEntry
{
    [DataField(required: true)]
    public EntProtoId Item;

    /// <summary>Цена в марках.</summary>
    [DataField]
    public int Price;

    /// <summary>Сколько осталось; -1 — без ограничения.</summary>
    [DataField]
    public int Amount = -1;
}

/// <summary>
/// Городской автомат: продаёт товары за марки. Оплата — с активной карты покупателя, выручка — в казну Города.
/// </summary>
[RegisterComponent]
public sealed partial class CityVendorComponent : Component
{
    [DataField]
    public List<CityVendorEntry> Inventory = new();
}

/// <summary>
/// Автомат пайков: бесплатный паёк раз в N минут на счёт. Выдаёт только тем, у кого есть доступ
/// рабочего места (AccessReader автомата) — то есть назначение от Гражданского Инспектора.
/// </summary>
[RegisterComponent]
public sealed partial class CityRationDispenserComponent : Component
{
    [DataField]
    public List<EntProtoId> Items = new();

    [DataField]
    public float CooldownMinutes = 20f;

    /// <summary>Номер счёта → когда можно получить следующий паёк.</summary>
    [ViewVariables]
    public Dictionary<int, TimeSpan> NextRation = new();
}

[Serializable, NetSerializable]
public enum CityVendorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CityVendorUiEntry(int index, string name, int price, int amount)
{
    public readonly int Index = index;
    public readonly string Name = name;
    public readonly int Price = price;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class CityVendorUiState(List<CityVendorUiEntry> items) : BoundUserInterfaceState
{
    public readonly List<CityVendorUiEntry> Items = items;
}

[Serializable, NetSerializable]
public sealed class CityVendorBuyMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}
