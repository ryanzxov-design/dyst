// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: мазутная форсунка — навесной блок вплотную к горну (смотрит на него). Жидкое топливо в бак — ведром или
// жидкостным экспортером. Включённая сама разжигает горн, подкидывает ему топливо и дует под давлением — горн держит
// жар как с мехами, качать не нужно.

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Industry;

[RegisterComponent]
public sealed partial class DystopiaFuelBurnerComponent : Component
{
    [DataField]
    public string Solution = "fuel";

    /// <summary>Топливо → сколько секунд горения горна даёт единица.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, float> Fuels = new();

    /// <summary>Сколько единиц в секунду сжигает, пока горн не полон.</summary>
    [DataField]
    public float Rate = 0.5f;

    [DataField]
    public float Interval = 1f;

    [DataField]
    public bool On;

    [ViewVariables]
    public bool Burning;

    [ViewVariables]
    public TimeSpan NextUpdate;
}

[Serializable, NetSerializable]
public enum DystopiaFuelBurnerVisuals : byte
{
    Burning,
}
