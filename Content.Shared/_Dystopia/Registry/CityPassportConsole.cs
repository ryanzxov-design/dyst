// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: «Паспортный стол» — консоль Гражданской Инспекции для ID-карт граждан. Список граждан, все карты
// гражданина (серийный номер, кем и когда выдана, жива или аннулирована), аннулирование карты, выдача новой
// (бесплатно, без ограничений), восстановление в должности после отстранения.

using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Registry;

[RegisterComponent]
public sealed partial class CityPassportConsoleComponent : Component
{
    /// <summary>Выдают и аннулируют карты, восстанавливают в должности.</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> IssueJobs = new();

    /// <summary>Распоряжаются картами граждан вне учёта лояльности (Консула).</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> SupremeJobs = new();

    /// <summary>Бланк на случай, если у должности в стартовом снаряжении нет карты.</summary>
    [DataField]
    public EntProtoId? FallbackCard;

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public TimeSpan NextPrint;

    /// <summary>Открытый на консоли гражданин.</summary>
    [ViewVariables]
    public int? Selected;
}

[Serializable, NetSerializable]
public enum CityPassportConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CityPassportListEntry
{
    public int Number;
    public string Name = string.Empty;
    public string JobTitle = string.Empty;
    public CityCitizenStatus Status;
    public bool Dismissed;
    public bool Outside;

    /// <summary>Сколько карт действует (не аннулированы и не уничтожены).</summary>
    public int LiveCards;
}

[Serializable, NetSerializable]
public sealed class CityPassportDossier
{
    public int Number;
    public string Name = string.Empty;
    public string JobTitle = string.Empty;
    public CityCitizenStatus Status;
    public bool Dismissed;
    public bool Outside;
    public HumanoidCharacterProfile? Appearance;
    public List<CityIdCardEntry> Cards = new();
}

[Serializable, NetSerializable]
public sealed class CityPassportConsoleUiState(
    List<CityPassportListEntry> citizens,
    CityPassportDossier? dossier,
    bool canEdit,
    bool canSupreme) : BoundUserInterfaceState
{
    public readonly List<CityPassportListEntry> Citizens = citizens;
    public readonly CityPassportDossier? Dossier = dossier;
    public readonly bool CanEdit = canEdit;
    public readonly bool CanSupreme = canSupreme;
}

[Serializable, NetSerializable]
public sealed class CityPassportSelectMessage(int? number) : BoundUserInterfaceMessage
{
    public readonly int? Number = number;
}

[Serializable, NetSerializable]
public sealed class CityPassportAnnulMessage(int number, int card, string reason) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
    public readonly int Card = card;
    public readonly string Reason = reason;
}

[Serializable, NetSerializable]
public sealed class CityPassportIssueMessage(int number) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
}

[Serializable, NetSerializable]
public sealed class CityPassportReinstateMessage(int number) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
}
