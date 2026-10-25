// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: сеть кабеля управления — просто соединяет узлы (пульт реактора ↔ колонны). Без тока и данных: кто в одной
// сети, тот и связан.

using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.NodeContainer.NodeGroups;

namespace Content.Server._Dystopia.Energy;

[NodeGroup(NodeGroupID.DystopiaControl)]
public sealed class DystopiaControlNet : BaseNodeGroup
{
}
