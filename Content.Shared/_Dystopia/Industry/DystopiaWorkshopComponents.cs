// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: мастерская — горн с большим тиглем, литейные желоба, литейный стол со сменными формами.

using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

/// <summary>
/// Литейный желоб — строящаяся конструкция. Соединяется с соседними желобами, как трубы: два желоба связаны, если
/// у обоих есть отверстие друг к другу. Расплав, налитый в любой желоб, расходится по всей связанной сети и по
/// порядку (ближние первыми) заполняет литейные столы и изложницы, к которым подходит отверстие желоба.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaCastingChannelComponent : Component
{
    /// <summary>Отверстия желоба при повороте 0 (поворот конструкции их поворачивает).</summary>
    [DataField(required: true)]
    public List<Direction> Openings = new();

    /// <summary>Сколько желобов расплав обходит за раз (защита от огромных сетей).</summary>
    [DataField]
    public int MaxSegments = 64;

    /// <summary>Сколько секунд показывать бегущий по желобу металл.</summary>
    [DataField]
    public float FlowVisualTime = 2f;

    [DataField]
    public SoundSpecifier? FlowSound;

    [ViewVariables]
    public TimeSpan FlowUntil;

    [ViewVariables]
    public bool Flowing;
}

/// <summary>
/// Наклон тигля, встроенного в конструкцию (горн с большим тиглем): расплав подаётся в присоединённый желоб,
/// а без желоба — в литейные столы и изложницы на соседних клетках.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaTiltPourComponent : Component
{
    /// <summary>Сколько секунд наклонять.</summary>
    [DataField]
    public float Delay = 2f;

    [DataField]
    public SoundSpecifier? PourSound;
}

/// <summary>
/// Тигель-предмет, который ставят на горн: клик им по горну — установка (DoAfter), горн превращается
/// в <see cref="Mounted"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaForgeMountableComponent : Component
{
    /// <summary>Конструкция «горн с этим тиглем».</summary>
    [DataField(required: true)]
    public EntProtoId Mounted;

    /// <summary>Сколько секунд ставить.</summary>
    [DataField]
    public float Delay = 4f;
}

/// <summary>
/// Горн с установленным тиглем. Действие «Снять тигель» (DoAfter) возвращает обычный горн <see cref="Forge"/>
/// и тигель-предмет <see cref="Item"/>; содержимое, жар и топливо переносятся.
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaMountedCrucibleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Forge;

    [DataField(required: true)]
    public EntProtoId Item;

    /// <summary>Сколько секунд снимать.</summary>
    [DataField]
    public float Delay = 4f;
}

/// <summary>
/// Сменная литейная форма для литейного стола: какая отливка и сколько расплава вмещает
/// (форма ×10 — десять отливок за заливку).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaCastingFormComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DystopiaMouldShapePrototype> Shape;

    /// <summary>Сколько расплава вмещает форма.</summary>
    [DataField(required: true)]
    public float Volume = 10f;

    /// <summary>Как форма выглядит, вставленная в стол (значение для GenericVisualizer).</summary>
    [DataField(required: true)]
    public string Visual = string.Empty;
}

[Serializable, NetSerializable]
public enum DystopiaCastingChannelVisuals : byte
{
    Flowing,
}

[Serializable, NetSerializable]
public enum DystopiaCastingTableVisuals : byte
{
    Form,
}

[Serializable, NetSerializable]
public sealed partial class DystopiaTiltPourDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class DystopiaForgeMountDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class DystopiaForgeUnmountDoAfterEvent : SimpleDoAfterEvent
{
}
