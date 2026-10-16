// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: наклон тигля, встроенного в горн (горн с большим тиглем). Расплав подаётся в присоединённый литейный
// желоб (у которого отверстие смотрит на горн) и расходится по сети; без желоба — в столы и изложницы рядом.

using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaTiltPourSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private DystopiaCastingChannelSystem _channel = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaTiltPourComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaTiltPourComponent, DystopiaTiltPourDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerbs(Entity<DystopiaTiltPourComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-tilt-pour-verb"),
            Act = () => TryStartTilt(ent, user),
            Priority = 2,
        });
    }

    public void TryStartTilt(Entity<DystopiaTiltPourComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new DystopiaTiltPourDoAfterEvent(),
            ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<DystopiaTiltPourComponent> ent, ref DystopiaTiltPourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        Pour(ent, args.User);
    }

    public void Pour(Entity<DystopiaTiltPourComponent> ent, EntityUid user)
    {
        if (!TryComp<DystopiaCrucibleComponent>(ent, out var crucible) ||
            !_solution.TryGetSolution(ent.Owner, crucible.Solution, out var soln, out var solution))
        {
            return;
        }

        if (solution.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-tongs-pour-empty"), ent, user);
            return;
        }

        FixedPoint2 poured;
        if (_channel.FindAttachedChannel(ent) is { } channel)
        {
            poured = _channel.Pour(user, ent, soln.Value, channel);
        }
        else if (_channel.FindAdjacentMoulds(ent) is { Count: > 0 } moulds)
        {
            poured = _channel.PourInto(user, ent, soln.Value, moulds);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("dystopia-tilt-pour-no-target"), ent, user);
            return;
        }

        if (poured <= FixedPoint2.Zero)
            return;

        _audio.PlayPvs(ent.Comp.PourSound, ent);
        _popup.PopupEntity(Loc.GetString("dystopia-tilt-pour-done", ("amount", poured)), ent, user);
    }
}
