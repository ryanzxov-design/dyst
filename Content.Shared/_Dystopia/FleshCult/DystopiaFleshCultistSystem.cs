using Content.Shared.Antag;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Shared._Dystopia.FleshCult;

/// <summary>
/// Культисты видят друг друга: значок над головой по рангу. Не-культистам компонент не передаётся вовсе.
/// </summary>
public sealed partial class DystopiaFleshCultistSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFleshCultistComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt);
        SubscribeLocalEvent<DystopiaFleshCultistComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DystopiaFleshCultistComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStateAttempt(Entity<DystopiaFleshCultistComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanSee(args.Player);
    }

    private bool CanSee(ICommonSession? player)
    {
        // В повторах сессии нет — показываем.
        if (player?.AttachedEntity is not { } viewer)
            return true;

        return HasComp<DystopiaFleshCultistComponent>(viewer) || HasComp<ShowAntagIconsComponent>(viewer);
    }

    private void OnStartup(Entity<DystopiaFleshCultistComponent> ent, ref ComponentStartup args)
    {
        // Новый культист должен увидеть остальных, а остальные — его.
        var query = AllEntityQuery<DystopiaFleshCultistComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Dirty(uid, comp);
        }
    }

    private void OnGetStatusIcons(Entity<DystopiaFleshCultistComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ProtoMan.Resolve(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }
}
