// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: осмотр тигля и изложницы — что внутри, сколько и жидкое ли (вместо общего «содержит ... вещество»).

using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.Industry;

public static class DystopiaFoundryExamine
{
    /// <summary>
    /// «Внутри 35 из 160 ед., жидкое:» и по строке на каждый реагент. Жидкое — если температура не ниже
    /// точки плавления основного вещества.
    /// </summary>
    public static void PushContents(ExaminedEvent args, Solution solution, float temperature, IPrototypeManager proto)
    {
        if (solution.Volume <= 0)
        {
            args.PushMarkup(Loc.GetString("dystopia-foundry-contents-empty"));
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
            ("volume", solution.Volume), ("max", solution.MaxVolume)));

        foreach (var (reagent, quantity) in solution.GetReagentPrototypes(proto).OrderByDescending(pair => pair.Value.Value))
        {
            args.PushMarkup(Loc.GetString("dystopia-foundry-contents-entry",
                ("color", reagent.SubstanceColor.ToHexNoAlpha()),
                ("name", reagent.LocalizedName),
                ("amount", quantity)));
        }
    }
}
