// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: Реестр граждан Города (социоучёт). Гражданину, вошедшему в раунд через лобби на профессию Города
// (в начале смены или поздним входом), Реестр выдаёт номер и заводит личное дело. Банк открывает счёт с тем же
// номером — поэтому номер гражданина и номер счёта совпадают. Культисты из логова, админ-спавн, роли призраков
// в Реестр не попадают: их профессий нет в нормах лояльности, а для спавна без лобби нет события входа.

using System.Diagnostics.CodeAnalysis;
using Content.Server._Dystopia.Economy;
using Content.Server.Administration.Managers;
using Content.Server.CriminalRecords.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Events;
using Content.Shared.StationRecords.Systems;
using Content.Shared._Dystopia.Economy;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Forensics.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.GameTicking;
using Content.Shared.Access.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityRegistrySystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private CriminalRecordsSystem _criminal = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private IAdminManager _admin = default!;

    /// <summary>Идёт своя запись в криминальные записи — её отголосок (RecordModifiedEvent) не разбираем.</summary>
    private bool _pushing;

    public const int MaxReasonLength = 300;
    public const int MaxNotesLength = 1000;
    public const int MaxDetailsLength = 600;

    public override void Initialize()
    {
        base.Initialize();

        // Раньше банка: банк откроет счёт с уже выданным номером.
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn, before: new[] { typeof(CityBankSystem) });
        SubscribeLocalEvent<RecordModifiedEvent>(OnSecurityRecordModified);
    }

    #region Внесение в Реестр

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!TryComp<CityRegistryComponent>(ev.Station, out var registry) || ev.JobId == null)
            return;

        var job = new ProtoId<JobPrototype>(ev.JobId);
        if (!IsCitizenJob(registry, job))
            return;

        var profile = ev.Profile;
        var record = new CityCitizenRecord
        {
            Number = IssueNumber((ev.Station, registry)),
            Name = Name(ev.Mob),
            Age = profile.Age,
            Species = profile.Species,
            Gender = profile.Gender,
            Job = job,
            Department = DepartmentOf(job),
            Fingerprint = CompOrNull<FingerprintComponent>(ev.Mob)?.Fingerprint,
            Dna = CompOrNull<DnaComponent>(ev.Mob)?.DNA,
            Owner = ev.Mob,
            Appearance = profile,
            JoinedAt = _ticker.RoundDuration(),
        };

        if (!registry.OutsideLoyalty.Contains(job))
        {
            record.LoyaltyNorm = Math.Clamp(registry.LoyaltyNorms[job], 0, registry.MaxLoyalty);
            record.Loyalty = record.LoyaltyNorm;
        }

        registry.Citizens[record.Number] = record;
        EnsureComp<CityCitizenComponent>(ev.Mob).Number = record.Number;

        var author = Loc.GetString("dystopia-registry-author-registry");
        AddEntry((ev.Station, registry), record, CityRegistryEntryKind.Service,
            Loc.GetString("dystopia-registry-entry-registered"), author);

        // карта, с которой гражданин вошёл в Город, — его карта №1
        if (_idCard.TryFindIdCard(ev.Mob, out var card))
            RegisterCard((ev.Station, registry), record, card.Owner, author);
    }

    /// <summary>Гражданин ли носитель этой профессии (есть ли она в Реестре).</summary>
    public static bool IsCitizenJob(CityRegistryComponent registry, ProtoId<JobPrototype> job)
    {
        return registry.LoyaltyNorms.ContainsKey(job) || registry.OutsideLoyalty.Contains(job);
    }

    /// <summary>Новый шестизначный номер: не занят ни в Реестре, ни среди счетов банка.</summary>
    private int IssueNumber(Entity<CityRegistryComponent> registry)
    {
        TryComp<CityBankComponent>(registry, out var bank);

        int number;
        do
        {
            number = _random.Next(100000, 1000000);
        } while (registry.Comp.Citizens.ContainsKey(number) || bank != null && bank.Accounts.ContainsKey(number));

        return number;
    }

    private ProtoId<DepartmentPrototype>? DepartmentOf(ProtoId<JobPrototype> job)
    {
        foreach (var department in _prototype.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (department.Roles.Contains(job))
                return new ProtoId<DepartmentPrototype>(department.ID);
        }

        return null;
    }

    #endregion

    #region Поиск

    /// <summary>Реестр Города (у нас одна станция).</summary>
    public bool TryGetRegistry(out Entity<CityRegistryComponent> registry)
    {
        var query = EntityQueryEnumerator<CityRegistryComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            registry = (uid, comp);
            return true;
        }

        registry = default;
        return false;
    }

    /// <summary>Номер гражданина по телу. null — не гражданин (нет в Реестре).</summary>
    public int? NumberOf(EntityUid owner)
    {
        return TryComp<CityCitizenComponent>(owner, out var citizen) ? citizen.Number : null;
    }

    public bool TryGetRecord(int number, [NotNullWhen(true)] out CityCitizenRecord? record)
    {
        record = null;
        return TryGetRegistry(out var registry) && registry.Comp.Citizens.TryGetValue(number, out record);
    }

    public bool TryGetRecord(EntityUid owner, [NotNullWhen(true)] out CityCitizenRecord? record)
    {
        record = null;
        return NumberOf(owner) is { } number && TryGetRecord(number, out record);
    }

    #endregion

    #region Изменение дела

    /// <summary>Записать в журнал дела. Лояльность меняется на loyaltyDelta (в пределах 0…потолок).</summary>
    public CityRegistryEntry AddEntry(
        Entity<CityRegistryComponent> registry,
        CityCitizenRecord record,
        CityRegistryEntryKind kind,
        string text,
        string author,
        string details = "",
        int? lawId = null,
        int? lawNumber = null,
        string? sanctionClass = null,
        int loyaltyDelta = 0)
    {
        var applied = ChangeLoyalty(registry, record, loyaltyDelta);
        var entry = new CityRegistryEntry
        {
            Time = _ticker.RoundDuration(),
            Kind = kind,
            Text = text,
            Details = Trim(details, MaxDetailsLength),
            LawId = lawId,
            LawNumber = lawNumber,
            SanctionClass = sanctionClass,
            LoyaltyDelta = applied,
            Author = author,
        };

        record.Journal.Add(entry);
        var overflow = record.Journal.Count - Math.Max(1, registry.Comp.JournalLimit);
        if (overflow > 0)
            record.Journal.RemoveRange(0, overflow);

        return entry;
    }

    /// <summary>Изменить лояльность. Возвращает, на сколько она изменилась на самом деле (у Консула — 0).</summary>
    public int ChangeLoyalty(Entity<CityRegistryComponent> registry, CityCitizenRecord record, int delta)
    {
        if (record.Loyalty is not { } loyalty || delta == 0)
            return 0;

        var next = Math.Clamp(loyalty + delta, 0, registry.Comp.MaxLoyalty);
        record.Loyalty = next;
        return next - loyalty;
    }

    /// <summary>
    /// Сменить статус с записью в журнал. Консулу статус не меняют. Смерть статус сама не меняет — её отмечают
    /// через консоль социоучёта. Поражение в правах («вне закона») замораживает счёт, отстраняет от должности и
    /// обнуляет лояльность; снятие поражения размораживает только счёт, замороженный им самим.
    /// </summary>
    public bool SetStatus(
        Entity<CityRegistryComponent> registry,
        CityCitizenRecord record,
        CityCitizenStatus status,
        string reason,
        string author)
    {
        if (IsOutsideLoyalty(registry, record))
            return false;

        if (record.Status == status)
            return false;

        var old = record.Status;
        record.Status = status;
        record.StatusReason = Trim(reason, MaxReasonLength);

        var details = record.StatusReason;
        var loyaltyDelta = 0;
        if (status == CityCitizenStatus.Disenfranchised)
        {
            if (SetAccountFrozen(record, true, author))
                record.FrozenByOutlaw = true;

            var annulled = AnnulAllCards(registry, record, Loc.GetString("dystopia-registry-outlaw-card-reason"), author);
            SetDismissed(record, true);
            loyaltyDelta = -(record.Loyalty ?? 0);
            details = JoinDetails(details, Loc.GetString("dystopia-registry-outlaw-effects", ("cards", annulled)));
        }
        else if (old == CityCitizenStatus.Disenfranchised)
        {
            if (record.FrozenByOutlaw)
            {
                SetAccountFrozen(record, false, author);
                record.FrozenByOutlaw = false;
            }

            details = JoinDetails(details, Loc.GetString("dystopia-registry-outlaw-lifted"));
        }

        AddEntry(registry, record, CityRegistryEntryKind.Service,
            Loc.GetString("dystopia-registry-entry-status",
                ("old", StatusName(old)), ("new", StatusName(status))),
            author, details, loyaltyDelta: loyaltyDelta);

        PushToSecurity(registry, record, author);
        return true;
    }

    /// <summary>Заморозить/разморозить счёт гражданина в Банке Города. true — состояние счёта изменилось.</summary>
    private bool SetAccountFrozen(CityCitizenRecord record, bool frozen, string author)
    {
        return _bank.TryGetBank(out var bank) &&
               bank.Comp.Accounts.TryGetValue(record.Number, out var account) &&
               _bank.SetFrozen(bank, account, frozen, author);
    }

    private static string JoinDetails(string first, string second)
    {
        return first.Length == 0 ? second : $"{first} {second}";
    }

    public void SetNotes(Entity<CityRegistryComponent> registry, CityCitizenRecord record, string notes, string author)
    {
        record.Notes = Trim(notes, MaxNotesLength);
        AddEntry(registry, record, CityRegistryEntryKind.Service, Loc.GetString("dystopia-registry-entry-notes"), author);
    }

    #endregion

    #region Криминальные записи (значки розыска, программа «Розыск»)

    /// <summary>
    /// Статус Реестра → ванильные криминальные записи: от них зависят значки на визорах, очках и масках Стражи.
    /// Если в записях уже стоит статус, который означает то же самое (например, «освобождён по УДО» при «под
    /// надзором»), его не трогаем.
    /// </summary>
    public void PushToSecurity(Entity<CityRegistryComponent> registry, CityCitizenRecord record, string author)
    {
        if (!registry.Comp.SecurityStatuses.TryGetValue(record.Status, out var status) ||
            FindSecurityKey(registry, record) is not { } key ||
            !_records.TryGetRecord<CriminalRecord>(key, out var criminal))
        {
            return;
        }

        if (criminal.Status == status ||
            registry.Comp.FromSecurityStatuses.TryGetValue(criminal.Status, out var same) && same == record.Status)
        {
            return;
        }

        _pushing = true;
        try
        {
            _criminal.OverwriteStatus(key, criminal, status,
                record.StatusReason.Length > 0 ? record.StatusReason : null, author);
        }
        finally
        {
            _pushing = false;
        }
    }

    /// <summary>
    /// Ванильная консоль криминальных записей поменяла статус → Реестр (если у статуса есть пара). Консула и
    /// поражение в правах она не трогает: такие правки откатываются к статусу Реестра.
    /// </summary>
    private void OnSecurityRecordModified(ref RecordModifiedEvent ev)
    {
        if (_pushing ||
            !TryComp<CityRegistryComponent>(ev.Key.OriginStation, out var registryComp) ||
            !_records.TryGetRecord<CriminalRecord>(ev.Key, out var criminal))
        {
            return;
        }

        Entity<CityRegistryComponent> registry = (ev.Key.OriginStation, registryComp);
        if (FindCitizen(registry, ev.Key) is not { } record || IsOutsideLoyalty(registry, record))
            return;

        var author = criminal.InitiatorName ?? Loc.GetString("dystopia-registry-author-security-console");
        if (!registry.Comp.FromSecurityStatuses.TryGetValue(criminal.Status, out var status) || status == record.Status)
            return;

        if (status == CityCitizenStatus.Disenfranchised || record.Status == CityCitizenStatus.Disenfranchised)
        {
            PushToSecurity(registry, record, Loc.GetString("dystopia-registry-author-registry"));
            return;
        }

        SetStatus(registry, record, status, criminal.Reason ?? string.Empty, author);
    }

    /// <summary>Ключ записи гражданина в станционном учёте: по имени и ДНК (запоминается в деле).</summary>
    private StationRecordKey? FindSecurityKey(Entity<CityRegistryComponent> registry, CityCitizenRecord record)
    {
        if (record.SecurityKey is { } known && _records.TryGetRecord<GeneralStationRecord>(known, out _))
            return known;

        foreach (var (id, general) in _records.GetRecordsOfType<GeneralStationRecord>(registry.Owner))
        {
            if (general.Name != record.Name || record.Dna != null && general.DNA != record.Dna)
                continue;

            record.SecurityKey = new StationRecordKey(id, registry.Owner);
            return record.SecurityKey;
        }

        return null;
    }

    private CityCitizenRecord? FindCitizen(Entity<CityRegistryComponent> registry, StationRecordKey key)
    {
        foreach (var record in registry.Comp.Citizens.Values)
        {
            if (record.SecurityKey is { } known && known.Equals(key))
                return record;
        }

        // ключ ещё не запоминали — сверяем по имени и ДНК
        if (!_records.TryGetRecord<GeneralStationRecord>(key, out var general))
            return null;

        foreach (var record in registry.Comp.Citizens.Values)
        {
            if (record.Name != general.Name || record.Dna != null && general.DNA != record.Dna)
                continue;

            record.SecurityKey = key;
            return record;
        }

        return null;
    }

    #endregion

    /// <summary>Вне учёта лояльности (Консул): его дело закрыто для правки.</summary>
    public static bool IsOutsideLoyalty(Entity<CityRegistryComponent> registry, CityCitizenRecord record)
    {
        return record.Job is { } job && registry.Comp.OutsideLoyalty.Contains(job);
    }

    /// <summary>
    /// Дело того, чья ID-карта сейчас у пользователя (в руке или в слоте ID). Кто держит карту — от того имени и
    /// действует, как в банке.
    /// </summary>
    public bool TryGetHolderRecord(EntityUid user, [NotNullWhen(true)] out CityCitizenRecord? record)
    {
        record = null;
        return _idCard.TryFindIdCard(user, out var card) &&
               TryComp<CityBankCardComponent>(card, out var bankCard) &&
               bankCard.AccountId is { } number &&
               TryGetRecord(number, out record);
    }

    /// <summary>
    /// Админ-призрак (агост): консоли Города пускают его со всеми правами, без ID-карты и должности.
    /// </summary>
    public bool IsOverseer(EntityUid user)
    {
        return HasComp<GhostComponent>(user) && _admin.IsAdmin(user);
    }

    /// <summary>Подпись в журнале: «№102628 стражник» (по карте пользователя) или имя, если карты гражданина нет.</summary>
    public string AuthorOf(EntityUid user)
    {
        if (!TryGetHolderRecord(user, out var record))
            return Name(user);

        var job = record.Job is { } id && _prototype.TryIndex(id, out var proto) ? proto.LocalizedName : string.Empty;
        return Loc.GetString("dystopia-registry-author", ("number", record.Number.ToString()), ("job", job));
    }

    public string StatusName(CityCitizenStatus status)
    {
        return Loc.GetString($"dystopia-registry-status-{status.ToString().ToLowerInvariant()}");
    }

    private static string Trim(string text, int max)
    {
        text = text.Trim();
        return text.Length > max ? text[..max] : text;
    }
}
