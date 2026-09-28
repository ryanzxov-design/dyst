using Robust.Shared.Audio;

namespace Content.Shared._Dystopia.FleshCult;

/// <summary>
/// Рёв огнемёта: при стрельбе играет на самом оружии не чаще раза в Interval секунд
/// (огнемёт стреляет 12 раз в секунду — звук на каждый выстрел наложился бы в кашу).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaFlamethrowerSoundComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    /// <summary>Не чаще раза в столько секунд (чуть меньше длины звука — рёв идёт непрерывно).</summary>
    [DataField]
    public float Interval = 1.5f;

    [ViewVariables]
    public TimeSpan NextRoar;
}
