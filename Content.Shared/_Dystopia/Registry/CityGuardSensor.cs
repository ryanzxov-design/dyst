// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: датчик Стражи — в маске стражника. Всегда включён на координаты и состояние, выключить нельзя; работает,
// пока маска надета (и приспущенная тоже), снятая маска данных не даёт. Видит его только соционадзор (вкладка
// «Дозор»), не медицинские мониторы. Тяжёлое состояние и смерть стражника, сигнал бедствия из маски — тревоги.

using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Registry;

[RegisterComponent]
public sealed partial class CityGuardSensorComponent : Component
{
    /// <summary>С какого урона стражник считается раненым.</summary>
    [DataField]
    public FixedPoint2 HurtDamage = 40;

    /// <summary>Сколько секунд пути хранить для следа на карте.</summary>
    [DataField]
    public TimeSpan TrailLength = TimeSpan.FromSeconds(30);

    /// <summary>Сигнал бедствия — не чаще раза в этот срок (и у действия такая же перезарядка).</summary>
    [DataField]
    public TimeSpan DistressCooldown = TimeSpan.FromSeconds(30);

    /// <summary>Кто носит маску сейчас.</summary>
    [ViewVariables]
    public EntityUid? Wearer;

    /// <summary>Номер последнего носителя (снятая маска числится за ним).</summary>
    [ViewVariables]
    public int? LastWearerNumber;

    [ViewVariables]
    public CityGuardHealth Health = CityGuardHealth.NoData;

    /// <summary>Последнее известное место (сетка и точка на ней) и когда оно было.</summary>
    [ViewVariables]
    public EntityUid? LastGrid;

    [ViewVariables]
    public Vector2 LastPosition;

    [ViewVariables]
    public TimeSpan? LastSeenAt;

    /// <summary>След: точки за последние секунды, старые — первыми.</summary>
    [ViewVariables]
    public List<(TimeSpan Time, Vector2 Position)> Trail = new();

    [ViewVariables]
    public TimeSpan NextDistress;
}

[Serializable, NetSerializable]
public enum CityGuardHealth : byte
{
    Ok,
    Hurt,
    Critical,
    Dead,

    /// <summary>Маска снята — данных нет.</summary>
    NoData,
}

/// <summary>Действие маски Стражи «Сигнал бедствия».</summary>
public sealed partial class CityGuardDistressEvent : InstantActionEvent;
