// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: ночное зрение окуляров маски выключается, когда маску приспускают, и включается, когда поднимают.
// Голос маски (способ речи и номер вместо имени) — на сервере: DystopiaMaskedNameSystem.

using Content.Shared.Clothing;
using Content.Shared.NightVision;
using Content.Shared.Overlays;

namespace Content.Shared._Dystopia.Clothing;

public sealed class DystopiaFaceMaskSystem : EntitySystem
{
    [Dependency] private SharedNightVisionSystem _nightVision = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFaceMaskComponent, ItemMaskToggledEvent>(OnMaskToggled);
    }

    private void OnMaskToggled(Entity<DystopiaFaceMaskComponent> ent, ref ItemMaskToggledEvent args)
    {
        if (!ent.Comp.NightVisionOnlyWhenUp || !TryComp<NightVisionComponent>(ent, out var nightVision))
            return;

        _nightVision.SetEnabled((ent.Owner, nightVision), !args.Mask.Comp.IsToggled, args.Wearer);
    }
}
