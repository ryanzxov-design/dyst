using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.FleshCult;

/// <summary>Ранг в Культе Плоти.</summary>
[Serializable, NetSerializable]
public enum DystopiaFleshRank : byte
{
    Carrier,
    Preacher,
    Apostle,
}

/// <summary>
/// Культист Плоти. Состояние компонента видят только другие культисты: остальные игроки
/// даже не узнают, что он есть (иначе Проповедника можно было бы вычислить).
/// Культисты видят друг над другом значок Культа.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DystopiaFleshCultistComponent : Component
{
    [DataField, AutoNetworkedField]
    public DystopiaFleshRank Rank = DystopiaFleshRank.Carrier;

    [DataField, AutoNetworkedField]
    public ProtoId<FactionIconPrototype> StatusIcon = "DystopiaFleshCarrierIcon";

    public override bool SessionSpecific => true;
}
