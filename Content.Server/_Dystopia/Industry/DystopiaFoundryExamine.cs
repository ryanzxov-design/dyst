// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: осмотр тигля, изложницы и машин — что внутри, сколько и жидкое ли (вместо общего «содержит ... вещество»).

using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.Industry;

public static class DystopiaFoundryExamine
{
    /// <summary>
    /// «Внутри 35 из 160 ед., жидкое:» и по строке на каждый реагент. Жидкое — если температура не ниже
    /// точки плавления основного вещества.
    /// </summary>
    public static void PushContents(ExaminedEvent args, Solution solution, float temperature, IPrototypeManager proto,
        string emptyKey = "dystopia-foundry-contents-empty", int priority = 0)
    {
        if (solution.Volume <= 0)
        {
            args.PushMarkup(Loc.GetString(emptyKey), priority);
            return;
        }

        var molten = true;
        if (solution.GetPrimaryReagentId() is { } primary &&
            proto.TryIndex<ReagentPrototype>(primary.Prototype, out var main) &&
            main.MeltingPoint is { } melting)
        {
            molten = temperature >= melting + DystopiaCrucibleSystem.KelvinOffset;
        }

        args.PushMarkup(Loc.GetString(molten ? "dystopia-foundry-contents-liquid" : "dystopia-foundry-contents-solid",
            ("volume", solution.Volume), ("max", solution.MaxVolume)), priority);

        foreach (var (reagent, quantity) in solution.GetReagentPrototypes(proto).OrderByDescending(pair => pair.Value.Value))
        {
            args.PushMarkup(Loc.GetString("dystopia-foundry-contents-entry",
                ("color", reagent.SubstanceColor.ToHexNoAlpha()),
                ("name", reagent.LocalizedName),
                ("amount", quantity)), priority);
        }
    }

    /// <summary>Предметы по видам (стопки — по названию стопки), больше — выше.</summary>
    public static List<(string Name, int Count)> CountItems(IEntityManager entMan, IPrototypeManager proto,
        IEnumerable<EntityUid> items)
    {
        var counts = new Dictionary<string, int>();
        foreach (var item in items)
        {
            if (entMan.IsQueuedForDeletion(item))
                continue;

            var name = entMan.GetComponent<MetaDataComponent>(item).EntityName;
            var count = 1;
            if (entMan.TryGetComponent<StackComponent>(item, out var stack))
            {
                count = stack.Count;
                if (proto.TryIndex(stack.StackTypeId, out var stackProto) && !string.IsNullOrEmpty(stackProto.Name.Id))
                    name = Loc.GetString(stackProto.Name);
            }

            counts[name] = counts.GetValueOrDefault(name) + count;
        }

        return counts.OrderByDescending(pair => pair.Value).Select(pair => (pair.Key, pair.Value)).ToList();
    }
}
