// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: городские камеры Реестра. Раз в пару секунд каждая работающая камера (настенная или надетая маска Стражи)
// осматривает круг вокруг себя (сквозь стены не видит) и отмечает людей:
//  • ID-карта в руке или в слоте ID (в том числе в КПК) — отметка в маршрут владельца карты;
//  • открытое лицо гражданина — отметка в его маршрут;
//  • лицо скрыто и карты нет — запись в «Неопознанные» (камера закрытой зоны поднимает тревогу).
// Замеченный разыскиваемый или поражённый в правах — тревога на канал Стражи. Маска Стражи работает камерой, только
// пока надета и не приспущена; в консоли камер она видна как «маска Стражи · №…», носитель ничего не замечает.

using System.Diagnostics.CodeAnalysis;
using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Content.Server.SurveillanceCamera;
using Content.Shared._Dystopia.Economy;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Radio;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityCameraSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;
    [Dependency] private SurveillanceCameraSystem _cameras = default!;
    [Dependency] private SurveillanceCameraMapSystem _cameraMap = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private CityRegistrySystem _registry = default!;

    private float _accumulator;

    private readonly HashSet<Entity<IdentityComponent>> _targets = new();

    /// <summary>Кто стоит за записью «Неопознанных» (знает только сервер).</summary>
    private readonly Dictionary<int, EntityUid> _unidentifiedBodies = new();

    /// <summary>Следующая тревога о гражданине (по номеру) и о неопознанном (по телу).</summary>
    private readonly Dictionary<int, TimeSpan> _citizenAlerts = new();
    private readonly Dictionary<EntityUid, TimeSpan> _strangerAlerts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityCameraComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CityCameraComponent, TransformSpeakerNameEvent>(OnSpeakerName);
        SubscribeLocalEvent<CityCameraComponent, RefreshNameModifiersEvent>(OnRefreshName);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnMapInit(Entity<CityCameraComponent> ent, ref MapInitEvent args)
    {
        // маска включается, только когда её наденут
        if (ent.Comp.Mask)
            UpdateMask(ent);
    }

    /// <summary>Тревоги камер звучат на канале Стражи от имени «Камеры Реестра», а не от имени маски или камеры.</summary>
    private void OnSpeakerName(Entity<CityCameraComponent> ent, ref TransformSpeakerNameEvent args)
    {
        args.VoiceName = Loc.GetString("dystopia-camera-speaker");
    }

    /// <summary>Надетая маска в консоли камер: «маска Стражи · №102628».</summary>
    private void OnRefreshName(Entity<CityCameraComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (ent.Comp.Mask && ent.Comp.WearerNumber is { } number)
            args.AddModifier(ent.Comp.MaskName, 0, ("number", number.ToString()));
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _unidentifiedBodies.Clear();
        _citizenAlerts.Clear();
        _strangerAlerts.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_registry.TryGetRegistry(out var registry))
            return;

        _accumulator += frameTime;
        if (_accumulator < Math.Max(0.5f, registry.Comp.ScanInterval))
            return;
        _accumulator = 0f;

        var now = _ticker.RoundDuration();
        PurgeUnidentified(registry, now);

        var query = EntityQueryEnumerator<SurveillanceCameraComponent>();
        while (query.MoveNext(out var uid, out var camera))
        {
            TryComp<CityCameraComponent>(uid, out var city);
            if (city == null && !registry.Comp.ScanAllCameras)
                continue;

            if (city is { Mask: true })
            {
                UpdateMask((uid, city), camera);
                // в консоли камер маска движется вместе с носителем; снятая (запасная в шкафу) на карте не видна
                if (camera.Active)
                    _cameraMap.UpdateCameraMarker((uid, camera));
                _cameraMap.SetCameraVisibility(uid, camera.Active);
            }

            if (!camera.Active)
                continue;

            Scan(registry, (uid, camera), city, now);
        }
    }

    #region Маска Стражи

    /// <summary>Маска — камера, только пока надета на лицо и не приспущена.</summary>
    private void UpdateMask(Entity<CityCameraComponent> ent, SurveillanceCameraComponent? camera = null)
    {
        if (!Resolve(ent, ref camera, false))
            return;

        EntityUid? wearer = null;
        if (TryComp<ClothingComponent>(ent, out var clothing) &&
            clothing.InSlotFlag is { } slot && (slot & SlotFlags.MASK) != 0 &&
            _container.TryGetContainingContainer((ent.Owner, null, null), out var container))
        {
            wearer = container.Owner;
        }

        var lowered = TryComp<MaskComponent>(ent, out var mask) && mask.IsToggled;
        var active = wearer != null && !lowered;

        if (ent.Comp.Wearer != wearer)
        {
            ent.Comp.Wearer = wearer;
            ent.Comp.WearerNumber = wearer is { } body ? _registry.NumberOf(body) : null;
            _nameModifier.RefreshNameModifiers(ent.Owner);
        }

        if (camera.Active != active)
            _cameras.SetActive(ent, active, camera);
    }

    #endregion

    #region Осмотр

    private void Scan(
        Entity<CityRegistryComponent> registry,
        Entity<SurveillanceCameraComponent> camera,
        CityCameraComponent? city,
        TimeSpan now)
    {
        var origin = _transform.GetMapCoordinates(camera);
        var range = city?.Range ?? registry.Comp.DefaultCameraRange;
        var wearer = city?.Wearer;

        _targets.Clear();
        _lookup.GetEntitiesInRange(origin, range, _targets, LookupFlags.Uncontained);

        foreach (var target in _targets)
        {
            var uid = target.Owner;
            if (uid == wearer || uid == camera.Owner)
                continue;

            if (!_examine.InRangeUnOccluded(origin, _transform.GetMapCoordinates(uid), range, null))
                continue;

            // лицо — отметка тому, чьё это лицо; карта — владельцу карты (даже если её носит другой)
            var face = FaceVisible(uid);
            int? byFace = null;
            if (face && _registry.NumberOf(uid) is { } faceNumber &&
                registry.Comp.Citizens.ContainsKey(faceNumber))
            {
                byFace = faceNumber;
                Sight(registry, camera, city, faceNumber, CitySightingMethod.Face, now);
            }

            if (TryGetCardNumber(registry, uid, out var cardNumber))
            {
                if (cardNumber != byFace)
                    Sight(registry, camera, city, cardNumber.Value, CitySightingMethod.Card, now);
                continue;
            }

            if (!face)
                SightStranger(registry, camera, city, uid, now);
        }
    }

    /// <summary>Видно ли лицо (ванильная проверка опознания: маска, шлем, капюшон его прячут).</summary>
    private bool FaceVisible(EntityUid uid)
    {
        var ev = new SeeIdentityAttemptEvent();
        RaiseLocalEvent(uid, ev);
        return !ev.Cancelled && ev.NameOverride == null;
    }

    /// <summary>Номер гражданина по ID-карте в любой руке или в слоте ID (в том числе в КПК).</summary>
    private bool TryGetCardNumber(Entity<CityRegistryComponent> registry, EntityUid uid, [NotNullWhen(true)] out int? number)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (CardNumber(registry, held) is { } found)
            {
                number = found;
                return true;
            }
        }

        if (_inventory.TryGetSlotEntity(uid, "id", out var slot) && CardNumber(registry, slot.Value) is { } inSlot)
        {
            number = inSlot;
            return true;
        }

        number = null;
        return false;
    }

    /// <summary>Чья карта: по метке Реестра (узнаёт и аннулированную), иначе по привязке к счёту.</summary>
    private int? CardNumber(Entity<CityRegistryComponent> registry, EntityUid item)
    {
        if (!_idCard.TryGetIdCard(item, out var card))
            return null;

        int? number = TryComp<CityIdCardComponent>(card, out var cityCard) ? cityCard.Number
            : CompOrNull<CityBankCardComponent>(card)?.AccountId;
        return number is { } found && registry.Comp.Citizens.ContainsKey(found) ? found : null;
    }

    #endregion

    #region Маршрут

    /// <summary>
    /// Отметка в маршрут. Пока человек не уходит из кадра дольше <see cref="CityRegistryComponent.SightingCooldown"/>,
    /// отметка той же камеры продлевается («12:03–12:15»), а не повторяется.
    /// </summary>
    private void Sight(
        Entity<CityRegistryComponent> registry,
        Entity<SurveillanceCameraComponent> camera,
        CityCameraComponent? city,
        int number,
        CitySightingMethod method,
        TimeSpan now)
    {
        if (!registry.Comp.Citizens.TryGetValue(number, out var record))
            return;

        var cameraNet = GetNetEntity(camera.Owner);
        var cooldown = registry.Comp.SightingCooldown;
        var sightings = record.Sightings;

        // та же камера видела его только что — продлеваем её отметку (и когда две камеры смотрят на одно место,
        // каждая продлевает свою, а не плодит новые)
        for (var i = sightings.Count - 1; i >= 0; i--)
        {
            var sighting = sightings[i];
            if (sighting.CameraEntity != cameraNet || now - sighting.LastTime > cooldown)
                continue;

            sighting.LastTime = now;
            return;
        }

        var added = new CitySighting
        {
            Time = now,
            LastTime = now,
            Place = PlaceOf(camera),
            Camera = CameraName(camera, city),
            Method = method,
            Mask = city?.Mask == true,
            CameraEntity = cameraNet,
        };
        AddSighting(registry, record, added);
        Alert(registry, record, camera, city, added.Place, now);
    }

    public void AddSighting(Entity<CityRegistryComponent> registry, CityCitizenRecord record, CitySighting sighting)
    {
        record.Sightings.Add(sighting);
        var overflow = record.Sightings.Count - Math.Max(1, registry.Comp.RouteLength);
        if (overflow > 0)
            record.Sightings.RemoveRange(0, overflow);
    }

    #endregion

    #region Неопознанные

    private void SightStranger(
        Entity<CityRegistryComponent> registry,
        Entity<SurveillanceCameraComponent> camera,
        CityCameraComponent? city,
        EntityUid body,
        TimeSpan now)
    {
        var cameraNet = GetNetEntity(camera.Owner);
        var restricted = city?.Restricted == true;
        var list = registry.Comp.Unidentified;

        // тот же человек у той же камеры — продлеваем запись
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var entry = list[i];
            if (entry.CameraEntity != cameraNet ||
                !_unidentifiedBodies.TryGetValue(entry.Id, out var known) || known != body)
            {
                continue;
            }

            if (now - entry.LastTime <= registry.Comp.SightingCooldown)
            {
                entry.LastTime = now;
                return;
            }

            break;
        }

        var sighting = new CityUnidentifiedSighting
        {
            Id = registry.Comp.NextUnidentified++,
            Description = Identity.Name(body, EntityManager),
            Time = now,
            LastTime = now,
            Place = PlaceOf(camera),
            Camera = CameraName(camera, city),
            Mask = city?.Mask == true,
            Restricted = restricted,
            CameraEntity = cameraNet,
        };

        list.Add(sighting);
        _unidentifiedBodies[sighting.Id] = body;

        var overflow = list.Count - Math.Max(1, registry.Comp.UnidentifiedLimit);
        for (var i = 0; i < overflow; i++)
        {
            _unidentifiedBodies.Remove(list[0].Id);
            list.RemoveAt(0);
        }

        if (restricted)
            AlertStranger(registry, sighting, camera, body, now);
    }

    private void PurgeUnidentified(Entity<CityRegistryComponent> registry, TimeSpan now)
    {
        var lifetime = registry.Comp.UnidentifiedLifetime;
        registry.Comp.Unidentified.RemoveAll(entry =>
        {
            if (now - entry.LastTime <= lifetime)
                return false;

            _unidentifiedBodies.Remove(entry.Id);
            return true;
        });
    }

    /// <summary>
    /// «Опознать»: отметку из «Неопознанных» — в маршрут гражданина (и запись в журнал дела). Сервер не проверяет,
    /// тот ли это человек: опознание — решение Стражи.
    /// </summary>
    public bool Identify(Entity<CityRegistryComponent> registry, int sightingId, CityCitizenRecord record, string author)
    {
        var list = registry.Comp.Unidentified;
        var index = list.FindIndex(e => e.Id == sightingId);
        if (index < 0)
            return false;

        var entry = list[index];
        list.RemoveAt(index);
        _unidentifiedBodies.Remove(entry.Id);

        var sighting = new CitySighting
        {
            Time = entry.Time,
            LastTime = entry.LastTime,
            Place = entry.Place,
            Camera = entry.Camera,
            Method = CitySightingMethod.Manual,
            Mask = entry.Mask,
            CameraEntity = entry.CameraEntity,
        };

        // маршрут — по времени: опознанная отметка встаёт на своё место
        var position = record.Sightings.FindLastIndex(s => s.LastTime <= sighting.LastTime) + 1;
        record.Sightings.Insert(position, sighting);
        var overflow = record.Sightings.Count - Math.Max(1, registry.Comp.RouteLength);
        if (overflow > 0)
            record.Sightings.RemoveRange(0, overflow);

        _registry.AddEntry(registry, record, CityRegistryEntryKind.Service,
            Loc.GetString("dystopia-registry-entry-identified"),
            author,
            Loc.GetString("dystopia-registry-entry-identified-details",
                ("camera", entry.Camera), ("place", entry.Place), ("time", Clock(entry.LastTime))));
        return true;
    }

    #endregion

    #region Тревоги

    /// <summary>
    /// Разыскиваемый или поражённый в правах попал в кадр — тревога на канал Стражи. Звучит при новой отметке (не
    /// пока он стоит в кадре) и о том же человеке — не чаще раза в <see cref="CityRegistryComponent.AlertCooldown"/>.
    /// </summary>
    private void Alert(
        Entity<CityRegistryComponent> registry,
        CityCitizenRecord record,
        EntityUid camera,
        CityCameraComponent? city,
        string place,
        TimeSpan now)
    {
        string key;
        switch (record.Status)
        {
            case CityCitizenStatus.Wanted:
                key = "dystopia-camera-alert-wanted";
                break;
            case CityCitizenStatus.Disenfranchised:
                key = "dystopia-camera-alert-outlaw";
                break;
            default:
                return;
        }

        if (_citizenAlerts.TryGetValue(record.Number, out var next) && now < next)
            return;
        _citizenAlerts[record.Number] = now + registry.Comp.AlertCooldown;

        Broadcast(registry, camera, Loc.GetString(key,
            ("number", record.Number.ToString()),
            ("name", FormattedMessage.EscapeText(record.Name)),
            ("camera", FormattedMessage.EscapeText(CameraName(camera, city))),
            ("place", FormattedMessage.EscapeText(place)),
            ("reason", record.StatusReason.Length > 0 ? FormattedMessage.EscapeText(record.StatusReason) : "—")));
    }

    private void AlertStranger(
        Entity<CityRegistryComponent> registry,
        CityUnidentifiedSighting sighting,
        EntityUid camera,
        EntityUid body,
        TimeSpan now)
    {
        if (_strangerAlerts.TryGetValue(body, out var next) && now < next)
            return;
        _strangerAlerts[body] = now + registry.Comp.AlertCooldown;

        Broadcast(registry, camera, Loc.GetString("dystopia-camera-alert-restricted",
            ("description", FormattedMessage.EscapeText(sighting.Description)),
            ("camera", FormattedMessage.EscapeText(sighting.Camera)),
            ("place", FormattedMessage.EscapeText(sighting.Place))));
    }

    private void Broadcast(Entity<CityRegistryComponent> registry, EntityUid camera, string message)
    {
        if (!_prototype.TryIndex<RadioChannelPrototype>(registry.Comp.AlertChannel, out var channel))
            return;

        _radio.SendRadioMessage(camera, message, channel, camera, escapeMarkup: false);
    }

    #endregion

    #region Имена

    /// <summary>Ближайший ориентир навигационной карты.</summary>
    private string PlaceOf(EntityUid camera)
    {
        return _navMap.GetNearestBeaconString(camera, onlyName: true);
    }

    public string CameraName(EntityUid camera, CityCameraComponent? city = null)
    {
        if (!TryComp<SurveillanceCameraComponent>(camera, out var comp))
            return Name(camera);

        if (Resolve(camera, ref city, false) && city.Mask)
            return Name(camera);

        return comp.UseEntityNameAsCameraId ? Name(camera) : comp.CameraId;
    }

    public static string Clock(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}";
    }

    #endregion
}
