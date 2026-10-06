// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: местная анестезия (лидокаин) — укол в одну часть тела: она не болит, и её можно оперировать
// в сознании без штрафа.

using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Dystopia.Health.Medical.Pain;

/// <summary>Часть тела обезболена местно до Until.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LocalAnesthesiaComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public TimeSpan Until;
}

/// <summary>Шприц местного обезболивания: колют в часть, куда целится врач.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LocalAnesthesiaInjectorComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(4);

    [DataField]
    public TimeSpan InjectTime = TimeSpan.FromSeconds(2);
}

[Serializable, NetSerializable]
public sealed partial class LocalAnesthesiaDoAfterEvent : DoAfterEvent
{
    public NetEntity Part;

    public LocalAnesthesiaDoAfterEvent(NetEntity part)
    {
        Part = part;
    }

    public override DoAfterEvent Clone() => this;
}

public sealed partial class LocalAnesthesiaSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LocalAnesthesiaInjectorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LocalAnesthesiaInjectorComponent, LocalAnesthesiaDoAfterEvent>(OnDoAfter);
    }

    /// <summary>Обезболена ли часть местно прямо сейчас.</summary>
    public bool IsNumb(EntityUid part)
    {
        return TryComp<LocalAnesthesiaComponent>(part, out var local) && _timing.CurTime < local.Until;
    }

    private void OnAfterInteract(Entity<LocalAnesthesiaInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target
            || _wounds.GetAimedPart(target, args.User) is not { } part)
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.InjectTime,
            new LocalAnesthesiaDoAfterEvent(GetNetEntity(part)), ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnDoAfter(Entity<LocalAnesthesiaInjectorComponent> ent, ref LocalAnesthesiaDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        var part = GetEntity(args.Part);
        if (!Exists(part) || !TryComp<BodyPartComponent>(part, out var bodyPart) || bodyPart.Body != target)
            return;

        var local = EnsureComp<LocalAnesthesiaComponent>(part);
        local.Until = _timing.CurTime + ent.Comp.Duration;
        Dirty(part, local);

        _popup.PopupEntity(Loc.GetString("local-anesthesia-applied", ("part", Name(part))), target, args.User);
        if (_net.IsServer)
            QueueDel(ent.Owner);
    }
}
