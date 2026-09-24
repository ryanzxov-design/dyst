using System.Linq;
using Content.Server.Popups;
using Content.Shared._Dystopia.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Консоль Управления Городом, раздел «Казна»: ставки зарплат и налогов, премии, изъятия, журнал.
/// Все действия проверяют доступ (AccessReader консоли) на сервере.
/// </summary>
public sealed partial class CityConsoleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;

    private const int MaxSalary = 100000;
    private const int MaxReasonLength = 120;
    private const int ShownLogEntries = 50;

    private float _refreshAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleSetRatesMessage>(OnSetRates);
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleBonusMessage>(OnBonus);
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleSeizeMessage>(OnSeize);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Раз в секунду обновляем открытые консоли: таймер выплаты, казна, новые жители.
        _refreshAccumulator += frameTime;
        if (_refreshAccumulator < 1f)
            return;
        _refreshAccumulator = 0f;

        var query = EntityQueryEnumerator<CityConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, CityConsoleUiKey.Key))
                UpdateUi(uid);
        }
    }

    private void OnOpened(Entity<CityConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner);
    }

    private void UpdateAllConsoles()
    {
        var query = EntityQueryEnumerator<CityConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, CityConsoleUiKey.Key))
                UpdateUi(uid);
        }
    }

    private void UpdateUi(EntityUid console)
    {
        if (!_bank.TryGetBank(out var bank))
        {
            _ui.SetUiState(console, CityConsoleUiKey.Key,
                new CityConsoleBoundUserInterfaceState(0, 0, new(), new(), new()));
            return;
        }

        var jobs = new List<CityConsoleJobEntry>();
        var jobIds = bank.Comp.Salaries.Keys.Concat(bank.Comp.TaxRates.Keys).Distinct();
        foreach (var jobId in jobIds)
        {
            jobs.Add(new CityConsoleJobEntry(
                jobId.Id,
                JobName(jobId),
                bank.Comp.Salaries.GetValueOrDefault(jobId),
                bank.Comp.TaxRates.GetValueOrDefault(jobId)));
        }

        var accounts = bank.Comp.Accounts.Values
            .OrderBy(a => a.Name)
            .Select(a => new CityConsoleAccountEntry(
                a.Id,
                a.Name,
                a.Job is { } job ? JobName(job) : "-",
                a.Balance,
                a.Frozen))
            .ToList();

        var log = bank.Comp.Log.TakeLast(ShownLogEntries).Reverse().ToList();
        var seconds = Math.Max(0, (int) (bank.Comp.NextPayday - _timing.CurTime).TotalSeconds);

        _ui.SetUiState(console, CityConsoleUiKey.Key,
            new CityConsoleBoundUserInterfaceState(bank.Comp.Treasury, seconds, jobs, accounts, log));
    }

    private string JobName(ProtoId<JobPrototype> job)
    {
        return ProtoMan.TryIndex(job, out var proto) ? proto.LocalizedName : job.Id;
    }

    /// <summary>Проверка доступа на сервере: клиенту не доверяем.</summary>
    private bool CheckAccess(EntityUid console, EntityUid actor)
    {
        if (_access.IsAllowed(actor, console))
            return true;

        _popup.PopupEntity(Loc.GetString("dystopia-city-console-access-denied"), console, actor);
        return false;
    }

    private string CleanReason(string reason)
    {
        reason = reason.Trim();
        if (reason.Length > MaxReasonLength)
            reason = reason[..MaxReasonLength];

        return string.IsNullOrEmpty(reason) ? Loc.GetString("dystopia-city-console-no-reason") : reason;
    }

    private void OnSetRates(Entity<CityConsoleComponent> ent, ref CityConsoleSetRatesMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank))
            return;

        var job = new ProtoId<JobPrototype>(args.JobId);
        if (!bank.Comp.Salaries.ContainsKey(job) && !bank.Comp.TaxRates.ContainsKey(job))
            return;

        var salary = Math.Clamp(args.Salary, 0, MaxSalary);
        var tax = Math.Clamp(args.Tax, 0, 100);

        bank.Comp.Salaries[job] = salary;
        bank.Comp.TaxRates[job] = tax;

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-rates",
            ("actor", Name(args.Actor)), ("job", JobName(job)), ("salary", salary), ("tax", tax)));

        UpdateAllConsoles();
    }

    private void OnBonus(Entity<CityConsoleComponent> ent, ref CityConsoleBonusMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank))
            return;

        if (!bank.Comp.Accounts.TryGetValue(args.AccountId, out var account) || args.Amount <= 0)
            return;

        var reason = CleanReason(args.Reason);

        if (!_bank.TryPayBonus(bank, account, args.Amount, reason, out var net, out var tax))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-city-console-not-enough-treasury"), ent.Owner, args.Actor);
            return;
        }

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-bonus",
            ("actor", Name(args.Actor)), ("name", account.Name), ("id", account.Id),
            ("net", net), ("tax", tax), ("reason", reason)));

        UpdateAllConsoles();
    }

    private void OnSeize(Entity<CityConsoleComponent> ent, ref CityConsoleSeizeMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank))
            return;

        if (!bank.Comp.Accounts.TryGetValue(args.AccountId, out var account) || args.Amount <= 0)
            return;

        var reason = CleanReason(args.Reason);
        var taken = _bank.Seize(bank, account, args.Amount, reason);

        if (taken <= 0)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-city-console-nothing-to-seize"), ent.Owner, args.Actor);
            return;
        }

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-seize",
            ("actor", Name(args.Actor)), ("name", account.Name), ("id", account.Id),
            ("amount", taken), ("reason", reason)));

        UpdateAllConsoles();
    }
}
