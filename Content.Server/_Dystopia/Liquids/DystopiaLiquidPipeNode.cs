// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: узел жидкостной трубы. Соединяется только с такими же узлами (отдельный слой, газовые трубы его не видят)
// по направлениям трубы с учётом поворота.

using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server._Dystopia.Liquids;

[DataDefinition]
public sealed partial class DystopiaLiquidPipeNode : Node, IRotatableNode
{
    /// <summary>Направления трубы для сущности, смотрящей на юг (поворот 0).</summary>
    [DataField("pipeDirection")]
    public PipeDirection OriginalPipeDirection;

    /// <summary>Направления с учётом текущего поворота.</summary>
    [ViewVariables]
    public PipeDirection CurrentPipeDirection { get; private set; }

    /// <summary>Сколько жидкости вмещает этот участок, единиц.</summary>
    [DataField]
    public float Volume = 50f;

    [ViewVariables]
    public DystopiaLiquidNet? Net => NodeGroup as DystopiaLiquidNet;

    public override void Initialize(EntityUid owner, IEntityManager entMan)
    {
        base.Initialize(owner, entMan);
        UpdateDirection(entMan);
    }

    private void UpdateDirection(IEntityManager entMan)
    {
        var xform = entMan.GetComponent<TransformComponent>(Owner);
        CurrentPipeDirection = OriginalPipeDirection.RotatePipeDirection(xform.LocalRotation);
    }

    bool IRotatableNode.RotateNode(in MoveEvent ev)
    {
        if (OriginalPipeDirection == PipeDirection.Fourway)
            return false;

        var old = CurrentPipeDirection;
        CurrentPipeDirection = OriginalPipeDirection.RotatePipeDirection(ev.NewRotation);
        return old != CurrentPipeDirection;
    }

    public override void OnAnchorStateChanged(IEntityManager entityManager, bool anchored)
    {
        if (anchored)
            UpdateDirection(entityManager);
    }

    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        var mapSystem = entMan.System<SharedMapSystem>();
        var pos = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);

        for (var i = 0; i < PipeDirectionHelpers.PipeDirections; i++)
        {
            var dir = (PipeDirection) (1 << i);
            if (!CurrentPipeDirection.HasDirection(dir))
                continue;

            var opposite = dir.GetOpposite();
            foreach (var entity in mapSystem.GetAnchoredEntities(gridEnt, pos.Offset(dir.ToDirection())))
            {
                if (!nodeQuery.TryGetComponent(entity, out var container))
                    continue;

                foreach (var node in container.Nodes.Values)
                {
                    if (node is DystopiaLiquidPipeNode pipe &&
                        pipe.NodeGroupID == NodeGroupID &&
                        pipe.CurrentPipeDirection.HasDirection(opposite))
                    {
                        yield return pipe;
                    }
                }
            }
        }
    }
}
