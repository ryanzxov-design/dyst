// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: узел опоры ЛЭП — как клемма (кабель на той же клетке), плюс узлы опор, связанных с этой проводами.

using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Nodes;
using Content.Shared._Dystopia.Industry;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server._Dystopia.Energy;

[DataDefinition]
public sealed partial class DystopiaPylonNode : CableDeviceNode
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
            yield return node;

        if (!entMan.TryGetComponent<DystopiaPylonComponent>(Owner, out var pylon))
            yield break;

        foreach (var other in pylon.Links)
        {
            if (!nodeQuery.TryGetComponent(other, out var container) ||
                !container.Nodes.TryGetValue(pylon.Node, out var node) || node == this)
            {
                continue;
            }

            yield return node;
        }
    }
}
