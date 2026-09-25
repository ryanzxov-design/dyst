using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Economy;

// Админ-команды Банка Города — для тестов, пока нет консоли Консула.

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CityBankInfoCommand : LocalizedEntityCommands
{
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override string Command => "citybank_info";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_bank.TryGetBank(out var bank))
        {
            shell.WriteError(Loc.GetString("cmd-citybank-no-bank"));
            return;
        }

        var next = Math.Max(0, (int) (bank.Comp.NextPayday - _timing.CurTime).TotalSeconds);
        shell.WriteLine(Loc.GetString("cmd-citybank-info-header",
            ("treasury", bank.Comp.Treasury), ("accounts", bank.Comp.Accounts.Count), ("next", next)));

        foreach (var account in bank.Comp.Accounts.Values.OrderBy(a => a.Id))
        {
            var frozen = account.Frozen ? " [ЗАМОРОЖЕН]" : string.Empty;
            var debt = account.Debt > 0 ? $" долг {account.Debt}" : string.Empty;
            shell.WriteLine($"  №{account.Id}  {account.Name}  ({account.Job?.Id ?? "-"})  {account.Balance}{debt}{frozen}");
        }
    }
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CityBankTreasuryCommand : LocalizedEntityCommands
{
    [Dependency] private CityBankSystem _bank = default!;

    public override string Command => "citybank_treasury";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var amount))
        {
            shell.WriteError(Loc.GetString("cmd-citybank-bad-amount"));
            return;
        }

        if (!_bank.TryGetBank(out var bank))
        {
            shell.WriteError(Loc.GetString("cmd-citybank-no-bank"));
            return;
        }

        bank.Comp.Treasury = Math.Max(0, bank.Comp.Treasury + amount);
        shell.WriteLine(Loc.GetString("cmd-citybank-treasury-done", ("treasury", bank.Comp.Treasury)));
    }
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CityBankDepositCommand : LocalizedEntityCommands
{
    [Dependency] private CityBankSystem _bank = default!;

    public override string Command => "citybank_deposit";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[0], out var id) || !int.TryParse(args[1], out var amount))
        {
            shell.WriteError(Loc.GetString("cmd-citybank-bad-amount"));
            return;
        }

        if (!_bank.TryGetBank(out var bank))
        {
            shell.WriteError(Loc.GetString("cmd-citybank-no-bank"));
            return;
        }

        if (!bank.Comp.Accounts.TryGetValue(id, out var account))
        {
            shell.WriteError(Loc.GetString("cmd-citybank-no-account", ("id", id)));
            return;
        }

        _bank.Deposit(account, amount);
        shell.WriteLine(Loc.GetString("cmd-citybank-deposit-done",
            ("id", account.Id), ("name", account.Name), ("balance", account.Balance)));
    }
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CityBankPaydayCommand : LocalizedEntityCommands
{
    [Dependency] private CityBankSystem _bank = default!;

    public override string Command => "citybank_payday";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_bank.TryGetBank(out var bank))
        {
            shell.WriteError(Loc.GetString("cmd-citybank-no-bank"));
            return;
        }

        var (paid, unpaid) = _bank.Payday(bank);
        shell.WriteLine(Loc.GetString("cmd-citybank-payday-done",
            ("paid", paid), ("unpaid", unpaid), ("treasury", bank.Comp.Treasury)));
    }
}
