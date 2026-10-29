// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: снаряжение Единицы Внешнего Патруля (оболочка, шлем, лицевая маска, КПК). Надетое — не снимается никем:
// ни самой Единицей, ни другими, ни с трупа. Снять или заменить его можно только машиной модификации (позже);
// с трупа — только распилить и разварить на бронеэлементы (позже). Система — UnitLockedEquipmentSystem.

namespace Content.Shared._Dystopia.Patrol;

[RegisterComponent]
public sealed partial class UnitLockedEquipmentComponent : Component
{
    /// <summary>
    /// Ключ локализации для отказа при попытке снять.
    /// </summary>
    [DataField]
    public LocId Reason = "dystopia-unit-locked-equipment-refuse";
}
