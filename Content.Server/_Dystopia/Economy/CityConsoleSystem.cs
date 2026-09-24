using System.Linq;
using Content.Server._Dystopia.Laws;
using Content.Server.Popups;
using Content.Shared._Dystopia.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.AlertLevel;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Chat;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Консоль Управления Городом.
/// «Казна»: ставки зарплат и налогов, премии, изъятия, журнал.
/// «Положения»: режимы Города (уровни тревоги станции) и консульские уведомления.
/// «Законы»: Свод законов и шкала санкций (хранит и раздаёт в КПК CityLawsSystem).
/// Все действия проверяют доступ (AccessReader консоли) на сервере.
/// </summary>
public sealed partial class CityConsoleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private CityLawsSystem _laws = default!;
    [Dependency] private GameTicker _ticker = default!;

    private static readonly Color AnnouncementColor = Color.FromHex("#D9B44A");

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
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleSetModeMessage>(OnSetMode);
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleAnnounceMessage>(OnAnnounce);
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleNewLawMessage>(OnNewLaw);
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleSaveLawMessage>(OnSaveLaw);
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleDeleteLawMessage>(OnDeleteLaw);
        SubscribeLocalEvent<CityConsoleComponent, CityConsoleSaveSanctionsMessage>(OnSaveSanctions);
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
                new CityConsoleBoundUserInterfaceState(0, 0, new(), new(), new(), new(), string.Empty, 0, new(), new(), string.Empty));
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

        // Положения: уровни тревоги станции Города (банк висит на той же станции).
        var modes = new List<CityConsoleModeEntry>();
        var currentMode = string.Empty;
        if (TryComp<AlertLevelComponent>(bank.Owner, out var alert))
        {
            currentMode = alert.CurrentAlertLevel.Id;
            foreach (var level in alert.AvailableAlertLevels)
            {
                if (!ProtoMan.TryIndex(level, out var proto))
                    continue;

                modes.Add(new CityConsoleModeEntry(
                    level.Id,
                    proto.LocalizedName,
                    _alertLevel.AlertLevelInstructions(proto),
                    proto.Color));
            }
        }

        var cooldown = 0;
        if (TryComp<CityConsoleComponent>(console, out var consoleComp))
            cooldown = Math.Max(0, (int) Math.Ceiling((consoleComp.NextAnnouncement - _timing.CurTime).TotalSeconds));

        var lawsState = _laws.GetUiState();

        _ui.SetUiState(console, CityConsoleUiKey.Key,
            new CityConsoleBoundUserInterfaceState(bank.Comp.Treasury, seconds, jobs, accounts, log, modes, currentMode, cooldown,
                lawsState.Laws, lawsState.Sanctions, lawsState.GeneralProvision));
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

    private void OnSetMode(Entity<CityConsoleComponent> ent, ref CityConsoleSetModeMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank))
            return;

        if (!TryComp<AlertLevelComponent>(bank.Owner, out var alert))
            return;

        var level = new ProtoId<AlertLevelPrototype>(args.ModeId);
        if (alert.CurrentAlertLevel == level)
            return;

        // Проверяем, что такое положение есть у Города. Перебором, а не .Contains():
        // AlertLevelComponent разрешает чужим системам только чтение полей.
        var available = false;
        foreach (var candidate in alert.AvailableAlertLevels)
        {
            if (candidate != level)
                continue;

            available = true;
            break;
        }

        if (!available)
            return;

        if (_timing.CurTime < ent.Comp.NextModeChange)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-city-console-cooldown"), ent.Owner, args.Actor);
            return;
        }

        if (!ProtoMan.TryIndex(level, out var proto))
            return;

        ent.Comp.NextModeChange = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ModeChangeCooldown);

        // Звук положения играет AlertLevelSystem, а объявление делаем своё — от имени Консула.
        _alertLevel.SetLevel((bank.Owner, alert), level, playSound: true, announce: false, force: true);

        var text = Loc.GetString("dystopia-city-console-mode-announcement",
            ("name", proto.LocalizedName),
            ("announcement", _alertLevel.AlertLevelAnnouncement(proto)),
            ("instructions", _alertLevel.AlertLevelInstructions(proto)));

        _chat.DispatchStationAnnouncement(bank.Owner, text,
            sender: Loc.GetString("dystopia-city-console-sender"),
            playDefaultSound: proto.Sound == null,
            colorOverride: proto.Color);

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-mode",
            ("actor", Name(args.Actor)), ("name", proto.LocalizedName)));

        UpdateAllConsoles();
    }

    private void OnAnnounce(Entity<CityConsoleComponent> ent, ref CityConsoleAnnounceMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank))
            return;

        if (_timing.CurTime < ent.Comp.NextAnnouncement)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-city-console-cooldown"), ent.Owner, args.Actor);
            return;
        }

        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var text = SharedChatSystem.SanitizeAnnouncement(args.Text, maxLength);
        if (string.IsNullOrWhiteSpace(text))
            return;

        ent.Comp.NextAnnouncement = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.AnnouncementCooldown);

        _chat.DispatchStationAnnouncement(bank.Owner, text,
            sender: Loc.GetString("dystopia-city-console-sender"),
            playDefaultSound: true,
            colorOverride: AnnouncementColor);

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-announce",
            ("actor", Name(args.Actor)), ("text", text)));

        UpdateAllConsoles();
    }

    private void OnNewLaw(Entity<CityConsoleComponent> ent, ref CityConsoleNewLawMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank) || !_laws.TryGetLaws(out var laws))
            return;

        var law = _laws.CreateLaw(laws, _ticker.RoundDuration());
        if (law == null)
            return;

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-law-new",
            ("actor", Name(args.Actor)), ("number", law.Number)));

        UpdateAllConsoles();
    }

    private void OnSaveLaw(Entity<CityConsoleComponent> ent, ref CityConsoleSaveLawMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank) || !_laws.TryGetLaws(out var laws))
            return;

        var law = _laws.UpdateLaw(laws, args.Id, args.Number, args.Title, args.Text, args.Sanction, _ticker.RoundDuration());
        if (law == null)
            return;

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-law-save",
            ("actor", Name(args.Actor)), ("number", law.Number), ("title", law.Title)));

        UpdateAllConsoles();
    }

    private void OnDeleteLaw(Entity<CityConsoleComponent> ent, ref CityConsoleDeleteLawMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank) || !_laws.TryGetLaws(out var laws))
            return;

        var law = _laws.DeleteLaw(laws, args.Id);
        if (law == null)
            return;

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-law-delete",
            ("actor", Name(args.Actor)), ("number", law.Number), ("title", law.Title)));

        UpdateAllConsoles();
    }

    private void OnSaveSanctions(Entity<CityConsoleComponent> ent, ref CityConsoleSaveSanctionsMessage args)
    {
        if (!CheckAccess(ent.Owner, args.Actor) || !_bank.TryGetBank(out var bank) || !_laws.TryGetLaws(out var laws))
            return;

        _laws.SetSanctions(laws, args.Sanctions, args.GeneralProvision);

        _bank.AddLog(bank, Loc.GetString("dystopia-city-console-log-sanctions", ("actor", Name(args.Actor))));

        UpdateAllConsoles();
    }
}
