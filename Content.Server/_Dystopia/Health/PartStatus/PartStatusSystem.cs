// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared._Dystopia.Health.PartStatus.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Dystopia.Health.PartStatus;

/// <summary>Клик по кукле состояния — в чат приходит осмотр своих частей тела: состояние и раны.</summary>
public sealed partial class PartStatusSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IChatManager _chat = default!;

    private static readonly BodyPartType[] Order =
        { BodyPartType.Head, BodyPartType.Chest, BodyPartType.Arm, BodyPartType.Hand, BodyPartType.Groin, BodyPartType.Leg, BodyPartType.Foot };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GetPartStatusEvent>(OnGetPartStatus);
    }

    private void OnGetPartStatus(GetPartStatusEvent message, EntitySessionEventArgs args)
    {
        var entity = GetEntity(message.Uid);
        if (args.SenderSession.AttachedEntity != entity || _mobState.IsIncapacitated(entity) ||
            !TryComp<ActorComponent>(entity, out var actor))
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append(Loc.GetString("part-status-title"));

        var parts = _body.GetBodyChildren(entity)
            .Where(p => HasComp<WoundableComponent>(p.Id))
            .OrderBy(p => Array.IndexOf(Order, p.Component.PartType))
            .ThenBy(p => p.Component.Symmetry);

        foreach (var (partId, part) in parts)
        {
            var woundable = Comp<WoundableComponent>(partId);
            var name = Loc.GetString($"part-status-part-{part.PartType}-{part.Symmetry}");
            var state = Loc.GetString($"part-status-severity-{woundable.WoundableSeverity}");

            var wounds = _wounds.GetWoundableWounds(partId, woundable)
                .Where(w => w.Comp.WoundVisibility == WoundVisibility.Always && !w.Comp.IsScar)
                .Select(w => Loc.GetString("part-status-wound",
                    ("type", Loc.GetString($"part-status-damage-{w.Comp.DamageType.Id}")),
                    ("severity", Loc.GetString($"part-status-wound-severity-{w.Comp.WoundSeverity}"))))
                .ToList();

            sb.Append('\n');
            sb.Append(Loc.GetString("part-status-line", ("part", name), ("state", state)));
            if (wounds.Count > 0)
                sb.Append(Loc.GetString("part-status-wounds", ("wounds", string.Join(", ", wounds))));
        }

        var text = sb.ToString();
        _chat.ChatMessageToOne(ChatChannel.Emotes, text, text, EntityUid.Invalid, false,
            actor.PlayerSession.Channel, recordReplay: false);
    }
}
