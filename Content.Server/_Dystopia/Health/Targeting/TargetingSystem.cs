// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds;
using Content.Shared._Dystopia.Health.Targeting;
using Content.Shared._Dystopia.Health.Targeting.Events;
using Content.Shared.Mobs;

namespace Content.Server._Dystopia.Health.Targeting;

public sealed class TargetingSystem : SharedTargetingSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TargetChangeEvent>(OnTargetChange);
        SubscribeLocalEvent<TargetingComponent, MobStateChangedEvent>(OnMobStateChange);
    }

    private void OnTargetChange(TargetChangeEvent message, EntitySessionEventArgs args)
    {
        var uid = GetEntity(message.Uid);

        // Менять цель можно только своему телу
        if (args.SenderSession.AttachedEntity != uid || !TryComp<TargetingComponent>(uid, out var target))
            return;

        target.Target = message.BodyPart;
        Dirty(uid, target);
    }

    private void OnMobStateChange(EntityUid uid, TargetingComponent component, MobStateChangedEvent args)
    {
        var changed = false;

        if (args.NewMobState == MobState.Dead)
        {
            foreach (var part in GetValidParts())
            {
                component.BodyStatus[part] = WoundableSeverity.Severed;
                changed = true;
            }
        }
        else if (args is { OldMobState: MobState.Dead, NewMobState: MobState.Alive or MobState.Critical })
        {
            // Ф3: состояния частей будут браться из ран. Пока ран нет — после оживления все части целы.
            foreach (var part in GetValidParts())
            {
                component.BodyStatus[part] = WoundableSeverity.Healthy;
            }

            changed = true;
        }

        if (!changed)
            return;

        Dirty(uid, component);
        RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(uid)), uid);
    }
}
