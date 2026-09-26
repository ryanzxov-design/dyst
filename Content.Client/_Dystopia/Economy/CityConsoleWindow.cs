using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.Laws;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Economy;
using Content.Shared._Dystopia.Laws;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Economy;

/// <summary>
/// Окно Консоли Управления Городом. Слева — разделы, справа — рабочая область.
/// Разделы: «Казна», «Положения», «Законы», «Фонды».
/// Окно собрано в коде (без XAML), чтобы его было проще менять.
/// </summary>
public sealed class CityConsoleWindow : CityWindow
{
    public event Action<string, int, int>? OnSetRates;
    public event Action<int, int, string>? OnBonus;
    public event Action<int, int, string>? OnSeize;
    public event Action<string>? OnSetMode;
    public event Action<string>? OnAnnounce;
    public event Action? OnNewLaw;
    public event Action<int, int, string, string, string>? OnSaveLaw;
    public event Action<int>? OnDeleteLaw;
    public event Action<List<CitySanctionClass>, string>? OnSaveSanctions;
    public event Action<string, int, bool>? OnFundTransfer;

    private static readonly Color AccentColor = CityUi.Accent;
    private static readonly Color DimColor = CityUi.Dim;

    private readonly Control _treasuryPanel;
    private readonly Control _decreesPanel;
    private readonly Control _lawsPanel;
    private readonly CityButton _treasuryTab;
    private readonly CityButton _decreesTab;
    private readonly CityButton _lawsTab;
    private readonly CityButton _fundsTab;
    private readonly Control _fundsPanel;
    private readonly BoxContainer _fundsList;
    private string _fundsSignature = string.Empty;

    // Казна
    private readonly Label _treasuryLabel;
    private readonly Label _paydayLabel;
    private readonly Label _accountsCountLabel;
    private readonly Label _navModeLabel;
    private readonly GridContainer _ratesGrid;
    private readonly Dictionary<string, (LineEdit Salary, LineEdit Tax)> _rateEdits = new();
    private readonly Dictionary<string, (int Salary, int Tax)> _lastServerRates = new();
    private readonly CityDropdown _accountSelect;
    private readonly LineEdit _amountEdit;
    private readonly LineEdit _reasonEdit;
    private readonly BoxContainer _logBox;
    private string _accountsSignature = string.Empty;
    private int _selectedAccount = -1;
    private string _logSignature = string.Empty;

    // Положения
    private readonly Label _currentModeLabel;
    private readonly RichTextLabel _modeInfo;
    private readonly BoxContainer _modeButtons;
    private readonly LineEdit _announceEdit;
    private readonly Button _announceButton;
    private readonly Label _announceCooldownLabel;
    private string _modesSignature = string.Empty;

    // Законы
    private readonly CityButton _articlesSubTab;
    private readonly CityButton _sanctionsSubTab;
    private readonly Control _articlesView;
    private readonly Control _sanctionsView;
    private readonly BoxContainer _lawList;
    private readonly BoxContainer _lawEditor;
    private readonly Label _lawHint;
    private readonly LineEdit _lawNumber;
    private readonly Label _lawEnacted;
    private readonly LineEdit _lawTitle;
    private readonly TextEdit _lawText;
    private readonly LineEdit _lawSanction;
    private readonly Button _lawDelete;
    private readonly GridContainer _sanctionsGrid;
    private readonly LineEdit _generalProvision;
    private readonly Dictionary<string, (LineEdit Legal, LineEdit Disciplinary)> _sanctionEdits = new();
    private readonly Dictionary<string, (string Legal, string Disciplinary)> _lastServerSanctions = new();
    private string _lastServerProvision = string.Empty;
    private List<CityLaw> _laws = new();
    private string _lawsSignature = string.Empty;
    private int _selectedLawId = -1;
    private string _filledLawSignature = string.Empty;
    private bool _selectNewestLaw;
    private int _maxLawIdBeforeCreate;
    private bool _deleteArmed;

    public CityConsoleWindow()
    {
        WindowTitle = Loc.GetString("dystopia-city-console-title");
        Subtitle = Loc.GetString("dystopia-city-console-subtitle");
        Slogan = Loc.GetString("dystopia-city-ui-slogan");
        MinSize = new Vector2(900, 720);
        SetSize = new Vector2(980, 780);

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
            MinWidth = 180,
            SeparationOverride = 8,
        };
        root.AddChild(nav);

        _treasuryTab = MakeTab(1, "dystopia-city-console-tab-treasury");
        _decreesTab = MakeTab(2, "dystopia-city-console-tab-decrees");
        _lawsTab = MakeTab(3, "dystopia-city-console-tab-laws");
        _fundsTab = MakeTab(4, "dystopia-city-console-tab-funds");
        nav.AddChild(_treasuryTab);
        nav.AddChild(_decreesTab);
        nav.AddChild(_lawsTab);
        nav.AddChild(_fundsTab);

        // Действующее положение — внизу навигации
        nav.AddChild(new Control { VerticalExpand = true });
        var modeCard = new CityCard();
        var modeBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        modeBox.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-city-console-nav-mode").ToUpperInvariant(), CityUi.Dim, CityUi.Regular(10)));
        _navModeLabel = CityUi.MakeLabel(string.Empty, CityUi.Accent, CityUi.Bold(13));
        _navModeLabel.ClipText = true;
        modeBox.AddChild(_navModeLabel);
        modeCard.AddChild(modeBox);
        nav.AddChild(modeCard);

        // Вертикальный разделитель между навигацией и содержимым
        root.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CityUi.Line },
            MinWidth = 1,
            VerticalExpand = true,
        });

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
        var decrees = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        _decreesPanel = decrees;
        var lawsPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        _lawsPanel = lawsPanel;
        var fundsPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        _fundsPanel = fundsPanel;
        fundsPanel.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-funds-hint"), FontColorOverride = DimColor });
        var fundsScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _fundsList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 10 };
        fundsScroll.AddChild(_fundsList);
        fundsPanel.AddChild(fundsScroll);

        content.AddChild(_treasuryPanel);
        content.AddChild(_decreesPanel);
        content.AddChild(_lawsPanel);
        content.AddChild(_fundsPanel);

        _treasuryTab.OnPressed += _ => SelectTab(0);
        _decreesTab.OnPressed += _ => SelectTab(1);
        _lawsTab.OnPressed += _ => SelectTab(2);
        _fundsTab.OnPressed += _ => SelectTab(3);

        // --- Казна: шапка ---
        var cards = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 10,
        };
        _treasuryLabel = AddCard(cards, "dystopia-city-console-card-treasury", "dystopia-city-console-card-treasury-unit", true);
        _paydayLabel = AddCard(cards, "dystopia-city-console-card-payday", "dystopia-city-console-card-payday-unit", false);
        _accountsCountLabel = AddCard(cards, "dystopia-city-console-card-accounts", "dystopia-city-console-card-accounts-unit", false);
        _treasuryPanel.AddChild(cards);

        // --- Казна: ставки профессий ---
        _treasuryPanel.AddChild(MakeHeader("dystopia-city-console-rates-header"));
        var ratesScroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            MinHeight = 170,
        };
        _ratesGrid = new GridContainer { Columns = 4, HorizontalExpand = true };
        ratesScroll.AddChild(_ratesGrid);
        _treasuryPanel.AddChild(ratesScroll);

        // --- Казна: премии и изъятия ---
        _treasuryPanel.AddChild(MakeHeader("dystopia-city-console-money-header"));
        _accountSelect = new CityDropdown { HorizontalExpand = true };
        _accountSelect.OnItemSelected += id => _selectedAccount = id;
        _treasuryPanel.AddChild(_accountSelect);

        var moneyRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        _amountEdit = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), MinWidth = 110, PlaceHolder = Loc.GetString("dystopia-city-console-amount") };
        _reasonEdit = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-city-console-reason") };
        var bonusButton = CityUi.MakeButton(Loc.GetString("dystopia-city-console-bonus"), CityButtonStyle.Primary);
        var seizeButton = CityUi.MakeButton(Loc.GetString("dystopia-city-console-seize"), CityButtonStyle.Danger);
        bonusButton.OnPressed += _ => SendMoney(true);
        seizeButton.OnPressed += _ => SendMoney(false);
        moneyRow.AddChild(_amountEdit);
        moneyRow.AddChild(_reasonEdit);
        moneyRow.AddChild(bonusButton);
        moneyRow.AddChild(seizeButton);
        _treasuryPanel.AddChild(moneyRow);

        // --- Казна: журнал ---
        _treasuryPanel.AddChild(MakeHeader("dystopia-city-console-log-header"));
        var logScroll = new ScrollContainer { HScrollEnabled = false, MinHeight = 100, VerticalExpand = true };
        _logBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        logScroll.AddChild(_logBox);
        var logPanel = CityUi.MakePanel(CityUi.Input, CityUi.Line, 8);
        logPanel.VerticalExpand = true;
        logPanel.AddChild(logScroll);
        _treasuryPanel.AddChild(logPanel);

        // --- Положения ---
        _currentModeLabel = new Label();
        _modeInfo = new RichTextLabel { HorizontalExpand = true };
        decrees.AddChild(_currentModeLabel);
        decrees.AddChild(_modeInfo);

        decrees.AddChild(MakeHeader("dystopia-city-console-modes-header"));
        _modeButtons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        decrees.AddChild(_modeButtons);

        decrees.AddChild(MakeHeader("dystopia-city-console-announce-header"));
        var announceRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        _announceEdit = new LineEdit
        { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3),
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("dystopia-city-console-announce-placeholder"),
        };
        _announceButton = new CityButton(Loc.GetString("dystopia-city-console-announce-button").ToUpperInvariant(), CityButtonStyle.Normal) { };
        _announceButton.OnPressed += _ =>
        {
            var text = _announceEdit.Text.Trim();
            if (text.Length == 0)
                return;

            OnAnnounce?.Invoke(text);
            _announceEdit.Text = string.Empty;
        };
        announceRow.AddChild(_announceEdit);
        announceRow.AddChild(_announceButton);
        decrees.AddChild(announceRow);

        _announceCooldownLabel = new Label { FontColorOverride = DimColor };
        decrees.AddChild(_announceCooldownLabel);

        // --- Законы ---
        var subTabs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        _articlesSubTab = CityUi.MakeButton(Loc.GetString("dystopia-laws-tab-articles"), CityButtonStyle.Tab);
        _articlesSubTab.HorizontalExpand = true;
        _sanctionsSubTab = CityUi.MakeButton(Loc.GetString("dystopia-laws-tab-sanctions-scale"), CityButtonStyle.Tab);
        _sanctionsSubTab.HorizontalExpand = true;
        subTabs.AddChild(_articlesSubTab);
        subTabs.AddChild(_sanctionsSubTab);
        lawsPanel.AddChild(subTabs);

        var lawsContent = new Control { HorizontalExpand = true, VerticalExpand = true };
        lawsPanel.AddChild(lawsContent);

        // Статьи: слева список, справа редактор.
        var articlesView = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        _articlesView = articlesView;
        lawsContent.AddChild(articlesView);

        var listColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 230,
            VerticalExpand = true,
            SeparationOverride = 4,
        };
        var listScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _lawList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 2 };
        listScroll.AddChild(_lawList);
        listColumn.AddChild(listScroll);
        var newLaw = new CityButton(Loc.GetString("dystopia-city-console-law-new").ToUpperInvariant(), CityButtonStyle.Normal) { };
        newLaw.OnPressed += _ =>
        {
            _selectNewestLaw = true;
            _maxLawIdBeforeCreate = _laws.Count == 0 ? 0 : _laws.Max(l => l.Id);
            OnNewLaw?.Invoke();
        };
        listColumn.AddChild(newLaw);
        articlesView.AddChild(listColumn);

        var editorColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
        };
        _lawHint = new Label { Text = Loc.GetString("dystopia-city-console-law-hint"), FontColorOverride = DimColor };
        editorColumn.AddChild(_lawHint);

        _lawEditor = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
            Visible = false,
        };
        editorColumn.AddChild(_lawEditor);
        articlesView.AddChild(editorColumn);

        var numberRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, SeparationOverride = 6 };
        numberRow.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-law-number") });
        _lawNumber = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), MinWidth = 60 };
        numberRow.AddChild(_lawNumber);
        _lawEnacted = new Label { FontColorOverride = DimColor, HorizontalExpand = true };
        numberRow.AddChild(_lawEnacted);
        _lawEditor.AddChild(numberRow);

        _lawEditor.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-law-title") });
        _lawTitle = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true };
        _lawEditor.AddChild(_lawTitle);

        _lawEditor.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-law-text") });
        _lawText = new TextEdit { HorizontalExpand = true, VerticalExpand = true, MinHeight = 150 };
        _lawEditor.AddChild(_lawText);

        _lawEditor.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-law-sanction") });
        _lawSanction = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true };
        _lawEditor.AddChild(_lawSanction);

        var lawButtons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, SeparationOverride = 6 };
        var saveLaw = new CityButton(Loc.GetString("dystopia-city-console-law-save").ToUpperInvariant(), CityButtonStyle.Normal) { HorizontalExpand = true };
        saveLaw.OnPressed += _ => SaveSelectedLaw();
        _lawDelete = CityUi.MakeButton(Loc.GetString("dystopia-city-console-law-delete"), CityButtonStyle.Danger);
        _lawDelete.OnPressed += _ => DeleteSelectedLaw();
        lawButtons.AddChild(saveLaw);
        lawButtons.AddChild(_lawDelete);
        _lawEditor.AddChild(lawButtons);

        // Шкала санкций.
        var sanctionsView = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        _sanctionsView = sanctionsView;
        lawsContent.AddChild(sanctionsView);

        _sanctionsGrid = new GridContainer { Columns = 3, HorizontalExpand = true };
        sanctionsView.AddChild(_sanctionsGrid);
        sanctionsView.AddChild(new Label { Text = Loc.GetString("dystopia-laws-general-provision"), FontColorOverride = AccentColor });
        _generalProvision = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true };
        sanctionsView.AddChild(_generalProvision);
        var saveSanctions = new CityButton(Loc.GetString("dystopia-city-console-sanctions-save").ToUpperInvariant(), CityButtonStyle.Normal) { };
        saveSanctions.OnPressed += _ => SaveSanctions();
        sanctionsView.AddChild(saveSanctions);

        _articlesSubTab.OnPressed += _ => SelectLawsSubTab(false);
        _sanctionsSubTab.OnPressed += _ => SelectLawsSubTab(true);
        SelectLawsSubTab(false);

        SelectTab(0);
    }

    private void SelectLawsSubTab(bool sanctions)
    {
        _articlesView.Visible = !sanctions;
        _sanctionsView.Visible = sanctions;
        _articlesSubTab.Active = !sanctions;
        _sanctionsSubTab.Active = sanctions;
    }

    private static CityButton MakeTab(int number, string loc)
    {
        var button = CityUi.MakeButton($"{number:00}   {Loc.GetString(loc)}", CityButtonStyle.Tab);
        button.HorizontalExpand = true;
        return button;
    }

    private static Control MakeHeader(string loc)
    {
        return CityUi.SectionHeader(Loc.GetString(loc));
    }

    /// <summary>Карточка-показатель: подпись, крупное значение, единица измерения. Возвращает метку значения.</summary>
    private static Label AddCard(BoxContainer row, string captionLoc, string unitLoc, bool highlight)
    {
        var card = new CityCard { HorizontalExpand = true };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        box.AddChild(CityUi.MakeLabel(Loc.GetString(captionLoc).ToUpperInvariant(), CityUi.Dim, CityUi.Regular(10)));
        var valueRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var value = CityUi.MakeLabel("—", highlight ? CityUi.Glow : CityUi.Text, CityUi.Bold(20));
        valueRow.AddChild(value);
        var unit = CityUi.MakeLabel(Loc.GetString(unitLoc), CityUi.Muted, CityUi.Regular(10));
        unit.VerticalAlignment = VAlignment.Bottom;
        unit.Margin = new Thickness(0, 0, 0, 4);
        valueRow.AddChild(unit);
        box.AddChild(valueRow);
        card.AddChild(box);
        row.AddChild(card);
        return value;
    }

    private static string FormatNumber(int value)
    {
        return value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');
    }

    private void SelectTab(int index)
    {
        _treasuryPanel.Visible = index == 0;
        _decreesPanel.Visible = index == 1;
        _lawsPanel.Visible = index == 2;
        _fundsPanel.Visible = index == 3;
        _treasuryTab.Active = index == 0;
        _decreesTab.Active = index == 1;
        _lawsTab.Active = index == 2;
        _fundsTab.Active = index == 3;
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
        _treasuryLabel.Text = FormatNumber(state.Treasury);
        _paydayLabel.Text = $"{state.SecondsToPayday / 60:00}:{state.SecondsToPayday % 60:00}";
        _accountsCountLabel.Text = state.Accounts.Count.ToString();

        UpdateRates(state.Jobs);
        UpdateAccounts(state.Accounts);
        UpdateLog(state.Log);
        UpdateModes(state);
        UpdateLaws(state);
        UpdateSanctions(state);
        UpdateFunds(state.Funds);
    }

    private readonly Dictionary<string, Label> _fundBalances = new();

    private void UpdateFunds(List<CityConsoleFundEntry> funds)
    {
        // Строки фондов строим один раз (чтобы не сбивать ввод суммы), потом обновляем только балансы.
        var signature = string.Join(";", funds.Select(f => f.Id));
        if (signature != _fundsSignature)
        {
            _fundsSignature = signature;
            _fundsList.RemoveAllChildren();
            _fundBalances.Clear();

            foreach (var fund in funds)
            {
                var id = fund.Id;
                var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 4 };
                box.AddChild(new Label { Text = fund.Name, FontColorOverride = AccentColor });
                if (fund.Description.Length > 0)
                    box.AddChild(new Label { Text = fund.Description, FontColorOverride = DimColor });

                var balance = new Label();
                box.AddChild(balance);
                _fundBalances[id] = balance;

                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
                var amount = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), MinWidth = 120, PlaceHolder = Loc.GetString("dystopia-city-console-amount") };
                var toFund = new CityButton(Loc.GetString("dystopia-city-console-fund-deposit").ToUpperInvariant(), CityButtonStyle.Normal) { };
                var fromFund = new CityButton(Loc.GetString("dystopia-city-console-fund-withdraw").ToUpperInvariant(), CityButtonStyle.Normal) { };
                toFund.OnPressed += _ =>
                {
                    if (int.TryParse(amount.Text.Trim(), out var value) && value > 0)
                    {
                        OnFundTransfer?.Invoke(id, value, true);
                        amount.Text = string.Empty;
                    }
                };
                fromFund.OnPressed += _ =>
                {
                    if (int.TryParse(amount.Text.Trim(), out var value) && value > 0)
                    {
                        OnFundTransfer?.Invoke(id, value, false);
                        amount.Text = string.Empty;
                    }
                };
                row.AddChild(amount);
                row.AddChild(toFund);
                row.AddChild(fromFund);
                box.AddChild(row);

                _fundsList.AddChild(box);
            }

            if (funds.Count == 0)
                _fundsList.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-funds-empty"), FontColorOverride = DimColor });
        }

        foreach (var fund in funds)
        {
            if (_fundBalances.TryGetValue(fund.Id, out var label))
                label.Text = Loc.GetString("dystopia-city-console-fund-balance", ("amount", fund.Balance));
        }
    }

    private static string LawSignature(CityLaw law)
    {
        return $"{law.Id}|{law.Number}|{law.Title}|{law.Text}|{law.Sanction}|{law.EnactedAt}";
    }

    private void UpdateLaws(CityConsoleBoundUserInterfaceState state)
    {
        _laws = state.Laws;

        // Только что созданная статья выбирается автоматически.
        if (_selectNewestLaw)
        {
            var created = _laws.Where(l => l.Id > _maxLawIdBeforeCreate).OrderByDescending(l => l.Id).FirstOrDefault();
            if (created != null)
            {
                _selectNewestLaw = false;
                SelectLaw(created.Id);
            }
        }

        var signature = string.Join(";", _laws.Select(l => $"{l.Id}:{l.Number}:{l.Title}"));
        if (signature != _lawsSignature)
        {
            _lawsSignature = signature;
            RebuildLawList();
        }

        var selected = _laws.FirstOrDefault(l => l.Id == _selectedLawId);
        if (selected == null)
        {
            if (_selectedLawId != -1)
                SelectLaw(-1);
            return;
        }

        // Статью изменили на сервере — обновляем редактор, если Консул сейчас не печатает в нём.
        if (LawSignature(selected) != _filledLawSignature && !EditorFocused())
            FillEditor(selected);
    }

    private bool EditorFocused()
    {
        return _lawNumber.HasKeyboardFocus() || _lawTitle.HasKeyboardFocus() ||
               _lawText.HasKeyboardFocus() || _lawSanction.HasKeyboardFocus();
    }

    private void RebuildLawList()
    {
        _lawList.RemoveAllChildren();
        foreach (var law in _laws)
        {
            var id = law.Id;
            var button = new CityButton(Loc.GetString("dystopia-laws-article-header", ("number", law.Number), ("title", law.Title)), CityButtonStyle.Tab)
            {
                Active = id == _selectedLawId,
                HorizontalExpand = true,
                Marquee = true,
            };
            button.OnPressed += _ => SelectLaw(id);
            _lawList.AddChild(button);
        }
    }

    private void SelectLaw(int id)
    {
        _selectedLawId = id;
        _deleteArmed = false;
        _lawDelete.Text = Loc.GetString("dystopia-city-console-law-delete").ToUpperInvariant();

        var law = _laws.FirstOrDefault(l => l.Id == id);
        _lawEditor.Visible = law != null;
        _lawHint.Visible = law == null;

        if (law != null)
            FillEditor(law);
        else
            _filledLawSignature = string.Empty;

        RebuildLawList();
    }

    private void FillEditor(CityLaw law)
    {
        _lawNumber.Text = law.Number.ToString();
        _lawTitle.Text = law.Title;
        _lawText.TextRope = new Rope.Leaf(law.Text);
        _lawSanction.Text = law.Sanction;
        _lawEnacted.Text = Loc.GetString("dystopia-laws-article-enacted", ("time", CityLawsUiFragment.FormatTime(law.EnactedAt)));
        _filledLawSignature = LawSignature(law);
    }

    private void SaveSelectedLaw()
    {
        if (_selectedLawId < 0 || !int.TryParse(_lawNumber.Text.Trim(), out var number))
            return;

        OnSaveLaw?.Invoke(_selectedLawId, number, _lawTitle.Text, Rope.Collapse(_lawText.TextRope), _lawSanction.Text);
    }

    private void DeleteSelectedLaw()
    {
        if (_selectedLawId < 0)
            return;

        // Удаление в два нажатия, чтобы не снести статью случайно.
        if (!_deleteArmed)
        {
            _deleteArmed = true;
            _lawDelete.Text = Loc.GetString("dystopia-city-console-law-delete-confirm").ToUpperInvariant();
            return;
        }

        _deleteArmed = false;
        _lawDelete.Text = Loc.GetString("dystopia-city-console-law-delete").ToUpperInvariant();
        OnDeleteLaw?.Invoke(_selectedLawId);
    }

    private void UpdateSanctions(CityConsoleBoundUserInterfaceState state)
    {
        if (_sanctionEdits.Count != state.Sanctions.Count || state.Sanctions.Any(s => !_sanctionEdits.ContainsKey(s.Class)))
        {
            _sanctionsGrid.RemoveAllChildren();
            _sanctionEdits.Clear();
            _lastServerSanctions.Clear();

            _sanctionsGrid.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-sanctions-col-class"), FontColorOverride = DimColor });
            _sanctionsGrid.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-sanctions-col-legal"), FontColorOverride = DimColor });
            _sanctionsGrid.AddChild(new Label { Text = Loc.GetString("dystopia-city-console-sanctions-col-disciplinary"), FontColorOverride = DimColor });

            foreach (var sanction in state.Sanctions)
            {
                var legal = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true, Text = sanction.Legal };
                var disciplinary = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), HorizontalExpand = true, Text = sanction.Disciplinary };
                _sanctionsGrid.AddChild(new Label { Text = sanction.Class, FontColorOverride = AccentColor, MinWidth = 50 });
                _sanctionsGrid.AddChild(legal);
                _sanctionsGrid.AddChild(disciplinary);
                _sanctionEdits[sanction.Class] = (legal, disciplinary);
                _lastServerSanctions[sanction.Class] = (sanction.Legal, sanction.Disciplinary);
            }

            _generalProvision.Text = state.GeneralProvision;
            _lastServerProvision = state.GeneralProvision;
            return;
        }

        foreach (var sanction in state.Sanctions)
        {
            var edits = _sanctionEdits[sanction.Class];
            var last = _lastServerSanctions[sanction.Class];

            if (last.Legal != sanction.Legal && !edits.Legal.HasKeyboardFocus())
                edits.Legal.Text = sanction.Legal;
            if (last.Disciplinary != sanction.Disciplinary && !edits.Disciplinary.HasKeyboardFocus())
                edits.Disciplinary.Text = sanction.Disciplinary;

            _lastServerSanctions[sanction.Class] = (sanction.Legal, sanction.Disciplinary);
        }

        if (_lastServerProvision != state.GeneralProvision && !_generalProvision.HasKeyboardFocus())
            _generalProvision.Text = state.GeneralProvision;
        _lastServerProvision = state.GeneralProvision;
    }

    private void SaveSanctions()
    {
        var list = new List<CitySanctionClass>();
        foreach (var (cls, edits) in _sanctionEdits)
        {
            list.Add(new CitySanctionClass { Class = cls, Legal = edits.Legal.Text, Disciplinary = edits.Disciplinary.Text });
        }

        OnSaveSanctions?.Invoke(list, _generalProvision.Text);
    }

    private void UpdateModes(CityConsoleBoundUserInterfaceState state)
    {
        var current = state.Modes.FirstOrDefault(m => m.Id == state.CurrentMode);
        if (current != null)
        {
            _currentModeLabel.Text = Loc.GetString("dystopia-city-console-current-mode", ("name", current.Name));
            _navModeLabel.Text = current.Name.ToUpperInvariant();
            _navModeLabel.FontColorOverride = current.Color;
            _currentModeLabel.FontColorOverride = current.Color;
            _modeInfo.SetMessage(current.Instructions, DimColor);
        }
        else
        {
            _currentModeLabel.Text = Loc.GetString("dystopia-city-console-no-modes");
            _currentModeLabel.FontColorOverride = DimColor;
            _modeInfo.SetMessage(string.Empty);
        }

        if (state.AnnouncementCooldown > 0)
        {
            _announceButton.Disabled = true;
            _announceCooldownLabel.Text = Loc.GetString("dystopia-city-console-announce-cooldown",
                ("seconds", state.AnnouncementCooldown));
        }
        else
        {
            _announceButton.Disabled = false;
            _announceCooldownLabel.Text = string.Empty;
        }

        // Кнопки режимов пересобираем только когда что-то поменялось.
        var signature = state.CurrentMode + "|" + string.Join(";", state.Modes.Select(m => m.Id));
        if (signature == _modesSignature)
            return;
        _modesSignature = signature;

        _modeButtons.RemoveAllChildren();
        foreach (var mode in state.Modes)
        {
            var id = mode.Id;
            var button = new CityButton(mode.Name.ToUpperInvariant(), mode.Id == state.CurrentMode ? CityButtonStyle.Primary : CityButtonStyle.Normal)
            {
                HorizontalExpand = true,
                MinHeight = 32,
                Disabled = mode.Id == state.CurrentMode,
                TextColor = mode.Color,
            };
            button.OnPressed += _ => OnSetMode?.Invoke(id);
            _modeButtons.AddChild(button);
        }
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
                var salaryEdit = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), MinWidth = 90, Text = job.Salary.ToString() };
                var taxEdit = new LineEdit { StyleBoxOverride = CityUi.Box(CityUi.Input, CityUi.Line, 1, 6, 3), MinWidth = 60, Text = job.Tax.ToString() };
                var save = new CityButton(Loc.GetString("dystopia-city-console-save").ToUpperInvariant(), CityButtonStyle.Normal) { };
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
            _logBox.AddChild(new Label
            {
                Text = line,
                FontOverride = CityUi.Mono(11),
                ClipText = true,
                FontColorOverride = _logBox.ChildCount == 0 ? CityUi.Text : CityUi.Dim,
            });
        }
    }
}
