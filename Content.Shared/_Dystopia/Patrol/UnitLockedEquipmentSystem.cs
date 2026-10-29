// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: см. UnitLockedEquipmentComponent.

using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Dystopia.Patrol;

public sealed class UnitLockedEquipmentSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnitLockedEquipmentComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<UnitLockedEquipmentComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUnequipAttempt(Entity<UnitLockedEquipmentComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        // одежда, лежащая не в своём слоте (например, КПК в кармане), не держится
        if (TryComp<ClothingComponent>(ent, out var clothing) && (clothing.Slots & args.SlotFlags) == SlotFlags.NONE)
            return;

        args.Reason = ent.Comp.Reason;
        args.Cancel();
    }

    private void OnExamined(Entity<UnitLockedEquipmentComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dystopia-unit-locked-equipment-examine"));
    }
}
