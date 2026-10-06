// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Health.Medical.Addiction;

/// <summary>
/// Зависимость от веществ. Пока вещество в крови, растёт привыкание; без него — медленно спадает.
/// Привыкший без дозы проходит стадии ломки: боль, дрожь, судороги. Чем сильнее привыкание, тем слабее
/// действует обезболивание этой группы (толерантность). Всё настраивается в YAML.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AddictionComponent : Component
{
    /// <summary>Привыкание по группам веществ, 0..100.</summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, float> Levels = new();

    /// <summary>Стадия ломки по группам (0 — нет).</summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, int> Stages = new();

    [ViewVariables]
    public Dictionary<string, TimeSpan> LastDose = new();

    [ViewVariables]
    public Dictionary<string, TimeSpan> NextSymptom = new();

    [ViewVariables]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public Dictionary<string, AddictionGroup> Groups = new()
    {
        {
            "Opioids", new AddictionGroup
            {
                Reagents = new()
                {
                    { "Morphine", 0.02f },
                    { "Promedol", 0.025f },
                    { "Tramadol", 0.006f },
                },
                Antagonists = new() { "Naloxone" },
            }
        },
    };
}

[DataDefinition]
public sealed partial class AddictionGroup
{
    /// <summary>Сколько привыкания в секунду даёт единица вещества в крови.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Reagents = new();

    /// <summary>Антагонисты (налоксон): у зависимого сразу вызывают ломку второй стадии.</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Antagonists = new();

    /// <summary>Сколько привыкания уходит в секунду, пока вещества нет.</summary>
    [DataField]
    public float Decay = 0.01f;

    /// <summary>С какого привыкания начинается зависимость (и ломка без дозы).</summary>
    [DataField]
    public float AddictedLevel = 25f;

    /// <summary>Тяжёлая (третья) стадия ломки возможна только с такого привыкания.</summary>
    [DataField]
    public float SevereLevel = 60f;

    /// <summary>Через сколько после последней дозы начинается каждая стадия.</summary>
    [DataField]
    public List<TimeSpan> StageDelays = new()
    {
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(7),
        TimeSpan.FromMinutes(14),
    };

    /// <summary>Боль ломки по стадиям (1, 2, 3).</summary>
    [DataField]
    public List<float> StagePain = new() { 12f, 30f, 55f };

    /// <summary>При привыкании 100 обезболивание этой группы слабее на эту долю.</summary>
    [DataField]
    public float MaxTolerance = 0.5f;

    [DataField]
    public TimeSpan SymptomInterval = TimeSpan.FromSeconds(40);

    /// <summary>Судороги на третьей стадии: обездвиживание.</summary>
    [DataField]
    public TimeSpan SeizureDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public DamageSpecifier SeizureDamage = new() { DamageDict = { { "Poison", 2 } } };
}
