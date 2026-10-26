// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: голос из-под маски Стражи. Пока маска на лице (не приспущена):
//  - способ речи — свой (шрифт Terminus, глаголы «чеканит»…), если его не подменил кто-то другой;
//  - имя голоса — номер гражданина «[#102628]» (номер счёта; без счёта — «[#——————]»). Подмена — в самом источнике
//    голоса (TransformSpeakerNameEvent), поэтому номер звучит везде: речь, шёпот, рация, интеркомы, динамики, телефон.
// Настоящее имя возвращают адресно (правки ванили с пометкой Dystopia-MaskName): в речи, шёпоте, эмоциях и наушниках
// тем, у кого надета вещь с DystopiaNameSight (гарнитуры Стражи и командования, маска Стражи на лице); призраки и
// админы видят «Имя [#102628]».

using Content.Server._Dystopia.Economy;
using Content.Server.Administration.Managers;
using Content.Shared._Dystopia.Clothing;
using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Inventory;
using Robust.Shared.Player;

namespace Content.Server._Dystopia.Chat;

/// <summary>Как слушатель видит имя говорящего из-под маски.</summary>
public enum DystopiaNameView : byte
{
    /// <summary>Номер гражданина.</summary>
    Masked,

    /// <summary>Настоящее имя.</summary>
    Real,

    /// <summary>Настоящее имя и номер (призраки, админы).</summary>
    Full,
}

public sealed class DystopiaMaskedNameSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly string[] SightSlots = { "ears", "mask" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFaceMaskComponent, InventoryRelayedEvent<TransformSpeakerNameEvent>>(OnSpeakerName);
    }

    private void OnSpeakerName(Entity<DystopiaFaceMaskComponent> ent, ref InventoryRelayedEvent<TransformSpeakerNameEvent> args)
    {
        if (IsPulledDown(ent))
            return;

        var ev = args.Args;
        if (ent.Comp.SpeechVerb is { } verb && ev.SpeechVerb == null)
            ev.SpeechVerb = verb;

        // имя подменяем, только если его никто ещё не подменил (маска-голосовик и т.п.)
        if (ent.Comp.HideName && ev.VoiceName == Name(ev.Sender))
            ev.VoiceName = MaskedLabel(ev.Sender);
    }

    private bool IsPulledDown(EntityUid item)
    {
        return TryComp<MaskComponent>(item, out var mask) && mask.IsToggled;
    }

    /// <summary>Номер вместо имени: «[#102628]».</summary>
    public string MaskedLabel(EntityUid speaker)
    {
        return _bank.CitizenNumber(speaker) is { } number
            ? Loc.GetString("dystopia-citizen-masked", ("number", number.ToString()))
            : Loc.GetString("dystopia-citizen-masked-unknown");
    }

    /// <summary>Имя с номером для призраков и админов: «Надежда Ильина [#102628]».</summary>
    public string FullLabel(EntityUid speaker)
    {
        return _bank.CitizenNumber(speaker) is { } number
            ? Loc.GetString("dystopia-citizen-full", ("name", Name(speaker)), ("number", number.ToString()))
            : Loc.GetString("dystopia-citizen-full-unknown", ("name", Name(speaker)));
    }

    /// <summary>
    /// Говорит ли сейчас источник из-под маски: его голос — номер гражданина (а не подменён чем-то другим).
    /// </summary>
    public bool IsMaskedVoice(EntityUid speaker, string voiceName)
    {
        return voiceName == MaskedLabel(speaker);
    }

    /// <summary>Говорящий в маске, прячущей имя (для эмоций — их имя берётся не из голоса).</summary>
    public bool IsMasked(EntityUid speaker)
    {
        if (!_inventory.TryGetSlotEntity(speaker, "mask", out var mask) ||
            !TryComp<DystopiaFaceMaskComponent>(mask, out var face) ||
            !face.HideName)
        {
            return false;
        }

        return !IsPulledDown(mask.Value);
    }

    /// <summary>Как видит имя этот слушатель.</summary>
    public DystopiaNameView ViewOf(ICommonSession session, bool observer)
    {
        if (observer || _admin.IsAdmin(session))
            return DystopiaNameView.Full;

        return session.AttachedEntity is { } listener ? ViewOf(listener) : DystopiaNameView.Masked;
    }

    /// <summary>Как видит имя сущность-слушатель (без учёта админских прав).</summary>
    public DystopiaNameView ViewOf(EntityUid listener)
    {
        if (HasComp<GhostComponent>(listener))
            return DystopiaNameView.Full;

        foreach (var slot in SightSlots)
        {
            if (_inventory.TryGetSlotEntity(listener, slot, out var item) &&
                HasComp<DystopiaNameSightComponent>(item) &&
                !IsPulledDown(item.Value))
            {
                return DystopiaNameView.Real;
            }
        }

        return DystopiaNameView.Masked;
    }

    /// <summary>Подпись говорящего для этого взгляда.</summary>
    public string LabelFor(EntityUid speaker, DystopiaNameView view)
    {
        return view switch
        {
            DystopiaNameView.Real => Name(speaker),
            DystopiaNameView.Full => FullLabel(speaker),
            _ => MaskedLabel(speaker),
        };
    }
}
