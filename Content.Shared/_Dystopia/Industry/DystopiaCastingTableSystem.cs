// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: литейный стол — сменная форма в слоте. Вставили форму — стол вмещает столько расплава, сколько форма;
// пока в форме металл (жидкий или застывающий), её не вынуть.

using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Dystopia.Industry;

public sealed partial class DystopiaCastingTableSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public const string NoForm = "none";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaMouldComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<DystopiaMouldComponent, EntInsertedIntoContainerMessage>(OnFormInserted);
        SubscribeLocalEvent<DystopiaMouldComponent, EntRemovedFromContainerMessage>(OnFormRemoved);
    }

    private bool HasMetal(Entity<DystopiaMouldComponent> ent)
    {
        return _solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) &&
               solution.Volume > FixedPoint2.Zero;
    }

    private void OnEjectAttempt(Entity<DystopiaMouldComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.FormSlot != args.Slot.ID || !HasMetal(ent))
            return;

        args.Cancelled = true;
        if (_net.IsServer && args.User is { } user)
            _popup.PopupEntity(Loc.GetString("dystopia-casting-table-form-busy"), ent, user);
    }

    private void OnFormInserted(Entity<DystopiaMouldComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (ent.Comp.FormSlot != args.Container.ID)
            return;

        var volume = TryComp<DystopiaCastingFormComponent>(args.Entity, out var form) ? form.Volume : 0f;
        SetCapacity(ent, volume);
        _appearance.SetData(ent, DystopiaCastingTableVisuals.Form, form?.Visual ?? NoForm);
    }

    private void OnFormRemoved(Entity<DystopiaMouldComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (ent.Comp.FormSlot != args.Container.ID)
            return;

        SetCapacity(ent, 0f);
        _appearance.SetData(ent, DystopiaCastingTableVisuals.Form, NoForm);
    }

    private void SetCapacity(Entity<DystopiaMouldComponent> ent, float volume)
    {
        if (_net.IsClient)
            return;

        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out _))
            _solution.SetCapacity(soln.Value, FixedPoint2.New(volume));
    }
}
