// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: топливные лампы — керосиновая и карбидная. Ручная: включается в руке, заправляется из любой ёмкости
// (карбидная — ещё и кусками карбида: карбид с водой тут же даёт ацетилен, на нём она и горит). ПКМ ручной лампой по
// стене — «Повесить лампу»: появляется настенная с тем же топливом; с неё лампу снимают обратно в руку.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

[RegisterComponent]
public sealed partial class DystopiaFuelLampComponent : Component
{
    [DataField]
    public string Solution = "fuel";

    /// <summary>Топливо → секунд горения на единицу.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, float> Fuels = new();

    /// <summary>Что можно засыпать кусками (стопка → вещества за один кусок).</summary>
    [DataField]
    public Dictionary<ProtoId<StackPrototype>, Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>> LoadItems = new();

    /// <summary>Ручная лампа: во что превращается, когда её вешают на стену.</summary>
    [DataField]
    public EntProtoId? MountedPrototype;

    /// <summary>Настенная лампа: во что превращается, когда её снимают.</summary>
    [DataField]
    public EntProtoId? ItemPrototype;

    [DataField]
    public bool On;

    [DataField]
    public float Interval = 1f;

    [ViewVariables]
    public TimeSpan NextUpdate;
}

[Serializable, NetSerializable]
public enum DystopiaFuelLampVisuals : byte
{
    On,
}
