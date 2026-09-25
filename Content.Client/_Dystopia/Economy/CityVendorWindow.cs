using System.Numerics;
using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Dystopia.Economy;

/// <summary>Окно городского автомата: товары, цены, остаток, кнопка «Купить».</summary>
public sealed class CityVendorWindow : DefaultWindow
{
    public event Action<int>? OnBuy;

    private static readonly Color AccentColor = Color.FromHex("#D9B44A");
    private static readonly Color DimColor = Color.FromHex("#8A8A8A");

    private readonly GridContainer _grid;

    public CityVendorWindow()
    {
        Title = Loc.GetString("dystopia-vendor-title");
        MinSize = new Vector2(420, 300);
        SetSize = new Vector2(460, 380);

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

            var buy = new Button { Text = Loc.GetString("dystopia-vendor-buy"), Disabled = item.Amount == 0 };
            buy.OnPressed += _ => OnBuy?.Invoke(index);
            _grid.AddChild(buy);
        }
    }
}
