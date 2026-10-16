// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: раскалённые предметы, кузнечные клещи, меха, закалка водой.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

/// <summary>
/// Предмет может раскалиться. Раскалённый (Hot) рукой не взять — только клещами (<see cref="DystopiaTongsComponent"/>).
/// Тронул голой рукой — ожог той руки, которой тронул; раскалился в руке — ожог этой руки и предмет падает.
/// Hot выставляет система, которая греет предмет (тигель, изложница).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DystopiaHotItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Hot;

    /// <summary>Ожог руки: за касание и за каждую проверку, пока раскалённое в руке.</summary>
    [DataField]
    public DamageSpecifier BurnDamage = new();

    /// <summary>Как часто проверять, не раскалилось ли оно у кого-то в руке, секунды.</summary>
    [DataField]
    public float CheckInterval = 1f;

    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>
/// Кузнечные клещи. Ткнул клещами в раскалённый тигель или изложницу — зажал (предмет внутри клещей).
/// Зажатым тиглем: в изложницу или другой тигель — разлить, в горн или стол — поставить, в пол — положить.
/// Z (в руке) или действие — разжать.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaTongsComponent : Component
{
    /// <summary>Контейнер-слот для зажатого предмета.</summary>
    [DataField]
    public string Slot = "dystopia_tongs_grip";

    /// <summary>Что можно зажать (дополнительно к тому, что предмет может раскалиться).</summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public SoundSpecifier? GripSound;

    [DataField]
    public SoundSpecifier? ReleaseSound;

    [DataField]
    public SoundSpecifier? PourSound;
}

/// <summary>
/// Меха: ставятся вплотную к горну и прикручиваются к полу. Пока их качают, горящий горн в радиусе
/// <see cref="Range"/> раздувается (предел жара — BellowsMaxTemperature горна).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaBellowsComponent : Component
{
    /// <summary>Радиус поиска горна, клетки (центр к центру).</summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>Один качок, секунды. Качают, пока не отойдут.</summary>
    [DataField]
    public float PumpDelay = 1f;

    /// <summary>Сколько секунд горн держит жар после одного качка.</summary>
    [DataField]
    public float BlowDuration = 3f;

    /// <summary>Работают только прикрученными к полу.</summary>
    [DataField]
    public bool RequireAnchored = true;

    /// <summary>Сколько секунд показывать сжатые меха после качка.</summary>
    [DataField]
    public float PumpVisualTime = 0.5f;

    [DataField]
    public SoundSpecifier? PumpSound;

    [ViewVariables]
    public bool Pumping;

    [ViewVariables]
    public TimeSpan PumpVisualUntil;
}

/// <summary>
/// Закалка: охлаждающие реагенты (вода...) на раскалённом предмете вскипают и забирают жар.
/// Срабатывает, если ими брызнуть (распылитель, огнетушитель, плеснуть) или налить внутрь.
/// Температуру предмета знает его система (<see cref="DystopiaGetTemperatureEvent"/>, <see cref="DystopiaCoolEvent"/>).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaQuenchableComponent : Component
{
    /// <summary>Раствор, из которого выкипает налитый охладитель.</summary>
    [DataField]
    public string Solution = "default";

    /// <summary>Охладители: на сколько K остывает предмет от одной единицы.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, float> Coolants = new();

    /// <summary>Ниже этой температуры охладитель не вскипает (налитая в холодный тигель вода остаётся водой), K.</summary>
    [DataField]
    public float MinTemperature = 373.15f;

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>Не чаще, чем раз в столько секунд — звук и сообщение.</summary>
    [DataField]
    public float EffectCooldown = 1f;

    [ViewVariables]
    public TimeSpan NextEffect;
}

/// <summary>Узнать температуру предмета (K). Заполняет система, которая её ведёт.</summary>
[ByRefEvent]
public record struct DystopiaGetTemperatureEvent(float? Temperature = null);

/// <summary>Остудить предмет на Kelvin (не ниже окружения).</summary>
[ByRefEvent]
public record struct DystopiaCoolEvent(float Kelvin);

[Serializable, NetSerializable]
public enum DystopiaForgeVisuals : byte
{
    Lit,
    Bellows,
}

[Serializable, NetSerializable]
public enum DystopiaBellowsVisuals : byte
{
    Pumping,
}

[Serializable, NetSerializable]
public enum DystopiaTongsVisuals : byte
{
    Holding,
}

[Serializable, NetSerializable]
public sealed partial class DystopiaBellowsPumpDoAfterEvent : SimpleDoAfterEvent
{
}
