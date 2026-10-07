using System;

namespace Content.Shared._Goobstation.Virology;

[RegisterComponent]
public sealed partial class ActiveVirologyMachineComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;
}
