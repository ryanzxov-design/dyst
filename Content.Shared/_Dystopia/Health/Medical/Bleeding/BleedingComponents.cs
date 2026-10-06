// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Health.Medical.Bleeding;

/// <summary>
/// Рана, которая может кровоточить (порез, укол, сильный ушиб). Кровотечение тела — сумма кровотечений его ран.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BleedInflicterComponent : Component
{
    /// <summary>Рана начинает кровоточить с такой тяжести.</summary>
    [DataField]
    public FixedPoint2 SeverityThreshold = 4;

    /// <summary>Сила кровотечения на единицу тяжести раны.</summary>
    [DataField]
    public float BleedingCoefficient = 0.08f;

    /// <summary>Через сколько секунд небольшая рана свернётся сама.</summary>
    [DataField]
    public float ClotTime = 120f;

    /// <summary>Раны тяжелее этого сами не сворачиваются — нужна перевязка, жгут или операция.</summary>
    [DataField]
    public FixedPoint2 ClotSeverityLimit = 25;

    [ViewVariables, AutoNetworkedField]
    public bool IsBleeding;

    /// <summary>Перевязана: не кровоточит, пока рану снова не разбередят.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool Bandaged;

    [ViewVariables, AutoNetworkedField]
    public float BleedingAmount;

    /// <summary>
    /// Рана слишком глубокая для повязки, но её туго забинтовали: кровь идёт слабее (доля от обычного).
    /// 1 — не перевязана. Сбрасывается, когда рану разбередят.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float PackedFactor = 1f;

    [ViewVariables]
    public TimeSpan BleedingStarted;
}

/// <summary>Тело, у которого кровотечение считается по ранам (ванильное «кровотечение от урона» отключено).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WoundBleedingComponent : Component
{
    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>Что мы выставили в прошлый раз — чтобы заметить изменения от лекарств (транексамовая кислота, гепарин).</summary>
    [ViewVariables]
    public float LastSetBleed;

    /// <summary>Поправка от лекарств к кровотечению ран (минус — останавливают, плюс — разжижают кровь).</summary>
    [ViewVariables]
    public float ExternalModifier;

    /// <summary>Как быстро действие лекарств на кровотечение сходит на нет (в секунду).</summary>
    [DataField]
    public float ExternalDecay = 0.02f;

    /// <summary>Как часто пересчитывать кровотечение тела.</summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>С какой силы кровотечение части считается сильным (анализатор).</summary>
    [DataField]
    public float HeavyBleeding = 1.5f;

    /// <summary>Сколько жгут можно держать без вреда.</summary>
    [DataField]
    public TimeSpan TourniquetSafeTime = TimeSpan.FromMinutes(5);

    /// <summary>За сколько до конца безопасного времени конечность немеет (предупреждение).</summary>
    [DataField]
    public TimeSpan TourniquetWarning = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan NecrosisInterval = TimeSpan.FromSeconds(15);

    /// <summary>Урон конечности под жгутом после безопасного времени, раз в NecrosisInterval.</summary>
    [DataField]
    public DamageSpecifier NecrosisDamage = new() { DamageDict = { { "Cellular", 2 } } };

    /// <summary>
    /// Обычная повязка (марля, бинт) останавливает только раны не тяжелее этого. Глубже — лишь ослабляет кровотечение:
    /// нужна медицинская нить, гемостатическая губка, жгут или операция.
    /// </summary>
    [DataField]
    public float BandageMaxSeverity = 30f;

    /// <summary>Глубокая рана под повязкой кровоточит с такой долей от обычного.</summary>
    [DataField]
    public float BandagePackedFactor = 0.35f;

    /// <summary>Повреждённые вены части: её раны кровоточат во столько раз сильнее и не сворачиваются сами.</summary>
    [DataField]
    public float VeinBleedMultiplier = 2f;

    /// <summary>Сколько длится прижигание раны прижигателем.</summary>
    [DataField]
    public TimeSpan CauterizeTime = TimeSpan.FromSeconds(3);

    /// <summary>Ожог от прижигания (в ту часть, которую прижигают).</summary>
    [DataField]
    public DamageSpecifier CauterizeDamage = new() { DamageDict = { { "Heat", 4 } } };

    /// <summary>Предмет, который выдаётся при снятии жгута.</summary>
    [DataField]
    public EntProtoId TourniquetItem = "Tourniquet";
}

/// <summary>Жгут на руке или ноге: кровотечение ниже перекрыто, но без крови конечность со временем отмирает.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TourniquetAppliedComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public TimeSpan AppliedAt;

    [ViewVariables]
    public bool Warned;

    [ViewVariables]
    public TimeSpan NextNecrosis;
}

/// <summary>
/// Наложена повязка или жгут (вызывается из ванильного лечения вместо простого «уменьшить кровотечение»).
/// Handled — значит, кровотечение обработала система ран.
/// </summary>
[ByRefEvent]
public record struct BandageAppliedEvent(EntityUid User, EntityUid Item, float BloodlossModifier, bool Handled = false);

/// <summary>
/// Рана-ожог прижигает: если она за раз потяжелела на Threshold и больше, кровотечение части останавливается
/// (огонь, раскалённый металл, лазер).
/// </summary>
[RegisterComponent]
public sealed partial class CauterizingWoundComponent : Component
{
    [DataField]
    public FixedPoint2 Threshold = 5;
}

[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class CauterizeDoAfterEvent : Content.Shared.DoAfter.DoAfterEvent
{
    public NetEntity Part;

    public CauterizeDoAfterEvent(NetEntity part)
    {
        Part = part;
    }

    public override Content.Shared.DoAfter.DoAfterEvent Clone() => this;
}

/// <summary>Гемостатическое средство (губка): повязка им останавливает раны глубже обычного предела.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HemostaticComponent : Component
{
    /// <summary>До какой тяжести раны останавливает (0 — любые).</summary>
    [DataField]
    public float MaxSeverity;
}
