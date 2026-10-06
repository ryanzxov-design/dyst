// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос хирургии Shitmed из Goob-Station (AGPL-3.0): начало операции — окно открывается действием
// «Оперировать» хирургическим инструментом или щелчком инструментом по лежащему пациенту.
// Зависимости все в SharedSurgerySystem.cs (анализатор запрещает повторять их в частях класса).

using Content.Shared._Dystopia.Health.Surgery.Tools;
using Content.Shared.Interaction;
using Content.Shared.Medical.Healing;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Dystopia.Health.Surgery;

public abstract partial class SharedSurgerySystem
{
    private EntityQuery<SurgeryTargetComponent> _targetQuery;

    private static readonly SpriteSpecifier VerbIcon =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Specific/Medical/Surgery/scalpel.rsi"), "scalpel");

    private void InitializeStart()
    {
        _targetQuery = GetEntityQuery<SurgeryTargetComponent>();

        SubscribeLocalEvent<SurgeryToolComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
        SubscribeLocalEvent<SurgeryToolComponent, AfterInteractEvent>(OnToolAfterInteract);
    }

    /// <summary>Открыть окно операции над пациентом.</summary>
    public bool AttemptStartSurgery(EntityUid user, EntityUid target)
    {
        if (!_targetQuery.TryComp(target, out var targetComp) || !targetComp.CanOperate)
            return false;

        if (user == target)
        {
            _popup.PopupClient(Loc.GetString("surgery-error-self-surgery"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!IsLyingDown(target, user))
            return false;

        if (!_ui.HasUi(target, SurgeryUIKey.Key))
        {
            // Пациенты, появившиеся до хирургии (или без MapInit), получают окно здесь
            if (_net.IsClient)
                return true;

            _ui.SetUi(target, SurgeryUIKey.Key, new InterfaceData("SurgeryBui"));
        }

        _ui.OpenUi(target, SurgeryUIKey.Key, user);
        RefreshUI(target);
        return true;
    }

    private void OnUtilityVerb(Entity<SurgeryToolComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        var target = args.Target;
        var user = args.User;
        if (!args.CanInteract || !args.CanAccess || user == target || !_targetQuery.HasComp(target))
            return;

        args.Verbs.Add(new UtilityVerb
        {
            Act = () => AttemptStartSurgery(user, target),
            Icon = VerbIcon,
            Text = Loc.GetString("surgery-verb-text"),
            Message = Loc.GetString("surgery-verb-message"),
            DoContactInteraction = true,
        });
    }

    private void OnToolAfterInteract(Entity<SurgeryToolComponent> ent, ref AfterInteractEvent args)
    {
        // Нить и бинты — ещё и обычное лечение: щелчок ими лечит, а операция открывается через меню
        if (args.Handled
            || !args.CanReach
            || args.Target is not { } target
            || target == args.User
            || !_targetQuery.HasComp(target)
            || HasComp<HealingComponent>(ent)
            || !_standing.IsDown(target) && !IsBuckledLying(target))
        {
            return;
        }

        args.Handled = AttemptStartSurgery(args.User, target);
    }

    private bool IsBuckledLying(EntityUid target)
    {
        if (!TryComp<Content.Shared.Buckle.Components.BuckleComponent>(target, out var buckle)
            || !TryComp<Content.Shared.Buckle.Components.StrapComponent>(buckle.BuckledTo, out var strap))
        {
            return false;
        }

        // Поле читаем в локальную переменную: методы прямо на чужом поле запрещает анализатор доступа
        var rotation = strap.Rotation;
        return rotation.GetCardinalDir() is Direction.West or Direction.East;
    }
}
