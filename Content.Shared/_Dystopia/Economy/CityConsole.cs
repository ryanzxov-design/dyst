using Content.Shared._Dystopia.Laws;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>
/// Консоль Управления Городом (Консул). Сама логика — на сервере (CityConsoleSystem).
/// </summary>
[RegisterComponent]
public sealed partial class CityConsoleComponent : Component
{
    /// <summary>Пауза между консульскими уведомлениями, в секундах.</summary>
    [DataField]
    public float AnnouncementCooldown = 60f;

    /// <summary>Пауза между сменами положения, в секундах.</summary>
    [DataField]
    public float ModeChangeCooldown = 10f;

    [ViewVariables]
    public TimeSpan NextAnnouncement = TimeSpan.Zero;

    [ViewVariables]
    public TimeSpan NextModeChange = TimeSpan.Zero;
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
public sealed class CityConsoleModeEntry(string id, string name, string instructions, Color color)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Instructions = instructions;
    public readonly Color Color = color;
}

[Serializable, NetSerializable]
public sealed class CityConsoleBoundUserInterfaceState(
    int treasury,
    int secondsToPayday,
    List<CityConsoleJobEntry> jobs,
    List<CityConsoleAccountEntry> accounts,
    List<string> log,
    List<CityConsoleModeEntry> modes,
    string currentMode,
    int announcementCooldown,
    List<CityLaw> laws,
    List<CitySanctionClass> sanctions,
    string generalProvision) : BoundUserInterfaceState
{
    public readonly int Treasury = treasury;
    public readonly int SecondsToPayday = secondsToPayday;
    public readonly List<CityConsoleJobEntry> Jobs = jobs;
    public readonly List<CityConsoleAccountEntry> Accounts = accounts;
    public readonly List<string> Log = log;
    public readonly List<CityConsoleModeEntry> Modes = modes;
    public readonly string CurrentMode = currentMode;
    public readonly int AnnouncementCooldown = announcementCooldown;
    public readonly List<CityLaw> Laws = laws;
    public readonly List<CitySanctionClass> Sanctions = sanctions;
    public readonly string GeneralProvision = generalProvision;
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

/// <summary>Ввести положение Города (уровень тревоги).</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleSetModeMessage(string modeId) : BoundUserInterfaceMessage
{
    public readonly string ModeId = modeId;
}

/// <summary>Консульское уведомление на весь Город.</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleAnnounceMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

/// <summary>Создать новую статью Свода законов.</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleNewLawMessage : BoundUserInterfaceMessage
{
}

/// <summary>Сохранить статью Свода законов.</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleSaveLawMessage(int id, int number, string title, string text, string sanction)
    : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly int Number = number;
    public readonly string Title = title;
    public readonly string Text = text;
    public readonly string Sanction = sanction;
}

/// <summary>Удалить статью Свода законов.</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleDeleteLawMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

/// <summary>Сохранить шкалу санкций.</summary>
[Serializable, NetSerializable]
public sealed class CityConsoleSaveSanctionsMessage(List<CitySanctionClass> sanctions, string generalProvision)
    : BoundUserInterfaceMessage
{
    public readonly List<CitySanctionClass> Sanctions = sanctions;
    public readonly string GeneralProvision = generalProvision;
}
