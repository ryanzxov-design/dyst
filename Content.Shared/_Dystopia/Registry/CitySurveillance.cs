// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: соционадзор — пост наблюдения Стражи. Камеры Города и маски стражников (список и карта, картинка, речь
// рядом с камерой, «Голос Стражи»), лента тревог с приёмом вызова, слежка за гражданином (картинка сама переходит на
// камеру, которая его видит), патрули (маски стражников), слова-маркеры. Данные копит CityCameraSystem, консоль —
// CitySurveillanceConsoleSystem.

using System.Numerics;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Registry;

[RegisterComponent]
public sealed partial class CitySurveillanceConsoleComponent : Component
{
    /// <summary>
    /// Кто может действовать: принимать тревоги, вести гражданина, править слова-маркеры, говорить голосом Стражи.
    /// Смотреть камеры — всем, кого пускает AccessReader консоли.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>> UseJobs = new();

    /// <summary>Самая длинная реплика «Голоса Стражи».</summary>
    [DataField]
    public int MaxVoiceLength = 160;

    /// <summary>Пауза между репликами «Голоса Стражи».</summary>
    [DataField]
    public TimeSpan VoiceCooldown = TimeSpan.FromSeconds(3);

    /// <summary>Службы, чьи маски показываются во вкладке «Патрули».</summary>
    [DataField]
    public List<ProtoId<DepartmentPrototype>> PatrolDepartments = new();

    /// <summary>Сколько минут пути показывать при слежке.</summary>
    [DataField]
    public TimeSpan TrackWindow = TimeSpan.FromMinutes(10);

    [DataField]
    public SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    /// <summary>Камера, которую сейчас показывает консоль.</summary>
    [ViewVariables]
    public EntityUid? Camera;

    /// <summary>Кого ведёт консоль (номер гражданина).</summary>
    [ViewVariables]
    public int? Tracking;

    [ViewVariables]
    public TimeSpan NextVoice;
}

[Serializable, NetSerializable]
public enum CitySurveillanceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CityCameraKind : byte
{
    Wall,
    Restricted,
    Mask,
}

/// <summary>Камера в списке и на карте.</summary>
[Serializable, NetSerializable]
public sealed class CitySurveillanceCamera
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Place = string.Empty;
    public CityCameraKind Kind;
    public bool Active;

    /// <summary>Почему не работает: «нет питания», «отключена» (пусто — работает).</summary>
    public string Status = string.Empty;

    /// <summary>Положение на сетке Города (для карты).</summary>
    public NetEntity Grid;
    public Vector2 Position;
}

[Serializable, NetSerializable]
public enum CityAlertKind : byte
{
    /// <summary>Замечен разыскиваемый.</summary>
    Wanted,

    /// <summary>Замечен поражённый в правах.</summary>
    Outlaw,

    /// <summary>Неопознанный в закрытой зоне.</summary>
    Restricted,

    /// <summary>Камера погасла или уничтожена.</summary>
    CameraDown,

    /// <summary>Прозвучало слово-маркер.</summary>
    Keyword,
}

/// <summary>Тревога в ленте соционадзора.</summary>
[Serializable, NetSerializable]
public sealed class CityAlert
{
    public int Id;
    public TimeSpan Time;
    public CityAlertKind Kind;
    public string Text = string.Empty;
    public string Place = string.Empty;
    public NetEntity? Camera;

    /// <summary>Гражданин, о котором тревога (если известен).</summary>
    public int? Number;

    /// <summary>Кто принял вызов (пусто — не принят).</summary>
    public string AcceptedBy = string.Empty;
    public TimeSpan AcceptedAt;

    /// <summary>Тревога снята сама (камера снова в эфире).</summary>
    public bool Resolved;
    public TimeSpan ResolvedAt;
}

/// <summary>Реплика, услышанная камерой.</summary>
[Serializable, NetSerializable]
public sealed class CitySpeechLine
{
    public TimeSpan Time;

    /// <summary>Имя, как его слышит стоящий рядом (из-под маски — номер).</summary>
    public string Name = string.Empty;
    public string Text = string.Empty;

    /// <summary>Сказано голосом Стражи с этой консоли или другой.</summary>
    public bool Voice;

    /// <summary>В реплике есть слово-маркер.</summary>
    public bool Keyword;
}

[Serializable, NetSerializable]
public enum CityMaskState : byte
{
    /// <summary>Маска на лице — картинка есть.</summary>
    Up,

    /// <summary>Маска приспущена.</summary>
    Lowered,

    /// <summary>Маски на лице нет.</summary>
    Off,
}

/// <summary>Стражник в списке патрулей.</summary>
[Serializable, NetSerializable]
public sealed class CityPatrolEntry
{
    public int Number;
    public string JobTitle = string.Empty;
    public CityMaskState Mask;

    /// <summary>В смене (игрок в теле).</summary>
    public bool OnShift;

    /// <summary>Где последний раз была картинка маски.</summary>
    public string Place = string.Empty;

    /// <summary>Сколько уже нет картинки (null — картинка есть или не было никогда).</summary>
    public TimeSpan? DarkFor;

    public NetEntity? Camera;
}

/// <summary>Слежка за гражданином.</summary>
[Serializable, NetSerializable]
public sealed class CityTrackInfo
{
    public int Number;
    public string Name = string.Empty;
    public CityCitizenStatus Status;

    /// <summary>Камера, которая видит его сейчас (null — сейчас не видит ни одна).</summary>
    public NetEntity? Current;

    /// <summary>Путь за последние минуты, старые — первыми.</summary>
    public List<CitySighting> Path = new();
}

[Serializable, NetSerializable]
public sealed class CitySurveillanceUiState(
    List<CitySurveillanceCamera> cameras,
    NetEntity? camera,
    List<CitySpeechLine> speech,
    List<CityAlert> alerts,
    CityTrackInfo? track,
    int? tracking,
    List<CityPatrolEntry> patrols,
    List<string> keywords,
    bool canUse) : BoundUserInterfaceState
{
    public readonly List<CitySurveillanceCamera> Cameras = cameras;

    /// <summary>Камера на экране консоли.</summary>
    public readonly NetEntity? Camera = camera;

    /// <summary>Что слышно у этой камеры, новые — последними.</summary>
    public readonly List<CitySpeechLine> Speech = speech;

    /// <summary>Тревоги, новые — первыми.</summary>
    public readonly List<CityAlert> Alerts = alerts;

    public readonly CityTrackInfo? Track = track;
    public readonly int? Tracking = tracking;
    public readonly List<CityPatrolEntry> Patrols = patrols;
    public readonly List<string> Keywords = keywords;
    public readonly bool CanUse = canUse;
}

/// <summary>Показать камеру (null — выключить экран).</summary>
[Serializable, NetSerializable]
public sealed class CitySurveillanceSelectMessage(NetEntity? camera) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Camera = camera;
}

[Serializable, NetSerializable]
public sealed class CitySurveillanceAcceptMessage(int alert) : BoundUserInterfaceMessage
{
    public readonly int Alert = alert;
}

/// <summary>Вести гражданина (null — перестать).</summary>
[Serializable, NetSerializable]
public sealed class CitySurveillanceTrackMessage(int? number) : BoundUserInterfaceMessage
{
    public readonly int? Number = number;
}

[Serializable, NetSerializable]
public sealed class CitySurveillanceKeywordMessage(string word, bool add) : BoundUserInterfaceMessage
{
    public readonly string Word = word;
    public readonly bool Add = add;
}

/// <summary>«Голос Стражи»: вслух через камеру/маску или (маска) — только носителю в ухо.</summary>
[Serializable, NetSerializable]
public sealed class CitySurveillanceVoiceMessage(string text, bool personal) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
    public readonly bool Personal = personal;
}
