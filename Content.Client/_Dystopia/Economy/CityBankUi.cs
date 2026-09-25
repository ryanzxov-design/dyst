using Content.Client.UserInterface.Fragments;
using Content.Shared._Dystopia.Economy;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Economy;

/// <summary>Программа КПК «Банк»: баланс, история операций, переводы.</summary>
public sealed partial class CityBankUi : UIFragment
{
    private CityBankUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new CityBankUiFragment();
        _fragment.OnTransfer += (to, amount, comment) =>
            userInterface.SendMessage(new CartridgeUiMessage(new CityBankTransferMessageEvent(to, amount, comment)));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CityBankUiState cast)
            _fragment?.UpdateState(cast);
    }
}

public sealed class CityBankUiFragment : BoxContainer
{
    private static readonly Color AccentColor = Color.FromHex("#D9B44A");
    private static readonly Color DimColor = Color.FromHex("#8A8A8A");
    private static readonly Color WarnColor = Color.FromHex("#E05A4F");

    public event Action<int, int, string>? OnTransfer;

    private readonly Label _account;
    private readonly Label _balance;
    private readonly Label _debt;
    private readonly BoxContainer _transferBox;
    private readonly LineEdit _to;
    private readonly LineEdit _amount;
    private readonly LineEdit _comment;
    private readonly BoxContainer _history;
    private string _historySignature = string.Empty;

    public CityBankUiFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 4;
        Margin = new Thickness(4);

        _account = new Label { FontColorOverride = DimColor };
        _balance = new Label { FontColorOverride = AccentColor };
        _debt = new Label { FontColorOverride = WarnColor, Visible = false };
        AddChild(_account);
        AddChild(_balance);
        AddChild(_debt);

        _transferBox = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 2 };
        _transferBox.AddChild(new Label { Text = Loc.GetString("dystopia-bank-ui-transfer-header"), FontColorOverride = AccentColor, Margin = new Thickness(0, 6, 0, 0) });
        _to = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-bank-ui-to") };
        _amount = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-bank-ui-amount") };
        _comment = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-bank-ui-comment") };
        var send = new Button { Text = Loc.GetString("dystopia-bank-ui-send") };
        send.OnPressed += _ =>
        {
            if (!int.TryParse(_to.Text.Trim(), out var to) || !int.TryParse(_amount.Text.Trim(), out var amount) || amount <= 0)
                return;

            OnTransfer?.Invoke(to, amount, _comment.Text);
            _amount.Text = string.Empty;
            _comment.Text = string.Empty;
        };
        _transferBox.AddChild(_to);
        _transferBox.AddChild(_amount);
        _transferBox.AddChild(_comment);
        _transferBox.AddChild(send);
        AddChild(_transferBox);

        AddChild(new Label { Text = Loc.GetString("dystopia-bank-ui-history-header"), FontColorOverride = AccentColor, Margin = new Thickness(0, 6, 0, 0) });
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        _history = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        scroll.AddChild(_history);
        AddChild(scroll);
    }

    public void UpdateState(CityBankUiState state)
    {
        if (!state.HasAccount)
        {
            _account.Text = Loc.GetString("dystopia-bank-ui-no-card");
            _balance.Text = string.Empty;
            _debt.Visible = false;
            _transferBox.Visible = false;
            _history.RemoveAllChildren();
            _historySignature = string.Empty;
            return;
        }

        _transferBox.Visible = true;
        _account.Text = Loc.GetString("dystopia-bank-ui-account", ("id", state.AccountId), ("name", state.Owner));
        _balance.Text = Loc.GetString("dystopia-bank-ui-balance", ("balance", state.Balance)) +
                        (state.Frozen ? Loc.GetString("dystopia-bank-ui-frozen") : string.Empty);
        _debt.Visible = state.Debt > 0;
        _debt.Text = Loc.GetString("dystopia-bank-ui-debt", ("debt", state.Debt));

        var signature = string.Join("\n", state.History);
        if (signature == _historySignature)
            return;
        _historySignature = signature;

        _history.RemoveAllChildren();
        if (state.History.Count == 0)
        {
            _history.AddChild(new Label { Text = Loc.GetString("dystopia-bank-ui-history-empty"), FontColorOverride = DimColor });
            return;
        }

        foreach (var line in state.History)
        {
            var label = new RichTextLabel { HorizontalExpand = true };
            label.SetMessage(Robust.Shared.Utility.FormattedMessage.FromUnformatted(line));
            _history.AddChild(label);
        }
    }
}
