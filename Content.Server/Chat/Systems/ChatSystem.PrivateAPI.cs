using System.Linq;
using Content.Server._Dystopia.Chat; // Dystopia-MaskName
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Radio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    [Dependency] private DystopiaMaskedNameSystem _maskedNames = default!; // Dystopia-MaskName

    /// <summary>Dystopia-MaskName: имя в обёртке сообщения — ссылкой (как у ванили) или простым текстом.</summary>
    private string ChatName(EntityUid source, string name, bool link)
    {
        return link && ChatNameLinks
            ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(name)}\" entity=\"{GetNetEntity(source)}\" entitynamecolor=\"true\"]"
            : FormattedMessage.EscapeText(name);
    }

    private void SendEntitySpeak(
        EntityUid source,
        string originalMessage,
        ChatTransmitRange range,
        string? nameOverride,
        bool hideLog = false,
        bool ignoreActionBlocker = false
        )
    {
        if (!_actionBlocker.CanSpeak(source) && !ignoreActionBlocker)
            return;

        var message = TransformSpeech(source, originalMessage);

        if (message.Length == 0)
            return;

        var speech = GetSpeechVerb(source, message);

        // get the entity's apparent name (if no override provided).
        string name;
        if (nameOverride != null)
        {
            name = nameOverride;
        }
        else
        {
            var nameEv = new TransformSpeakerNameEvent(source, Name(source));
            RaiseLocalEvent(source, nameEv);
            name = nameEv.VoiceName;
            // Check for a speech verb override
            if (nameEv.SpeechVerb != null && ProtoMan.Resolve(nameEv.SpeechVerb, out var proto))
                speech = proto;
        }

        // Dystopia-MaskName: из-под маски голос — номер гражданина (без ссылки), настоящее имя — тем, кому положено
        var masked = nameOverride == null && _maskedNames.IsMaskedVoice(source, name);
        var verb = Loc.GetString(_random.Pick(speech.SpeechVerbStrings));
        string Wrap(string shownName) => Loc.GetString(speech.Bold ? "chat-manager-entity-say-bold-wrap-message" : "chat-manager-entity-say-wrap-message",
            ("entityName", shownName),
            ("verb", verb),
            ("fontType", speech.FontId),
            ("fontSize", speech.FontSize),
            ("message", FormattedMessage.EscapeText(message)));

        name = ChatName(source, name, !masked);
        var wrappedMessage = Wrap(name);
        Func<ICommonSession, ICChatRecipientData, string>? wrapFor = null;
        if (masked)
        {
            var real = Wrap(ChatName(source, _maskedNames.LabelFor(source, DystopiaNameView.Real), true));
            var full = Wrap(ChatName(source, _maskedNames.LabelFor(source, DystopiaNameView.Full), true));
            wrapFor = (session, data) => _maskedNames.ViewOf(session, data.Observer) switch
            {
                DystopiaNameView.Real => real,
                DystopiaNameView.Full => full,
                _ => wrappedMessage,
            };
        }

        SendInVoiceRange(ChatChannel.Local, message, wrappedMessage, source, range, wrapFor: wrapFor);

        var ev = new EntitySpokeEvent(source, message, originalMessage, null, null);
        RaiseLocalEvent(source, ev, true);

        // To avoid logging any messages sent by entities that are not players, like vendors, cloning, etc.
        // Also doesn't log if hideLog is true.
        if (!HasComp<ActorComponent>(source) || hideLog)
            return;

        if (originalMessage == message)
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {source} as {name}: {originalMessage}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {source}: {originalMessage}.");
        }
        else
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Say from {source} as {name}, original: {originalMessage}, transformed: {message}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Say from {source}, original: {originalMessage}, transformed: {message}.");
        }
    }

    private void SendEntityWhisper(
        EntityUid source,
        string originalMessage,
        ChatTransmitRange range,
        RadioChannelPrototype? channel,
        string? nameOverride,
        bool hideLog = false,
        bool ignoreActionBlocker = false
        )
    {
        if (!_actionBlocker.CanSpeak(source) && !ignoreActionBlocker)
            return;

        var message = TransformSpeech(source, FormattedMessage.RemoveMarkupOrThrow(originalMessage));
        if (message.Length == 0)
            return;

        var obfuscatedMessage = ObfuscateMessageReadability(message, 0.2f);

        // get the entity's name by visual identity (if no override provided).
        string nameIdentity = FormattedMessage.EscapeText(nameOverride ?? Identity.Name(source, EntityManager));
        // get the entity's name by voice (if no override provided).
        string name;
        if (nameOverride != null)
        {
            name = nameOverride;
        }
        else
        {
            var nameEv = new TransformSpeakerNameEvent(source, Name(source));
            RaiseLocalEvent(source, nameEv);
            name = nameEv.VoiceName;
        }
        // Dystopia-MaskName: из-под маски — номер (и издали тоже), настоящее имя — тем, кому положено
        var masked = nameOverride == null && _maskedNames.IsMaskedVoice(source, name);
        if (masked)
            nameIdentity = FormattedMessage.EscapeText(name);

        name = ChatName(source, name, !masked);

        var wrappedMessage = Loc.GetString("chat-manager-entity-whisper-wrap-message",
            ("entityName", name), ("message", FormattedMessage.EscapeText(message)));

        var wrappedobfuscatedMessage = Loc.GetString("chat-manager-entity-whisper-wrap-message",
            ("entityName", nameIdentity), ("message", FormattedMessage.EscapeText(obfuscatedMessage)));

        // Dystopia-MaskName: обёртки с настоящим именем (и с именем и номером) для своих
        string WhisperFor(DystopiaNameView view, string text, string fallback)
        {
            if (!masked || view == DystopiaNameView.Masked)
                return fallback;

            return Loc.GetString("chat-manager-entity-whisper-wrap-message",
                ("entityName", ChatName(source, _maskedNames.LabelFor(source, view), true)),
                ("message", FormattedMessage.EscapeText(text)));
        }

        var wrappedUnknownMessage = Loc.GetString("chat-manager-entity-whisper-unknown-wrap-message",
            ("message", FormattedMessage.EscapeText(obfuscatedMessage)));


        foreach (var (session, data) in GetRecipients(source, WhisperMuffledRange))
        {
            EntityUid listener;

            if (session.AttachedEntity is not { Valid: true } playerEntity)
                continue;
            listener = session.AttachedEntity.Value;

            if (MessageRangeCheck(session, data, range) != MessageRangeCheckResult.Full)
                continue; // Won't get logged to chat, and ghosts are too far away to see the pop-up, so we just won't send it to them.

            var view = masked ? _maskedNames.ViewOf(session, data.Observer) : DystopiaNameView.Masked; // Dystopia-MaskName
            if (data.Range <= WhisperClearRange || data.Observer)
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, message, WhisperFor(view, message, wrappedMessage), source, false, session.Channel);
            //If listener is too far, they only hear fragments of the message
            else if (_examineSystem.InRangeUnOccluded(source, listener, WhisperMuffledRange))
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, obfuscatedMessage, WhisperFor(view, obfuscatedMessage, wrappedobfuscatedMessage), source, false, session.Channel);
            //If listener is too far and has no line of sight, they can't identify the whisperer's identity
            else
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, obfuscatedMessage, wrappedUnknownMessage, source, false, session.Channel);
        }

        _replay.RecordServerMessage(new ChatMessage(ChatChannel.Whisper, message, wrappedMessage, GetNetEntity(source), null, MessageRangeHideChatForReplay(range)));

        var ev = new EntitySpokeEvent(source, message, originalMessage, channel, obfuscatedMessage);
        RaiseLocalEvent(source, ev, true);
        if (!hideLog)
            if (originalMessage == message)
            {
                if (name != Name(source))
                    _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Whisper from {source} as {name}: {originalMessage}.");
                else
                    _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Whisper from {source}: {originalMessage}.");
            }
            else
            {
                if (name != Name(source))
                    _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Whisper from {source} as {name}, original: {originalMessage}, transformed: {message}.");
                else
                    _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Whisper from {source}, original: {originalMessage}, transformed: {message}.");
            }
    }

    protected override void SendEntityEmote(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        string? nameOverride,
        bool hideLog = false,
        bool checkEmote = true,
        bool ignoreActionBlocker = false,
        NetUserId? author = null
        )
    {
        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        // get the entity's apparent name (if no override provided).
        var ent = Identity.Entity(source, EntityManager);
        // Dystopia-MaskName: в маске Стражи — номер гражданина, своим — настоящее имя
        var masked = nameOverride == null && _maskedNames.IsMasked(source);
        string name = FormattedMessage.EscapeText(nameOverride ?? (masked ? _maskedNames.MaskedLabel(source) : Name(ent)));

        // Emotes use Identity.Name, since it doesn't actually involve your voice at all.
        string WrapEmote(string shownName) => Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", shownName),
            ("entity", ent),
            ("message", FormattedMessage.RemoveMarkupOrThrow(action)));

        var wrappedMessage = WrapEmote(name);

        if (checkEmote &&
            !TryEmoteChatInput(source, action))
            return;

        Func<ICommonSession, ICChatRecipientData, string>? wrapFor = null;
        if (masked)
        {
            var real = WrapEmote(FormattedMessage.EscapeText(_maskedNames.LabelFor(source, DystopiaNameView.Real)));
            var full = WrapEmote(FormattedMessage.EscapeText(_maskedNames.LabelFor(source, DystopiaNameView.Full)));
            wrapFor = (session, data) => _maskedNames.ViewOf(session, data.Observer) switch
            {
                DystopiaNameView.Real => real,
                DystopiaNameView.Full => full,
                _ => wrappedMessage,
            };
        }

        SendInVoiceRange(ChatChannel.Emotes, action, wrappedMessage, source, range, author, wrapFor);
        if (!hideLog)
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {source} as {name}: {action}");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {source}: {action}");
    }

    // ReSharper disable once InconsistentNaming
    private void SendLOOC(EntityUid source, ICommonSession player, string message, bool hideChat)
    {
        var name = FormattedMessage.EscapeText(Identity.Name(source, EntityManager));
        name = ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(name)}\" entity=\"{GetNetEntity(source)}\" color=\"{ChatChannel.LOOC.TextColor().ToHex()}\"]": FormattedMessage.EscapeText(name);
        if (_adminManager.IsAdmin(player))
        {
            if (!_adminLoocEnabled) return;
        }
        else if (!_loocEnabled) return;

        // If crit player LOOC is disabled, don't send the message at all.
        if (!_critLoocEnabled && _mobStateSystem.IsCritical(source))
            return;

        var wrappedMessage = Loc.GetString("chat-manager-entity-looc-wrap-message",
            ("entityName", name),
            ("message", FormattedMessage.EscapeText(message)));

        SendInVoiceRange(ChatChannel.LOOC, message, wrappedMessage, source, hideChat ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal, player.UserId);
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"LOOC from {source}: {message}");
    }

    private void SendDeadChat(EntityUid source, ICommonSession player, string message, bool hideChat)
    {
        if (!_adminManager.IsAdmin(player) && !_deadChatEnabled)
            return;

        var clients = GetDeadChatClients();
        string wrappedMessage;
        if (_adminManager.IsAdmin(player))
        {
            var userName = ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(player.Channel.UserName)}\" entity=\"{GetNetEntity(source)}\" color=\"{ChatChannel.Dead.TextColor().ToHex()}\"]" : FormattedMessage.EscapeText(player.Channel.UserName);
            wrappedMessage = Loc.GetString("chat-manager-send-admin-dead-chat-wrap-message",
                ("adminChannelName", Loc.GetString("chat-manager-admin-channel-name")),
                ("userName", (userName)),
                ("message", FormattedMessage.EscapeText(message)));
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Admin dead chat from {source}: {message}");
        }
        else
        {
            var playerName = ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(Name(source))}\" entity=\"{GetNetEntity(source)}\" color=\"{ChatChannel.Dead.TextColor().ToHex()}\"]" : FormattedMessage.EscapeText(Name(source));
            wrappedMessage = Loc.GetString("chat-manager-send-dead-chat-wrap-message",
                ("deadChannelName", Loc.GetString("chat-manager-dead-channel-name")),
                ("playerName", (playerName)),
                ("message", FormattedMessage.EscapeText(message)));
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Dead chat from {source}: {message}");
        }

        _chatManager.ChatMessageToMany(ChatChannel.Dead, message, wrappedMessage, source, hideChat, true, clients.ToList(), author: player.UserId);
    }
}
