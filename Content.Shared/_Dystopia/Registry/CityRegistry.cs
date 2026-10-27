// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: Реестр граждан Города — основа социоучёта. Номер гражданина выдаёт Реестр (банк открывает счёт с тем же
// номером), здесь же личное дело: анкета, внешность для фото, лояльность, статус, журнал нарушений и поощрений.
// Живёт один раунд. Вся логика — на сервере (CityRegistrySystem).

using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
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

    /// <summary>Сколько записей журнала хранить на одно дело (старые уходят).</summary>
    [DataField]
    public int JournalLimit = 100;

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

    /// <summary>Статья Свода законов (у нарушения).</summary>
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

    /// <summary>Тело гражданина (для отметок камер, смерти и т.п.).</summary>
    [ViewVariables] public EntityUid? Owner;

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

    /// <summary>Статус до смерти — вернётся, если гражданина оживят.</summary>
    [ViewVariables] public CityCitizenStatus StatusBeforeDeath = CityCitizenStatus.Citizen;

    /// <summary>Отстранён от должности (нарушение класса с отстранением).</summary>
    [ViewVariables(VVAccess.ReadWrite)] public bool Dismissed;

    /// <summary>Особые приметы — свободный текст Стражи.</summary>
    [ViewVariables(VVAccess.ReadWrite)] public string Notes = string.Empty;

    /// <summary>Журнал: нарушения, поощрения, служебные записи. Новые — в конце.</summary>
    [ViewVariables] public List<CityRegistryEntry> Journal = new();
}
