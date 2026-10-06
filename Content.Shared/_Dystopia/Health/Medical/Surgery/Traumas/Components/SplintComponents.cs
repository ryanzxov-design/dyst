// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;

/// <summary>Шина: накладывается на часть с повреждённой костью (куда целится врач).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SplintComponent : Component
{
    [DataField]
    public TimeSpan ApplyTime = TimeSpan.FromSeconds(4);

    /// <summary>Сколько целостности кости восстанавливается в секунду под шиной.</summary>
    [DataField]
    public FixedPoint2 HealPerSecond = 0.1;

    /// <summary>Под шиной кость для движения и рук считается не хуже этой стадии (сломанная — как треснувшая).</summary>
    [DataField]
    public BoneSeverity StabilizedSeverity = BoneSeverity.Cracked;
}

/// <summary>На части наложена шина: кость понемногу срастается, перелом мешает меньше. Снимается сама, когда кость цела.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoneSplintedComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 HealPerSecond = 0.1;

    [DataField, AutoNetworkedField]
    public BoneSeverity StabilizedSeverity = BoneSeverity.Cracked;
}

[Serializable, NetSerializable]
public sealed partial class SplintDoAfterEvent : DoAfterEvent
{
    public NetEntity Part;

    public SplintDoAfterEvent(NetEntity part)
    {
        Part = part;
    }

    public override DoAfterEvent Clone() => this;
}
