using Content.Shared._Dystopia.FleshCult;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.FleshCult;

/// <summary>Рёв огнемёта при стрельбе (по событию выстрела на самом оружии).</summary>
public sealed partial class DystopiaFlamethrowerSoundSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFlamethrowerSoundComponent, GunShotEvent>(OnShot);
    }

    private void OnShot(Entity<DystopiaFlamethrowerSoundComponent> ent, ref GunShotEvent args)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextRoar)
            return;

        ent.Comp.NextRoar = now + TimeSpan.FromSeconds(ent.Comp.Interval);
        _audio.PlayPvs(ent.Comp.Sound, ent.Owner);
    }
}
