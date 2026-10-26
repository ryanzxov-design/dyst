// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: луч фонарика точно за курсором. В боевом режиме персонаж поворачивается к курсору шагами (поворот идёт
// через сеть, мелкие движения мыши не отправляются — см. dystopia.aim_rotation_tolerance), и конус света, который
// крутится вместе с ним (autoRot), шёл бы теми же шагами. Поэтому у своего персонажа каждый кадр доворачиваем сами
// светящие вещи (в руках и надетые; лежат в контейнере, их собственный спрайт не рисуется) на точный угол до курсора —
// только у себя, по сети ничего не идёт. Вне боевого режима доворот снимается.
// (Поле Rotation у света менять нельзя: им владеет SharedPointLightSystem.)

using Content.Shared.Inventory;
using Content.Shared.MouseRotator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client._Dystopia.Combat;

public sealed class DystopiaAimLightSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _aimed = new();
    private readonly HashSet<EntityUid> _seen = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _seen.Clear();

        if (_player.LocalEntity is { } player
            && HasComp<MouseRotatorComponent>(player)
            && _input.MouseScreenPosition.IsValid
            && _eye.PixelToMap(_input.MouseScreenPosition) is { } mouse
            && mouse.MapId != MapId.Nullspace)
        {
            var aim = (mouse.Position - _transform.GetWorldPosition(player)).ToWorldAngle();
            foreach (var item in _inventory.GetHandOrInventoryEntities(player))
            {
                if (!TryComp<PointLightComponent>(item, out var light) || !light.MaskAutoRotate)
                    continue;

                // маска света с autoRot смотрит туда же, куда вещь: поворачиваем вещь точно на курсор
                _transform.SetWorldRotation(item, aim);
                _seen.Add(item);
                _aimed.Add(item);
            }
        }

        // всё, что больше не наводится (вышли из боевого режима, убрали фонарь), — без доворота
        _aimed.RemoveWhere(uid =>
        {
            if (_seen.Contains(uid))
                return false;

            if (!TerminatingOrDeleted(uid))
                _transform.SetLocalRotationNoLerp(uid, Angle.Zero);

            return true;
        });
    }
}
