// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: корпус окна КПК Города — вокруг янтарного экрана. Цвета списаны со спрайта КПК своей должности
// (корпус, тень, полоса, метки начальника, огоньки). Задаётся в pdas.yml; окно рисует его на клиенте.

using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Pda;

[Serializable, NetSerializable]
public enum CityPdaCaseStyle : byte
{
    /// <summary>Казённая сталь: крашеный металл, заклёпки, трафарет, планка цвета службы слева.</summary>
    Steel,

    /// <summary>Служебный планшет (Стража): резиновый корпус, уголки цвета службы, клавиши на корпусе.</summary>
    Tablet,
}

[RegisterComponent]
public sealed partial class CityPdaCaseComponent : Component
{
    [DataField]
    public CityPdaCaseStyle Style = CityPdaCaseStyle.Steel;

    /// <summary>Корпус: у стали — краска (главный цвет спрайта), у планшета — тёмная резина.</summary>
    [DataField]
    public Color Body = Color.FromHex("#999875");

    /// <summary>У стали — планка слева, у планшета — уголки-бамперы.</summary>
    [DataField]
    public Color Trim = Color.FromHex("#7B7A5B");

    /// <summary>Кромка и тёмные детали (заклёпки, утопленная рамка экрана).</summary>
    [DataField]
    public Color Edge = Color.FromHex("#353526");

    /// <summary>Цветная полоса спрайта (у сержанта — жёлтая, у фармацевта — оранжевая); нет — без полосы.</summary>
    [DataField]
    public Color? Stripe;

    /// <summary>Метки начальника службы (две цветные риски на спрайте главы).</summary>
    [DataField]
    public List<Color> Marks = new();

    /// <summary>Огоньки сбоку спрайта — горят в шапке корпуса.</summary>
    [DataField]
    public List<Color> Lights = new();

    /// <summary>Цвет трафаретных надписей на корпусе.</summary>
    [DataField]
    public Color Stencil = Color.FromHex("#2A2A1E");

    /// <summary>Клавиши планшета.</summary>
    [DataField]
    public Color Keycap = Color.FromHex("#3A4252");

    /// <summary>Трафарет службы: «ГОРОДСКАЯ СТРАЖА», «САНИТАРНАЯ СЛУЖБА».</summary>
    [DataField]
    public LocId Label = "dystopia-pda-label-citizen";
}
