using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Dystopia.Economy;

/// <summary>Окно вексельного принтера: остаток фонда, сумма и основание, печать векселя.</summary>
public sealed class VexelPrinterWindow : CityWindow
{
    public event Action<int, string>? OnPrint;

    private static readonly Color AccentColor = CityUi.Accent;
    private static readonly Color DimColor = CityUi.Dim;

    private readonly Label _fund;
    private readonly Label _limit;
    private readonly LineEdit _amount;
    private readonly LineEdit _reason;

    public VexelPrinterWindow()
    {
        WindowTitle = Loc.GetString("dystopia-vexel-printer-title");
        Subtitle = Loc.GetString("dystopia-city-ui-sub-vexel");
        Slogan = Loc.GetString("dystopia-city-ui-slogan-vexel");
        MinSize = new Vector2(480, 340);
        SetSize = new Vector2(520, 360);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        _fund = new Label { FontColorOverride = AccentColor };
        _limit = new Label { FontColorOverride = DimColor };
        root.AddChild(_fund);
        root.AddChild(_limit);

        _amount = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-city-console-amount") };
        _reason = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-vexel-printer-reason") };
        root.AddChild(_amount);
        root.AddChild(_reason);

        var print = CityUi.MakeButton(Loc.GetString("dystopia-vexel-printer-print"), CityButtonStyle.Primary);
        print.OnPressed += _ =>
        {
            if (!int.TryParse(_amount.Text.Trim(), out var amount) || amount <= 0)
                return;

            OnPrint?.Invoke(amount, _reason.Text);
            _amount.Text = string.Empty;
        };
        root.AddChild(print);
        root.AddChild(new Label { Text = Loc.GetString("dystopia-vexel-printer-hint"), FontColorOverride = DimColor });
    }

    public void UpdateState(VexelPrinterUiState state)
    {
        _fund.Text = Loc.GetString("dystopia-vexel-printer-fund", ("fund", state.FundName), ("amount", state.FundBalance));
        _limit.Text = Loc.GetString("dystopia-vexel-printer-limit", ("amount", state.MaxAmount));
    }
}
