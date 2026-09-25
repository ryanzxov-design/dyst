using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>
/// Платёжный терминал Лавки. Привязывается к счёту продавца, продавец выставляет счёт,
/// покупатель прикладывает карту или КПК — деньги переходят со счёта на счёт (с налогом продавца).
/// Логика — на сервере (PaymentTerminalSystem).
/// </summary>
[RegisterComponent]
public sealed partial class PaymentTerminalComponent : Component
{
    /// <summary>Счёт, на который идут оплаты.</summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int? LinkedAccount;

    [ViewVariables(VVAccess.ReadWrite)]
    public int PendingAmount;

    [ViewVariables(VVAccess.ReadWrite)]
    public string PendingDescription = string.Empty;

    /// <summary>Номер текущего счёта к оплате: меняется при каждом выставлении, чтобы нельзя было оплатить старый.</summary>
    [ViewVariables]
    public int BillId;

    /// <summary>Сколько секунд прикладывать карту.</summary>
    [DataField]
    public float PayDelay = 1.5f;

    [DataField]
    public int MaxAmount = 100000;
}

[Serializable, NetSerializable]
public enum PaymentTerminalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PaymentTerminalUiState(int? linkedAccount, string linkedName, int pendingAmount, string pendingDescription, int taxRate)
    : BoundUserInterfaceState
{
    public readonly int? LinkedAccount = linkedAccount;
    public readonly string LinkedName = linkedName;
    public readonly int PendingAmount = pendingAmount;
    public readonly string PendingDescription = pendingDescription;

    /// <summary>Налог профессии владельца привязанного счёта, в процентах.</summary>
    public readonly int TaxRate = taxRate;
}

[Serializable, NetSerializable]
public sealed class PaymentTerminalSetBillMessage(int amount, string description) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
    public readonly string Description = description;
}

[Serializable, NetSerializable]
public sealed class PaymentTerminalCancelBillMessage : BoundUserInterfaceMessage
{
}

/// <summary>Привязать терминал к счёту активной карты того, кто нажал.</summary>
[Serializable, NetSerializable]
public sealed class PaymentTerminalLinkMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class PaymentTerminalUnlinkMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class PaymentTerminalDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public int BillId;
}
