// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: ID-карты граждан в Реестре. Карта, с которой гражданин вошёл в Город, становится картой №1 (К-377410-01);
// Паспортный стол выдаёт новые (бесплатно, сколько угодно) и аннулирует старые. Аннулированная карта без доступов и
// отвязана от счёта, но камеры по ней узнают владельца. Отстранение от должности снимает доступы с живых карт
// (сохраняя их), восстановление возвращает. Поражение в правах аннулирует все карты гражданина.

using System.Linq;
using Content.Shared._Dystopia.Economy;
using Content.Shared._Dystopia.Registry;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.PDA;
using Content.Shared.StationRecords.Components;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityRegistrySystem
{
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private StationRecordKeyStorageSystem _keyStorage = default!;
    [Dependency] private IComponentFactory _factory = default!;

    private static readonly ProtoId<AccessLevelPrototype>[] NoAccess = [];

    public string SerialOf(int number, int index)
    {
        return Loc.GetString("dystopia-card-serial", ("number", number.ToString()), ("index", index.ToString("00")));
    }

    /// <summary>Внести карту в дело гражданина. Отстранённому карта сразу выдаётся без доступов.</summary>
    public CityIdCardRecord RegisterCard(Entity<CityRegistryComponent> registry, CityCitizenRecord record, EntityUid card, string author)
    {
        var entry = new CityIdCardRecord
        {
            Index = record.Cards.Count + 1,
            IssuedAt = _ticker.RoundDuration(),
            IssuedBy = author,
            Card = card,
        };
        entry.Serial = SerialOf(record.Number, entry.Index);
        record.Cards.Add(entry);

        var comp = EnsureComp<CityIdCardComponent>(card);
        comp.Number = record.Number;
        comp.Index = entry.Index;
        comp.Serial = entry.Serial;
        comp.Annulled = false;
        comp.SuspendedTags = null;

        if (record.Dismissed)
            SuspendCard(card, comp);

        return entry;
    }

    /// <summary>Номер гражданина-владельца карты (в том числе аннулированной). Для КПК — по карте внутри.</summary>
    public int? CardOwner(EntityUid item)
    {
        if (TryComp<PdaComponent>(item, out var pda) && pda.ContainedId is { } contained)
            item = contained;

        return TryComp<CityIdCardComponent>(item, out var card) ? card.Number : null;
    }

    #region Аннулирование

    /// <summary>
    /// Аннулировать карту: доступы сняты и выключены, от счёта отвязана. Камеры по ней по-прежнему узнают владельца.
    /// </summary>
    public bool AnnulCard(
        Entity<CityRegistryComponent> registry,
        CityCitizenRecord record,
        int index,
        string reason,
        string author,
        bool journal = true)
    {
        var entry = record.Cards.Find(c => c.Index == index);
        if (entry == null || entry.Annulled)
            return false;

        entry.Annulled = true;
        entry.AnnulledAt = _ticker.RoundDuration();
        entry.AnnulledBy = author;
        entry.AnnulReason = Trim(reason, MaxReasonLength);

        if (entry.Card is { } card && !TerminatingOrDeleted(card))
        {
            if (TryComp<CityIdCardComponent>(card, out var comp))
            {
                comp.Annulled = true;
                comp.SuspendedTags = null;
            }

            if (TryComp<AccessComponent>(card, out var access))
            {
                _access.TrySetTags(card, NoAccess, access);
                _access.SetAccessEnabled(card, false, access);
            }

            if (TryComp<CityBankCardComponent>(card, out var bankCard))
            {
                bankCard.AccountId = null;
                bankCard.Bank = null;
            }
        }

        if (journal)
        {
            AddEntry(registry, record, CityRegistryEntryKind.Service,
                Loc.GetString("dystopia-registry-entry-card-annulled", ("serial", entry.Serial)),
                author, entry.AnnulReason);
        }

        return true;
    }

    /// <summary>Аннулировать все живые карты гражданина (без записей в журнал). Возвращает, сколько аннулировано.</summary>
    public int AnnulAllCards(Entity<CityRegistryComponent> registry, CityCitizenRecord record, string reason, string author)
    {
        var count = 0;
        foreach (var entry in record.Cards.ToList())
        {
            if (AnnulCard(registry, record, entry.Index, reason, author, false))
                count++;
        }

        return count;
    }

    #endregion

    #region Выдача

    /// <summary>
    /// Выдать новую карту: карта профессии (из стартового снаряжения; если там КПК — карта из него) с доступами
    /// профессии, на имя гражданина, привязанная к его счёту. fallback — если у профессии карты не нашлось.
    /// </summary>
    public EntityUid? IssueCard(
        Entity<CityRegistryComponent> registry,
        CityCitizenRecord record,
        EntityCoordinates coordinates,
        string author,
        EntProtoId? fallback = null)
    {
        if ((CardPrototypeOf(record) ?? fallback) is not { } prototype)
            return null;

        var card = Spawn(prototype, coordinates);
        _idCard.TryChangeFullName(card, record.Name);

        var bankCard = EnsureComp<CityBankCardComponent>(card);
        if (_bank.TryGetBank(out var bank) && bank.Comp.Accounts.ContainsKey(record.Number))
        {
            bankCard.AccountId = record.Number;
            bankCard.Bank = bank.Owner;
        }

        if (FindSecurityKey(registry, record) is { } key && HasComp<StationRecordKeyStorageComponent>(card))
            _keyStorage.AssignKey(card, key);

        var entry = RegisterCard(registry, record, card, author);
        AddEntry(registry, record, CityRegistryEntryKind.Service,
            Loc.GetString("dystopia-registry-entry-card-issued", ("serial", entry.Serial)), author);
        return card;
    }

    private EntProtoId? CardPrototypeOf(CityCitizenRecord record)
    {
        if (record.Job is not { } jobId ||
            !_prototype.TryIndex(jobId, out var job) ||
            job.StartingGear is not { } gearId ||
            !_prototype.TryIndex(gearId, out var gear) ||
            !gear.Equipment.TryGetValue("id", out var id) ||
            !_prototype.TryIndex(id, out var item))
        {
            return null;
        }

        if (item.TryGetComponent<PdaComponent>(out var pda, _factory))
            return pda.IdCard;

        if (item.TryGetComponent<IdCardComponent>(out _, _factory))
            return id;

        return null;
    }

    #endregion

    #region Отстранение

    /// <summary>
    /// Отстранить от должности или снять отстранение. Отстранение снимает доступы с живых карт (запоминая их),
    /// снятие — возвращает.
    /// </summary>
    public void SetDismissed(CityCitizenRecord record, bool dismissed)
    {
        if (record.Dismissed == dismissed)
            return;

        record.Dismissed = dismissed;
        foreach (var entry in record.Cards)
        {
            if (entry.Annulled || entry.Card is not { } card || TerminatingOrDeleted(card) ||
                !TryComp<CityIdCardComponent>(card, out var comp))
            {
                continue;
            }

            if (dismissed)
                SuspendCard(card, comp);
            else
                RestoreCard(card, comp);
        }
    }

    /// <summary>Восстановить в должности (Паспортный стол). Поражённого в правах не восстанавливают.</summary>
    public bool Reinstate(Entity<CityRegistryComponent> registry, CityCitizenRecord record, string author)
    {
        if (!record.Dismissed || record.Status == CityCitizenStatus.Disenfranchised || IsOutsideLoyalty(registry, record))
            return false;

        SetDismissed(record, false);
        AddEntry(registry, record, CityRegistryEntryKind.Service, Loc.GetString("dystopia-registry-entry-reinstated"), author);
        return true;
    }

    private void SuspendCard(EntityUid card, CityIdCardComponent comp)
    {
        if (comp.Annulled || comp.SuspendedTags != null || !TryComp<AccessComponent>(card, out var access))
            return;

        comp.SuspendedTags = new HashSet<ProtoId<AccessLevelPrototype>>(access.Tags);
        _access.TrySetTags(card, NoAccess, access);
    }

    private void RestoreCard(EntityUid card, CityIdCardComponent comp)
    {
        if (comp.SuspendedTags is not { } saved)
            return;

        comp.SuspendedTags = null;
        if (comp.Annulled || !TryComp<AccessComponent>(card, out var access))
            return;

        // что ГИ дописал на карту, пока она была приостановлена, остаётся
        var tags = access.Tags.Union(saved).ToList();
        _access.TrySetTags(card, tags, access);
    }

    #endregion

    #region Осмотр

    /// <summary>Строки осмотра карты (и КПК с картой): серийный номер, владелец, аннулирована/приостановлена.</summary>
    public bool PushCardExamine(EntityUid item, ExaminedEvent args)
    {
        if (TryComp<PdaComponent>(item, out var pda) && pda.ContainedId is { } contained)
            item = contained;

        if (!TryComp<CityIdCardComponent>(item, out var card))
            return false;

        args.PushMarkup(Loc.GetString("dystopia-card-examine",
            ("serial", card.Serial), ("number", card.Number.ToString())));

        if (card.Annulled)
            args.PushMarkup(Loc.GetString("dystopia-card-examine-annulled"));
        else if (card.SuspendedTags != null)
            args.PushMarkup(Loc.GetString("dystopia-card-examine-suspended"));

        return true;
    }

    #endregion

    /// <summary>Карты для показа в консолях.</summary>
    public List<CityIdCardEntry> CardEntries(CityCitizenRecord record)
    {
        var list = new List<CityIdCardEntry>();
        foreach (var entry in record.Cards)
        {
            var exists = entry.Card is { } card && !TerminatingOrDeleted(card);
            list.Add(new CityIdCardEntry
            {
                Index = entry.Index,
                Serial = entry.Serial,
                IssuedAt = entry.IssuedAt,
                IssuedBy = entry.IssuedBy,
                Annulled = entry.Annulled,
                AnnulledAt = entry.AnnulledAt,
                AnnulledBy = entry.AnnulledBy,
                AnnulReason = entry.AnnulReason,
                Destroyed = !exists,
                Suspended = exists && CompOrNull<CityIdCardComponent>(entry.Card!.Value)?.SuspendedTags != null,
            });
        }

        return list;
    }
}
