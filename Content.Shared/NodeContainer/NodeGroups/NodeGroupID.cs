namespace Content.Shared.NodeContainer.NodeGroups;

public enum NodeGroupID : byte
{
    Default,
    HVPower,
    MVPower,
    Apc,
    AMEngine,
    Pipe,
    WireNet,

    /// <summary>
    /// Group used by the TEG.
    /// </summary>
    /// <seealso cref="Content.Server.Power.Generation.Teg.TegSystem"/>
    /// <seealso cref="Content.Server.Power.Generation.Teg.TegNodeGroup"/>
    Teg,
    ExCable,
    DystopiaLiquid, // Dystopia-Liquids: жидкостные трубы
    DystopiaControl, // Dystopia-Energy: кабель управления (пульт реактора)
}
