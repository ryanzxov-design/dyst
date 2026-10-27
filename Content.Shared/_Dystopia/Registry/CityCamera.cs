// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: городские камеры Реестра. Камера (настенная или маска Стражи) отмечает граждан в поле зрения: по ID-карте
// в руке или в слоте ID (опознаётся владелец карты) или по открытому лицу. Отметки складываются в маршрут личного
// дела, люди со скрытым лицом и без карты — во вкладку «Неопознанные». Вся логика — на сервере (CityCameraSystem).

using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Registry;

/// <summary>
/// Камера Реестра. Вешается вместе с ванильной SurveillanceCamera: та даёт картинку в консоли камер, эта — учёт.
/// </summary>
[RegisterComponent]
public sealed partial class CityCameraComponent : Component
{
    /// <summary>Радиус обзора в тайлах (круг, сквозь стены не видит).</summary>
    [DataField]
    public float Range = 7f;

    /// <summary>Камера закрытой зоны: неопознанный в её поле зрения — тревога Страже.</summary>
    [DataField]
    public bool Restricted;

    /// <summary>
    /// Маска Стражи: камера работает, только пока маска надета и не приспущена. Носитель этого никак не замечает.
    /// </summary>
    [DataField]
    public bool Mask;

    /// <summary>Как маска называется в консоли камер, пока надета (к имени добавляется номер носителя).</summary>
    [DataField]
    public LocId MaskName = "dystopia-camera-mask-name";

    /// <summary>Кто носит маску (для имени в консоли камер).</summary>
    [ViewVariables]
    public EntityUid? Wearer;

    /// <summary>Номер гражданина-носителя (для имени маски).</summary>
    [ViewVariables]
    public int? WearerNumber;

    /// <summary>Номер последнего носителя маски (чтобы и снятая маска числилась за стражником).</summary>
    [ViewVariables]
    public int? LastWearerNumber;

    /// <summary>Работала ли камера при прошлом осмотре (для тревоги «камера погасла»).</summary>
    [ViewVariables]
    public bool WasActive;

    /// <summary>Открытая тревога «камера погасла» (снимается, когда камера снова в эфире).</summary>
    [ViewVariables]
    public int? DownAlert;

    /// <summary>Когда камера (маска) последний раз давала картинку и где.</summary>
    [ViewVariables]
    public TimeSpan? LastActiveAt;

    [ViewVariables]
    public string LastPlace = string.Empty;
}

[Serializable, NetSerializable]
public enum CitySightingMethod : byte
{
    /// <summary>По ID-карте (опознан владелец карты).</summary>
    Card,

    /// <summary>По открытому лицу.</summary>
    Face,

    /// <summary>Опознан Стражей по записи из «Неопознанных».</summary>
    Manual,
}

/// <summary>Отметка камеры в маршруте гражданина.</summary>
[Serializable, NetSerializable]
public sealed class CitySighting
{
    /// <summary>Когда камера увидела впервые и когда — в последний раз (пока гражданин не уходил из кадра).</summary>
    public TimeSpan Time;
    public TimeSpan LastTime;

    /// <summary>Ближайший ориентир (маяк навигационной карты).</summary>
    public string Place = string.Empty;

    /// <summary>Имя камеры.</summary>
    public string Camera = string.Empty;

    public CitySightingMethod Method;

    /// <summary>Отметила маска Стражи, а не настенная камера.</summary>
    public bool Mask;

    /// <summary>Камера — для кнопки «Смотреть».</summary>
    public NetEntity CameraEntity;
}

/// <summary>Неопознанный: лицо скрыто, карты нет.</summary>
[Serializable, NetSerializable]
public sealed class CityUnidentifiedSighting
{
    public int Id;

    /// <summary>Как его видит камера: «молодой мужчина» и т.п.</summary>
    public string Description = string.Empty;

    public TimeSpan Time;
    public TimeSpan LastTime;
    public string Place = string.Empty;
    public string Camera = string.Empty;
    public bool Mask;

    /// <summary>Замечен камерой закрытой зоны.</summary>
    public bool Restricted;

    public NetEntity CameraEntity;
}
