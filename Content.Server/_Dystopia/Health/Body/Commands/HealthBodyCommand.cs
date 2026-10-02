// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: проверка слоя совместимости тела (Ф1). Показывает части тела и органы так, как их видит система здоровья.

using System.Text;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Console;

namespace Content.Server._Dystopia.Health.Body.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class HealthBodyCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "healthbody";
    public string Description => "Показывает части тела и органы так, как их видит система здоровья.";
    public string Help => "healthbody <uid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !NetEntity.TryParse(args[0], out var netUid) ||
            !_entities.TryGetEntity(netUid, out var uid))
        {
            shell.WriteLine(Help);
            return;
        }

        var body = _entities.System<SharedBodySystem>();
        var sb = new StringBuilder();

        if (!body.TryGetRootPart(uid.Value, out var root))
        {
            shell.WriteLine("Нет корневой части (груди) — это не тело или слой совместимости не подключён.");
            return;
        }

        sb.AppendLine($"Корень: {_entities.ToPrettyString(root.Value.Owner)}");
        foreach (var (part, comp) in body.GetBodyChildren(uid.Value))
        {
            body.TryGetParentBodyPart(part, out var parent, out _);
            sb.AppendLine($"- {comp.PartType} {comp.Symmetry}: {_entities.ToPrettyString(part)}" +
                          $" | тело: {(comp.Body is { } b ? _entities.ToPrettyString(b).ToString() : "нет")}" +
                          $" | родитель: {(parent is { } p ? _entities.ToPrettyString(p).ToString() : "—")}");

            foreach (var (organ, _) in body.GetPartOrgans(part, comp))
            {
                sb.AppendLine($"    орган: {_entities.ToPrettyString(organ)}");
            }
        }

        sb.AppendLine($"Всего частей: рук {body.GetBodyPartCount(uid.Value, BodyPartType.Arm)}, " +
                      $"ног {body.GetBodyPartCount(uid.Value, BodyPartType.Leg)}");
        shell.WriteLine(sb.ToString());
    }
}
