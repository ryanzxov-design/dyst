using System.Linq;
using System.Numerics;
using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Dystopia.Economy;

/// <summary>
/// Окно Консоли Управления Городом. Слева — разделы, справа — рабочая область.
/// Сейчас работает раздел «Казна»; «Положения» и «Законы» — заглушки.
/// Окно собрано в коде (без XAML), чтобы его было проще менять.
/// </summary>
public sealed class CityConsoleWindow : DefaultWindow
{
    public event Action<string, int, int>? OnSetRates;
    public event Action<int, int, string>? OnBonus;
    public event Action<int, int, string>? OnSeize;

    private static readonly Color AccentColor = Color.FromHex("#B8962E");
    private static readonly Color DimColor = Color.FromHex("#8A8A8A");

    private readonly Control _treasuryPanel;
    private readonly Control _decreesPanel;
    private readonly Control _lawsPanel;
    private readonly Button _treasuryTab;
    private readonly Button _decreesTab;
    private readonly Button _lawsTab;

    // Казна
    private readonly Label _treasuryLabel;
    private readonly Label _paydayLabel;
    private readonly GridContainer _ratesGrid;
    private readonly Dictionary<string, (LineEdit Salary, LineEdit Tax)> _rateEdits = new();
    private readonly Dictionary<string, (int Salary, int Tax)> _lastServerRates = new();
    private readonly OptionButton _accountSelect;
    private readonly LineEdit _amountEdit;
    private readonly LineEdit _reasonEdit;
    private readonly BoxContainer _logBox;
    private string _accountsSignature = string.Empty;
    private int _selectedAccount = -1;
    private string _logSignature = string.Empty;

    public CityConsoleWindow()
    {
        Title = Loc.GetString("dystopia-city-console-title");
        MinSize = new Vector2(820, 560);
        SetSize = new Vector2(900, 640);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
        };
        Contents.AddChild(root);

        // --- Левая навигация ---
        var nav = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 160,
            SeparationOverride = 4,
        };
        root.AddChild(nav);

        _treasuryTab = MakeTab("dystopia-city-console-tab-treasury");
        _decreesTab = MakeTab("dystopia-city-console-tab-decrees");
        _lawsTab = MakeTab("dystopia-city-console-tab-laws");
        nav.AddChild(_treasuryTab);
        nav.AddChild(_decreesTab);
        nav.AddChild(_lawsTab);

        // --- Правая рабочая область ---
        var content = new Control { HorizontalExpand = true, VerticalExpand = true };
        root.AddChild(content);

        _treasuryPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        _decreesPanel = MakeStub("dystopia-city-console-stub-decrees");
        _lawsPanel = MakeStub("dystopia-city-console-stub-laws");
        content.AddChild(_treasuryPanel);
        content.AddChild(_decreesPanel);
        content.AddChild(_lawsPanel);

        _treasuryTab.OnPressed += _ => SelectTab(0);
        _decreesTab.OnPressed += _ => SelectTab(1);
        _lawsTab.OnPressed += _ => SelectTab(2);

        // --- Казна: шапка ---
        _treasuryLabel = new Label { FontColorOverride = AccentColor };
        _paydayLabel = new Label { FontColorOverride = DimColor };
        _treasuryPanel.AddChild(_treasuryLabel);
        _treasuryPanel.AddChild(_paydayLabel);

        // --- Казна: ставки профессий ---
        _treasuryPanel.AddChild(MakeHeader("dystopia-city-console-rates-header"));
        var ratesScroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            MinHeight = 220,
        };
        _ratesGrid = new GridContainer { Columns = 4, HorizontalExpand = true };
        ratesScroll.AddChild(_ratesGrid);
        _treasuryPanel.AddChild(ratesScroll);

        // --- Казна: премии и изъятия ---
        _treasuryPanel.AddChild(MakeHeader("dystopia-city-console-money-header"));
        _accountSelect = new OptionButton { HorizontalExpand = true };
        _accountSelect.OnItemSelected += args =>
        {
            args.Button.SelectId(args.Id);
            _selectedAccount = args.Id;
        };
        _treasuryPanel.AddChild(_accountSelect);

        var moneyRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        _amountEdit = new LineEdit { MinWidth = 110, PlaceHolder = Loc.GetString("dystopia-city-console-amount") };
        _reasonEdit = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-city-console-reason") };
        var bonusButton = new Button { Text = Loc.GetString("dystopia-city-console-bonus") };
        var seizeButton = new Button { Text = Loc.GetString("dystopia-city-console-seize") };
        bonusButton.OnPressed += _ => SendMoney(true);
        seizeButton.OnPressed += _ => SendMoney(false);
        moneyRow.AddChild(_amountEdit);
        moneyRow.AddChild(_reasonEdit);
        moneyRow.AddChild(bonusButton);
        moneyRow.AddChild(seizeButton);
        _treasuryPanel.AddChild(moneyRow);

        // --- Казна: журнал ---
        _treasuryPanel.AddChild(MakeHeader("dystopia-city-console-log-header"));
        var logScroll = new ScrollContainer { HScrollEnabled = false, MinHeight = 120 };
        _logBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        logScroll.AddChild(_logBox);
        _treasuryPanel.AddChild(logScroll);

        SelectTab(0);
    }

    private static Button MakeTab(string loc)
    {
        return new Button
        {
            Text = Loc.GetString(loc),
            ToggleMode = true,
            HorizontalExpand = true,
            MinHeight = 36,
        };
    }

    private static Label MakeHeader(string loc)
    {
        return new Label { Text = Loc.GetString(loc), FontColorOverride = AccentColor, Margin = new Thickness(0, 6, 0, 0) };
    }

    private static Control MakeStub(string loc)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        box.AddChild(new Label { Text = Loc.GetString(loc), FontColorOverride = DimColor });
        return box;
    }

    private void SelectTab(int index)
    {
        _treasuryPanel.Visible = index == 0;
        _decreesPanel.Visible = index == 1;
        _lawsPanel.Visible = index == 2;
        _treasuryTab.Pressed = index == 0;
        _decreesTab.Pressed = index == 1;
        _lawsTab.Pressed = index == 2;
    }

    private void SendMoney(bool bonus)
    {
        if (_selectedAccount < 0 || !int.TryParse(_amountEdit.Text.Trim(), out var amount) || amount <= 0)
            return;

        var reason = _reasonEdit.Text;
        if (bonus)
            OnBonus?.Invoke(_selectedAccount, amount, reason);
        else
            OnSeize?.Invoke(_selectedAccount, amount, reason);

        _amountEdit.Text = string.Empty;
        _reasonEdit.Text = string.Empty;
    }

    public void UpdateState(CityConsoleBoundUserInterfaceState state)
    {
        _treasuryLabel.Text = Loc.GetString("dystopia-city-console-treasury", ("amount", state.Treasury));
        _paydayLabel.Text = Loc.GetString("dystopia-city-console-payday",
            ("minutes", state.SecondsToPayday / 60), ("seconds", (state.SecondsToPayday % 60).ToString("00")));

        UpdateRates(state.Jobs);
        UpdateAccounts(state.Accounts);
        UpdateLog(state.Log);
    }

    private void UpdateRates(List<CityConsoleJobEntry> jobs)
    {
        // Таблицу строим один раз; потом только обновляем значения, которые изменились на сервере,
        // чтобы не сбивать то, что Консул сейчас печатает.
        if (_rateEdits.Count != jobs.Count || jobs.Any(j => !_rateEdits.ContainsKey(j.JobId)))
        {
            _ratesGrid.RemoveAllChildren();
            _rateEdits.Clear();
            _lastServerRates.Clear();

            _ratesGrid.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-col-job"), FontColorOverride = DimColor });
            _ratesGrid.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-col-salary"), FontColorOverride = DimColor });
            _ratesGrid.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-col-tax"), FontColorOverride = DimColor });
            _ratesGrid.AddChild(new Control());

            foreach (var job in jobs)
            {
                var salaryEdit = new LineEdit { MinWidth = 90, Text = job.Salary.ToString() };
                var taxEdit = new LineEdit { MinWidth = 60, Text = job.Tax.ToString() };
                var save = new Button { Text = Loc.GetString("dystopia-city-console-save") };
                var jobId = job.JobId;

                save.OnPressed += _ =>
                {
                    if (int.TryParse(salaryEdit.Text.Trim(), out var salary) &&
                        int.TryParse(taxEdit.Text.Trim(), out var tax))
                    {
                        OnSetRates?.Invoke(jobId, salary, tax);
                    }
                };

                _ratesGrid.AddChild(new Label { Text = job.Name, HorizontalExpand = true, ClipText = true });
                _ratesGrid.AddChild(salaryEdit);
                _ratesGrid.AddChild(taxEdit);
                _ratesGrid.AddChild(save);

                _rateEdits[job.JobId] = (salaryEdit, taxEdit);
                _lastServerRates[job.JobId] = (job.Salary, job.Tax);
            }

            return;
        }

        foreach (var job in jobs)
        {
            var edits = _rateEdits[job.JobId];
            var last = _lastServerRates[job.JobId];

            if (last.Salary != job.Salary && !edits.Salary.HasKeyboardFocus())
                edits.Salary.Text = job.Salary.ToString();
            if (last.Tax != job.Tax && !edits.Tax.HasKeyboardFocus())
                edits.Tax.Text = job.Tax.ToString();

            _lastServerRates[job.JobId] = (job.Salary, job.Tax);
        }
    }

    private void UpdateAccounts(List<CityConsoleAccountEntry> accounts)
    {
        var signature = string.Join(";", accounts.Select(a => $"{a.Id}:{a.Balance}:{a.Frozen}"));
        if (signature == _accountsSignature)
            return;
        _accountsSignature = signature;

        _accountSelect.Clear();
        var stillThere = false;

        foreach (var account in accounts)
        {
            var frozen = account.Frozen ? Loc.GetString("dystopia-city-console-frozen") : string.Empty;
            _accountSelect.AddItem(Loc.GetString("dystopia-city-console-account-line",
                ("id", account.Id), ("name", account.Name), ("job", account.Job),
                ("balance", account.Balance), ("frozen", frozen)), account.Id);

            if (account.Id == _selectedAccount)
                stillThere = true;
        }

        if (stillThere)
            _accountSelect.SelectId(_selectedAccount);
        else if (accounts.Count > 0)
        {
            _selectedAccount = accounts[0].Id;
            _accountSelect.SelectId(_selectedAccount);
        }
        else
        {
            _selectedAccount = -1;
        }
    }

    private void UpdateLog(List<string> log)
    {
        var signature = string.Join("\n", log);
        if (signature == _logSignature)
            return;
        _logSignature = signature;

        _logBox.RemoveAllChildren();
        if (log.Count == 0)
        {
            _logBox.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-log-empty"), FontColorOverride = DimColor });
            return;
        }

        foreach (var line in log)
        {
            _logBox.AddChild(new Label { Text = line });
        }
    }
}
