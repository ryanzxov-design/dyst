namespace Content.Shared._Dystopia.Economy;

/// <summary>
/// Делает ID-карту ключом к счёту в Банке Города.
/// Добавляется к карте автоматически при появлении жителя.
/// </summary>
[RegisterComponent]
public sealed partial class CityBankCardComponent : Component
{
    /// <summary>Номер привязанного счёта.</summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int? AccountId;

    /// <summary>Станция, в банке которой открыт счёт.</summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Bank;
}
