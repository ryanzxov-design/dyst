// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: кузнечные клещи. Ткнул в раскалённый тигель/изложницу — зажал; зажатым тиглем в изложницу — разлил,
// в литейный желоб — разлил по изложницам под ним, в горн или стол — поставил, в пол — положил. Z или действие — разжать.
// Решение «обработано» принимается и на клиенте (чтобы клиент не предсказывал постановку клещей на горн),
// сами действия — только на сервере.

using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._Dystopia.Industry;

public sealed partial class DystopiaTongsSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SolutionTransferSystem _transfer = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private DystopiaCastingChannelSystem _channel = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaTongsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DystopiaTongsComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DystopiaTongsComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DystopiaTongsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaTongsComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaTongsComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<DystopiaTongsComponent, EntInsertedIntoContainerMessage>(OnGripChanged);
        SubscribeLocalEvent<DystopiaTongsComponent, EntRemovedFromContainerMessage>(OnReleased);
        SubscribeLocalEvent<DystopiaTongsComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnInit(Entity<DystopiaTongsComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Slot);
    }

    /// <summary>Что зажато в клещах.</summary>
    public EntityUid? GetHeld(Entity<DystopiaTongsComponent> ent)
    {
        return _container.TryGetContainer(ent, ent.Comp.Slot, out var container) && container is ContainerSlot slot
            ? slot.ContainedEntity
            : null;
    }

    private bool CanGrip(Entity<DystopiaTongsComponent> ent, EntityUid target)
    {
        return HasComp<DystopiaHotItemComponent>(target) &&
               HasComp<ItemComponent>(target) &&
               !_container.IsEntityInContainer(target) &&
               _whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, target);
    }

    // ------------------------------------------------------------------ тычок клещами

    private void OnAfterInteract(Entity<DystopiaTongsComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var held = GetHeld(ent);
        if (held == null)
        {
            if (args.Target is not { } target)
                return;

            // По горну — берём стоящий на нём тигель
            if (!CanGrip(ent, target) && TryComp<ItemPlacerComponent>(target, out var placer))
            {
                foreach (var placed in placer.PlacedEntities)
                {
                    if (CanGrip(ent, placed))
                    {
                        target = placed;
                        break;
                    }

                    if (HasComp<DystopiaForgeMountableComponent>(placed))
                        target = placed; // скажем, что большой тигель не поднять
                }
            }

            if (!CanGrip(ent, target))
            {
                // Большой тигель клещами не поднять — его ставят на горн.
                if (HasComp<DystopiaForgeMountableComponent>(target))
                {
                    args.Handled = true;
                    _popup.PopupEntity(Loc.GetString("dystopia-tongs-too-heavy", ("item", target)), target, args.User);
                }

                return;
            }

            args.Handled = true;
            if (Transform(target).Anchored)
            {
                _popup.PopupEntity(Loc.GetString("dystopia-tongs-anchored", ("item", target)), target, args.User);
                return;
            }

            if (_net.IsServer)
                Grip(ent, target, args.User);
            return;
        }

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (args.Target is not { } dest)
        {
            Release(ent, held.Value, args.ClickLocation, args.User);
            return;
        }

        if (TryPour(held.Value, dest, args.User))
            return;

        if (TryComp<PlaceableSurfaceComponent>(dest, out var surface))
        {
            if (!surface.IsPlaceable)
            {
                _popup.PopupEntity(Loc.GetString("dystopia-tongs-surface-full"), dest, args.User);
                return;
            }

            var coords = surface.PlaceCentered
                ? Transform(dest).Coordinates.Offset(surface.PositionOffset)
                : args.ClickLocation;
            Release(ent, held.Value, coords, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dystopia-tongs-cant-place"), dest, args.User);
    }

    private void Grip(Entity<DystopiaTongsComponent> ent, EntityUid target, EntityUid user)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Slot, out var container) || !_container.Insert(target, container))
            return;

        _audio.PlayPvs(ent.Comp.GripSound, ent);
        _popup.PopupEntity(Loc.GetString("dystopia-tongs-gripped", ("item", target)), ent, user);
    }

    public void Release(Entity<DystopiaTongsComponent> ent, EntityUid held, EntityCoordinates coords, EntityUid? user)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Slot, out var container))
            return;

        // Ставим ровно, без случайного поворота
        if (!_container.Remove(held, container, destination: coords, localRotation: Angle.Zero))
            return;

        _audio.PlayPvs(ent.Comp.ReleaseSound, ent);
        if (user != null)
            _popup.PopupEntity(Loc.GetString("dystopia-tongs-released", ("item", held)), ent, user.Value);
    }

    /// <summary>Разлить из зажатого тигля в изложницу или другой тигель — сколько влезет.</summary>
    private bool TryPour(EntityUid held, EntityUid target, EntityUid user)
    {
        if (!TryComp<DystopiaCrucibleComponent>(held, out var source))
            return false;

        // В литейный желоб — расплав разойдётся по изложницам под ним.
        if (TryComp<DystopiaCastingChannelComponent>(target, out var channel))
        {
            if (!_solution.TryGetSolution(held, source.Solution, out var fromSoln, out var fromSolution))
                return true;

            if (fromSolution.Volume <= FixedPoint2.Zero)
            {
                _popup.PopupEntity(Loc.GetString("dystopia-tongs-pour-empty"), target, user);
                return true;
            }

            var poured = _channel.Pour(user, held, fromSoln.Value, (target, channel));
            if (poured > FixedPoint2.Zero)
            {
                if (TryComp<DystopiaTongsComponent>(Transform(held).ParentUid, out var gripping))
                    _audio.PlayPvs(gripping.PourSound, target);

                _popup.PopupEntity(Loc.GetString("dystopia-tongs-poured", ("amount", poured), ("target", target)),
                    target, user);
            }

            return true;
        }

        string? targetSolution = null;
        if (TryComp<DystopiaMouldComponent>(target, out var mould))
            targetSolution = mould.Solution;
        else if (TryComp<DystopiaCrucibleComponent>(target, out var crucible))
            targetSolution = crucible.Solution;

        if (targetSolution == null)
            return false;

        if (!_solution.TryGetSolution(held, source.Solution, out var src, out var srcSolution) ||
            !_solution.TryGetSolution(target, targetSolution, out var dst, out var dstSolution))
        {
            return true;
        }

        if (srcSolution.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-tongs-pour-empty"), target, user);
            return true;
        }

        if (dstSolution.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-tongs-pour-full"), target, user);
            return true;
        }

        var moved = _transfer.Transfer(new SolutionTransferData(user, held, src.Value, target, dst.Value,
            dstSolution.AvailableVolume));
        if (moved <= FixedPoint2.Zero)
            return true;

        if (TryComp<DystopiaTongsComponent>(Transform(held).ParentUid, out var tongs))
            _audio.PlayPvs(tongs.PourSound, target);

        _popup.PopupEntity(Loc.GetString("dystopia-tongs-poured", ("amount", moved), ("target", target)), target, user);
        return true;
    }

    // ------------------------------------------------------------------ разжать

    private void OnUseInHand(Entity<DystopiaTongsComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || GetHeld(ent) is not { } held)
            return;

        args.Handled = true;
        if (_net.IsServer)
            Release(ent, held, _xform.GetMoverCoordinates(args.User), args.User);
    }

    private void OnGetVerbs(Entity<DystopiaTongsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetHeld(ent) is not { } held)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dystopia-tongs-release-verb"),
            Act = () =>
            {
                if (_net.IsServer)
                    Release(ent, held, _xform.GetMoverCoordinates(user), user);
            },
        });
    }

    private void OnExamined(Entity<DystopiaTongsComponent> ent, ref ExaminedEvent args)
    {
        if (GetHeld(ent) is { } held)
            args.PushMarkup(Loc.GetString("dystopia-tongs-holding", ("item", held)));
    }

    // Клещи с зажатым тиглем не убрать в сумку: только в руки.
    private void OnInsertAttempt(Entity<DystopiaTongsComponent> ent, ref ContainerGettingInsertedAttemptEvent args)
    {
        if (GetHeld(ent) != null && !HasComp<HandsComponent>(args.Container.Owner))
            args.Cancel();
    }

    private void OnGripChanged(Entity<DystopiaTongsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Slot)
            _appearance.SetData(ent, DystopiaTongsVisuals.Holding, true);
    }

    private void OnReleased(Entity<DystopiaTongsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Slot)
            _appearance.SetData(ent, DystopiaTongsVisuals.Holding, false);
    }

    // Клещи уничтожили — зажатое падает, а не исчезает вместе с ними.
    private void OnTerminating(Entity<DystopiaTongsComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_net.IsClient || GetHeld(ent) is not { } held)
            return;

        if (TerminatingOrDeleted(Transform(ent).MapUid))
            return;

        Release(ent, held, _xform.GetMoverCoordinates(ent), null);
    }
}
