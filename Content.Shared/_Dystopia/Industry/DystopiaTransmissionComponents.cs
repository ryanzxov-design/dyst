// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: энергетика, шаг 4 — передача на расстояние.
//
// ЛЭП: опоры, провода между ними (моток провода — тычок в одну опору, затем в другую, пролёт до MaxSpan клеток).
// Опоры, связанные проводами, — одна высоковольтная сеть (узел DystopiaPylonNode), к ней подключаются кабелем под
// опорой. Провода рисует клиент.
// Повышающие трансформаторы: берут ток из нижней сети как потребитель и отдают в верхнюю как генератор, через буфер
// на секунду работы (отдаётся только уже полученное, с потерями).
// Нефтепровод: два терминала, связанные закопанной трубой (стопка секций — тычок в один терминал, затем в другой).
// Насос отправляющего терминала гонит жидкость из своего бака в бак принимающего. Вдоль трассы — колодцы; повреждённый
// колодец течёт, разрушенный — рвёт трубу.

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

/// <summary>Опора ЛЭП.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DystopiaPylonComponent : Component
{
    /// <summary>С какими опорами связана проводами.</summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> Links = new();

    /// <summary>Самый длинный пролёт, клеток.</summary>
    [DataField]
    public float MaxSpan = 12f;

    /// <summary>Сколько проводов (пролётов) можно повесить на одну опору.</summary>
    [DataField]
    public int MaxLinks = 4;

    [DataField]
    public string Node = "power";

    /// <summary>Точка крепления проводов относительно центра клетки, клеток (для отрисовки).</summary>
    [DataField]
    public Vector2 Attach = new(0f, 1.08f);

    /// <summary>Фазы: сдвиг каждого провода по горизонтали от точки крепления, клеток (изоляторы на траверсе).</summary>
    [DataField]
    public List<float> Phases = new() { -0.36f, 0f, 0.36f };

    /// <summary>Толщина провода на экране, клеток.</summary>
    [DataField]
    public float WireWidth = 0.08f;

    /// <summary>Что возвращается при снятии провода (стопка на одну клетку пролёта).</summary>
    [DataField]
    public EntProtoId WireStack = "DystopiaPylonWire1";
}

/// <summary>Моток провода ЛЭП / стопка секций трубы: первая опора (терминал), к которой уже прицепили конец.</summary>
[RegisterComponent]
public sealed partial class DystopiaLinkerComponent : Component
{
    /// <summary>Клеток на одну штуку из стопки (провод — 1, секция трубы — 4).</summary>
    [DataField]
    public float TilesPerItem = 1f;

    [ViewVariables]
    public EntityUid? First;
}

/// <summary>Повышающий трансформатор: нижняя сеть → верхняя.</summary>
[RegisterComponent]
public sealed partial class DystopiaTransformerComponent : Component
{
    /// <summary>Предел мощности на выходе, Вт.</summary>
    [DataField]
    public float MaxPower = 30000f;

    [DataField]
    public float Efficiency = 0.95f;

    /// <summary>Буфер — столько секунд работы на пределе (сглаживает и не даёт отдать неполученное).</summary>
    [DataField]
    public float BufferSeconds = 1f;

    /// <summary>Сколько сверх расхода наверху брать снизу (запас, чтобы мощность могла расти).</summary>
    [DataField]
    public float Headroom = 0.2f;

    [DataField]
    public float MinDraw = 500f;

    /// <summary>Накоплено, Дж.</summary>
    [ViewVariables]
    public float Buffer;
}

/// <summary>Терминал нефтепровода.</summary>
[RegisterComponent]
public sealed partial class DystopiaPipelineTerminalComponent : Component
{
    [DataField]
    public string Solution = "tank";

    [DataField]
    public EntityUid? Linked;

    [DataField]
    public DystopiaPipelineMode Mode = DystopiaPipelineMode.Stop;

    /// <summary>Сколько гонит насос, ед/с (до MaxRate).</summary>
    [DataField]
    public float Rate = 30f;

    [DataField]
    public float MaxRate = 30f;

    /// <summary>Самая длинная трасса, клеток.</summary>
    [DataField]
    public float MaxLength = 80f;

    /// <summary>Колодцы через столько клеток.</summary>
    [DataField]
    public int MarkerSpacing = 4;

    [DataField]
    public EntProtoId Marker = "DystopiaPipelineMarker";

    [DataField]
    public EntProtoId SectionStack = "DystopiaPipelineSection1";

    /// <summary>Сколько секций ушло на трассу (вернутся при разборке).</summary>
    [DataField]
    public int Sections;

    /// <summary>Колодцы трассы — от этого терминала к другому (только у того, кто прокладывал).</summary>
    [DataField]
    public List<EntityUid> Markers = new();

    /// <summary>Места колодцев (если колодец уничтожен — труба порвана здесь). Трасса может идти через несколько
    /// сеток (фундамент и грунт планеты), поэтому — координаты, а не клетки одной сетки.</summary>
    [DataField]
    public List<EntityCoordinates> MarkerSpots = new();

    /// <summary>Доля перекачки, что уходит в течь из повреждённого колодца.</summary>
    [DataField]
    public float LeakShare = 0.25f;

    /// <summary>Урон колодца, с которого он течёт и с которого рвёт трубу.</summary>
    [DataField]
    public float LeakDamage = 50f;

    [DataField]
    public float BreakDamage = 150f;

    [ViewVariables]
    public float LastFlow;

    [ViewVariables]
    public int Leaks;

    [ViewVariables]
    public bool Broken;
}

/// <summary>Колодец трассы нефтепровода (метка; состояние ведёт терминал).</summary>
[RegisterComponent]
public sealed partial class DystopiaPipelineMarkerComponent : Component
{
    [DataField]
    public EntityUid? Terminal;
}

[Serializable, NetSerializable]
public enum DystopiaPipelineMode : byte
{
    Stop,
    Send,
    Receive,
}

[Serializable, NetSerializable]
public enum DystopiaPipelineUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DystopiaPipelineVisuals : byte
{
    Mode,
    Leak,
}

[Serializable, NetSerializable]
public sealed class DystopiaPipelineUiState(
    bool linked,
    int length,
    DystopiaPipelineMode mode,
    float rate,
    float maxRate,
    float volume,
    float maxVolume,
    string contents,
    float flow,
    int leaks,
    bool broken,
    bool powered) : BoundUserInterfaceState
{
    public readonly bool Linked = linked;
    public readonly int Length = length;
    public readonly DystopiaPipelineMode Mode = mode;
    public readonly float Rate = rate;
    public readonly float MaxRate = maxRate;
    public readonly float Volume = volume;
    public readonly float MaxVolume = maxVolume;
    public readonly string Contents = contents;
    public readonly float Flow = flow;
    public readonly int Leaks = leaks;
    public readonly bool Broken = broken;
    public readonly bool Powered = powered;
}

[Serializable, NetSerializable]
public sealed class DystopiaPipelineModeMessage(DystopiaPipelineMode mode) : BoundUserInterfaceMessage
{
    public readonly DystopiaPipelineMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class DystopiaPipelineRateMessage(float rate) : BoundUserInterfaceMessage
{
    public readonly float Rate = rate;
}
