// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: программа КПК «Мой статус» — что Город знает о владельце карты, лежащей в КПК: статус, лояльность,
// должность и ID-карта. Журнал дела, заметки Стражи и маршрут гражданину не показываются.

using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Registry;

[RegisterComponent]
public sealed partial class CityStatusCartridgeComponent : Component
{
    /// <summary>Показывать ли основание статуса («в розыске: …»).</summary>
    [DataField]
    public bool ShowStatusReason = true;

    /// <summary>Как часто обновлять открытую программу.</summary>
    [DataField]
    public TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextRefresh;
}

[Serializable, NetSerializable]
public sealed class CityStatusUiState : BoundUserInterfaceState
{
    /// <summary>В КПК лежит карта гражданина, и он есть в Реестре.</summary>
    public bool HasRecord;

    public int Number;
    public string Name = string.Empty;
    public string JobTitle = string.Empty;
    public string Department = string.Empty;

    public CityCitizenStatus Status;
    public string StatusReason = string.Empty;
    public bool Dismissed;

    /// <summary>Вне системы лояльности (Консул): лояльность не ведётся.</summary>
    public bool Outside;
    public int? Loyalty;
    public int? LoyaltyNorm;
    public int MaxLoyalty;

    /// <summary>Карта в КПК: серийный номер и не аннулирована ли.</summary>
    public string CardSerial = string.Empty;
    public bool CardAnnulled;
}
