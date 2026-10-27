// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: «Паспортный стол» (консоль Гражданской Инспекции). Список граждан, карты гражданина, аннулирование,
// выдача новой карты (печатается у консоли), восстановление в должности. Доступ к консоли — её AccessReader;
// права на действия — по профессии того, чья ID-карта у пользователя (отстранённый прав не имеет). Картами граждан
// вне учёта лояльности (Консула) распоряжается только высшая власть.

using System.Linq;
using Content.Server.Popups;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Access.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityPassportConsoleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CityRegistrySystem _registry = default!;

    private float _refreshAccumulator;
    private readonly Dictionary<EntityUid, EntityUid> _lastActor = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityPassportConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CityPassportConsoleComponent, CityPassportSelectMessage>(OnSelect);
        SubscribeLocalEvent<CityPassportConsoleComponent, CityPassportAnnulMessage>(OnAnnul);
        SubscribeLocalEvent<CityPassportConsoleComponent, CityPassportIssueMessage>(OnIssue);
        SubscribeLocalEvent<CityPassportConsoleComponent, CityPassportReinstateMessage>(OnReinstate);
        SubscribeLocalEvent<CityPassportConsoleComponent, ComponentShutdown>((uid, _, _) => _lastActor.Remove(uid));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // карты аннулируют и с консоли социоучёта (поражение в правах), уничтожают — обновляемся раз в пару секунд
        _refreshAccumulator += frameTime;
        if (_refreshAccumulator < 2f)
            return;
        _refreshAccumulator = 0f;

        var query = EntityQueryEnumerator<CityPassportConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, CityPassportConsoleUiKey.Key))
                UpdateUi((uid, comp), null);
        }
    }

    private void OnOpened(Entity<CityPassportConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor);
    }

    #region Права

    private readonly record struct Rights(bool Edit, bool Supreme);

    private Rights RightsOf(Entity<CityPassportConsoleComponent> ent, EntityUid user)
    {
        // админ-призрак может всё
        if (_registry.IsOverseer(user))
            return new Rights(true, true);

        if (!_registry.TryGetHolderRecord(user, out var record) || record.Job is not { } job || record.Dismissed)
            return default;

        return new Rights(ent.Comp.IssueJobs.Contains(job), ent.Comp.SupremeJobs.Contains(job));
    }

    /// <summary>Можно ли пользователю распоряжаться делом этого гражданина. Нет — отказ со звуком.</summary>
    private bool TryGetEditable(
        Entity<CityPassportConsoleComponent> ent,
        EntityUid user,
        int number,
        out Entity<CityRegistryComponent> registry,
        out CityCitizenRecord record)
    {
        record = default!;
        if (!_registry.TryGetRegistry(out registry) || !registry.Comp.Citizens.TryGetValue(number, out var found))
            return false;

        record = found;
        var rights = RightsOf(ent, user);
        if (_access.IsAllowed(user, ent.Owner) && rights.Edit &&
            (!CityRegistrySystem.IsOutsideLoyalty(registry, record) || rights.Supreme))
        {
            return true;
        }

        Deny(ent, user, "dystopia-passport-console-denied");
        return false;
    }

    private void Deny(Entity<CityPassportConsoleComponent> ent, EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), ent.Owner, user);
        _audio.PlayPvs(ent.Comp.DeniedSound, ent.Owner);
    }

    #endregion

    #region Действия

    private void OnSelect(Entity<CityPassportConsoleComponent> ent, ref CityPassportSelectMessage args)
    {
        ent.Comp.Selected = args.Number;
        UpdateUi(ent, args.Actor);
    }

    private void OnAnnul(Entity<CityPassportConsoleComponent> ent, ref CityPassportAnnulMessage args)
    {
        if (!TryGetEditable(ent, args.Actor, args.Number, out var registry, out var record))
            return;

        if (string.IsNullOrWhiteSpace(args.Reason))
        {
            Deny(ent, args.Actor, "dystopia-passport-console-reason-required");
            return;
        }

        if (_registry.AnnulCard(registry, record, args.Card, args.Reason, _registry.AuthorOf(args.Actor)))
            UpdateUi(ent, args.Actor);
    }

    private void OnIssue(Entity<CityPassportConsoleComponent> ent, ref CityPassportIssueMessage args)
    {
        if (!TryGetEditable(ent, args.Actor, args.Number, out var registry, out var record))
            return;

        if (_timing.CurTime < ent.Comp.NextPrint)
            return;
        ent.Comp.NextPrint = _timing.CurTime + ent.Comp.PrintCooldown;

        var card = _registry.IssueCard(registry, record, Transform(ent.Owner).Coordinates,
            _registry.AuthorOf(args.Actor), ent.Comp.FallbackCard);
        if (card == null)
        {
            Deny(ent, args.Actor, "dystopia-passport-console-issue-failed");
            return;
        }

        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
        UpdateUi(ent, args.Actor);
    }

    private void OnReinstate(Entity<CityPassportConsoleComponent> ent, ref CityPassportReinstateMessage args)
    {
        if (!TryGetEditable(ent, args.Actor, args.Number, out var registry, out var record))
            return;

        if (_registry.Reinstate(registry, record, _registry.AuthorOf(args.Actor)))
            UpdateUi(ent, args.Actor);
    }

    #endregion

    #region Состояние

    private void UpdateUi(Entity<CityPassportConsoleComponent> ent, EntityUid? actor)
    {
        // состояние у консоли одно на всех: кнопки — по правам последнего пользователя (сервер всё равно проверяет)
        if (actor != null)
            _lastActor[ent.Owner] = actor.Value;

        var user = actor ?? _lastActor.GetValueOrDefault(ent.Owner);
        var rights = user.IsValid() && Exists(user) ? RightsOf(ent, user) : default;

        var citizens = new List<CityPassportListEntry>();
        CityPassportDossier? dossier = null;

        if (_registry.TryGetRegistry(out var registry))
        {
            foreach (var record in registry.Comp.Citizens.Values)
            {
                var cards = _registry.CardEntries(record);
                citizens.Add(new CityPassportListEntry
                {
                    Number = record.Number,
                    Name = record.Name,
                    JobTitle = JobTitle(record),
                    Status = record.Status,
                    Dismissed = record.Dismissed,
                    Outside = CityRegistrySystem.IsOutsideLoyalty(registry, record),
                    LiveCards = cards.Count(c => !c.Annulled && !c.Destroyed),
                });
            }

            if (ent.Comp.Selected is { } selected && registry.Comp.Citizens.TryGetValue(selected, out var chosen))
            {
                dossier = new CityPassportDossier
                {
                    Number = chosen.Number,
                    Name = chosen.Name,
                    JobTitle = JobTitle(chosen),
                    Status = chosen.Status,
                    Dismissed = chosen.Dismissed,
                    Outside = CityRegistrySystem.IsOutsideLoyalty(registry, chosen),
                    Appearance = chosen.Appearance,
                    Cards = _registry.CardEntries(chosen),
                };
            }
        }

        citizens = citizens
            .OrderBy(c => c.Outside ? 0 : 1)
            .ThenBy(c => c.Name)
            .ToList();

        _ui.SetUiState(ent.Owner, CityPassportConsoleUiKey.Key,
            new CityPassportConsoleUiState(citizens, dossier, rights.Edit, rights.Supreme));
    }

    private string JobTitle(CityCitizenRecord record)
    {
        return record.Job is { } job && _prototype.TryIndex(job, out var proto) ? proto.LocalizedName : "—";
    }

    #endregion
}
