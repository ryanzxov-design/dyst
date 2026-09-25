using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Pda;

/// <summary>
/// Программы, которые ставятся в КПК Города при его появлении (без возможности удалить):
/// «Свод законов», «Банк» и т.п. Список задаётся в pdas.yml.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaPdaProgramsComponent : Component
{
    [DataField]
    public List<EntProtoId> Programs = new();
}
