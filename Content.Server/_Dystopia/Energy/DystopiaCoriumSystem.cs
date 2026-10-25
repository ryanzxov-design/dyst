// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: жидкий кориум застывает в глыбу через SolidifyTime (глыбу разбивают — куски собирают захватом).

using Content.Shared._Dystopia.Industry;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaCoriumSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaCoriumComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DystopiaCoriumComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SolidifyAt == TimeSpan.Zero)
            ent.Comp.SolidifyAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SolidifyTime);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + TimeSpan.FromSeconds(2);
        var query = EntityQueryEnumerator<DystopiaCoriumComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var corium, out var xform))
        {
            if (corium.SolidifyAt == TimeSpan.Zero || now < corium.SolidifyAt)
                continue;

            Spawn(corium.Solid, _xform.GetMoverCoordinates(uid, xform));
            QueueDel(uid);
        }
    }

    /// <summary>Отладка: застыть сразу.</summary>
    public void SolidifyNow(Entity<DystopiaCoriumComponent> ent)
    {
        ent.Comp.SolidifyAt = _timing.CurTime;
    }
}
