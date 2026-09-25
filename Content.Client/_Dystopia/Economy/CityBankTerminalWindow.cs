using System.Linq;
using System.Numerics;
using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Economy;

/// <summary>
/// Окно терминала банковских операций: поиск, реестр счетов с заморозкой, журнал операций.
/// Нажатие «Операции» у счёта показывает в журнале только его операции.
/// </summary>
public sealed class CityBankTerminalWindow : DefaultWindow
{
    public event Action<int, bool>? OnSetFrozen;

    private static readonly Color AccentColor = Color.FromHex("#B8962E");
    private static readonly Color DimColor = Color.FromHex("#8A8A8A");
    private static readonly Color WarnColor = Color.FromHex("#E05A4F");

    private readonly LineEdit _search;
    private readonly GridContainer _accountsGrid;
    private readonly Label _ledgerHeader;
    private readonly Button _resetFilter;
    private readonly BoxContainer _ledgerBox;

    private CityBankTerminalUiState? _state;
    private int? _accountFilter;
    private string _accountsSignature = string.Empty;
    private string _ledgerSignature = string.Empty;

    public CityBankTerminalWindow()
    {
        Title = Loc.GetString("dystopia-bank-terminal-title");
        MinSize = new Vector2(760, 520);
        SetSize = new Vector2(860, 640);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        _search = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-bank-terminal-search") };
        _search.OnTextChanged += _ => Redraw(true);
        root.AddChild(_search);

        root.AddChild(new Label { Text = Loc.GetString("dystopia-bank-terminal-accounts-header"), FontColorOverride = AccentColor });
        var accountsScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinHeight = 200 };
        _accountsGrid = new GridContainer { Columns = 7, HorizontalExpand = true };
        accountsScroll.AddChild(_accountsGrid);
        root.AddChild(accountsScroll);

        var ledgerHeaderRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        _ledgerHeader = new Label { FontColorOverride = AccentColor, HorizontalExpand = true };
        _resetFilter = new Button { Text = Loc.GetString("dystopia-bank-terminal-reset-filter"), Visible = false };
        _resetFilter.OnPressed += _ =>
        {
            _accountFilter = null;
            Redraw(true);
        };
        ledgerHeaderRow.AddChild(_ledgerHeader);
        ledgerHeaderRow.AddChild(_resetFilter);
        root.AddChild(ledgerHeaderRow);

        var ledgerScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinHeight = 180 };
        _ledgerBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        ledgerScroll.AddChild(_ledgerBox);
        root.AddChild(ledgerScroll);
    }

    public void UpdateState(CityBankTerminalUiState state)
    {
        _state = state;
        Redraw(false);
    }

    private bool MatchesSearch(CityBankTerminalAccountEntry account, string search)
    {
        if (search.Length == 0)
            return true;

        return account.Id.ToString().Contains(search) ||
               account.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               account.Job.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void Redraw(bool force)
    {
        if (_state == null)
            return;

        var search = _search.Text.Trim();
        var accounts = _state.Accounts.Where(a => MatchesSearch(a, search)).ToList();

        // --- Реестр счетов ---
        var accountsSignature = search + "|" + string.Join(";", accounts.Select(a => $"{a.Id}:{a.Balance}:{a.Debt}:{a.Frozen}"));
        if (force || accountsSignature != _accountsSignature)
        {
            _accountsSignature = accountsSignature;
            _accountsGrid.RemoveAllChildren();

            foreach (var col in new[] { "id", "name", "job", "balance", "debt", "status", "actions" })
            {
                _accountsGrid.AddChild(new Label
                {
                    Text = col == "actions" ? string.Empty : Loc.GetString($"dystopia-bank-terminal-col-{col}"),
                    FontColorOverride = DimColor,
                });
            }

            foreach (var account in accounts)
            {
                var id = account.Id;
                _accountsGrid.AddChild(new Label { Text = $"№{account.Id}" });
                _accountsGrid.AddChild(new Label { Text = account.Name, HorizontalExpand = true, ClipText = true });
                _accountsGrid.AddChild(new Label { Text = account.Job, ClipText = true, MinWidth = 120 });
                _accountsGrid.AddChild(new Label { Text = account.Balance.ToString() });
                _accountsGrid.AddChild(new Label
                {
                    Text = account.Debt > 0 ? account.Debt.ToString() : "—",
                    FontColorOverride = account.Debt > 0 ? WarnColor : null,
                });
                _accountsGrid.AddChild(new Label
                {
                    Text = Loc.GetString(account.Frozen ? "dystopia-bank-terminal-status-frozen" : "dystopia-bank-terminal-status-active"),
                    FontColorOverride = account.Frozen ? WarnColor : null,
                });

                var actions = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
                var history = new Button { Text = Loc.GetString("dystopia-bank-terminal-history") };
                history.OnPressed += _ =>
                {
                    _accountFilter = id;
                    Redraw(true);
                };
                var freeze = new Button
                {
                    Text = Loc.GetString(account.Frozen ? "dystopia-bank-terminal-unfreeze" : "dystopia-bank-terminal-freeze"),
                };
                var frozen = account.Frozen;
                freeze.OnPressed += _ => OnSetFrozen?.Invoke(id, !frozen);
                actions.AddChild(history);
                actions.AddChild(freeze);
                _accountsGrid.AddChild(actions);
            }
        }

        // --- Журнал операций ---
        var ledger = _state.Ledger.Where(e => _accountFilter == null || e.From == _accountFilter || e.To == _accountFilter).ToList();
        _ledgerHeader.Text = _accountFilter == null
            ? Loc.GetString("dystopia-bank-terminal-ledger-header")
            : Loc.GetString("dystopia-bank-terminal-ledger-header-account", ("id", _accountFilter.Value));
        _resetFilter.Visible = _accountFilter != null;

        var ledgerSignature = (_accountFilter?.ToString() ?? "all") + "|" + ledger.Count + "|" + (ledger.Count > 0 ? ledger[0].Text : string.Empty);
        if (!force && ledgerSignature == _ledgerSignature)
            return;
        _ledgerSignature = ledgerSignature;

        _ledgerBox.RemoveAllChildren();
        if (ledger.Count == 0)
        {
            _ledgerBox.AddChild(new Label { Text = Loc.GetString("dystopia-bank-terminal-ledger-empty"), FontColorOverride = DimColor });
            return;
        }

        foreach (var entry in ledger)
        {
            var label = new RichTextLabel { HorizontalExpand = true };
            label.SetMessage(FormattedMessage.FromUnformatted(entry.Text));
            _ledgerBox.AddChild(label);
        }
    }
}
