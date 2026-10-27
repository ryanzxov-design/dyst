// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: камеры Реестра для соционадзора.
//  • Лента тревог: разыскиваемый, «вне закона», неопознанный в закрытой зоне, погасшая или уничтоженная камера,
//    слово-маркер. Тревогу принимают с консоли — на канал Стражи уходит, кто принял вызов, чтобы двое не ехали на один.
//  • Камера слушает: что сказано рядом (имя — как слышит стоящий рядом, из-под маски — номер), помнит последние
//    реплики; слово-маркер в реплике поднимает тревогу.
//  • «Голос Стражи»: вслух через камеру или маску (голос звучит на месте камеры), а через маску ещё и лично
//    носителю — в ухо, никто вокруг не слышит.
//  • Камера погасла (нет питания, перерезаны провода, ЭМИ) — тревога; снова в эфире — тревога снимается сама.

using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Speech;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityCameraSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier VoiceClick = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");

    /// <summary>Камера, которая сейчас говорит голосом Стражи (её речь подписывается «Голос Стражи»).</summary>
    private EntityUid? _voiceSpeaker;

    /// <summary>Что слышала каждая камера.</summary>
    private readonly Dictionary<EntityUid, List<CitySpeechLine>> _speech = new();

    /// <summary>Следующая тревога о слове-маркере от этого говорящего.</summary>
    private readonly Dictionary<(EntityUid Speaker, string Word), TimeSpan> _keywordAlerts = new();

    /// <summary>Недавние реплики со словом-маркером (одна реплика — одна тревога, сколько бы камер её ни слышали).</summary>
    private readonly Dictionary<(EntityUid Speaker, string Message), Utterance> _utterances = new();

    private static readonly TimeSpan UtteranceWindow = TimeSpan.FromSeconds(2);

    private void InitializeSurveillance()
    {
        SubscribeLocalEvent<CityCameraComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<CityCameraComponent, ComponentShutdown>(OnCameraShutdown);
    }

    private void ClearSurveillance()
    {
        _speech.Clear();
        _keywordAlerts.Clear();
        _utterances.Clear();
        _voiceSpeaker = null;
    }

    #region Работа камеры

    /// <summary>
    /// Следит, работает ли камера: настенная погасла — тревога, снова в эфире — тревога снимается. Маска запоминает,
    /// когда и где последний раз давала картинку (для патрулей).
    /// </summary>
    private void TrackActivity(
        Entity<CityRegistryComponent> registry,
        Entity<SurveillanceCameraComponent> camera,
        CityCameraComponent city,
        TimeSpan now)
    {
        var active = camera.Comp.Active;
        if (active)
        {
            city.LastActiveAt = now;
            if (city.Mask)
                city.LastPlace = PlaceOf(camera);
        }

        if (city.Mask || active == city.WasActive)
            return;

        city.WasActive = active;
        if (!active)
        {
            var reason = TryComp<ApcPowerReceiverComponent>(camera, out var power) && !power.Powered
                ? "dystopia-surveillance-down-power"
                : "dystopia-surveillance-down-off";
            var name = CameraName(camera, city);
            var alert = AddAlert(registry, CityAlertKind.CameraDown,
                Loc.GetString("dystopia-surveillance-alert-down", ("camera", name), ("reason", Loc.GetString(reason))),
                PlaceOf(camera), camera.Owner, null);
            city.DownAlert = alert.Id;
            return;
        }

        // снова в эфире — открытая тревога о ней снимается сама
        if (city.DownAlert is { } id && registry.Comp.Alerts.Find(a => a.Id == id) is { } open)
        {
            open.Resolved = true;
            open.ResolvedAt = now;
        }

        city.DownAlert = null;
    }

    /// <summary>Уничтоженная настенная камера — тревога (кроме конца раунда, когда удаляется всё).</summary>
    private void OnCameraShutdown(Entity<CityCameraComponent> ent, ref ComponentShutdown args)
    {
        _speech.Remove(ent.Owner);
        if (ent.Comp.Mask || !ent.Comp.WasActive || !_registry.TryGetRegistry(out var registry) ||
            TerminatingOrDeleted(registry.Owner))
        {
            return;
        }

        var xform = Transform(ent);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map) ||
            xform.GridUid is { } grid && TerminatingOrDeleted(grid))
        {
            return;
        }

        AddAlert(registry, CityAlertKind.CameraDown,
            Loc.GetString("dystopia-surveillance-alert-destroyed", ("camera", CameraName(ent))),
            PlaceOf(ent), null, null);
    }

    #endregion

    #region Лента тревог

    public CityAlert AddAlert(
        Entity<CityRegistryComponent> registry,
        CityAlertKind kind,
        string text,
        string place,
        EntityUid? camera,
        int? number)
    {
        var alert = new CityAlert
        {
            Id = registry.Comp.NextAlert++,
            Time = _ticker.RoundDuration(),
            Kind = kind,
            Text = text,
            Place = place,
            Camera = camera is { } uid && !TerminatingOrDeleted(uid) ? GetNetEntity(uid) : null,
            Number = number,
        };

        var list = registry.Comp.Alerts;
        list.Add(alert);
        var overflow = list.Count - Math.Max(1, registry.Comp.AlertLimit);
        if (overflow > 0)
            list.RemoveRange(0, overflow);

        return alert;
    }

    /// <summary>Убрать тревогу из ленты (null — все принятые и снятые сами).</summary>
    public bool DismissAlert(Entity<CityRegistryComponent> registry, int? id)
    {
        if (id is { } single)
            return registry.Comp.Alerts.RemoveAll(a => a.Id == single) > 0;

        return registry.Comp.Alerts.RemoveAll(a => a.AcceptedBy.Length > 0 || a.Resolved) > 0;
    }

    /// <summary>Принять вызов: подпись в ленте и сообщение на канал Стражи, кто выехал.</summary>
    public bool AcceptAlert(Entity<CityRegistryComponent> registry, int id, string author, EntityUid console)
    {
        if (registry.Comp.Alerts.Find(a => a.Id == id) is not { } alert || alert.AcceptedBy.Length > 0)
            return false;

        alert.AcceptedBy = author;
        alert.AcceptedAt = _ticker.RoundDuration();
        Broadcast(registry, console, Loc.GetString("dystopia-surveillance-accepted-radio",
            ("id", id.ToString()),
            ("author", FormattedMessage.EscapeText(author)),
            ("text", FormattedMessage.EscapeText(alert.Text)),
            ("place", FormattedMessage.EscapeText(alert.Place))));
        return true;
    }

    #endregion

    #region Речь и слова-маркеры

    private void OnListen(Entity<CityCameraComponent> ent, ref ListenEvent args)
    {
        if (!TryComp<SurveillanceCameraComponent>(ent, out var camera) || !camera.Active ||
            !_registry.TryGetRegistry(out var registry))
        {
            return;
        }

        var voice = _voiceSpeaker != null && args.Source == _voiceSpeaker;
        string name;
        if (voice)
        {
            name = Loc.GetString("dystopia-camera-voice");
        }
        else
        {
            // как слышит стоящий рядом: голос со всеми подменами (из-под маски Стражи — номер)
            var nameEv = new TransformSpeakerNameEvent(args.Source, Name(args.Source));
            RaiseLocalEvent(args.Source, nameEv);
            name = nameEv.VoiceName;
        }

        var keyword = voice ? null : FindKeyword(registry, args.Message);
        AddLine(registry, ent, new CitySpeechLine
        {
            Time = _ticker.RoundDuration(),
            Name = name,
            Text = args.Message,
            Voice = voice,
            Keyword = keyword != null,
        });

        if (keyword == null)
            return;

        var now = _ticker.RoundDuration();
        var cameraName = CameraName(ent, ent.Comp);

        // одна реплика — одна тревога: её слышат все камеры рядом, их имена дописываются в ту же тревогу
        var key = (args.Source, args.Message);
        if (_utterances.TryGetValue(key, out var heard) && now - heard.Time <= UtteranceWindow)
        {
            if (!heard.Cameras.Contains(cameraName))
            {
                heard.Cameras.Add(cameraName);
                if (registry.Comp.Alerts.Find(a => a.Id == heard.AlertId) is { } same)
                    same.Text = KeywordText(heard);
            }

            return;
        }

        // тот же человек, то же слово — тревога не чаще раза в KeywordCooldown
        if (_keywordAlerts.TryGetValue((args.Source, keyword), out var next) && now < next)
            return;
        _keywordAlerts[(args.Source, keyword)] = now + registry.Comp.KeywordCooldown;

        var utterance = new Utterance(now, keyword, name, args.Message);
        utterance.Cameras.Add(cameraName);
        var place = PlaceOf(ent);
        var alert = AddAlert(registry, CityAlertKind.Keyword, KeywordText(utterance), place, ent.Owner,
            _registry.NumberOf(args.Source));
        utterance.AlertId = alert.Id;

        if (_utterances.Count > 64)
        {
            foreach (var old in _utterances.Where(u => now - u.Value.Time > UtteranceWindow).Select(u => u.Key).ToList())
            {
                _utterances.Remove(old);
            }
        }
        _utterances[key] = utterance;

        Broadcast(registry, ent, Loc.GetString("dystopia-camera-alert-keyword",
            ("word", FormattedMessage.EscapeText(keyword)),
            ("name", FormattedMessage.EscapeText(name)),
            ("text", FormattedMessage.EscapeText(args.Message)),
            ("camera", FormattedMessage.EscapeText(cameraName)),
            ("place", FormattedMessage.EscapeText(place))));
    }

    private string KeywordText(Utterance utterance)
    {
        return Loc.GetString("dystopia-surveillance-alert-keyword",
            ("word", utterance.Word), ("name", utterance.Name), ("text", utterance.Text),
            ("camera", string.Join(", ", utterance.Cameras.Select(c => $"«{c}»"))));
    }

    /// <summary>Реплика со словом-маркером: какие камеры её слышали и в какую тревогу она записана.</summary>
    private sealed class Utterance(TimeSpan time, string word, string name, string text)
    {
        public readonly TimeSpan Time = time;
        public readonly string Word = word;
        public readonly string Name = name;
        public readonly string Text = text;
        public readonly List<string> Cameras = new();
        public int AlertId;
    }

    /// <summary>Слово-маркер в реплике: слово, которое начинается с маркера («культ» — «культисты»).</summary>
    private static string? FindKeyword(Entity<CityRegistryComponent> registry, string message)
    {
        if (registry.Comp.Keywords.Count == 0)
            return null;

        var words = Regex.Split(message.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(w => w.Length > 0)
            .ToList();

        foreach (var keyword in registry.Comp.Keywords)
        {
            if (words.Any(w => w.StartsWith(keyword, StringComparison.Ordinal)))
                return keyword;
        }

        return null;
    }

    private void AddLine(Entity<CityRegistryComponent> registry, EntityUid camera, CitySpeechLine line)
    {
        if (!_speech.TryGetValue(camera, out var lines))
            _speech[camera] = lines = new List<CitySpeechLine>();

        lines.Add(line);
        var overflow = lines.Count - Math.Max(1, registry.Comp.SpeechLimit);
        if (overflow > 0)
            lines.RemoveRange(0, overflow);
    }

    /// <summary>Что камера слышала за последние минуты, старые — первыми.</summary>
    public List<CitySpeechLine> GetSpeech(Entity<CityRegistryComponent> registry, EntityUid camera)
    {
        if (!_speech.TryGetValue(camera, out var lines))
            return new List<CitySpeechLine>();

        var since = _ticker.RoundDuration() - registry.Comp.SpeechLifetime;
        lines.RemoveAll(l => l.Time < since);
        return new List<CitySpeechLine>(lines);
    }

    /// <summary>Добавить или убрать слово-маркер. Маркер — одно слово в нижнем регистре.</summary>
    public bool SetKeyword(Entity<CityRegistryComponent> registry, string word, bool add)
    {
        word = new string(word.Trim().ToLowerInvariant().TakeWhile(char.IsLetterOrDigit).ToArray());
        if (word.Length < 3 || word.Length > 24)
            return false;

        var list = registry.Comp.Keywords;
        if (add)
        {
            if (list.Contains(word) || list.Count >= 30)
                return false;

            list.Add(word);
            return true;
        }

        return list.Remove(word);
    }

    #endregion

    #region Голос Стражи

    /// <summary>
    /// «Голос Стражи». Вслух — реплика звучит на месте камеры (у маски — у лица стражника) под именем «Голос Стражи».
    /// Лично (только маска) — в ухо носителю, щелчок слышит только он. Возвращает false, если сказать нельзя.
    /// </summary>
    public bool Speak(EntityUid camera, string text, bool personal, string author)
    {
        if (!TryComp<CityCameraComponent>(camera, out var city) ||
            !TryComp<SurveillanceCameraComponent>(camera, out var cameraComp) || !cameraComp.Active ||
            !_registry.TryGetRegistry(out var registry))
        {
            return false;
        }

        text = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (text.Length == 0)
            return false;

        if (personal)
        {
            if (!city.Mask || city.Wearer is not { } wearer || !TryComp<ActorComponent>(wearer, out var actor))
                return false;

            var wrapped = Loc.GetString("dystopia-camera-voice-personal", ("text", FormattedMessage.EscapeText(text)));
            _chatManager.ChatMessageToOne(ChatChannel.Local, text, wrapped, camera, false, actor.PlayerSession.Channel);
            _audio.PlayEntity(VoiceClick, Filter.SinglePlayer(actor.PlayerSession), wearer, false);

            AddLine(registry, camera, new CitySpeechLine
            {
                Time = _ticker.RoundDuration(),
                Name = Loc.GetString("dystopia-camera-voice-personal-name", ("number", city.WearerNumber?.ToString() ?? "—")),
                Text = text,
                Voice = true,
            });
        }
        else
        {
            _voiceSpeaker = camera;
            try
            {
                _chat.TrySendInGameICMessage(camera, text, InGameICChatType.Speak, ChatTransmitRange.Normal,
                    nameOverride: Loc.GetString("dystopia-camera-voice"), checkRadioPrefix: false, ignoreActionBlocker: true);
            }
            finally
            {
                _voiceSpeaker = null;
            }
        }

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"Голос Стражи ({(personal ? "лично" : "вслух")}) через {ToPrettyString(camera):camera} от {author}: {text}");
        return true;
    }

    #endregion

    #region Для консоли

    public static CityCameraKind KindOf(CityCameraComponent city)
    {
        return city.Mask ? CityCameraKind.Mask : city.Restricted ? CityCameraKind.Restricted : CityCameraKind.Wall;
    }

    /// <summary>Камера для списка и карты консоли.</summary>
    public CitySurveillanceCamera Describe(Entity<SurveillanceCameraComponent> camera, CityCameraComponent city)
    {
        var entry = new CitySurveillanceCamera
        {
            Entity = GetNetEntity(camera),
            Name = CameraName(camera, city),
            Place = city.Mask ? city.LastPlace : PlaceOf(camera),
            Kind = KindOf(city),
            Active = camera.Comp.Active,
        };

        if (!entry.Active)
        {
            entry.Status = Loc.GetString(TryComp<ApcPowerReceiverComponent>(camera, out var power) && !power.Powered
                ? "dystopia-surveillance-down-power"
                : "dystopia-surveillance-down-off");
        }

        if (TryGridPosition(camera, out var grid, out var position))
        {
            entry.Grid = GetNetEntity(grid);
            entry.Position = position;
        }

        return entry;
    }

    /// <summary>Точка на сетке Города, где сейчас сущность (для карт консоли).</summary>
    public bool TryGridPosition(EntityUid uid, out EntityUid grid, out Vector2 position)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } found)
        {
            grid = default;
            position = default;
            return false;
        }

        grid = found;
        position = Vector2.Transform(_transform.GetWorldPosition(xform), _transform.GetInvWorldMatrix(found));
        return true;
    }

    #endregion
}
