using System.Numerics;
using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Dystopia.Economy;

/// <summary>Окно вексельного принтера: остаток фонда, сумма и основание, печать векселя.</summary>
public sealed class VexelPrinterWindow : DefaultWindow
{
    public event Action<int, string>? OnPrint;

    private static readonly Color AccentColor = Color.FromHex("#D9B44A");
    private static readonly Color DimColor = Color.FromHex("#8A8A8A");

    private readonly Label _fund;
    private readonly Label _limit;
    private readonly LineEdit _amount;
    private readonly LineEdit _reason;

    public VexelPrinterWindow()
    {
        Title = Loc.GetString("dystopia-vexel-printer-title");
        MinSize = new Vector2(420, 220);
        SetSize = new Vector2(440, 230);

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

        _amount = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-city-console-amount") };
        _reason = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-vexel-printer-reason") };
        root.AddChild(_amount);
        root.AddChild(_reason);

        var print = new Button { Text = Loc.GetString("dystopia-vexel-printer-print") };
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
