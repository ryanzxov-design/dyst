using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>
/// Терминал банковских операций (Стража и Администрация): реестр счетов, журнал операций, заморозка счетов.
/// </summary>
[RegisterComponent]
public sealed partial class CityBankTerminalComponent : Component
{
    /// <summary>Сколько последних записей реестра показывать.</summary>
    [DataField]
    public int ShownLedgerEntries = 300;
}

[Serializable, NetSerializable]
public enum CityBankTerminalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CityBankTerminalAccountEntry(int id, string name, string job, int balance, int debt, bool frozen)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string Job = job;
    public readonly int Balance = balance;
    public readonly int Debt = debt;
    public readonly bool Frozen = frozen;
}

[Serializable, NetSerializable]
public sealed class CityBankTerminalUiState(List<CityBankTerminalAccountEntry> accounts, List<CityBankLedgerEntry> ledger)
    : BoundUserInterfaceState
{
    public readonly List<CityBankTerminalAccountEntry> Accounts = accounts;

    /// <summary>Записи реестра, новые — первыми.</summary>
    public readonly List<CityBankLedgerEntry> Ledger = ledger;
}

/// <summary>Заморозить или разморозить счёт.</summary>
[Serializable, NetSerializable]
public sealed class CityBankTerminalSetFrozenMessage(int accountId, bool frozen) : BoundUserInterfaceMessage
{
    public readonly int AccountId = accountId;
    public readonly bool Frozen = frozen;
}
