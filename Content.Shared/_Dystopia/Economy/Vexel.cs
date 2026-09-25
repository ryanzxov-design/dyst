using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>
/// Вексельный принтер Гражданского Инспектора: печатает векселя на предъявителя из фонда.
/// </summary>
[RegisterComponent]
public sealed partial class VexelPrinterComponent : Component
{
    /// <summary>Фонд, из которого печатаются векселя.</summary>
    [DataField]
    public string Fund = "Rewards";

    [DataField]
    public int MaxAmount = 5000;
}

/// <summary>
/// Вексель на предъявителя. Кто использует его в руке — тому сумма и зачисляется (на активную карту).
/// </summary>
[RegisterComponent]
public sealed partial class VexelComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public int Amount;

    [ViewVariables(VVAccess.ReadWrite)]
    public string Serial = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public string Reason = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public string IssuedBy = string.Empty;
}

[Serializable, NetSerializable]
public enum VexelPrinterUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class VexelPrinterUiState(string fundName, int fundBalance, int maxAmount) : BoundUserInterfaceState
{
    public readonly string FundName = fundName;
    public readonly int FundBalance = fundBalance;
    public readonly int MaxAmount = maxAmount;
}

[Serializable, NetSerializable]
public sealed class VexelPrinterPrintMessage(int amount, string reason) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
    public readonly string Reason = reason;
}
