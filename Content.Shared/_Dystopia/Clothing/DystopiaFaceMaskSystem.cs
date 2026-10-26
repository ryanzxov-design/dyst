// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: голос маски. Речь и рация берут способ речи из TransformSpeakerNameEvent (как у маски-голосовика):
// пока маска на лице и не приспущена — подставляем свой, если его ещё никто не подставил. Ночное зрение окуляров
// выключается, когда маску приспускают, и включается, когда поднимают.

using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.NightVision;
using Content.Shared.Overlays;

namespace Content.Shared._Dystopia.Clothing;

public sealed class DystopiaFaceMaskSystem : EntitySystem
{
    [Dependency] private SharedNightVisionSystem _nightVision = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFaceMaskComponent, InventoryRelayedEvent<TransformSpeakerNameEvent>>(OnSpeakerName);
        SubscribeLocalEvent<DystopiaFaceMaskComponent, ItemMaskToggledEvent>(OnMaskToggled);
    }

    private void OnMaskToggled(Entity<DystopiaFaceMaskComponent> ent, ref ItemMaskToggledEvent args)
    {
        if (!ent.Comp.NightVisionOnlyWhenUp || !TryComp<NightVisionComponent>(ent, out var nightVision))
            return;

        _nightVision.SetEnabled((ent.Owner, nightVision), !args.Mask.Comp.IsToggled, args.Wearer);
    }

    private void OnSpeakerName(Entity<DystopiaFaceMaskComponent> ent, ref InventoryRelayedEvent<TransformSpeakerNameEvent> args)
    {
        if (ent.Comp.SpeechVerb is not { } verb || args.Args.SpeechVerb != null)
            return;

        if (TryComp<MaskComponent>(ent, out var mask) && mask.IsToggled)
            return;

        args.Args.SpeechVerb = verb;
    }
}
