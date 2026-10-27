// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: Реестр граждан Города — основа социоучёта. Номер гражданина выдаёт Реестр (банк открывает счёт с тем же
// номером), здесь же личное дело: анкета, внешность для фото, лояльность, статус, журнал нарушений и поощрений.
// Живёт один раунд. Вся логика — на сервере (CityRegistrySystem).

using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Registry;

/// <summary>
/// Реестр граждан. Вешается на станцию Города (прототип DystopiaCityStation). Кто гражданин и какая у него
/// норма лояльности — задаётся здесь же, в прототипе.
/// </summary>
[RegisterComponent]
public sealed partial class CityRegistryComponent : Component
{
    /// <summary>Потолок лояльности.</summary>
    [DataField]
    public int MaxLoyalty = 50;

    /// <summary>
    /// Норма лояльности по профессиям — с неё гражданин начинает смену. Профессия есть в этой таблице или в
    /// <see cref="OutsideLoyalty"/> — значит, её носитель гражданин и получает номер. Остальные (культисты из
    /// логова и прочие) в Реестр не попадают.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int> LoyaltyNorms = new();

    /// <summary>Граждане вне учёта лояльности (Консул: он сам — мера лояльности).</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> OutsideLoyalty = new();

    /// <summary>
    /// Какой статус ставить в ванильных криминальных записях при статусе Реестра. От статуса записей зависят
    /// значки розыска на визорах, очках и масках Стражи и программа КПК «Розыск».
    /// </summary>
    [DataField]
    public Dictionary<CityCitizenStatus, SecurityStatus> SecurityStatuses = new();

    /// <summary>
    /// Обратно: статус, поставленный в ванильной консоли криминальных записей, → статус Реестра. Чего здесь нет
    /// (например, «враждебен»), то Реестр не меняет. Поражение в правах ванильной консолью не ставится и не снимается.
    /// </summary>
    [DataField]
    public Dictionary<SecurityStatus, CityCitizenStatus> FromSecurityStatuses = new();

    /// <summary>Сколько записей журнала хранить на одно дело (старые уходят).</summary>
    [DataField]
    public int JournalLimit = 100;

    #region Камеры

    /// <summary>
    /// Ведут ли учёт и ванильные камеры наблюдения (без CityCamera) с настройками по умолчанию ниже. Нужно, пока
    /// у Города нет своей карты с городскими камерами.
    /// </summary>
    [DataField]
    public bool ScanAllCameras = true;

    /// <summary>Радиус обзора ванильной камеры, если она ведёт учёт.</summary>
    [DataField]
    public float DefaultCameraRange = 7f;

    /// <summary>Как часто камеры осматривают поле зрения (секунды).</summary>
    [DataField]
    public float ScanInterval = 2f;

    /// <summary>Сколько последних отметок хранить в маршруте гражданина.</summary>
    [DataField]
    public int RouteLength = 10;

    /// <summary>
    /// Одна камера отмечает одного человека не чаще раза в этот срок. Пока он не уходит из кадра дольше этого срока,
    /// отметка не дублируется, а продлевается («с 12:03 по 12:15»).
    /// </summary>
    [DataField]
    public TimeSpan SightingCooldown = TimeSpan.FromMinutes(1);

    /// <summary>Сколько хранится запись о неопознанном (от последнего раза, когда его видели).</summary>
    [DataField]
    public TimeSpan UnidentifiedLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Сколько записей о неопознанных хранить (старые уходят).</summary>
    [DataField]
    public int UnidentifiedLimit = 80;

    /// <summary>Канал тревог камер (канал Стражи).</summary>
    [DataField]
    public string AlertChannel = "Security";

    /// <summary>Тревога об одном и том же человеке — не чаще раза в этот срок.</summary>
    [DataField]
    public TimeSpan AlertCooldown = TimeSpan.FromMinutes(2);

    [ViewVariables]
    public int NextUnidentified = 1;

    /// <summary>Неопознанные: лицо скрыто, карты нет. Новые — в конце.</summary>
    [ViewVariables]
    public List<CityUnidentifiedSighting> Unidentified = new();

    #endregion

    /// <summary>Номер следующей ориентировки.</summary>
    [ViewVariables]
    public int NextPoster = 1;

    /// <summary>Личные дела по номеру гражданина.</summary>
    [ViewVariables]
    public Dictionary<int, CityCitizenRecord> Citizens = new();
}

/// <summary>Метка на теле гражданина: его номер в Реестре (быстрый поиск дела по телу).</summary>
[RegisterComponent]
public sealed partial class CityCitizenComponent : Component
{
    [ViewVariables]
    public int Number;
}

[Serializable, NetSerializable]
public enum CityCitizenStatus : byte
{
    Citizen,
    Supervised,
    Wanted,
    Detained,
    Disenfranchised,
    Dead,
}

[Serializable, NetSerializable]
public enum CityRegistryEntryKind : byte
{
    /// <summary>Нарушение статьи Свода законов.</summary>
    Violation,

    /// <summary>Поощрение.</summary>
    Reward,

    /// <summary>Служебная запись: смена статуса, приметы, внесение в Реестр.</summary>
    Service,
}

/// <summary>Запись журнала личного дела.</summary>
[Serializable, NetSerializable]
public sealed class CityRegistryEntry
{
    public TimeSpan Time;
    public CityRegistryEntryKind Kind;
    public string Text = string.Empty;

    /// <summary>Подробности: санкция, основание.</summary>
    public string Details = string.Empty;

    /// <summary>Статья Свода законов (у нарушения): внутренний идентификатор и номер на момент записи.</summary>
    public int? LawId;
    public int? LawNumber;

    /// <summary>Класс санкции (у нарушения).</summary>
    public string? SanctionClass;

    /// <summary>На сколько изменилась лояльность (0 — не менялась).</summary>
    public int LoyaltyDelta;

    /// <summary>Кто внёс: «№102628 стражник», «Реестр» и т.п.</summary>
    public string Author = string.Empty;
}

/// <summary>Личное дело гражданина.</summary>
public sealed class CityCitizenRecord
{
    [ViewVariables] public int Number;
    [ViewVariables(VVAccess.ReadWrite)] public string Name = string.Empty;
    [ViewVariables] public int Age;
    [ViewVariables] public ProtoId<SpeciesPrototype> Species;
    [ViewVariables] public Gender Gender;
    [ViewVariables] public ProtoId<JobPrototype>? Job;
    [ViewVariables] public ProtoId<DepartmentPrototype>? Department;
    [ViewVariables] public string? Fingerprint;
    [ViewVariables] public string? Dna;

    /// <summary>Тело гражданина (для отметок камер и т.п.).</summary>
    [ViewVariables] public EntityUid? Owner;

    /// <summary>Ключ его записи в ванильном станционном учёте (находится по имени и ДНК при первой сверке).</summary>
    [ViewVariables] public StationRecordKey? SecurityKey;

    /// <summary>Внешность при входе в Город — из неё консоль рисует фото без одежды.</summary>
    [ViewVariables] public HumanoidCharacterProfile? Appearance;

    /// <summary>Когда внесён в Реестр (время раунда).</summary>
    [ViewVariables] public TimeSpan JoinedAt;

    /// <summary>Лояльность. null — вне учёта (Консул).</summary>
    [ViewVariables(VVAccess.ReadWrite)] public int? Loyalty;

    /// <summary>Норма лояльности профессии (с ней гражданин начал смену).</summary>
    [ViewVariables] public int? LoyaltyNorm;

    [ViewVariables(VVAccess.ReadWrite)] public CityCitizenStatus Status = CityCitizenStatus.Citizen;

    /// <summary>Основание текущего статуса (для розыска обязательно).</summary>
    [ViewVariables(VVAccess.ReadWrite)] public string StatusReason = string.Empty;

    /// <summary>Отстранён от должности (нарушение класса с отстранением).</summary>
    [ViewVariables(VVAccess.ReadWrite)] public bool Dismissed;

    /// <summary>Особые приметы — свободный текст Стражи.</summary>
    [ViewVariables(VVAccess.ReadWrite)] public string Notes = string.Empty;

    /// <summary>Журнал: нарушения, поощрения, служебные записи. Новые — в конце.</summary>
    [ViewVariables] public List<CityRegistryEntry> Journal = new();

    /// <summary>Маршрут: последние отметки камер. Новые — в конце; последняя — последнее известное местонахождение.</summary>
    [ViewVariables] public List<CitySighting> Sightings = new();

    /// <summary>Счёт заморожен поражением в правах (снятие поражения размораживает только такой счёт).</summary>
    [ViewVariables] public bool FrozenByOutlaw;
}
