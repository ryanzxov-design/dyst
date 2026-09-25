using System.Linq;
using Content.Server.Popups;
using Content.Shared._Dystopia.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.Roles;
using Robust.Server.GameObjects;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Терминал банковских операций: просмотр реестра счетов и журнала операций, заморозка счетов.
/// Доступ — Стража или Администрация (AccessReader терминала), проверяется на сервере.
/// </summary>
public sealed partial class CityBankTerminalSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;

    private float _refreshAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityBankTerminalComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CityBankTerminalComponent, CityBankTerminalSetFrozenMessage>(OnSetFrozen);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _refreshAccumulator += frameTime;
        if (_refreshAccumulator < 2f)
            return;
        _refreshAccumulator = 0f;

        UpdateAllTerminals();
    }

    private void OnOpened(Entity<CityBankTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateAllTerminals()
    {
        var query = EntityQueryEnumerator<CityBankTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, CityBankTerminalUiKey.Key))
                UpdateUi((uid, comp));
        }
    }

    private void UpdateUi(Entity<CityBankTerminalComponent> ent)
    {
        if (!_bank.TryGetBank(out var bank))
        {
            _ui.SetUiState(ent.Owner, CityBankTerminalUiKey.Key, new CityBankTerminalUiState(new(), new()));
            return;
        }

        var accounts = bank.Comp.Accounts.Values
            .OrderBy(a => a.Id)
            .Select(a => new CityBankTerminalAccountEntry(
                a.Id,
                a.Name,
                a.Job is { } job && ProtoMan.TryIndex(job, out var proto) ? proto.LocalizedName : "-",
                a.Balance,
                a.Debt,
                a.Frozen))
            .ToList();

        var ledger = bank.Comp.Ledger.TakeLast(ent.Comp.ShownLedgerEntries).Reverse().ToList();

        _ui.SetUiState(ent.Owner, CityBankTerminalUiKey.Key, new CityBankTerminalUiState(accounts, ledger));
    }

    private void OnSetFrozen(Entity<CityBankTerminalComponent> ent, ref CityBankTerminalSetFrozenMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-city-console-access-denied"), ent.Owner, args.Actor);
            return;
        }

        if (!_bank.TryGetBank(out var bank) || !bank.Comp.Accounts.TryGetValue(args.AccountId, out var account))
            return;

        if (account.Frozen == args.Frozen)
            return;

        account.Frozen = args.Frozen;

        var actor = Name(args.Actor);
        _bank.AddLedger(bank, Loc.GetString(args.Frozen ? "dystopia-bank-ledger-frozen" : "dystopia-bank-ledger-unfrozen",
            ("id", account.Id), ("name", account.Name), ("actor", actor)), null, account.Id);
        _bank.AddHistory(bank, account, Loc.GetString(args.Frozen ? "dystopia-bank-history-frozen" : "dystopia-bank-history-unfrozen"));
        _bank.NotifyAccount(account, Loc.GetString(args.Frozen ? "dystopia-bank-frozen-notify" : "dystopia-bank-unfrozen-notify"));

        UpdateAllTerminals();
    }
}
