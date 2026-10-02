// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: слой совместимости системы здоровья с новой системой тела.
//
// В системе здоровья часть тела — компонент BodyPart со своим типом, стороной и ссылкой на тело.
// У нас часть тела — орган-сущность (Torso, Head, ArmLeft...). Этот компонент вешается на такие органы
// (через базовые прототипы органов) и даёт системе здоровья её часть тела. Поля — те, что использует ;
// остальные поля добавляются в следующих фазах вместе с механиками, которым они нужны
// (целостность и раны — Ф3, слоты и полость для хирургии — Ф8).

using Content.Shared._Dystopia.Health.Body.Part;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Part;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyPartComponent : Component
{
    /// <summary>Тело, в котором сейчас эта часть (null — часть отделена). Синхронизируется с OrganComponent.Body.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    [DataField, AutoNetworkedField]
    public BodyPartType PartType = BodyPartType.Other;

    [DataField, AutoNetworkedField]
    public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;

    /// <summary>Часть работает (отключается при тяжёлых ранах: рука не держит, нога не ходит).</summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public bool CanEnable = true;

    [DataField]
    public bool CanAttachChildren = true;

    [DataField]
    public BodyPartComposition PartComposition = BodyPartComposition.Organic;

    [DataField, AutoNetworkedField]
    public string Species { get; set; } = "";

    [DataField, AutoNetworkedField]
    public string? BaseLayerId;

    /// <summary>Компоненты, которые часть добавляет телу при прикреплении.</summary>
    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? OnAdd;

    /// <summary>Компоненты, которые часть убирает у тела при отделении.</summary>
    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? OnRemove;
}
