// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: сколько вещества сейчас в крови — для лекарств, действие которых считает C# (боль, органы, кости, зависимость).

using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Chemistry;

public sealed partial class BloodChemistrySystem : EntitySystem
{
    /// <summary>Растворы тела, где ищем вещества.</summary>
    public static readonly string[] Solutions = { "bloodstream", "chemicals" };

    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    /// <summary>Количество вещества в крови тела.</summary>
    public float GetQuantity(EntityUid body, ProtoId<ReagentPrototype> reagent)
    {
        var total = 0f;
        foreach (var name in Solutions)
        {
            if (_solutions.TryGetSolution(body, name, out _, out var solution))
                total += solution.GetTotalPrototypeQuantity(reagent).Float();
        }

        return total;
    }

    /// <summary>Сумма «количество × вес» по списку веществ (например, сила обезболивания).</summary>
    public float Weighted(EntityUid body, IReadOnlyDictionary<ProtoId<ReagentPrototype>, float> weights)
    {
        var total = 0f;
        foreach (var (reagent, weight) in weights)
        {
            var quantity = GetQuantity(body, reagent);
            if (quantity > 0)
                total += quantity * weight;
        }

        return total;
    }
}
