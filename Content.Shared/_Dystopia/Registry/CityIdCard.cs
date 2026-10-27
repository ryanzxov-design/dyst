// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: ID-карты граждан. У каждой карты серийный номер «К-377410-02» (номер гражданина и порядковый номер карты),
// карта навсегда привязана к гражданину. Карту можно аннулировать удалённо (доступы сняты, от счёта отвязана, но
// камеры по ней всё равно узнают владельца); отстранение от должности приостанавливает доступы живых карт.
// Вся логика — на сервере (CityRegistrySystem, партиал Cards).

using Content.Shared.Access;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Registry;

/// <summary>Метка на ID-карте гражданина: чья она и какая по счёту.</summary>
[RegisterComponent]
public sealed partial class CityIdCardComponent : Component
{
    /// <summary>Номер гражданина-владельца.</summary>
    [ViewVariables]
    public int Number;

    /// <summary>Порядковый номер карты у этого гражданина (1 — карта, с которой он вошёл в Город).</summary>
    [ViewVariables]
    public int Index;

    [ViewVariables]
    public string Serial = string.Empty;

    /// <summary>Аннулирована: доступов нет, от счёта отвязана. Обратно не включается.</summary>
    [ViewVariables]
    public bool Annulled;

    /// <summary>
    /// Доступы, снятые отстранением от должности (null — карта не приостановлена). Восстановление в должности
    /// возвращает их.
    /// </summary>
    [ViewVariables]
    public HashSet<ProtoId<AccessLevelPrototype>>? SuspendedTags;
}

/// <summary>Карта в личном деле гражданина (сервер).</summary>
public sealed class CityIdCardRecord
{
    [ViewVariables] public int Index;
    [ViewVariables] public string Serial = string.Empty;
    [ViewVariables] public TimeSpan IssuedAt;
    [ViewVariables] public string IssuedBy = string.Empty;
    [ViewVariables] public bool Annulled;
    [ViewVariables] public TimeSpan AnnulledAt;
    [ViewVariables] public string AnnulledBy = string.Empty;
    [ViewVariables] public string AnnulReason = string.Empty;

    /// <summary>Сама карта (может быть уже уничтожена).</summary>
    [ViewVariables] public EntityUid? Card;
}

/// <summary>Карта для показа в консолях.</summary>
[Serializable, NetSerializable]
public sealed class CityIdCardEntry
{
    public int Index;
    public string Serial = string.Empty;
    public TimeSpan IssuedAt;
    public string IssuedBy = string.Empty;
    public bool Annulled;
    public TimeSpan AnnulledAt;
    public string AnnulledBy = string.Empty;
    public string AnnulReason = string.Empty;

    /// <summary>Карта уничтожена (её больше нет в мире).</summary>
    public bool Destroyed;

    /// <summary>Доступы приостановлены отстранением.</summary>
    public bool Suspended;
}
