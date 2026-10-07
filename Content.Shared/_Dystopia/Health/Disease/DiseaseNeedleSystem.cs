// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: осмотр шприца показывает, что иглой уже кололи (заразу на ней не видно).

using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Disease;

public sealed partial class DiseaseNeedleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseNeedleComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DiseaseNeedleComponent> ent, ref ExaminedEvent args)
    {
        if (_timing.CurTime >= ent.Comp.NonSterileUntil)
            return;

        args.PushMarkup(Loc.GetString("disease-needle-not-sterile"));
    }

    /// <summary>
    /// Игла побывала в крови — некоторое время она нестерильна.
    /// </summary>
    public void MarkUsed(Entity<DiseaseNeedleComponent> ent)
    {
        ent.Comp.NonSterileUntil = _timing.CurTime + ent.Comp.NonSterileTime;
        Dirty(ent);
    }
}
