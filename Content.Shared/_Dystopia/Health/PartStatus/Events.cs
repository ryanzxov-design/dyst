// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.PartStatus.Events;

/// <summary>Клик по кукле состояния: показать в чате, что с частями тела.</summary>
[Serializable, NetSerializable]
public sealed class GetPartStatusEvent : EntityEventArgs
{
    public NetEntity Uid { get; }

    public GetPartStatusEvent(NetEntity uid)
    {
        Uid = uid;
    }
}
