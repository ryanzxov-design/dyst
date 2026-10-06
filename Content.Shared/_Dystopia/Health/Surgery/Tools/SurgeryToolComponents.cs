// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос Shitmed (Goob-Station, AGPL-3.0): хирургические инструменты. Один предмет может быть
// несколькими инструментами сразу (зажим = зажим + пинцет + инструмент для обработки ран).

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Dystopia.Health.Surgery.Tools;

public interface ISurgeryToolComponent
{
    /// <summary>Ключ локализации названия инструмента.</summary>
    public string ToolName { get; }

    /// <summary>Одноразовый инструмент: тратится при использовании.</summary>
    public bool? Used { get; set; }

    /// <summary>Множитель скорости шага этим инструментом.</summary>
    public float Speed { get; set; }
}

/// <summary>Предмет — хирургический инструмент (открывает окно операции, звуки).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryToolComponent : Component
{
    /// <summary>Не требовать включения (у приборов с переключателем).</summary>
    [DataField]
    public bool IgnoreToggle;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? StartSound;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? EndSound;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ScalpelComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-scalpel";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class RetractorComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-retractor";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class HemostatComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-hemostat";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class BoneSawComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-bonesaw";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CauteryComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-cautery";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DrillComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-drill";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class BoneSetterComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-bonesetter";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class BoneGelComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-bonegel";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class TweezersComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-tweezers";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

/// <summary>Для обработки ран и тканей (зажим, импровизированные инструменты).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TendingComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-tending";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

/// <summary>Нить, которой сшивают ткани (медицинская нить).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StitchesComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "surgery-tool-name-stitches";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

/// <summary>Вызывается на инструменте перед шагом: Cancelled — инструментом сейчас нельзя (выключен).</summary>
[ByRefEvent]
public record struct SurgeryToolUsedEvent(EntityUid User, EntityUid Target, bool IgnoreToggle = false, bool Cancelled = false);
