using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._Dystopia.FleshCult;

/// <summary>
/// Перетаскивание трупов на Колыбель: должно работать и на клиенте (подсветка, разрешение броска),
/// поэтому эта часть — общая. Само поглощение делает сервер (DystopiaFleshCradleSystem).
/// </summary>
public sealed partial class DystopiaFleshCradleSharedSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFleshCradleComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<DystopiaFleshCradleComponent, DragDropTargetEvent>(OnDragDropTarget);
    }

    public bool IsFeedableBody(EntityUid body)
    {
        return HasComp<MobStateComponent>(body) && _mobState.IsDead(body);
    }

    private void OnCanDropTarget(Entity<DystopiaFleshCradleComponent> ent, ref CanDropTargetEvent args)
    {
        if (!IsFeedableBody(args.Dragged))
            return;

        args.CanDrop = true;
        args.Handled = true;
    }

    private void OnDragDropTarget(Entity<DystopiaFleshCradleComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !IsFeedableBody(args.Dragged))
            return;

        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.FeedDelay,
            new DystopiaCradleFeedDoAfterEvent(), ent.Owner, target: args.Dragged, used: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }
}
