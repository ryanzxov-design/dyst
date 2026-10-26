// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: в боевом режиме персонаж поворачивается к курсору плавно, а не по четырём сторонам — и всё, что светит
// конусом из рук (фонарики: autoRot), светит ровно туда, куда смотрит индикатор атаки. Спрайт персонажа всё равно
// рисуется в одной из четырёх сторон (ближайшей), меняется только угол самой сущности.

using Content.Shared.CombatMode;
using Content.Shared.MouseRotator;
using Robust.Shared.Configuration;

namespace Content.Shared._Dystopia.Combat;

public sealed class DystopiaAimRotationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MouseRotatorComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<MouseRotatorComponent> ent, ref ComponentStartup args)
    {
        // только поворот от боевого режима (его добавляет SharedCombatModeSystem); турели и прочее не трогаем
        if (!HasComp<CombatModeComponent>(ent))
            return;

        ent.Comp.Simple4DirMode = false;
        ent.Comp.AngleTolerance = Angle.FromDegrees(_cfg.GetCVar(DystopiaCVars.AimRotationTolerance));
    }
}
