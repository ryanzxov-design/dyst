using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Economy;

/// <summary>
/// Штрафной терминал Стражи. Стражник выбирает статью Свода законов и сумму, затем применяет терминал
/// на нарушителе. Штраф списывается с активной карты нарушителя, остаток уходит в долг. Деньги — в казну.
/// </summary>
[RegisterComponent]
public sealed partial class FineTerminalComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public int PendingAmount;

    [ViewVariables(VVAccess.ReadWrite)]
    public string PendingArticle = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public string PendingReason = string.Empty;

    /// <summary>Сколько секунд выписывается штраф (нарушитель может уйти).</summary>
    [DataField]
    public float FineDelay = 3f;

    [DataField]
    public int MaxAmount = 100000;
}

[Serializable, NetSerializable]
public enum FineTerminalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FineTerminalLawEntry(int id, int number, string title, string sanction)
{
    public readonly int Id = id;
    public readonly int Number = number;
    public readonly string Title = title;
    public readonly string Sanction = sanction;
}

[Serializable, NetSerializable]
public sealed class FineTerminalUiState(List<FineTerminalLawEntry> laws, int pendingAmount, string pendingArticle, string pendingReason)
    : BoundUserInterfaceState
{
    public readonly List<FineTerminalLawEntry> Laws = laws;
    public readonly int PendingAmount = pendingAmount;
    public readonly string PendingArticle = pendingArticle;
    public readonly string PendingReason = pendingReason;
}

/// <summary>Подготовить штраф: статья (внутренний id статьи, -1 — без статьи), сумма, пояснение.</summary>
[Serializable, NetSerializable]
public sealed class FineTerminalPrepareMessage(int lawId, int amount, string reason) : BoundUserInterfaceMessage
{
    public readonly int LawId = lawId;
    public readonly int Amount = amount;
    public readonly string Reason = reason;
}

[Serializable, NetSerializable]
public sealed class FineTerminalClearMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class FineTerminalDoAfterEvent : SimpleDoAfterEvent
{
}
