using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>
/// Консоль Управления Городом (Консул). Сама логика — на сервере (CityConsoleSystem).
/// </summary>
[RegisterComponent]
public sealed partial class CityConsoleComponent : Component
{
}

[Serializable, NetSerializable]
public enum CityConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CityConsoleJobEntry(string jobId, string name, int salary, int tax)
{
    public readonly string JobId = jobId;
    public readonly string Name = name;
    public readonly int Salary = salary;
    public readonly int Tax = tax;
}

[Serializable, NetSerializable]
public sealed class CityConsoleAccountEntry(int id, string name, string job, int balance, bool frozen)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string Job = job;
    public readonly int Balance = balance;
    public readonly bool Frozen = frozen;
}

[Serializable, NetSerializable]
public sealed class CityConsoleBoundUserInterfaceState(
    int treasury,
    int secondsToPayday,
    List<CityConsoleJobEntry> jobs,
    List<CityConsoleAccountEntry> accounts,
    List<string> log) : BoundUserInterfaceState
{
    public readonly int Treasury = treasury;
    public readonly int SecondsToPayday = secondsToPayday;
    public readonly List<CityConsoleJobEntry> Jobs = jobs;
    public readonly List<CityConsoleAccountEntry> Accounts = accounts;
    public readonly List<string> Log = log;
}

/// <summary>Изменить зарплату и налог профессии.</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleSetRatesMessage(string jobId, int salary, int tax) : BoundUserInterfaceMessage
{
    public readonly string JobId = jobId;
    public readonly int Salary = salary;
    public readonly int Tax = tax;
}

/// <summary>Выписать премию жителю из казны.</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleBonusMessage(int accountId, int amount, string reason) : BoundUserInterfaceMessage
{
    public readonly int AccountId = accountId;
    public readonly int Amount = amount;
    public readonly string Reason = reason;
}

/// <summary>Изъять средства со счёта жителя в казну.</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleSeizeMessage(int accountId, int amount, string reason) : BoundUserInterfaceMessage
{
    public readonly int AccountId = accountId;
    public readonly int Amount = amount;
    public readonly string Reason = reason;
}
