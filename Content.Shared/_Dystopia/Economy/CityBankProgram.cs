using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>Программа КПК «Банк»: баланс, история, переводы. Работает со счётом карты, лежащей в этом КПК.</summary>
[RegisterComponent]
public sealed partial class CityBankCartridgeComponent : Component
{
}

[Serializable, NetSerializable]
public sealed class CityBankUiState(
    bool hasAccount,
    int accountId,
    string owner,
    int balance,
    int debt,
    bool frozen,
    List<string> history) : BoundUserInterfaceState
{
    public readonly bool HasAccount = hasAccount;
    public readonly int AccountId = accountId;
    public readonly string Owner = owner;
    public readonly int Balance = balance;
    public readonly int Debt = debt;
    public readonly bool Frozen = frozen;
    public readonly List<string> History = history;
}

/// <summary>Перевод на другой счёт из программы «Банк».</summary>
[Serializable, NetSerializable]
public sealed class CityBankTransferMessageEvent(int toAccount, int amount, string comment) : CartridgeMessageEvent
{
    public readonly int ToAccount = toAccount;
    public readonly int Amount = amount;
    public readonly string Comment = comment;
}
