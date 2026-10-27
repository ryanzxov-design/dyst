// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: консоль социоучёта — терминал Реестра граждан. Список граждан с поиском, личное дело (фото, анкета,
// лояльность, статус, журнал, банк), смена статуса, записи о нарушениях и поощрениях, особые приметы, печать
// ориентировки. Кто что может — по профессии владельца ID-карты (списки в прототипе консоли).

using Content.Shared._Dystopia.Laws;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Registry;

[RegisterComponent]
public sealed partial class CityRegistryConsoleComponent : Component
{
    /// <summary>Меняют статус (кроме поражения в правах), пишут нарушения и приметы, печатают ориентировки.</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> EditJobs = new();

    /// <summary>Пишут поощрения.</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> RewardJobs = new();

    /// <summary>Поражают в правах и снимают поражение (Консул).</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> SupremeJobs = new();

    /// <summary>Сколько лояльности можно добавить одним поощрением.</summary>
    [DataField]
    public int MaxReward = 10;

    /// <summary>Чем печатается ориентировка.</summary>
    [DataField]
    public EntProtoId PosterPrototype = "DystopiaWantedPoster";

    /// <summary>Печать, которой ориентировка заверяется сразу при печати (после неё лист не исправить).</summary>
    [DataField]
    public LocId PosterStampName = "dystopia-registry-poster-stamp";

    [DataField]
    public Color PosterStampColor = Color.FromHex("#4B6CAB");

    /// <summary>Оттиск на спрайте листа (состояние bureaucracy.rsi).</summary>
    [DataField]
    public string PosterStampState = "paper_stamp-hos";

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextPrint;

    /// <summary>Открытое на консоли личное дело.</summary>
    [ViewVariables]
    public int? Selected;
}

[Serializable, NetSerializable]
public enum CityRegistryConsoleUiKey : byte
{
    Key,
}

/// <summary>Строка общего списка.</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryListEntry
{
    public int Number;
    public string Name = string.Empty;
    public string JobTitle = string.Empty;
    public string? Department;
    public CityCitizenStatus Status;

    /// <summary>null — вне учёта лояльности (Консул).</summary>
    public int? Loyalty;
}

/// <summary>Служба для фильтра списка.</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryDepartmentEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
}

/// <summary>Статья Свода законов для формы нарушения.</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryLawEntry
{
    public int Id;
    public int Number;
    public string Title = string.Empty;
    public string Sanction = string.Empty;

    /// <summary>Классы, которые можно назначить по статье. Пусто — любой.</summary>
    public List<string> Classes = new();
}

/// <summary>Банковская сторона дела (только просмотр).</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryBankInfo
{
    public int Account;
    public int Balance;
    public int Debt;
    public int TaxRate;
    public bool Frozen;

    /// <summary>История операций, новые — первыми.</summary>
    public List<string> History = new();
}

/// <summary>Личное дело для показа.</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryDossier
{
    public int Number;
    public string Name = string.Empty;
    public int Age;
    public string Species = string.Empty;
    public Gender Gender;
    public string JobTitle = string.Empty;
    public string Department = string.Empty;
    public string? Fingerprint;
    public string? Dna;
    public TimeSpan JoinedAt;

    /// <summary>null — вне учёта лояльности: дело закрыто для правки, отпечатки и ДНК засекречены.</summary>
    public int? Loyalty;
    public int? LoyaltyNorm;
    public int MaxLoyalty;

    public CityCitizenStatus Status;
    public string StatusReason = string.Empty;
    public bool Dismissed;
    public string Notes = string.Empty;

    /// <summary>Новые — последними.</summary>
    public List<CityRegistryEntry> Journal = new();

    /// <summary>Внешность для фото.</summary>
    public HumanoidCharacterProfile? Appearance;

    public CityRegistryBankInfo? Bank;

    /// <summary>Маршрут по камерам, новые — последними.</summary>
    public List<CitySighting> Route = new();

    /// <summary>ID-карты гражданина по порядку выдачи.</summary>
    public List<CityIdCardEntry> Cards = new();
}

[Serializable, NetSerializable]
public sealed class CityRegistryConsoleUiState(
    List<CityRegistryListEntry> citizens,
    List<CityRegistryDepartmentEntry> departments,
    CityRegistryDossier? dossier,
    List<CityRegistryLawEntry> laws,
    List<CitySanctionClass> sanctions,
    List<CityUnidentifiedSighting> unidentified,
    bool canEdit,
    bool canReward,
    bool canSupreme,
    int maxReward) : BoundUserInterfaceState
{
    public readonly List<CityRegistryListEntry> Citizens = citizens;
    public readonly List<CityRegistryDepartmentEntry> Departments = departments;
    public readonly CityRegistryDossier? Dossier = dossier;
    public readonly List<CityRegistryLawEntry> Laws = laws;

    /// <summary>Шкала санкций в порядке возрастания тяжести.</summary>
    public readonly List<CitySanctionClass> Sanctions = sanctions;

    /// <summary>Неопознанные, новые — первыми.</summary>
    public readonly List<CityUnidentifiedSighting> Unidentified = unidentified;

    public readonly bool CanEdit = canEdit;
    public readonly bool CanReward = canReward;
    public readonly bool CanSupreme = canSupreme;
    public readonly int MaxReward = maxReward;
}

/// <summary>Открыть дело (null — вернуться к списку).</summary>
[Serializable, NetSerializable]
public sealed class CityRegistrySelectMessage(int? number) : BoundUserInterfaceMessage
{
    public readonly int? Number = number;
}

[Serializable, NetSerializable]
public sealed class CityRegistrySetStatusMessage(int number, CityCitizenStatus status, string reason) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
    public readonly CityCitizenStatus Status = status;
    public readonly string Reason = reason;
}

/// <summary>Нарушение: статья, класс, сколько лояльности снять (для классов с отстранением не важно).</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryViolationMessage(int number, int lawId, string sanctionClass, int loyalty, string details)
    : BoundUserInterfaceMessage
{
    public readonly int Number = number;
    public readonly int LawId = lawId;
    public readonly string SanctionClass = sanctionClass;
    public readonly int Loyalty = loyalty;
    public readonly string Details = details;
}

[Serializable, NetSerializable]
public sealed class CityRegistryRewardMessage(int number, int loyalty, string text) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
    public readonly int Loyalty = loyalty;
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class CityRegistryNotesMessage(int number, string notes) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
    public readonly string Notes = notes;
}

[Serializable, NetSerializable]
public sealed class CityRegistryPrintMessage(int number) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
}

/// <summary>Опознать: отметку из «Неопознанных» — в маршрут гражданина.</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryIdentifyMessage(int sighting, int number) : BoundUserInterfaceMessage
{
    public readonly int Sighting = sighting;
    public readonly int Number = number;
}

/// <summary>Смотреть через камеру (null — перестать смотреть).</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryWatchMessage(NetEntity? camera) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Camera = camera;
}

/// <summary>Сервер → клиент: через какую камеру сейчас смотрит этот пользователь (null — ни через какую).</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryWatchingMessage(NetEntity? camera, string title) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Camera = camera;
    public readonly string Title = title;
}

public static class CityRegistryRules
{
    /// <summary>
    /// Классы, которые можно назначить по статье этому гражданину: классы статьи (пусто — все), а если он уже
    /// нарушал эту статью — ещё и на ступень выше каждого из них. Порядок — по шкале санкций.
    /// </summary>
    public static List<string> AllowedClasses(IReadOnlyList<CitySanctionClass> scale, CityRegistryLawEntry law, bool repeat)
    {
        var order = new List<string>();
        foreach (var sanction in scale)
        {
            order.Add(sanction.Class);
        }

        if (law.Classes.Count == 0)
            return order;

        var allowed = new HashSet<string>(law.Classes);
        if (repeat)
        {
            foreach (var cls in law.Classes)
            {
                var index = order.IndexOf(cls);
                if (index >= 0 && index + 1 < order.Count)
                    allowed.Add(order[index + 1]);
            }
        }

        return order.FindAll(allowed.Contains);
    }

    /// <summary>Нарушал ли гражданин эту статью раньше (по журналу дела).</summary>
    public static bool IsRepeat(IEnumerable<CityRegistryEntry> journal, int lawId)
    {
        foreach (var entry in journal)
        {
            if (entry.Kind == CityRegistryEntryKind.Violation && entry.LawId == lawId)
                return true;
        }

        return false;
    }
}
