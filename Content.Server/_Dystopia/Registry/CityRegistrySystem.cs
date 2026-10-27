// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: Реестр граждан Города (социоучёт). Гражданину, вошедшему в раунд через лобби на профессию Города
// (в начале смены или поздним входом), Реестр выдаёт номер и заводит личное дело. Банк открывает счёт с тем же
// номером — поэтому номер гражданина и номер счёта совпадают. Культисты из логова, админ-спавн, роли призраков
// в Реестр не попадают: их профессий нет в нормах лояльности, а для спавна без лобби нет события входа.

using System.Diagnostics.CodeAnalysis;
using Content.Server._Dystopia.Economy;
using Content.Shared._Dystopia.Economy;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityRegistrySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public const int MaxReasonLength = 300;
    public const int MaxNotesLength = 1000;

    public override void Initialize()
    {
        base.Initialize();

        // Раньше банка: банк откроет счёт с уже выданным номером.
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn, before: new[] { typeof(CityBankSystem) });
        SubscribeLocalEvent<CityCitizenComponent, MobStateChangedEvent>(OnMobStateChanged);
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
            JoinedAt = _timing.CurTime,
        };

        if (!registry.OutsideLoyalty.Contains(job))
        {
            record.LoyaltyNorm = Math.Clamp(registry.LoyaltyNorms[job], 0, registry.MaxLoyalty);
            record.Loyalty = record.LoyaltyNorm;
        }

        registry.Citizens[record.Number] = record;
        EnsureComp<CityCitizenComponent>(ev.Mob).Number = record.Number;

        AddEntry((ev.Station, registry), record, CityRegistryEntryKind.Service,
            Loc.GetString("dystopia-registry-entry-registered"), Loc.GetString("dystopia-registry-author-registry"));
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
        int? lawNumber = null,
        string? sanctionClass = null,
        int loyaltyDelta = 0)
    {
        var applied = ChangeLoyalty(registry, record, loyaltyDelta);
        var entry = new CityRegistryEntry
        {
            Time = _timing.CurTime,
            Kind = kind,
            Text = text,
            Details = Trim(details, MaxReasonLength),
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

    /// <summary>Сменить статус с записью в журнал. Консулу статус не меняют (кроме смерти — см. ниже).</summary>
    public bool SetStatus(
        Entity<CityRegistryComponent> registry,
        CityCitizenRecord record,
        CityCitizenStatus status,
        string reason,
        string author)
    {
        if (record.Loyalty == null && record.Job is { } job && registry.Comp.OutsideLoyalty.Contains(job))
            return false;

        if (record.Status == status)
            return false;

        var old = record.Status;
        record.Status = status;
        record.StatusReason = Trim(reason, MaxReasonLength);

        AddEntry(registry, record, CityRegistryEntryKind.Service,
            Loc.GetString("dystopia-registry-entry-status",
                ("old", StatusName(old)), ("new", StatusName(status))),
            author, record.StatusReason);
        return true;
    }

    public void SetNotes(Entity<CityRegistryComponent> registry, CityCitizenRecord record, string notes, string author)
    {
        record.Notes = Trim(notes, MaxNotesLength);
        AddEntry(registry, record, CityRegistryEntryKind.Service, Loc.GetString("dystopia-registry-entry-notes"), author);
    }

    /// <summary>Смерть и оживление меняют статус сами; после оживления возвращается прежний.</summary>
    private void OnMobStateChanged(Entity<CityCitizenComponent> ent, ref MobStateChangedEvent args)
    {
        if (!TryGetRegistry(out var registry) || !registry.Comp.Citizens.TryGetValue(ent.Comp.Number, out var record))
            return;

        var author = Loc.GetString("dystopia-registry-author-registry");
        if (args.NewMobState == MobState.Dead && record.Status != CityCitizenStatus.Dead)
        {
            record.StatusBeforeDeath = record.Status;
            record.Status = CityCitizenStatus.Dead;
            AddEntry(registry, record, CityRegistryEntryKind.Service, Loc.GetString("dystopia-registry-entry-died"), author);
        }
        else if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead && record.Status == CityCitizenStatus.Dead)
        {
            record.Status = record.StatusBeforeDeath;
            AddEntry(registry, record, CityRegistryEntryKind.Service, Loc.GetString("dystopia-registry-entry-revived"), author);
        }
    }

    #endregion

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
