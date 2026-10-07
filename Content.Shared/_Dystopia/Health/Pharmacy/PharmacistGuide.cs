// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: «Справочник фармацевта» — древо синтеза лекарств. Справочник строится на клиенте из прототипов
// реакций и реагентов при открытии, поэтому новый препарат с рецептом появляется в нём сам.
// Окно — Content.Client/_Dystopia/Health/Pharmacy/

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Health.Pharmacy;

[Serializable, NetSerializable]
public enum PharmacistGuideUiKey : byte
{
    Key,
}

/// <summary>
/// Настройки справочника: откуда брать сырьё, какие группы реагентов считать препаратами и подсказки,
/// где добыть вещества без рецепта.
/// </summary>
[Prototype]
public sealed partial class PharmacistGuidePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Раздатчики: всё, что лежит в их наполнении (канистры), считается сырьём «из раздатчика».
    /// </summary>
    [DataField]
    public List<EntProtoId> Dispensers = new();

    /// <summary>
    /// Группы реагентов, которые попадают в список препаратов.
    /// </summary>
    [DataField]
    public List<string> Groups = new() { "Medicine" };

    /// <summary>
    /// Вещества, которые всегда считаются сырьём, даже если у них есть рецепт (воду берут из крана, а не синтезируют).
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Raw = new();

    /// <summary>
    /// Подсказка, где добыть вещество без рецепта.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, LocId> Sources = new();
}
