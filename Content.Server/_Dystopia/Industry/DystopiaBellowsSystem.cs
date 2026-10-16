// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меха — отдельная конструкция рядом с горном. Качают рукой (повторяется, пока не отойдёшь);
// каждый качок раздувает горящий горн рядом. Качать может кто угодно — хоть чернорабочий.

using Content.Shared._Dystopia.Industry;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaBellowsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DystopiaForgeSystem _forge = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaBellowsComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<DystopiaBellowsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaBellowsComponent, DystopiaBellowsPumpDoAfterEvent>(OnPump);
        SubscribeLocalEvent<DystopiaBellowsComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractHand(Entity<DystopiaBellowsComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryStartPumping(ent, args.User);
    }

    private void OnGetVerbs(Entity<DystopiaBellowsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-bellows-verb"),
            Act = () => TryStartPumping(ent, user),
        });
    }

    public void TryStartPumping(Entity<DystopiaBellowsComponent> ent, EntityUid user)
    {
        if (ent.Comp.RequireAnchored && !Transform(ent).Anchored)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-bellows-not-anchored"), ent, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.PumpDelay,
            new DystopiaBellowsPumpDoAfterEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPump(Entity<DystopiaBellowsComponent> ent, ref DystopiaBellowsPumpDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        ent.Comp.Pumping = true;
        ent.Comp.PumpVisualUntil = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.PumpVisualTime);
        _appearance.SetData(ent, DystopiaBellowsVisuals.Pumping, true);
        _audio.PlayPvs(ent.Comp.PumpSound, ent);

        var blew = false;
        foreach (var forge in _lookup.GetEntitiesInRange<DystopiaForgeComponent>(Transform(ent).Coordinates, ent.Comp.Range))
        {
            blew |= _forge.Blow(forge, ent.Comp.BlowDuration);
        }

        if (!blew)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-bellows-no-forge"), ent, args.User);
            return;
        }

        args.Repeat = true; // качают, пока не отойдут
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaBellowsComponent>();
        while (query.MoveNext(out var uid, out var bellows))
        {
            if (!bellows.Pumping || now < bellows.PumpVisualUntil)
                continue;

            bellows.Pumping = false;
            _appearance.SetData(uid, DystopiaBellowsVisuals.Pumping, false);
        }
    }

    private void OnExamined(Entity<DystopiaBellowsComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("dystopia-bellows-examine"));
        if (ent.Comp.RequireAnchored && !Transform(ent).Anchored)
            args.PushMarkup(Loc.GetString("dystopia-bellows-examine-loose"));
    }
}
