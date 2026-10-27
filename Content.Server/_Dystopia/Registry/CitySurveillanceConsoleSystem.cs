// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: консоль соционадзора — пост наблюдения Стражи.
//  • Камеры: список и карта (камеры Города, закрытые зоны, надетые маски Стражи), картинка выбранной камеры на экране,
//    речь рядом с ней, «Голос Стражи» (вслух через камеру или маску, лично — в ухо носителю маски).
//  • Тревоги: общая лента (камеры Реестра), «Смотреть» и «Принять» — кто принял, уходит на канал Стражи.
//  • Слежка: ведёт гражданина — картинка сама переходит на камеру, которая видит его сейчас; путь за последние минуты.
//  • Патрули: маски стражников — надета/приспущена/снята, где последний раз была картинка, в смене ли стражник.
//  • Слова-маркеры: список правится здесь.
// Смотреть — всем, кого пускает AccessReader; действовать — по профессии того, чья ID-карта у пользователя.
// Картинку видят все, у кого открыта консоль: сервер подписывает их на обзор текущей камеры.

using System.Linq;
using Content.Server.Popups;
using Content.Server.SurveillanceCamera;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Access.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CitySurveillanceConsoleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SurveillanceCameraSystem _cameras = default!;
    [Dependency] private CityCameraSystem _cityCameras = default!;
    [Dependency] private CityRegistrySystem _registry = default!;

    private float _accumulator;
    private readonly Dictionary<EntityUid, EntityUid> _lastActor = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CitySurveillanceConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CitySurveillanceConsoleComponent, BoundUIClosedEvent>(OnClosed);
        SubscribeLocalEvent<CitySurveillanceConsoleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CitySurveillanceConsoleComponent, CitySurveillanceSelectMessage>(OnSelect);
        SubscribeLocalEvent<CitySurveillanceConsoleComponent, CitySurveillanceAcceptMessage>(OnAccept);
        SubscribeLocalEvent<CitySurveillanceConsoleComponent, CitySurveillanceTrackMessage>(OnTrack);
        SubscribeLocalEvent<CitySurveillanceConsoleComponent, CitySurveillanceKeywordMessage>(OnKeyword);
        SubscribeLocalEvent<CitySurveillanceConsoleComponent, CitySurveillanceVoiceMessage>(OnVoice);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;
        _accumulator = 0f;

        var query = EntityQueryEnumerator<CitySurveillanceConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_ui.IsUiOpen(uid, CitySurveillanceUiKey.Key))
                continue;

            Entity<CitySurveillanceConsoleComponent> ent = (uid, comp);
            Follow(ent);
            Reconcile(ent);
            UpdateUi(ent, null);
        }
    }

    #region Картинка

    private void OnOpened(Entity<CitySurveillanceConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        Reconcile(ent);
        UpdateUi(ent, args.Actor);
    }

    private void OnClosed(Entity<CitySurveillanceConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is CitySurveillanceUiKey.Key && ent.Comp.Camera is { } camera && !TerminatingOrDeleted(camera))
            _cameras.RemoveActiveViewer(camera, args.Actor);
    }

    private void OnShutdown(Entity<CitySurveillanceConsoleComponent> ent, ref ComponentShutdown args)
    {
        _lastActor.Remove(ent.Owner);
        SetCamera(ent, null);
    }

    /// <summary>Сменить камеру на экране: смотрящие отписываются от старой и подписываются на новую.</summary>
    private void SetCamera(Entity<CitySurveillanceConsoleComponent> ent, EntityUid? camera)
    {
        if (ent.Comp.Camera == camera)
            return;

        if (ent.Comp.Camera is { } old && !TerminatingOrDeleted(old))
        {
            foreach (var actor in _ui.GetActors(ent.Owner, CitySurveillanceUiKey.Key).ToList())
            {
                _cameras.RemoveActiveViewer(old, actor);
            }
        }

        ent.Comp.Camera = camera;
        Reconcile(ent);
    }

    /// <summary>Все, у кого открыта консоль, видят текущую камеру (камера могла погаснуть и снова зажечься).</summary>
    private void Reconcile(Entity<CitySurveillanceConsoleComponent> ent)
    {
        if (ent.Comp.Camera is not { } camera)
            return;

        if (TerminatingOrDeleted(camera))
        {
            ent.Comp.Camera = null;
            return;
        }

        if (!TryComp<SurveillanceCameraComponent>(camera, out var comp) || !comp.Active)
            return;

        // повторная подписка безвредна: уже подписанного камера не подписывает второй раз
        foreach (var actor in _ui.GetActors(ent.Owner, CitySurveillanceUiKey.Key).ToList())
        {
            _cameras.AddActiveViewer(camera, actor, null, comp);
        }
    }

    private void OnSelect(Entity<CitySurveillanceConsoleComponent> ent, ref CitySurveillanceSelectMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent.Owner))
            return;

        EntityUid? camera = null;
        if (args.Camera is { } net)
        {
            if (!TryGetEntity(net, out var found) || !HasComp<CityCameraComponent>(found))
                return;
            camera = found;
        }

        // переключились руками — слежка больше не водит картинку
        ent.Comp.Tracking = null;
        SetCamera(ent, camera);
        UpdateUi(ent, args.Actor);
    }

    #endregion

    #region Права

    private bool CanUse(Entity<CitySurveillanceConsoleComponent> ent, EntityUid user)
    {
        return _access.IsAllowed(user, ent.Owner) &&
               _registry.TryGetHolderRecord(user, out var record) &&
               record.Job is { } job && !record.Dismissed &&
               ent.Comp.UseJobs.Contains(job);
    }

    private bool Check(Entity<CitySurveillanceConsoleComponent> ent, EntityUid user)
    {
        if (CanUse(ent, user))
            return true;

        Deny(ent, user, "dystopia-surveillance-denied");
        return false;
    }

    private void Deny(Entity<CitySurveillanceConsoleComponent> ent, EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), ent.Owner, user);
        _audio.PlayPvs(ent.Comp.DeniedSound, ent.Owner);
    }

    #endregion

    #region Действия

    private void OnAccept(Entity<CitySurveillanceConsoleComponent> ent, ref CitySurveillanceAcceptMessage args)
    {
        if (!Check(ent, args.Actor) || !_registry.TryGetRegistry(out var registry))
            return;

        if (_cityCameras.AcceptAlert(registry, args.Alert, _registry.AuthorOf(args.Actor), ent.Owner))
            UpdateUi(ent, args.Actor);
    }

    private void OnTrack(Entity<CitySurveillanceConsoleComponent> ent, ref CitySurveillanceTrackMessage args)
    {
        if (args.Number is not { } number)
        {
            ent.Comp.Tracking = null;
            UpdateUi(ent, args.Actor);
            return;
        }

        if (!Check(ent, args.Actor) || !_registry.TryGetRegistry(out var registry))
            return;

        // за Консулом не следят
        if (!registry.Comp.Citizens.TryGetValue(number, out var record) || CityRegistrySystem.IsOutsideLoyalty(registry, record))
        {
            Deny(ent, args.Actor, "dystopia-surveillance-track-failed");
            return;
        }

        ent.Comp.Tracking = number;
        Follow(ent);
        UpdateUi(ent, args.Actor);
    }

    private void OnKeyword(Entity<CitySurveillanceConsoleComponent> ent, ref CitySurveillanceKeywordMessage args)
    {
        if (!Check(ent, args.Actor) || !_registry.TryGetRegistry(out var registry))
            return;

        if (_cityCameras.SetKeyword(registry, args.Word, args.Add))
            UpdateUi(ent, args.Actor);
        else if (args.Add)
            Deny(ent, args.Actor, "dystopia-surveillance-keyword-failed");
    }

    private void OnVoice(Entity<CitySurveillanceConsoleComponent> ent, ref CitySurveillanceVoiceMessage args)
    {
        if (!Check(ent, args.Actor))
            return;

        if (_timing.CurTime < ent.Comp.NextVoice)
            return;

        var text = args.Text.Trim();
        if (text.Length == 0)
            return;
        if (text.Length > ent.Comp.MaxVoiceLength)
            text = text[..ent.Comp.MaxVoiceLength];

        if (ent.Comp.Camera is not { } camera ||
            !_cityCameras.Speak(camera, text, args.Personal, _registry.AuthorOf(args.Actor)))
        {
            Deny(ent, args.Actor, "dystopia-surveillance-voice-failed");
            return;
        }

        ent.Comp.NextVoice = _timing.CurTime + ent.Comp.VoiceCooldown;
        UpdateUi(ent, args.Actor);
    }

    #endregion

    #region Слежка

    /// <summary>Ведомый сейчас в кадре другой камеры — картинка переходит на неё.</summary>
    private void Follow(Entity<CitySurveillanceConsoleComponent> ent)
    {
        if (ent.Comp.Tracking is not { } number ||
            !_registry.TryGetRegistry(out var registry) ||
            !registry.Comp.Citizens.TryGetValue(number, out var record) ||
            CurrentSighting(registry, record) is not { } current ||
            !TryGetEntity(current.CameraEntity, out var camera) ||
            camera == ent.Comp.Camera)
        {
            return;
        }

        SetCamera(ent, camera);
    }

    /// <summary>Отметка камеры, которая видит гражданина прямо сейчас (null — сейчас не видит ни одна).</summary>
    private CitySighting? CurrentSighting(Entity<CityRegistryComponent> registry, CityCitizenRecord record)
    {
        if (record.Sightings.Count == 0)
            return null;

        var latest = record.Sightings.MaxBy(s => s.LastTime)!;
        var fresh = TimeSpan.FromSeconds(registry.Comp.ScanInterval * 2 + 1);
        return _ticker.RoundDuration() - latest.LastTime <= fresh ? latest : null;
    }

    #endregion

    #region Состояние

    private void UpdateUi(Entity<CitySurveillanceConsoleComponent> ent, EntityUid? actor)
    {
        if (actor != null)
            _lastActor[ent.Owner] = actor.Value;

        var user = actor ?? _lastActor.GetValueOrDefault(ent.Owner);
        var canUse = user.IsValid() && Exists(user) && CanUse(ent, user);

        var cameras = new List<CitySurveillanceCamera>();
        var speech = new List<CitySpeechLine>();
        var alerts = new List<CityAlert>();
        var patrols = new List<CityPatrolEntry>();
        var keywords = new List<string>();
        CityTrackInfo? track = null;

        var map = Transform(ent).MapID;
        var masks = new List<(EntityUid Uid, CityCameraComponent City, SurveillanceCameraComponent Camera)>();
        var query = EntityQueryEnumerator<CityCameraComponent, SurveillanceCameraComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var city, out var camera, out var xform))
        {
            if (xform.MapID != map)
                continue;

            if (city.Mask)
            {
                masks.Add((uid, city, camera));
                // в списке — только надетые маски (запасные в шкафу не нужны)
                if (city.Wearer == null)
                    continue;
            }

            cameras.Add(_cityCameras.Describe((uid, camera), city));
        }

        cameras = cameras
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();

        if (_registry.TryGetRegistry(out var registry))
        {
            if (ent.Comp.Camera is { } current)
                speech = _cityCameras.GetSpeech(registry, current);

            alerts = Enumerable.Reverse(registry.Comp.Alerts).ToList();
            keywords = new List<string>(registry.Comp.Keywords);
            patrols = Patrols(ent, registry, masks);

            if (ent.Comp.Tracking is { } number && registry.Comp.Citizens.TryGetValue(number, out var record))
            {
                var since = _ticker.RoundDuration() - ent.Comp.TrackWindow;
                track = new CityTrackInfo
                {
                    Number = record.Number,
                    Name = record.Name,
                    Status = record.Status,
                    Current = CurrentSighting(registry, record)?.CameraEntity,
                    Path = record.Sightings.Where(s => s.LastTime >= since).OrderBy(s => s.Time).ToList(),
                };
            }
        }

        NetEntity? shownNet = null;
        if (ent.Comp.Camera is { } shown && !TerminatingOrDeleted(shown))
            shownNet = GetNetEntity(shown);

        _ui.SetUiState(ent.Owner, CitySurveillanceUiKey.Key, new CitySurveillanceUiState(
            cameras,
            shownNet,
            speech,
            alerts,
            track,
            ent.Comp.Tracking,
            patrols,
            keywords,
            canUse));
    }

    /// <summary>Стражники служб патруля и их маски.</summary>
    private List<CityPatrolEntry> Patrols(
        Entity<CitySurveillanceConsoleComponent> ent,
        Entity<CityRegistryComponent> registry,
        List<(EntityUid Uid, CityCameraComponent City, SurveillanceCameraComponent Camera)> masks)
    {
        var now = _ticker.RoundDuration();
        var list = new List<CityPatrolEntry>();
        foreach (var record in registry.Comp.Citizens.Values)
        {
            if (record.Department is not { } department || !ent.Comp.PatrolDepartments.Contains(department))
                continue;

            var entry = new CityPatrolEntry
            {
                Number = record.Number,
                JobTitle = record.Job is { } job && _prototype.TryIndex(job, out var proto) ? proto.LocalizedName : "—",
                Mask = CityMaskState.Off,
                OnShift = record.Owner is { } body && !TerminatingOrDeleted(body) && HasComp<ActorComponent>(body),
            };

            // маска на лице сейчас, иначе — последняя маска, которую он носил
            var index = -1;
            if (record.Owner is { } owner && _inventory.TryGetSlotEntity(owner, "mask", out var worn))
            {
                var wornMask = worn.Value;
                index = masks.FindIndex(m => m.Uid == wornMask && m.City.Wearer == owner);
            }
            if (index < 0)
                index = masks.FindIndex(m => m.City.LastWearerNumber == record.Number);

            if (index >= 0)
            {
                var (maskUid, maskCity, maskCamera) = masks[index];
                if (maskCity.Wearer != null)
                {
                    entry.Mask = maskCamera.Active ? CityMaskState.Up
                        : TryComp<MaskComponent>(maskUid, out var maskComp) && maskComp.IsToggled ? CityMaskState.Lowered
                        : CityMaskState.Off;
                }

                entry.Place = maskCity.LastPlace;
                if (maskCamera.Active)
                    entry.Camera = GetNetEntity(maskUid);
                else if (maskCity.LastActiveAt is { } last)
                    entry.DarkFor = now - last;
            }

            list.Add(entry);
        }

        return list
            .OrderBy(p => p.OnShift ? 0 : 1)
            .ThenBy(p => p.Mask)
            .ThenBy(p => p.Number)
            .ToList();
    }

    #endregion
}
