// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Dystopia.UserInterface;
using Content.Client.UserInterface.Fragments;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Health.Pharmacy;

/// <summary>
/// Программа КПК «Справочник фармацевта». Древо слишком велико для экрана КПК, поэтому программа
/// открывает справочник отдельным окном (сразу при запуске и по кнопке).
/// </summary>
public sealed partial class PharmacistGuideUi : UIFragment
{
    private PharmacistGuideUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new PharmacistGuideUiFragment();

        _fragment.AddChild(CityUi.SectionHeader(Loc.GetString("pharm-guide-program-name")));
        var text = new RichTextLabel { HorizontalExpand = true };
        text.SetMessage(Loc.GetString("pharm-guide-program-text"));
        _fragment.AddChild(text);

        var open = CityUi.MakeButton(Loc.GetString("pharm-guide-program-open"), CityButtonStyle.Primary);
        open.OnPressed += _ => OpenWindow();
        _fragment.AddChild(open);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
    }

    public static void OpenWindow()
    {
        IoCManager.Resolve<IEntityManager>().System<PharmacistGuideSystem>().OpenWindow();
    }
}

/// <summary>
/// Экран программы в КПК. Окно справочника открывается, когда программу показывают (а не на каждое
/// обновление состояния КПК, иначе закрытое окно открывалось бы снова).
/// </summary>
public sealed class PharmacistGuideUiFragment : BoxContainer
{
    public PharmacistGuideUiFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 8;
        Margin = new Thickness(8);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        PharmacistGuideUi.OpenWindow();
    }
}
