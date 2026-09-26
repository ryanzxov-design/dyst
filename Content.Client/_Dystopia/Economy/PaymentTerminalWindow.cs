using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Dystopia.Economy;

/// <summary>Окно платёжного терминала: привязка к счёту, выставление и отмена счёта к оплате.</summary>
public sealed class PaymentTerminalWindow : CityWindow
{
    public event Action<int, string>? OnSetBill;
    public event Action? OnCancelBill;
    public event Action? OnLink;
    public event Action? OnUnlink;

    private static readonly Color AccentColor = CityUi.Accent;
    private static readonly Color DimColor = CityUi.Dim;

    private readonly Label _linked;
    private readonly Label _bill;
    private readonly LineEdit _amount;
    private readonly LineEdit _description;
    private readonly Button _setBill;
    private readonly Button _cancelBill;
    private readonly Button _unlink;
    private readonly Label _taxPreview;
    private int _taxRate;
    private int? _linkedAccount;

    public PaymentTerminalWindow()
    {
        WindowTitle = Loc.GetString("dystopia-payment-terminal-title");
        Subtitle = Loc.GetString("dystopia-city-ui-sub-payment");
        Slogan = Loc.GetString("dystopia-city-ui-slogan-payment");
        MinSize = new Vector2(480, 380);
        SetSize = new Vector2(520, 410);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        _linked = new Label { FontColorOverride = DimColor };
        root.AddChild(_linked);

        var linkRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var link = CityUi.MakeButton(Loc.GetString("dystopia-payment-terminal-link"));
        link.HorizontalExpand = true;
        link.OnPressed += _ => OnLink?.Invoke();
        _unlink = CityUi.MakeButton(Loc.GetString("dystopia-payment-terminal-unlink"));
        _unlink.OnPressed += _ => OnUnlink?.Invoke();
        linkRow.AddChild(link);
        linkRow.AddChild(_unlink);
        root.AddChild(linkRow);

        _bill = new Label { FontColorOverride = AccentColor, Margin = new Thickness(0, 6, 0, 0) };
        root.AddChild(_bill);

        _amount = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-payment-terminal-amount") };
        _description = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-payment-terminal-description") };
        root.AddChild(_amount);
        _taxPreview = new Label { FontColorOverride = DimColor };
        root.AddChild(_taxPreview);
        _amount.OnTextChanged += _ => UpdateTaxPreview();
        root.AddChild(_description);

        var billRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _setBill = CityUi.MakeButton(Loc.GetString("dystopia-payment-terminal-set-bill"), CityButtonStyle.Primary);
        _setBill.HorizontalExpand = true;
        _setBill.OnPressed += _ =>
        {
            if (!int.TryParse(_amount.Text.Trim(), out var amount) || amount <= 0)
                return;

            OnSetBill?.Invoke(amount, _description.Text);
        };
        _cancelBill = CityUi.MakeButton(Loc.GetString("dystopia-payment-terminal-cancel-bill"));
        _cancelBill.OnPressed += _ => OnCancelBill?.Invoke();
        billRow.AddChild(_setBill);
        billRow.AddChild(_cancelBill);
        root.AddChild(billRow);

        root.AddChild(new Label { Text = Loc.GetString("dystopia-payment-terminal-hint"), FontColorOverride = DimColor });
    }

    /// <summary>Сколько из введённой суммы уйдёт в налог и сколько получит продавец.</summary>
    private void UpdateTaxPreview()
    {
        if (_linkedAccount == null || !int.TryParse(_amount.Text.Trim(), out var amount) || amount <= 0)
        {
            _taxPreview.Text = _linkedAccount == null
                ? string.Empty
                : Loc.GetString("dystopia-payment-terminal-tax-rate", ("rate", _taxRate));
            return;
        }

        var tax = amount * _taxRate / 100;
        _taxPreview.Text = Loc.GetString("dystopia-payment-terminal-tax-preview",
            ("rate", _taxRate), ("tax", tax), ("net", amount - tax));
    }

    public void UpdateState(PaymentTerminalUiState state)
    {
        _taxRate = state.TaxRate;
        _linkedAccount = state.LinkedAccount;
        UpdateTaxPreview();

        _linked.Text = state.LinkedAccount is { } id
            ? Loc.GetString("dystopia-payment-terminal-linked-to", ("id", id), ("name", state.LinkedName))
            : Loc.GetString("dystopia-payment-terminal-unlinked");
        _unlink.Disabled = state.LinkedAccount == null;
        _setBill.Disabled = state.LinkedAccount == null;

        _bill.Text = state.PendingAmount > 0
            ? Loc.GetString("dystopia-payment-terminal-bill", ("amount", state.PendingAmount), ("description", state.PendingDescription))
            : Loc.GetString("dystopia-payment-terminal-no-bill-ui");
        _cancelBill.Disabled = state.PendingAmount <= 0;
    }
}
