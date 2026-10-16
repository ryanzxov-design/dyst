// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: горн с большим тиглем. Клик большим тиглем по горну — установка (DoAfter): горн становится одной
// конструкцией «горн с большим тиглем». Действие «Снять тигель» (ПКМ) — обратно горн и тигель-предмет.
// Топливо, огонь, жар, загруженные куски и расплав переносятся в обе стороны.

using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaForgeMountSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private DystopiaHotItemSystem _hot = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaForgeComponent, DystopiaForgeMountDoAfterEvent>(OnMountDoAfter);
        SubscribeLocalEvent<DystopiaMountedCrucibleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaMountedCrucibleComponent, DystopiaForgeUnmountDoAfterEvent>(OnUnmountDoAfter);
        SubscribeLocalEvent<DystopiaMountedCrucibleComponent, ExaminedEvent>(OnExamined);
    }

    // ------------------------------------------------------------------ установка

    /// <summary>Начать ставить тигель на горн. Зовёт горн, когда по нему кликнули тиглем.</summary>
    public void TryStartMount(Entity<DystopiaForgeComponent> forge, EntityUid crucible, EntityUid user)
    {
        if (!TryComp<DystopiaForgeMountableComponent>(crucible, out var mountable))
            return;

        if (HasComp<DystopiaMountedCrucibleComponent>(forge))
        {
            _popup.PopupEntity(Loc.GetString("dystopia-forge-mount-occupied"), forge, user);
            return;
        }

        if (TryComp<ItemPlacerComponent>(forge, out var placer) && placer.PlacedEntities.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-forge-mount-clear"), forge, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, mountable.Delay, new DystopiaForgeMountDoAfterEvent(),
            forge.Owner, target: forge.Owner, used: crucible)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDropItem = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnMountDoAfter(Entity<DystopiaForgeComponent> forge, ref DystopiaForgeMountDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } crucible ||
            TerminatingOrDeleted(crucible) ||
            !TryComp<DystopiaForgeMountableComponent>(crucible, out var mountable))
        {
            return;
        }

        args.Handled = true;
        var mounted = Spawn(mountable.Mounted, Transform(forge).Coordinates);
        CopyForge(forge, mounted);
        MoveCrucible(crucible, mounted);

        QueueDel(crucible);
        QueueDel(forge);
        _popup.PopupEntity(Loc.GetString("dystopia-forge-mounted"), mounted, args.User);
    }

    // ------------------------------------------------------------------ снятие

    private void OnGetVerbs(Entity<DystopiaMountedCrucibleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-forge-unmount-verb"),
            Act = () => TryStartUnmount(ent, user),
            Priority = 0,
        });
    }

    public void TryStartUnmount(Entity<DystopiaMountedCrucibleComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new DystopiaForgeUnmountDoAfterEvent(),
            ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnUnmountDoAfter(Entity<DystopiaMountedCrucibleComponent> ent, ref DystopiaForgeUnmountDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var coords = Transform(ent).Coordinates;
        var forge = Spawn(ent.Comp.Forge, coords);
        var crucible = Spawn(ent.Comp.Item, coords);
        CopyForge(ent, forge);
        MoveCrucible(ent, crucible);
        QueueDel(ent);

        // Холодный — сразу в руки (двумя руками); раскалённый останется на месте, пока не остынет.
        _hands.TryPickupAnyHand(args.User, crucible);
        _popup.PopupEntity(Loc.GetString("dystopia-forge-unmounted"), forge, args.User);
    }

    // ------------------------------------------------------------------ перенос состояния

    private void CopyForge(EntityUid from, EntityUid to)
    {
        if (!TryComp<DystopiaForgeComponent>(from, out var src) || !TryComp<DystopiaForgeComponent>(to, out var dst))
            return;

        dst.Fuel = src.Fuel;
        dst.Lit = src.Lit;
        dst.BellowsUntil = src.BellowsUntil;
    }

    /// <summary>Перенести из тигля в тигель загруженные куски, расплав и жар.</summary>
    private void MoveCrucible(EntityUid from, EntityUid to)
    {
        if (!TryComp<DystopiaCrucibleComponent>(from, out var src) || !TryComp<DystopiaCrucibleComponent>(to, out var dst))
            return;

        if (_container.TryGetContainer(from, src.ChargeContainer, out var fromCharge))
        {
            var toCharge = _container.EnsureContainer<Container>(to, dst.ChargeContainer);
            foreach (var item in new List<EntityUid>(fromCharge.ContainedEntities))
            {
                _container.Remove(item, fromCharge);
                _container.Insert(item, toCharge);
            }
        }

        if (_solution.TryGetSolution(from, src.Solution, out var fromSoln, out var fromSolution) &&
            fromSolution.Volume > 0 &&
            _solution.TryGetSolution(to, dst.Solution, out var toSoln, out _))
        {
            var moved = _solution.SplitSolution(fromSoln.Value, fromSolution.Volume);
            _solution.TryAddSolution(toSoln.Value, moved);
        }

        dst.Temperature = src.Temperature;
        dst.MeltProgress = src.MeltProgress;
        _hot.SetHot(to, dst.Temperature > dst.HotThreshold); // раскалённый сразу не взять голой рукой
    }

    private void OnExamined(Entity<DystopiaMountedCrucibleComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("dystopia-forge-mounted-examine"));
    }
}
