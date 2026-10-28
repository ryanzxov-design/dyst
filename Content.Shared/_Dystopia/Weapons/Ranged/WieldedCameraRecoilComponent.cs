// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: отдача в камеру меньше, когда оружие держат двумя руками. По мотивам BkmGunWieldBonus из Backmen
// (github.com/Rxup/space-station-14, AGPL-3.0). Система — WieldedCameraRecoilSystem.

namespace Content.Shared._Dystopia.Weapons.Ranged;

/// <summary>
/// Пока оружие держат двумя руками, к множителю отдачи в камеру (<c>Gun.cameraRecoilScalar</c>)
/// прибавляется <see cref="CameraRecoilScalar"/> (обычно отрицательное число — тряска меньше).
/// </summary>
[RegisterComponent]
public sealed partial class WieldedCameraRecoilComponent : Component
{
    [DataField]
    public float CameraRecoilScalar;
}
