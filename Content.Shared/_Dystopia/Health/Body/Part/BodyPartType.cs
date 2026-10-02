// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part
{
    /// <summary>Тип части тела.</summary>
    [Serializable, NetSerializable]
    public enum BodyPartType : byte
    {
        Other = 0,
        Chest = 1 << 0,
        Groin = 1 << 1,
        Head = 1 << 2,
        Arm = 1 << 3,
        Hand = 1 << 4,
        Leg = 1 << 5,
        Foot = 1 << 6,
        Tail = 1 << 7,
        Vital = Chest | Groin | Head,
    }
}
