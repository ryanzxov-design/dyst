// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: сеть жидкостных труб — один общий раствор на всю связанную сеть. Вместимость — сумма участков.
// Сети сливаются и делятся вместе с трубами; жидкость делится по вместимости новых сетей. Долю снятой или
// разобранной трубы система заранее выливает лужей (DystopiaLiquidSystem).

using System.Linq;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.Liquids;

[NodeGroup(NodeGroupID.DystopiaLiquid)]
public sealed class DystopiaLiquidNet : BaseNodeGroup
{
    [ViewVariables]
    public Solution Solution = new();

    private IPrototypeManager? _proto;

    public override void Initialize(Node sourceNode, IEntityManager entMan)
    {
        base.Initialize(sourceNode, entMan);
        _proto = IoCManager.Resolve<IPrototypeManager>();
    }

    public override void LoadNodes(List<Node> groupNodes)
    {
        base.LoadNodes(groupNodes);

        var volume = 0f;
        foreach (var node in groupNodes)
        {
            if (node is DystopiaLiquidPipeNode pipe)
                volume += pipe.Volume;
        }

        Solution.MaxVolume += FixedPoint2.New(volume);
    }

    public override void AfterRemake(IEnumerable<IGrouping<INodeGroup?, Node>> newGroups)
    {
        var targets = new List<(DystopiaLiquidNet Net, float Volume)>();
        var total = 0f;
        foreach (var group in newGroups)
        {
            if (group.Key is not DystopiaLiquidNet net)
                continue;

            var volume = group.OfType<DystopiaLiquidPipeNode>().Sum(n => n.Volume);
            if (volume <= 0f)
                continue;

            targets.Add((net, volume));
            total += volume;
        }

        if (targets.Count == 0 || Solution.Volume <= FixedPoint2.Zero)
            return;

        // Делим по вместимости; последняя сеть забирает остаток, чтобы ничего не терялось на округлении.
        for (var i = 0; i < targets.Count; i++)
        {
            var (net, volume) = targets[i];
            var part = i == targets.Count - 1
                ? Solution.SplitSolution(Solution.Volume)
                : Solution.SplitSolution(FixedPoint2.New(Solution.Volume.Float() * volume / total));
            total -= volume;
            net.Solution.AddSolution(part, _proto);
        }
    }

    public override string GetDebugData()
    {
        return $"Volume: {Solution.Volume}/{Solution.MaxVolume}\nTemperature: {Solution.Temperature:G3}";
    }
}
