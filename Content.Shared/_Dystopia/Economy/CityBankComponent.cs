using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>
/// Банк Города: казна, реестр личных счетов жителей, ставки зарплат и налогов по профессиям.
/// Вешается на станцию Города (см. прототип DystopiaCityStation).
/// Вся логика — на сервере, в CityBankSystem.
/// </summary>
[RegisterComponent]
public sealed partial class CityBankComponent : Component
{
    /// <summary>Деньги в казне Города. Из неё платятся зарплаты, в неё идут налоги.</summary>
    [DataField]
    public int Treasury = 100000;

    /// <summary>Как часто выплачивается зарплата, в минутах.</summary>
    [DataField]
    public float PayIntervalMinutes = 10f;

    /// <summary>Время следующей выплаты. Выставляется автоматически.</summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextPayday = TimeSpan.Zero;

    /// <summary>Стартовый баланс нового счёта — столько зарплат профессии.</summary>
    [DataField]
    public int StartingSalaries = 2;

    /// <summary>Зарплата профессии за один период (до налога).</summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int> Salaries = new();

    /// <summary>Налог профессии в процентах (0–100) от любого дохода.</summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int> TaxRates = new();

    /// <summary>Реестр счетов: номер счёта -> счёт.</summary>
    [ViewVariables]
    public Dictionary<int, CityBankAccount> Accounts = new();

    /// <summary>Журнал решений Консула (Консоль Управления Городом). Новые записи — в конце.</summary>
    [ViewVariables]
    public List<string> Log = new();

    /// <summary>Сколько последних записей журнала решений хранить.</summary>
    [DataField]
    public int MaxLogEntries = 100;

    /// <summary>Банковский реестр: все движения денег (терминал банковских операций). Новые записи — в конце.</summary>
    [ViewVariables]
    public List<CityBankLedgerEntry> Ledger = new();

    [DataField]
    public int MaxLedgerEntries = 500;

    /// <summary>Сколько последних операций хранить в истории каждого счёта.</summary>
    [DataField]
    public int MaxHistoryEntries = 30;
}

/// <summary>
/// Личный счёт жителя. Деньги принадлежат жителю, ID-карта — лишь ключ к счёту.
/// </summary>
public sealed class CityBankAccount
{
    [ViewVariables] public int Id;
    [ViewVariables(VVAccess.ReadWrite)] public string Name = string.Empty;
    [ViewVariables(VVAccess.ReadWrite)] public ProtoId<JobPrototype>? Job;
    [ViewVariables(VVAccess.ReadWrite)] public int Balance;
    [ViewVariables(VVAccess.ReadWrite)] public EntityUid? Owner;
    [ViewVariables(VVAccess.ReadWrite)] public bool Frozen;

    /// <summary>Долг перед Городом (неоплаченные штрафы). Гасится из будущих доходов.</summary>
    [ViewVariables(VVAccess.ReadWrite)] public int Debt;

    /// <summary>История операций по счёту (для программы КПК «Банк»). Новые записи — в конце.</summary>
    [ViewVariables] public List<string> History = new();
}

/// <summary>Запись банковского реестра.</summary>
[Serializable, NetSerializable]
public sealed class CityBankLedgerEntry(string text, int? from, int? to)
{
    public readonly string Text = text;

    /// <summary>Счёт, с которого ушли деньги (null — казна или вне банка).</summary>
    public readonly int? From = from;

    /// <summary>Счёт, на который пришли деньги (null — казна или вне банка).</summary>
    public readonly int? To = to;
}
