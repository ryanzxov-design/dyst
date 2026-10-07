namespace Content.Server._Goobstation.Virology;

[ByRefEvent]
public record struct VirologyMachineCheckEvent(bool Cancelled = false);

public record struct VirologyMachineDoneEvent(bool Success);
