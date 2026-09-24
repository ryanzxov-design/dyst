using Content.Shared.Roles;
using Robust.Shared.Prototypes;

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

    /// <summary>Журнал операций казны (для Консоли Управления Городом). Новые записи — в конце.</summary>
    [ViewVariables]
    public List<string> Log = new();

    /// <summary>Сколько последних записей журнала хранить.</summary>
    [DataField]
    public int MaxLogEntries = 100;
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
}
