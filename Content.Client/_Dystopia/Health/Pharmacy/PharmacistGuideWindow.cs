// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Health.Pharmacy;

/// <summary>
/// Окно «Справочника фармацевта» в стиле городского оборудования.
/// </summary>
public sealed class PharmacistGuideWindow : CityWindow
{
    public PharmacistGuideWindow()
    {
        WindowTitle = Loc.GetString("pharm-guide-title");
        Subtitle = Loc.GetString("pharm-guide-subtitle");
        Slogan = Loc.GetString("pharm-guide-slogan");
        Resizable = true;
        MinSize = new Vector2(1000, 600);
        SetSize = new Vector2(1360, 820);

        var data = IoCManager.Resolve<IEntityManager>().System<PharmacistGuideSystem>().GetData();
        Contents.AddChild(new PharmacistGuideControl(data));
    }
}

/// <summary>
/// Предмет «Справочник фармацевта»: открывает окно. Сервер ничего не присылает — всё строится на клиенте.
/// </summary>
public sealed partial class PharmacistGuideBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    protected override void Open()
    {
        base.Open();
        this.CreateWindow<PharmacistGuideWindow>();
    }
}
