// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: приспущенная маска пропадает с персонажа. У маски Стражи слои «на лице» прописаны явно (с отдельным
// светящимся слоем окуляров), а явные слои не смотрят на приставку «up», которую ставит приспускание, — поэтому
// после ванильной сборки слоёв убираем их, пока стоит эта приставка.
// Решаем именно по приставке одежды (а не по MaskComponent.IsToggled): перерисовку запускает смена приставки,
// а состояние маски может прийти с сервера позже — тогда маска рисовалась по старому признаку и «мигала» до
// следующей перерисовки (лечь, встать).

using Content.Shared._Dystopia.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;

namespace Content.Client._Dystopia.Clothing;

public sealed class DystopiaFaceMaskVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFaceMaskComponent, GetEquipmentVisualsEvent>(OnGetVisuals,
            after: [typeof(ClothingSystem)]);
    }

    private void OnGetVisuals(Entity<DystopiaFaceMaskComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!ent.Comp.HideWhenDown
            || !TryComp<ClothingComponent>(ent, out var clothing)
            || !TryComp<MaskComponent>(ent, out var mask)
            || clothing.EquippedPrefix != mask.EquippedPrefix)
        {
            return;
        }

        args.Layers.Clear();
    }
}
