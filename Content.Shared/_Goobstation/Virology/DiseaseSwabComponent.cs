using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.Virology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseSwabComponent : Component
{
    /// <summary>
    /// EntityUid of the sampled disease.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? DiseaseUid;
}
