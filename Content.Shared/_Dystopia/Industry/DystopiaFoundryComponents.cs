// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: ручное литьё — горн, тигель, изложница. Все числа — поля прототипов.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Industry;

/// <summary>
/// Тигель: внутри куски для плавки (контейнер) и раствор-расплав. У тигля своя температура: греется от горна,
/// остывает сам. Кусок плавится, когда температура дошла до его точки плавки (<see cref="DystopiaSmeltablePrototype"/>).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaCrucibleComponent : Component
{
    /// <summary>Имя раствора-расплава (id компонента Solution).</summary>
    [DataField]
    public string Solution = "crucible";

    /// <summary>Контейнер с кусками, ждущими плавки.</summary>
    [DataField]
    public string ChargeContainer = "dystopia_crucible_charge";

    /// <summary>Сколько единиц материала (100 = кусок руды / слиток) влезает в тигель кусками.</summary>
    [DataField]
    public int MaxCharge = 1000;

    /// <summary>Сколько единиц материала плавится в секунду, когда тигель достаточно горяч.</summary>
    [DataField]
    public float MeltRate = 100f;

    /// <summary>Остывание: доля разницы с окружением, теряемая за секунду.</summary>
    [DataField]
    public float CoolingRate = 0.005f;

    /// <summary>Температура окружения, если вокруг нет атмосферы, K.</summary>
    [DataField]
    public float FallbackAmbient = 293.15f;

    /// <summary>Выше этой температуры тигель раскалён: только клещами, K.</summary>
    [DataField]
    public float HotThreshold = 333.15f;

    /// <summary>Выше этой температуры тигель светится, K.</summary>
    [DataField]
    public float GlowThreshold = 873.15f;

    /// <summary>
    /// Чужие материалы в куске (нет прототипа плавки, например стекло в стальном шкафу) выгорают в этот реагент
    /// (шлак) — столько единиц на 100 единиц материала.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> ForeignSlag;

    [DataField]
    public FixedPoint2 ForeignSlagPer100 = 5;

    /// <summary>Шлак, который снимают с тигля.</summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> SlagReagent;

    /// <summary>Что получается из снятого шлака и сколько шлака на один кусок.</summary>
    [DataField(required: true)]
    public EntProtoId SlagLump;

    [DataField]
    public FixedPoint2 SlagPerLump = 5;

    /// <summary>Как часто считать нагрев и плавку, секунды.</summary>
    [DataField]
    public float UpdateInterval = 0.5f;

    // --- состояние ---

    [ViewVariables]
    public float Temperature = 293.15f;

    /// <summary>Источник жара (горн) задаёт, к какой температуре и как быстро греть, и до какого момента.</summary>
    [ViewVariables]
    public float HeatTarget;

    [ViewVariables]
    public float HeatRate;

    [ViewVariables]
    public TimeSpan HeatedUntil;

    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>Сколько единиц материала текущего куска уже расплавлено (плавка идёт постепенно).</summary>
    [ViewVariables]
    public float MeltProgress;
}

/// <summary>
/// Горн: на него ставят тигель, внутрь кидают топливо, разжигают огнём. Без мехов тянет до BaseMaxTemperature,
/// пока качают меха рядом (<see cref="DystopiaBellowsComponent"/>) — до BellowsMaxTemperature (железо и сталь).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaForgeComponent : Component
{
    /// <summary>Топливо по материалу: секунд горения на 100 единиц материала.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<MaterialPrototype>, float> FuelMaterials = new();

    /// <summary>Сколько секунд горения влезает в горн.</summary>
    [DataField]
    public float MaxFuel = 900f;

    /// <summary>Предел жара без мехов, K.</summary>
    [DataField]
    public float BaseMaxTemperature = 1373.15f;

    /// <summary>Предел жара, пока качают меха, K.</summary>
    [DataField]
    public float BellowsMaxTemperature = 1973.15f;

    /// <summary>Скорость нагрева тигля на горне, K в секунду.</summary>
    [DataField]
    public float HeatRate = 12f;

    /// <summary>С мехами топливо горит во столько раз быстрее.</summary>
    [DataField]
    public float BellowsFuelMultiplier = 2f;

    /// <summary>Как часто горн жжёт топливо и греет тигель, секунды.</summary>
    [DataField]
    public float UpdateInterval = 0.5f;

    [ViewVariables]
    public TimeSpan NextUpdate;

    // --- состояние ---

    [DataField]
    public float Fuel;

    [DataField]
    public bool Lit;

    [ViewVariables]
    public TimeSpan BellowsUntil;
}

/// <summary>
/// Изложница: в неё льют расплав, он остывает; ниже точки плавления основного металла и при наборе объёма формы
/// выходит отливка (или брак, если металл грязный или недолили).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaMouldComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DystopiaMouldShapePrototype> Shape;

    [DataField]
    public string Solution = "mould";

    /// <summary>Остывание: доля разницы с окружением, теряемая за секунду.</summary>
    [DataField]
    public float CoolingRate = 0.01f;

    [DataField]
    public float FallbackAmbient = 293.15f;

    [DataField]
    public float HotThreshold = 333.15f;

    [DataField]
    public float UpdateInterval = 0.5f;

    [ViewVariables]
    public TimeSpan NextUpdate;
}
