using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Dystopia.Economy;

/// <summary>Окно городского автомата: товары, цены, остаток, кнопка «Купить».</summary>
public sealed class CityVendorWindow : CityWindow
{
    public event Action<int>? OnBuy;

    private static readonly Color AccentColor = CityUi.Accent;
    private static readonly Color DimColor = CityUi.Dim;

    private readonly GridContainer _grid;

    public CityVendorWindow()
    {
        WindowTitle = Loc.GetString("dystopia-vendor-title");
        Subtitle = Loc.GetString("dystopia-city-ui-sub-vendor");
        Slogan = Loc.GetString("dystopia-city-ui-slogan-vendor");
        MinSize = new Vector2(480, 400);
        SetSize = new Vector2(520, 500);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        root.AddChild(new Label { Text = Loc.GetString("dystopia-vendor-hint"), FontColorOverride = DimColor });

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _grid = new GridContainer { Columns = 4, HorizontalExpand = true };
        scroll.AddChild(_grid);
        root.AddChild(scroll);
    }

    public void UpdateState(CityVendorUiState state)
    {
        _grid.RemoveAllChildren();

        foreach (var item in state.Items)
        {
            var index = item.Index;
            _grid.AddChild(new Label { Text = item.Name, HorizontalExpand = true, ClipText = true });
            _grid.AddChild(new Label
            {
                Text = Loc.GetString("dystopia-vendor-price", ("price", item.Price)),
                FontColorOverride = AccentColor,
                MinWidth = 90,
            });
            _grid.AddChild(new Label
            {
                Text = item.Amount < 0 ? string.Empty : Loc.GetString("dystopia-vendor-stock", ("amount", item.Amount)),
                FontColorOverride = DimColor,
                MinWidth = 70,
            });

            var buy = CityUi.MakeButton(Loc.GetString("dystopia-vendor-buy"), CityButtonStyle.Primary);
            buy.Disabled = item.Amount == 0;
            buy.OnPressed += _ => OnBuy?.Invoke(index);
            _grid.AddChild(buy);
        }
    }
}
