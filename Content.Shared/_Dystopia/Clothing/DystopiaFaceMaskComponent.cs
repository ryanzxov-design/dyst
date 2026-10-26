// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Speech;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Clothing;

/// <summary>
/// Dystopia: маска, которая меняет голос и пропадает с лица, когда её приспускают (маска Стражи).
/// Пока маска надета и не приспущена — речь и рация идут своим способом речи (свой шрифт и глагол), работает ночное
/// зрение окуляров.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DystopiaFaceMaskComponent : Component
{
    /// <summary>Способ речи в надетой маске (шрифт, кегль, глаголы «чеканит» и т.п.).</summary>
    [DataField]
    public ProtoId<SpeechVerbPrototype>? SpeechVerb;

    /// <summary>Приспущенная маска не рисуется на персонаже (у маски один спрайт — «на лице»).</summary>
    [DataField]
    public bool HideWhenDown = true;

    /// <summary>Ночное зрение маски (NightVision на ней же) работает, только пока маска на лице.</summary>
    [DataField]
    public bool NightVisionOnlyWhenUp = true;
}
