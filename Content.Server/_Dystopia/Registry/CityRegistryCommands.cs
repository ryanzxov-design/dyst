// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: админ-команды Реестра граждан — для тестов, пока нет консоли социоучёта.

using System.Linq;
using Content.Server.Administration;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Dystopia.Registry;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CityRegistryListCommand : LocalizedEntityCommands
{
    [Dependency] private CityRegistrySystem _registry = default!;

    public override string Command => "cityregistry_list";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_registry.TryGetRegistry(out var registry))
        {
            shell.WriteError(Loc.GetString("cmd-cityregistry-no-registry"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-cityregistry-list-header", ("count", registry.Comp.Citizens.Count)));
        foreach (var record in registry.Comp.Citizens.Values.OrderBy(r => r.Number))
        {
            var loyalty = record.Loyalty?.ToString() ?? "★";
            shell.WriteLine($"  №{record.Number}  {record.Name}  ({record.Job?.Id ?? "-"})  {_registry.StatusName(record.Status)}  {loyalty}");
        }
    }
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CityRegistryShowCommand : LocalizedEntityCommands
{
    [Dependency] private CityRegistrySystem _registry = default!;

    public override string Command => "cityregistry_show";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var number) || !_registry.TryGetRecord(number, out var record))
        {
            shell.WriteError(Loc.GetString("cmd-cityregistry-no-citizen"));
            return;
        }

        shell.WriteLine($"№{record.Number}  {record.Name}, {record.Age}  {record.Species.Id}  {record.Gender}");
        shell.WriteLine($"  {record.Job?.Id ?? "-"} / {record.Department?.Id ?? "-"}  отпечатки {record.Fingerprint ?? "-"}  ДНК {record.Dna ?? "-"}");
        shell.WriteLine($"  статус: {_registry.StatusName(record.Status)} {record.StatusReason}  лояльность: {record.Loyalty?.ToString() ?? "★"} (норма {record.LoyaltyNorm?.ToString() ?? "-"}){(record.Dismissed ? "  ОТСТРАНЁН" : string.Empty)}");
        if (record.Notes.Length > 0)
            shell.WriteLine($"  приметы: {record.Notes}");

        foreach (var entry in record.Journal)
        {
            var delta = entry.LoyaltyDelta != 0 ? $" [{entry.LoyaltyDelta:+#;-#}]" : string.Empty;
            shell.WriteLine($"  {entry.Time:hh\\:mm\\:ss}  {entry.Kind}  {entry.Text} {entry.Details}{delta}  — {entry.Author}");
        }
    }
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CityRegistryStatusCommand : LocalizedEntityCommands
{
    [Dependency] private CityRegistrySystem _registry = default!;

    public override string Command => "cityregistry_status";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out var number) ||
            !Enum.TryParse<CityCitizenStatus>(args[1], true, out var status))
        {
            shell.WriteError(Loc.GetString("cmd-cityregistry_status-help", ("command", Command)));
            return;
        }

        if (!_registry.TryGetRegistry(out var registry) || !registry.Comp.Citizens.TryGetValue(number, out var record))
        {
            shell.WriteError(Loc.GetString("cmd-cityregistry-no-citizen"));
            return;
        }

        var reason = string.Join(' ', args.Skip(2));
        if (!_registry.SetStatus(registry, record, status, reason, Loc.GetString("dystopia-registry-author-admin")))
        {
            shell.WriteError(Loc.GetString("cmd-cityregistry-status-unchanged"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-cityregistry-status-done", ("number", number.ToString()), ("status", _registry.StatusName(status))));
    }
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CityRegistryLoyaltyCommand : LocalizedEntityCommands
{
    [Dependency] private CityRegistrySystem _registry = default!;

    public override string Command => "cityregistry_loyalty";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out var number) || !int.TryParse(args[1], out var delta))
        {
            shell.WriteError(Loc.GetString("cmd-cityregistry_loyalty-help", ("command", Command)));
            return;
        }

        if (!_registry.TryGetRegistry(out var registry) || !registry.Comp.Citizens.TryGetValue(number, out var record))
        {
            shell.WriteError(Loc.GetString("cmd-cityregistry-no-citizen"));
            return;
        }

        if (record.Loyalty == null)
        {
            shell.WriteError(Loc.GetString("cmd-cityregistry-loyalty-outside"));
            return;
        }

        var reason = string.Join(' ', args.Skip(2));
        var kind = delta < 0 ? CityRegistryEntryKind.Violation : CityRegistryEntryKind.Reward;
        _registry.AddEntry(registry, record, kind, Loc.GetString("dystopia-registry-entry-admin-loyalty"),
            Loc.GetString("dystopia-registry-author-admin"), reason, loyaltyDelta: delta);

        shell.WriteLine(Loc.GetString("cmd-cityregistry-loyalty-done", ("number", number.ToString()), ("loyalty", record.Loyalty ?? 0)));
    }
}
