// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: консоль социоучёта (терминал Реестра граждан). Показ списка и личного дела, правка дела: статус,
// нарушения по Своду законов (класс по статье, повторное нарушение открывает класс выше, лояльность — в пределах
// класса, тяжкие классы отстраняют и обнуляют лояльность), поощрения, особые приметы, печать ориентировки.
// Маршрут по городским камерам, «Неопознанные» (опознать — перенести отметку в маршрут гражданина) и «Смотреть» —
// картинка с камеры, которая отметила человека, прямо в окне консоли.
// Доступ к консоли — её AccessReader; права на правку — по профессии того, чья ID-карта у пользователя.

using System.Linq;
using Content.Server._Dystopia.Economy;
using Content.Server._Dystopia.Laws;
using Content.Server.Popups;
using Content.Server.SurveillanceCamera;
using Content.Shared._Dystopia.Laws;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityRegistryConsoleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private CityRegistrySystem _registry = default!;
    [Dependency] private CityLawsSystem _laws = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private CityCameraSystem _cityCameras = default!;
    [Dependency] private SurveillanceCameraSystem _cameras = default!;

    /// <summary>Сколько последних банковских операций показывать в деле.</summary>
    private const int BankHistoryShown = 60;

    private float _refreshAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityRegistryConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CityRegistryConsoleComponent, CityRegistrySelectMessage>(OnSelect);
        SubscribeLocalEvent<CityRegistryConsoleComponent, CityRegistrySetStatusMessage>(OnSetStatus);
        SubscribeLocalEvent<CityRegistryConsoleComponent, CityRegistryViolationMessage>(OnViolation);
        SubscribeLocalEvent<CityRegistryConsoleComponent, CityRegistryRewardMessage>(OnReward);
        SubscribeLocalEvent<CityRegistryConsoleComponent, CityRegistryNotesMessage>(OnNotes);
        SubscribeLocalEvent<CityRegistryConsoleComponent, CityRegistryPrintMessage>(OnPrint);
        SubscribeLocalEvent<CityRegistryConsoleComponent, CityRegistryIdentifyMessage>(OnIdentify);
        SubscribeLocalEvent<CityRegistryConsoleComponent, CityRegistryWatchMessage>(OnWatch);
        SubscribeLocalEvent<CityRegistryConsoleComponent, BoundUIClosedEvent>(OnClosed);
        SubscribeLocalEvent<CityRegistryConsoleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SurveillanceCameraDeactivateEvent>(OnCameraDeactivated);
    }

    private void OnShutdown(Entity<CityRegistryConsoleComponent> ent, ref ComponentShutdown args)
    {
        _lastActor.Remove(ent.Owner);
        foreach (var (actor, watch) in _watching.ToList())
        {
            if (watch.Console == ent.Owner)
                StopWatching(actor, false);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // дела меняются и с других консолей, а баланс — от банка: открытые консоли обновляются раз в пару секунд
        _refreshAccumulator += frameTime;
        if (_refreshAccumulator < 2f)
            return;
        _refreshAccumulator = 0f;

        UpdateAll();
    }

    private void UpdateAll()
    {
        var query = EntityQueryEnumerator<CityRegistryConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, CityRegistryConsoleUiKey.Key))
                UpdateUi((uid, comp), null);
        }
    }

    private void OnOpened(Entity<CityRegistryConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor);
    }

    #region Права

    private readonly record struct Rights(bool Edit, bool Reward, bool Supreme);

    private Rights RightsOf(Entity<CityRegistryConsoleComponent> ent, EntityUid user)
    {
        if (!_registry.TryGetHolderRecord(user, out var record) || record.Job is not { } job)
            return default;

        return new Rights(
            ent.Comp.EditJobs.Contains(job),
            ent.Comp.RewardJobs.Contains(job),
            ent.Comp.SupremeJobs.Contains(job));
    }

    /// <summary>Пропускает ли консоль пользователя и может ли он править. Нет — всплывашка «отказано» и звук.</summary>
    private bool Check(Entity<CityRegistryConsoleComponent> ent, EntityUid user, Func<Rights, bool> need)
    {
        if (_access.IsAllowed(user, ent.Owner) && need(RightsOf(ent, user)))
            return true;

        Deny(ent, user, "dystopia-registry-console-denied");
        return false;
    }

    private void Deny(Entity<CityRegistryConsoleComponent> ent, EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), ent.Owner, user);
        _audio.PlayPvs(ent.Comp.DeniedSound, ent.Owner);
    }

    private bool TryGetCitizen(int number, out Entity<CityRegistryComponent> registry, out CityCitizenRecord record)
    {
        record = default!;
        if (!_registry.TryGetRegistry(out registry) || !registry.Comp.Citizens.TryGetValue(number, out var found))
            return false;

        record = found;
        return true;
    }

    #endregion

    #region Действия

    private void OnSelect(Entity<CityRegistryConsoleComponent> ent, ref CityRegistrySelectMessage args)
    {
        ent.Comp.Selected = args.Number;
        UpdateUi(ent, args.Actor);
    }

    private void OnSetStatus(Entity<CityRegistryConsoleComponent> ent, ref CityRegistrySetStatusMessage args)
    {
        var status = args.Status;
        if (!TryGetCitizen(args.Number, out var registry, out var record))
            return;

        // поражение в правах и его снятие — только высшая власть
        var supreme = status == CityCitizenStatus.Disenfranchised || record.Status == CityCitizenStatus.Disenfranchised;
        if (!Check(ent, args.Actor, r => r.Edit && (!supreme || r.Supreme)))
            return;

        if (status == CityCitizenStatus.Wanted && string.IsNullOrWhiteSpace(args.Reason))
        {
            Deny(ent, args.Actor, "dystopia-registry-console-reason-required");
            return;
        }

        if (!_registry.SetStatus(registry, record, status, args.Reason, _registry.AuthorOf(args.Actor)))
            return;

        UpdateUi(ent, args.Actor);
    }

    private void OnViolation(Entity<CityRegistryConsoleComponent> ent, ref CityRegistryViolationMessage args)
    {
        if (!Check(ent, args.Actor, r => r.Edit) ||
            !TryGetCitizen(args.Number, out var registry, out var record) ||
            CityRegistrySystem.IsOutsideLoyalty(registry, record) ||
            !_laws.TryGetLaws(out var laws))
        {
            return;
        }

        var lawId = args.LawId;
        var law = laws.Comp.Laws.FirstOrDefault(l => l.Id == lawId);
        if (law == null)
            return;

        var repeat = CityRegistryRules.IsRepeat(record.Journal, law.Id);
        var cls = args.SanctionClass;
        if (!CityRegistryRules.AllowedClasses(laws.Comp.Sanctions, LawEntry(law), repeat).Contains(cls))
            return;

        var sanction = laws.Comp.Sanctions.First(s => s.Class == cls);
        int delta;
        if (sanction.Dismissal)
        {
            delta = -(record.Loyalty ?? 0);
            record.Dismissed = true;
        }
        else
        {
            var min = Math.Min(sanction.LoyaltyMin, sanction.LoyaltyMax);
            var max = Math.Max(sanction.LoyaltyMin, sanction.LoyaltyMax);
            delta = -Math.Clamp(args.Loyalty, min, max);
        }

        var details = Loc.GetString(repeat ? "dystopia-registry-violation-details-repeat" : "dystopia-registry-violation-details",
            ("class", cls), ("legal", sanction.Legal));
        if (sanction.Dismissal)
            details += " " + Loc.GetString("dystopia-registry-violation-dismissed");
        if (!string.IsNullOrWhiteSpace(args.Details))
            details += " " + args.Details.Trim();

        _registry.AddEntry(registry, record, CityRegistryEntryKind.Violation,
            Loc.GetString("dystopia-registry-entry-violation", ("number", law.Number), ("title", law.Title)),
            _registry.AuthorOf(args.Actor), details, law.Id, law.Number, cls, delta);

        UpdateUi(ent, args.Actor);
    }

    private void OnReward(Entity<CityRegistryConsoleComponent> ent, ref CityRegistryRewardMessage args)
    {
        if (!Check(ent, args.Actor, r => r.Reward) ||
            !TryGetCitizen(args.Number, out var registry, out var record) ||
            CityRegistrySystem.IsOutsideLoyalty(registry, record))
        {
            return;
        }

        var amount = Math.Clamp(args.Loyalty, 1, Math.Max(1, ent.Comp.MaxReward));
        var text = string.IsNullOrWhiteSpace(args.Text) ? Loc.GetString("dystopia-registry-entry-reward") : args.Text.Trim();
        if (text.Length > CityRegistrySystem.MaxReasonLength)
            text = text[..CityRegistrySystem.MaxReasonLength];

        _registry.AddEntry(registry, record, CityRegistryEntryKind.Reward, text, _registry.AuthorOf(args.Actor),
            loyaltyDelta: amount);

        UpdateUi(ent, args.Actor);
    }

    private void OnNotes(Entity<CityRegistryConsoleComponent> ent, ref CityRegistryNotesMessage args)
    {
        if (!Check(ent, args.Actor, r => r.Edit) ||
            !TryGetCitizen(args.Number, out var registry, out var record) ||
            CityRegistrySystem.IsOutsideLoyalty(registry, record))
        {
            return;
        }

        _registry.SetNotes(registry, record, args.Notes, _registry.AuthorOf(args.Actor));
        UpdateUi(ent, args.Actor);
    }

    private void OnIdentify(Entity<CityRegistryConsoleComponent> ent, ref CityRegistryIdentifyMessage args)
    {
        if (!Check(ent, args.Actor, r => r.Edit))
            return;

        if (!TryGetCitizen(args.Number, out var registry, out var record) ||
            CityRegistrySystem.IsOutsideLoyalty(registry, record) ||
            !_cityCameras.Identify(registry, args.Sighting, record, _registry.AuthorOf(args.Actor)))
        {
            Deny(ent, args.Actor, "dystopia-registry-console-identify-failed");
            return;
        }

        UpdateUi(ent, args.Actor);
    }

    #region Смотреть через камеру

    private readonly record struct Watch(EntityUid Console, EntityUid Camera);

    /// <summary>Кто сейчас смотрит через камеру с консоли социоучёта.</summary>
    private readonly Dictionary<EntityUid, Watch> _watching = new();

    private void OnWatch(Entity<CityRegistryConsoleComponent> ent, ref CityRegistryWatchMessage args)
    {
        if (args.Camera is not { } net)
        {
            StopWatching(args.Actor, true);
            return;
        }

        if (!Check(ent, args.Actor, r => r.Edit))
            return;

        if (!TryGetEntity(net, out var camera) || !TryComp<SurveillanceCameraComponent>(camera, out var cameraComp) ||
            !cameraComp.Active)
        {
            Deny(ent, args.Actor, "dystopia-registry-console-camera-offline");
            StopWatching(args.Actor, true);
            return;
        }

        StopWatching(args.Actor, false);
        _cameras.AddActiveViewer(camera.Value, args.Actor, null, cameraComp);
        _watching[args.Actor] = new Watch(ent.Owner, camera.Value);

        _ui.ServerSendUiMessage(ent.Owner, CityRegistryConsoleUiKey.Key,
            new CityRegistryWatchingMessage(net, _cityCameras.CameraName(camera.Value)), args.Actor);
    }

    private void OnClosed(Entity<CityRegistryConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is CityRegistryConsoleUiKey.Key)
            StopWatching(args.Actor, false);
    }

    /// <summary>Камера выключилась (обесточена, сломана, маску сняли) — смотревшие через неё теряют картинку.</summary>
    private void OnCameraDeactivated(SurveillanceCameraDeactivateEvent args)
    {
        foreach (var (actor, watch) in _watching.ToList())
        {
            if (watch.Camera == args.Camera)
                StopWatching(actor, true);
        }
    }

    private void StopWatching(EntityUid actor, bool notify)
    {
        if (!_watching.Remove(actor, out var watch))
            return;

        if (!TerminatingOrDeleted(watch.Camera))
            _cameras.RemoveActiveViewer(watch.Camera, actor);

        if (notify && !TerminatingOrDeleted(watch.Console))
        {
            _ui.ServerSendUiMessage(watch.Console, CityRegistryConsoleUiKey.Key,
                new CityRegistryWatchingMessage(null, string.Empty), actor);
        }
    }

    #endregion

    private void OnPrint(Entity<CityRegistryConsoleComponent> ent, ref CityRegistryPrintMessage args)
    {
        if (!Check(ent, args.Actor, r => r.Edit) || !TryGetCitizen(args.Number, out var registry, out var record))
            return;

        if (record.Status != CityCitizenStatus.Wanted)
        {
            Deny(ent, args.Actor, "dystopia-registry-console-not-wanted");
            return;
        }

        if (_timing.CurTime < ent.Comp.NextPrint)
            return;
        ent.Comp.NextPrint = _timing.CurTime + ent.Comp.PrintCooldown;

        var poster = registry.Comp.NextPoster++;
        var job = record.Job is { } jobId && _prototype.TryIndex(jobId, out var jobProto) ? jobProto.LocalizedName : "—";
        var species = _prototype.TryIndex(record.Species, out var speciesProto) ? Loc.GetString(speciesProto.Name) : "—";
        var time = _ticker.RoundDuration();
        var content = Loc.GetString("dystopia-registry-poster",
            ("poster", poster.ToString()),
            ("number", record.Number.ToString()),
            ("name", FormattedMessage.EscapeText(record.Name)),
            ("job", job),
            ("age", record.Age.ToString()),
            ("species", species),
            ("notes", record.Notes.Length > 0 ? FormattedMessage.EscapeText(record.Notes) : "—"),
            ("reason", record.StatusReason.Length > 0 ? FormattedMessage.EscapeText(record.StatusReason) : "—"),
            ("time", $"{(int) time.TotalHours:00}:{time.Minutes:00}"),
            ("author", FormattedMessage.EscapeText(_registry.AuthorOf(args.Actor))));

        var paper = Spawn(ent.Comp.PosterPrototype, Transform(ent.Owner).Coordinates);
        _paper.SetContent(paper, content);
        // сразу заверяется печатью Стражи: заверенный лист ручкой не исправить
        if (TryComp<PaperComponent>(paper, out var paperComp))
        {
            _paper.TryStamp((paper, paperComp),
                new StampDisplayInfo
                {
                    StampedName = Loc.GetString(ent.Comp.PosterStampName),
                    StampedColor = ent.Comp.PosterStampColor,
                },
                ent.Comp.PosterStampState);
        }
        _meta.SetEntityName(paper, Loc.GetString("dystopia-registry-poster-name",
            ("poster", poster.ToString()), ("number", record.Number.ToString())));
        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
    }

    #endregion

    #region Состояние

    private static CityRegistryLawEntry LawEntry(CityLaw law)
    {
        return new CityRegistryLawEntry
        {
            Id = law.Id,
            Number = law.Number,
            Title = law.Title,
            Sanction = law.Sanction,
            Classes = new List<string>(law.Classes),
        };
    }

    /// <summary>Отправить состояние. actor — тот, для кого считаются права (null — последний пользователь).</summary>
    private void UpdateUi(Entity<CityRegistryConsoleComponent> ent, EntityUid? actor)
    {
        // Состояние у консоли одно на всех, кто в неё смотрит: кнопки правки — по правам последнего пользователя
        // (сервер всё равно проверяет права при каждом действии).
        if (actor != null)
            _lastActor[ent.Owner] = actor.Value;

        var user = actor ?? _lastActor.GetValueOrDefault(ent.Owner);
        var rights = user.IsValid() && Exists(user) ? RightsOf(ent, user) : default;

        var citizens = new List<CityRegistryListEntry>();
        var departments = new Dictionary<string, string>();
        CityRegistryDossier? dossier = null;
        var unidentified = new List<CityUnidentifiedSighting>();
        var maxReward = Math.Max(1, ent.Comp.MaxReward);

        if (_registry.TryGetRegistry(out var registry))
        {
            foreach (var record in registry.Comp.Citizens.Values)
            {
                string? department = null;
                if (record.Department is { } depId && _prototype.TryIndex(depId, out var depProto))
                {
                    department = depId.Id;
                    departments[depId.Id] = Loc.GetString(depProto.Name);
                }

                citizens.Add(new CityRegistryListEntry
                {
                    Number = record.Number,
                    Name = record.Name,
                    JobTitle = JobTitle(record),
                    Department = department,
                    Status = record.Status,
                    Loyalty = record.Loyalty,
                });
            }

            if (ent.Comp.Selected is { } selected && registry.Comp.Citizens.TryGetValue(selected, out var chosen))
                dossier = Dossier(registry, chosen);

            unidentified = Enumerable.Reverse(registry.Comp.Unidentified).ToList();
        }

        // вне учёта лояльности (Консул) — первым, дальше по службам и именам
        citizens = citizens
            .OrderBy(c => c.Loyalty == null ? 0 : 1)
            .ThenBy(c => c.Department == null ? string.Empty : departments[c.Department])
            .ThenBy(c => c.Name)
            .ToList();

        var laws = new List<CityRegistryLawEntry>();
        var sanctions = new List<CitySanctionClass>();
        if (_laws.TryGetLaws(out var lawsComp))
        {
            laws = _laws.GetSortedLaws(lawsComp.Comp).Select(LawEntry).ToList();
            sanctions = lawsComp.Comp.Sanctions.Select(s => s.Clone()).ToList();
        }

        var departmentList = departments
            .OrderBy(d => d.Value)
            .Select(d => new CityRegistryDepartmentEntry { Id = d.Key, Name = d.Value })
            .ToList();

        _ui.SetUiState(ent.Owner, CityRegistryConsoleUiKey.Key, new CityRegistryConsoleUiState(
            citizens, departmentList, dossier, laws, sanctions, unidentified, rights.Edit, rights.Reward, rights.Supreme, maxReward));
    }

    private readonly Dictionary<EntityUid, EntityUid> _lastActor = new();

    private string JobTitle(CityCitizenRecord record)
    {
        return record.Job is { } job && _prototype.TryIndex(job, out var proto) ? proto.LocalizedName : "—";
    }

    private CityRegistryDossier Dossier(Entity<CityRegistryComponent> registry, CityCitizenRecord record)
    {
        var outside = CityRegistrySystem.IsOutsideLoyalty(registry, record);
        var dossier = new CityRegistryDossier
        {
            Number = record.Number,
            Name = record.Name,
            Age = record.Age,
            Species = _prototype.TryIndex(record.Species, out var species) ? Loc.GetString(species.Name) : "—",
            Gender = record.Gender,
            JobTitle = JobTitle(record),
            Department = record.Department is { } dep && _prototype.TryIndex(dep, out var depProto)
                ? Loc.GetString(depProto.Name)
                : "—",
            Fingerprint = outside ? null : record.Fingerprint,
            Dna = outside ? null : record.Dna,
            JoinedAt = record.JoinedAt,
            Loyalty = record.Loyalty,
            LoyaltyNorm = record.LoyaltyNorm,
            MaxLoyalty = registry.Comp.MaxLoyalty,
            Status = record.Status,
            StatusReason = record.StatusReason,
            Dismissed = record.Dismissed,
            Notes = record.Notes,
            Journal = outside ? new() : new List<CityRegistryEntry>(record.Journal),
            Appearance = record.Appearance,
            Route = outside ? new() : new List<CitySighting>(record.Sightings),
        };

        if (!outside && _bank.TryGetBank(out var bank) && bank.Comp.Accounts.TryGetValue(record.Number, out var account))
        {
            dossier.Bank = new CityRegistryBankInfo
            {
                Account = account.Id,
                Balance = account.Balance,
                Debt = account.Debt,
                TaxRate = _bank.GetTaxRate(bank, account),
                Frozen = account.Frozen,
                History = Enumerable.Reverse(account.History).Take(BankHistoryShown).ToList(),
            };
        }

        return dossier;
    }

    #endregion
}
