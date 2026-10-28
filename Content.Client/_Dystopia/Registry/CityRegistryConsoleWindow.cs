// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: окно консоли социоучёта. Два вида: общий список (вкладки «Граждане» / «Розыск» с поиском и фильтрами
// по службе и статусу, «Неопознанные» — кого камеры видели со скрытым лицом и без карты) и личное дело (фото, номер,
// статус, лояльность; вкладки «Сводка», «Журнал», «Маршрут», «Банк»). Смена статуса — отдельным окошком, картинка
// с камеры («Смотреть») — тоже. Окно перерисовывает списки только при изменениях, поэтому то, что человек набирает
// в полях, не сбрасывается обновлениями с сервера.

using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Client.Lobby.UI.ProfileEditorControls;
using Content.Shared._Dystopia.Laws;
using Content.Shared._Dystopia.Registry;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client._Dystopia.Registry;

public sealed class CityRegistryConsoleWindow : CityWindow
{
    public event Action<int?>? OnSelect;
    public event Action<int, CityCitizenStatus, string>? OnSetStatus;
    public event Action<int, int, string, int, string>? OnViolation;
    public event Action<int, int, string>? OnReward;
    public event Action<int, string>? OnNotes;
    public event Action<int>? OnPrint;

    /// <summary>Опознать: запись «Неопознанных» (id) → гражданин (номер).</summary>
    public event Action<int, int>? OnIdentify;

    /// <summary>Смотреть через камеру (null — перестать).</summary>
    public event Action<NetEntity?>? OnWatch;

    private enum ListMode : byte
    {
        Citizens,
        Wanted,
        Unidentified,
    }

    private enum DossierTab : byte
    {
        Summary,
        Journal,
        Route,
        Bank,
    }

    private enum JournalFilter : byte
    {
        All,
        Violations,
        Rewards,
        Service,
    }

    private const int AllId = -1;

    private CityRegistryConsoleUiState? _state;
    private ListMode _mode = ListMode.Citizens;
    private DossierTab _tab = DossierTab.Summary;
    private JournalFilter _filter = JournalFilter.All;

    // --- список ---
    private readonly BoxContainer _listView;
    private readonly CityButton _tabCitizens;
    private readonly CityButton _tabWanted;
    private readonly CityButton _tabUnidentified;
    private readonly BoxContainer _citizensBox;
    private readonly LineEdit _search;
    private readonly CityDropdown _department;
    private readonly CityDropdown _statusFilter;
    private readonly BoxContainer _rows;
    private readonly Label _listFooter;
    private readonly List<string> _departmentIds = new();
    private string _departmentsSignature = string.Empty;
    private string _listSignature = string.Empty;

    // --- неопознанные ---
    private readonly BoxContainer _unidentifiedBox;
    private readonly BoxContainer _unidentifiedRows;
    private readonly Label _unidentifiedFooter;
    private readonly Dictionary<int, Label> _unidentifiedTimes = new();
    private string _unidentifiedSignature = string.Empty;

    // --- личное дело ---
    private readonly BoxContainer _dossierView;
    private readonly Label _dossierPrompt;
    private readonly ProfilePreviewSpriteView _photo;
    private readonly Label _photoCaption;
    private readonly Label _number;
    private readonly PanelContainer _statusBadge;
    private readonly Label _statusText;
    private readonly CityButton _statusButton;
    private readonly CityCard _loyaltyBox;
    private readonly Label _loyaltyValue;
    private readonly CityLoyaltyBar _loyaltyBar;
    private readonly CityWrapLabel _loyaltyHint;
    private readonly CityCard _outsideBox;
    private readonly Label _dismissed;
    private readonly CityWrapLabel _rightsHint;

    private readonly CityButton _subSummary;
    private readonly CityButton _subJournal;
    private readonly CityButton _subRoute;
    private readonly CityButton _subBank;
    private readonly BoxContainer _summaryView;
    private readonly BoxContainer _journalView;
    private readonly BoxContainer _routeView;
    private readonly BoxContainer _bankView;

    // сводка
    private readonly GridContainer _fields;
    private readonly CityWrapLabel _notesText;
    private readonly LineEdit _notesEdit;
    private readonly CityButton _notesEditButton;
    private readonly CityButton _notesSave;
    private readonly CityButton _notesCancel;
    private readonly CityButton _printButton;
    private string _fieldsSignature = string.Empty;
    private readonly BoxContainer _lastSeen;
    private string _lastSeenSignature = string.Empty;
    private readonly BoxContainer _cardList;
    private string _cardsSignature = string.Empty;

    // маршрут
    private readonly BoxContainer _routeRows;
    private string _routeSignature = string.Empty;

    // журнал
    private readonly CityButton _chipAll;
    private readonly CityButton _chipViolations;
    private readonly CityButton _chipRewards;
    private readonly CityButton _chipService;
    private readonly BoxContainer _journalRows;
    private readonly CityButton _addViolation;
    private readonly CityButton _addReward;
    private readonly PanelContainer _violationForm;
    private readonly PanelContainer _rewardForm;
    private readonly CityDropdown _law;
    private readonly CityDropdown _class;
    private readonly List<string> _classIds = new();
    private readonly LineEdit _violationLoyalty;
    private readonly LineEdit _violationDetails;
    private readonly Label _classHint;
    private readonly CityButton _violationWrite;
    private readonly LineEdit _rewardText;
    private readonly LineEdit _rewardAmount;
    private readonly Label _rewardHint;
    private string _journalSignature = string.Empty;
    private string _lawsSignature = string.Empty;
    private string _classesSignature = string.Empty;

    // банк
    private readonly BoxContainer _bankCards;
    private readonly Label _bankAccount;
    private readonly Label _bankAccountState;
    private readonly Label _bankBalance;
    private readonly Label _bankDebt;
    private readonly Label _bankTax;
    private readonly Label _bankNone;
    private readonly BoxContainer _bankHistory;
    private string _bankSignature = string.Empty;

    private int? _photoNumber;
    private CityRegistryStatusWindow? _statusWindow;

    public CityRegistryConsoleWindow()
    {
        WindowTitle = Loc.GetString("dystopia-registry-console-title");
        Subtitle = Loc.GetString("dystopia-registry-console-subtitle");
        Slogan = Loc.GetString("dystopia-registry-console-slogan");
        MinSize = new Vector2(860, 600);
        SetSize = new Vector2(980, 700);

        // ================= Список =================
        _listView = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(_listView);

        var tabs = Row(4);
        _tabCitizens = new CityButton(string.Empty, CityButtonStyle.Tab) { MinWidth = 180 };
        _tabWanted = new CityButton(string.Empty, CityButtonStyle.Tab) { MinWidth = 180 };
        _tabUnidentified = new CityButton(string.Empty, CityButtonStyle.Tab) { MinWidth = 180 };
        _tabCitizens.Label.ClipText = false;
        _tabWanted.Label.ClipText = false;
        _tabUnidentified.Label.ClipText = false;
        _tabCitizens.OnPressed += _ => SetMode(ListMode.Citizens);
        _tabWanted.OnPressed += _ => SetMode(ListMode.Wanted);
        _tabUnidentified.OnPressed += _ => SetMode(ListMode.Unidentified);
        tabs.AddChild(_tabCitizens);
        tabs.AddChild(_tabWanted);
        tabs.AddChild(_tabUnidentified);
        _listView.AddChild(tabs);

        _citizensBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        _listView.AddChild(_citizensBox);

        var filters = Row(10);
        filters.AddChild(Caption("dystopia-registry-search"));
        _search = CityUi.Field(Loc.GetString("dystopia-registry-search-placeholder"));
        _search.HorizontalExpand = true;
        _search.OnTextChanged += _ => RedrawList(true);
        filters.AddChild(_search);
        filters.AddChild(Caption("dystopia-registry-filter-department"));
        _department = new CityDropdown { MinWidth = 210 };
        _department.OnItemSelected += _ => RedrawList(true);
        filters.AddChild(_department);
        filters.AddChild(Caption("dystopia-registry-filter-status"));
        _statusFilter = new CityDropdown { MinWidth = 170 };
        _statusFilter.AddItem(Loc.GetString("dystopia-registry-filter-all"), AllId);
        foreach (var status in Enum.GetValues<CityCitizenStatus>())
        {
            _statusFilter.AddItem(StatusText(status), (int) status);
        }
        _statusFilter.OnItemSelected += _ => RedrawList(true);
        filters.AddChild(_statusFilter);
        _citizensBox.AddChild(filters);

        var table = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        table.VerticalExpand = true;
        var tableBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        table.AddChild(tableBox);
        var header = new PanelContainer { PanelOverride = CityUi.Box(CityUi.Bg2, CityUi.Bg2, 0, 8, 4) };
        header.AddChild(ListColumns(
            Small("dystopia-registry-col-number"),
            Small("dystopia-registry-col-name"),
            Small("dystopia-registry-col-job"),
            Small("dystopia-registry-col-status"),
            Small("dystopia-registry-col-loyalty")));
        tableBox.AddChild(header);
        tableBox.AddChild(CityUi.Separator(CityUi.Frame));
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        scroll.AddChild(_rows);
        tableBox.AddChild(scroll);
        _citizensBox.AddChild(table);

        _listFooter = CityUi.MakeLabel(string.Empty, CityUi.Dim, CityUi.Regular(11));
        _citizensBox.AddChild(_listFooter);

        // --- неопознанные ---
        _unidentifiedBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
            Visible = false,
        };
        _unidentifiedBox.AddChild(CityUi.Hint(Loc.GetString("dystopia-registry-unidentified-hint")));
        var unidentifiedTable = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        unidentifiedTable.VerticalExpand = true;
        var unidentifiedScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _unidentifiedRows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        unidentifiedScroll.AddChild(_unidentifiedRows);
        unidentifiedTable.AddChild(unidentifiedScroll);
        _unidentifiedBox.AddChild(unidentifiedTable);
        _unidentifiedFooter = CityUi.MakeLabel(string.Empty, CityUi.Dim, CityUi.Regular(11));
        _unidentifiedBox.AddChild(_unidentifiedFooter);
        _listView.AddChild(_unidentifiedBox);

        // ================= Личное дело =================
        _dossierView = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
            Visible = false,
        };
        Contents.AddChild(_dossierView);

        var top = Row(12);
        var back = CityUi.MakeButton(Loc.GetString("dystopia-registry-back"));
        back.OnPressed += _ => OnSelect?.Invoke(null);
        top.AddChild(back);
        _dossierPrompt = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Regular(12));
        _dossierPrompt.VerticalAlignment = VAlignment.Center;
        top.AddChild(_dossierPrompt);
        _dossierView.AddChild(top);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 14,
        };
        _dossierView.AddChild(body);

        // --- левая колонка ---
        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            MinWidth = 230,
            MaxWidth = 230,
        };
        body.AddChild(left);

        var photoCard = new CityCard { MinSize = new Vector2(230, 240) };
        var photoBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        _photo = new ProfilePreviewSpriteView
        {
            OverrideDirection = Direction.South,
            Stretch = SpriteView.StretchMode.None,
            Scale = new Vector2(5, 5),
            SetSize = new Vector2(200, 200),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            VerticalExpand = true,
        };
        photoBox.AddChild(_photo);
        _photoCaption = CityUi.MakeLabel(string.Empty, CityUi.Dim, CityUi.Regular(10));
        _photoCaption.HorizontalAlignment = HAlignment.Center;
        photoBox.AddChild(_photoCaption);
        photoCard.AddChild(photoBox);
        left.AddChild(photoCard);

        _number = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Title(13));
        left.AddChild(_number);

        _statusBadge = new PanelContainer { HorizontalAlignment = HAlignment.Left };
        _statusText = CityUi.MakeLabel(string.Empty, CityUi.Text, CityUi.Bold(12));
        _statusBadge.AddChild(_statusText);
        left.AddChild(_statusBadge);

        _dismissed = CityUi.MakeLabel(Loc.GetString("dystopia-registry-dismissed"), Danger, CityUi.Bold(11));
        left.AddChild(_dismissed);

        _statusButton = CityUi.MakeButton(Loc.GetString("dystopia-registry-change-status"));
        _statusButton.OnPressed += _ => OpenStatusWindow();
        left.AddChild(_statusButton);

        _loyaltyBox = new CityCard();
        var loyalty = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var loyaltyHead = Row(6);
        var loyaltyCaption = Small("dystopia-registry-loyalty");
        loyaltyCaption.HorizontalExpand = true;
        loyaltyHead.AddChild(loyaltyCaption);
        _loyaltyValue = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Bold(12));
        loyaltyHead.AddChild(_loyaltyValue);
        loyalty.AddChild(loyaltyHead);
        _loyaltyBar = new CityLoyaltyBar();
        loyalty.AddChild(_loyaltyBar);
        // в колонку шириной 230 «норма должности — 40 · −40 по журналу» одной строкой не влезает — переносим
        _loyaltyHint = CityUi.Hint(string.Empty);
        loyalty.AddChild(_loyaltyHint);
        _loyaltyBox.AddChild(loyalty);
        left.AddChild(_loyaltyBox);

        _outsideBox = new CityCard();
        _outsideBox.PanelOverride = CityUi.Box(CityUi.Panel, GoldDark, 1, 10, 8);
        var outside = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        // заголовок в две строки: в колонку шириной 230 одной строкой он не влезает
        foreach (var key in new[] { "dystopia-registry-outside-loyalty-1", "dystopia-registry-outside-loyalty-2" })
        {
            var title = CityUi.MakeLabel(Loc.GetString(key), Gold, CityUi.Bold(12));
            title.HorizontalAlignment = HAlignment.Center;
            outside.AddChild(title);
        }
        outside.AddChild(CityUi.Hint(Loc.GetString("dystopia-registry-outside-loyalty-hint")));
        _outsideBox.AddChild(outside);
        left.AddChild(_outsideBox);

        _rightsHint = CityUi.Hint(Loc.GetString("dystopia-registry-readonly"));
        left.AddChild(_rightsHint);

        // --- правая колонка ---
        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        body.AddChild(right);

        var subTabs = Row(4);
        _subSummary = SubTab("dystopia-registry-subtab-summary", DossierTab.Summary);
        _subJournal = SubTab("dystopia-registry-subtab-journal", DossierTab.Journal);
        _subRoute = SubTab("dystopia-registry-subtab-route", DossierTab.Route);
        _subBank = SubTab("dystopia-registry-subtab-bank", DossierTab.Bank);
        subTabs.AddChild(_subSummary);
        subTabs.AddChild(_subJournal);
        subTabs.AddChild(_subRoute);
        subTabs.AddChild(_subBank);
        right.AddChild(subTabs);
        right.AddChild(CityUi.Separator(CityUi.Accent));

        // сводка
        _summaryView = Column();
        _fields = new GridContainer { Columns = 2, HorizontalExpand = true, VSeparationOverride = 8, HSeparationOverride = 24 };
        _summaryView.AddChild(_fields);

        var seen = new CityCard();
        var seenBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        seenBox.AddChild(Small("dystopia-registry-last-seen"));
        _lastSeen = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        seenBox.AddChild(_lastSeen);
        seen.AddChild(seenBox);
        _summaryView.AddChild(seen);

        var cards = new CityCard();
        var cardsBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        cardsBox.AddChild(Small("dystopia-registry-cards"));
        _cardList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        cardsBox.AddChild(_cardList);
        cards.AddChild(cardsBox);
        _summaryView.AddChild(cards);

        var notes = new CityCard();
        var notesBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var notesHead = Row(6);
        var notesCaption = Small("dystopia-registry-notes");
        notesCaption.HorizontalExpand = true;
        notesHead.AddChild(notesCaption);
        _notesEditButton = CityUi.MakeButton(Loc.GetString("dystopia-registry-notes-edit"));
        _notesEditButton.OnPressed += _ => SetNotesEditing(true);
        notesHead.AddChild(_notesEditButton);
        notesBox.AddChild(notesHead);
        _notesText = CityUi.Hint(string.Empty, CityUi.Text);
        notesBox.AddChild(_notesText);
        _notesEdit = CityUi.Field(Loc.GetString("dystopia-registry-notes-placeholder"));
        _notesEdit.HorizontalExpand = true;
        _notesEdit.Visible = false;
        notesBox.AddChild(_notesEdit);
        var notesButtons = Row(6);
        _notesSave = CityUi.MakeButton(Loc.GetString("dystopia-registry-notes-save"), CityButtonStyle.Primary);
        _notesSave.OnPressed += _ =>
        {
            if (Dossier is { } d)
                OnNotes?.Invoke(d.Number, _notesEdit.Text);
            SetNotesEditing(false);
        };
        _notesCancel = CityUi.MakeButton(Loc.GetString("dystopia-registry-notes-cancel"));
        _notesCancel.OnPressed += _ => SetNotesEditing(false);
        notesButtons.AddChild(_notesSave);
        notesButtons.AddChild(_notesCancel);
        notesBox.AddChild(notesButtons);
        notes.AddChild(notesBox);
        _summaryView.AddChild(notes);

        _summaryView.AddChild(new Control { VerticalExpand = true });
        var actions = Row(8);
        var toBank = CityUi.MakeButton(Loc.GetString("dystopia-registry-action-bank"));
        toBank.OnPressed += _ => SetTab(DossierTab.Bank);
        var toJournal = CityUi.MakeButton(Loc.GetString("dystopia-registry-action-journal"));
        toJournal.OnPressed += _ => SetTab(DossierTab.Journal);
        _printButton = CityUi.MakeButton(Loc.GetString("dystopia-registry-action-print"), CityButtonStyle.Danger);
        _printButton.OnPressed += _ =>
        {
            if (Dossier is { } d)
                OnPrint?.Invoke(d.Number);
        };
        actions.AddChild(toBank);
        actions.AddChild(toJournal);
        actions.AddChild(_printButton);
        _summaryView.AddChild(actions);
        right.AddChild(_summaryView);

        // журнал
        _journalView = Column();
        var chips = Row(6);
        _chipAll = Chip(JournalFilter.All);
        _chipViolations = Chip(JournalFilter.Violations);
        _chipRewards = Chip(JournalFilter.Rewards);
        _chipService = Chip(JournalFilter.Service);
        chips.AddChild(_chipAll);
        chips.AddChild(_chipViolations);
        chips.AddChild(_chipRewards);
        chips.AddChild(_chipService);
        _journalView.AddChild(chips);

        // кнопки записи — отдельной строкой: рядом с фильтрами они не влезают в ширину вкладки
        var addRow = Row(6);
        addRow.AddChild(new Control { HorizontalExpand = true });
        _addViolation = CityUi.MakeButton(Loc.GetString("dystopia-registry-add-violation"), CityButtonStyle.Danger);
        _addViolation.OnPressed += _ => ToggleForm(true);
        _addReward = CityUi.MakeButton(Loc.GetString("dystopia-registry-add-reward"));
        _addReward.OnPressed += _ => ToggleForm(false);
        addRow.AddChild(_addViolation);
        addRow.AddChild(_addReward);
        _journalView.AddChild(addRow);

        var journalPanel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        journalPanel.VerticalExpand = true;
        var journalScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _journalRows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        journalScroll.AddChild(_journalRows);
        journalPanel.AddChild(journalScroll);
        _journalView.AddChild(journalPanel);

        // форма нарушения
        _violationForm = new PanelContainer { PanelOverride = CityUi.Box(CityUi.Panel, Danger, 1, 10, 8), Visible = false };
        var violation = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        violation.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-registry-form-violation").ToUpperInvariant(), Danger, CityUi.Bold(11)));
        var lawRow = Row(8);
        lawRow.AddChild(Caption("dystopia-registry-form-law"));
        _law = new CityDropdown { HorizontalExpand = true };
        _law.OnItemSelected += _ => RefreshClasses(true);
        lawRow.AddChild(_law);
        violation.AddChild(lawRow);
        var classRow = Row(8);
        classRow.AddChild(Caption("dystopia-registry-form-class"));
        _class = new CityDropdown { MinWidth = 220 };
        _class.OnItemSelected += _ => RefreshClassHint(true);
        classRow.AddChild(_class);
        classRow.AddChild(Caption("dystopia-registry-form-loyalty"));
        _violationLoyalty = CityUi.Field(string.Empty, 50);
        classRow.AddChild(_violationLoyalty);
        _classHint = CityUi.MakeLabel(string.Empty, CityUi.Dim, CityUi.Regular(11));
        _classHint.VerticalAlignment = VAlignment.Center;
        classRow.AddChild(_classHint);
        violation.AddChild(classRow);
        var detailsRow = Row(8);
        detailsRow.AddChild(Caption("dystopia-registry-form-details"));
        _violationDetails = CityUi.Field(Loc.GetString("dystopia-registry-form-details-placeholder"));
        _violationDetails.HorizontalExpand = true;
        detailsRow.AddChild(_violationDetails);
        _violationWrite = CityUi.MakeButton(Loc.GetString("dystopia-registry-form-write"), CityButtonStyle.Danger);
        _violationWrite.OnPressed += _ => SendViolation();
        detailsRow.AddChild(_violationWrite);
        violation.AddChild(detailsRow);
        _violationForm.AddChild(violation);
        _journalView.AddChild(_violationForm);

        // форма поощрения
        _rewardForm = new PanelContainer { PanelOverride = CityUi.Box(CityUi.Panel, CityUi.Accent, 1, 10, 8), Visible = false };
        var reward = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        reward.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-registry-form-reward").ToUpperInvariant(), CityUi.Accent, CityUi.Bold(11)));
        var rewardRow = Row(8);
        rewardRow.AddChild(Caption("dystopia-registry-form-reward-text"));
        _rewardText = CityUi.Field(Loc.GetString("dystopia-registry-form-reward-placeholder"));
        _rewardText.HorizontalExpand = true;
        rewardRow.AddChild(_rewardText);
        rewardRow.AddChild(Caption("dystopia-registry-form-reward-amount"));
        _rewardAmount = CityUi.Field(string.Empty, 50);
        _rewardAmount.Text = "1";
        rewardRow.AddChild(_rewardAmount);
        _rewardHint = CityUi.MakeLabel(string.Empty, CityUi.Dim, CityUi.Regular(11));
        _rewardHint.VerticalAlignment = VAlignment.Center;
        rewardRow.AddChild(_rewardHint);
        var rewardWrite = CityUi.MakeButton(Loc.GetString("dystopia-registry-form-reward-write"), CityButtonStyle.Primary);
        rewardWrite.OnPressed += _ => SendReward();
        rewardRow.AddChild(rewardWrite);
        reward.AddChild(rewardRow);
        _rewardForm.AddChild(reward);
        _journalView.AddChild(_rewardForm);
        right.AddChild(_journalView);

        // маршрут
        _routeView = Column();
        _routeView.AddChild(Small("dystopia-registry-route-header"));
        var routePanel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        routePanel.VerticalExpand = true;
        var routeScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _routeRows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        routeScroll.AddChild(_routeRows);
        routePanel.AddChild(routeScroll);
        _routeView.AddChild(routePanel);
        right.AddChild(_routeView);

        // банк
        _bankView = Column();
        _bankCards = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10, HorizontalExpand = true };
        (_bankAccount, _bankAccountState) = BankCard(_bankCards, "dystopia-registry-bank-account");
        (_bankBalance, _) = BankCard(_bankCards, "dystopia-registry-bank-balance", "dystopia-registry-bank-marks");
        (_bankDebt, _) = BankCard(_bankCards, "dystopia-registry-bank-debt", "dystopia-registry-bank-debt-hint");
        (_bankTax, _) = BankCard(_bankCards, "dystopia-registry-bank-tax", "dystopia-registry-bank-tax-hint");
        _bankView.AddChild(_bankCards);
        _bankNone = CityUi.MakeLabel(Loc.GetString("dystopia-registry-bank-none"), CityUi.Dim);
        _bankView.AddChild(_bankNone);
        var bankHead = Row(6);
        var bankCaption = Small("dystopia-registry-bank-history");
        bankCaption.HorizontalExpand = true;
        bankHead.AddChild(bankCaption);
        bankHead.AddChild(Small("dystopia-registry-bank-readonly"));
        _bankView.AddChild(bankHead);
        var bankPanel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 6);
        bankPanel.VerticalExpand = true;
        var bankScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _bankHistory = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 2 };
        bankScroll.AddChild(_bankHistory);
        bankPanel.AddChild(bankScroll);
        _bankView.AddChild(bankPanel);
        right.AddChild(_bankView);

        SetTab(DossierTab.Summary);
        SetNotesEditing(false);
    }

    private CityRegistryDossier? Dossier => _state?.Dossier;

    // ======================= Обновление =======================

    public void UpdateState(CityRegistryConsoleUiState state)
    {
        var previous = _state?.Dossier?.Number;
        _state = state;

        var dossier = state.Dossier;
        _listView.Visible = dossier == null;
        _dossierView.Visible = dossier != null;

        if (dossier == null)
        {
            _statusWindow?.Close();
            RedrawList(false);
            return;
        }

        if (previous != dossier.Number)
        {
            // открыли другое дело — с вкладки «Сводка», формы закрыты
            SetTab(DossierTab.Summary);
            SetNotesEditing(false);
            _violationForm.Visible = false;
            _rewardForm.Visible = false;
            _violationDetails.Text = string.Empty;
            _rewardText.Text = string.Empty;
            _journalSignature = _fieldsSignature = _bankSignature = _classesSignature = string.Empty;
            _routeSignature = _lastSeenSignature = _cardsSignature = string.Empty;
            _statusWindow?.Close();
        }

        RedrawDossier(dossier);
        _statusWindow?.Update(dossier, state.CanEdit, state.CanSupreme);
    }

    public override void Close()
    {
        _statusWindow?.Close();
        base.Close();
    }

    // ======================= Список =======================

    private void SetMode(ListMode mode)
    {
        _mode = mode;
        RedrawList(true);
    }

    private void RedrawList(bool force)
    {
        if (_state == null)
            return;

        var citizens = _state.Citizens;
        _tabCitizens.Text = Loc.GetString("dystopia-registry-tab-citizens", ("count", citizens.Count)).ToUpperInvariant();
        _tabWanted.Text = Loc.GetString("dystopia-registry-tab-wanted",
            ("count", citizens.Count(c => c.Status == CityCitizenStatus.Wanted))).ToUpperInvariant();
        _tabUnidentified.Text = Loc.GetString("dystopia-registry-tab-unidentified",
            ("count", _state.Unidentified.Count)).ToUpperInvariant();
        _tabCitizens.Active = _mode == ListMode.Citizens;
        _tabWanted.Active = _mode == ListMode.Wanted;
        _tabUnidentified.Active = _mode == ListMode.Unidentified;
        _citizensBox.Visible = _mode != ListMode.Unidentified;
        _unidentifiedBox.Visible = _mode == ListMode.Unidentified;

        if (_mode == ListMode.Unidentified)
        {
            RedrawUnidentified(force);
            return;
        }

        var wantedOnly = _mode == ListMode.Wanted;

        // службы для фильтра
        var departmentsSignature = string.Join(";", _state.Departments.Select(d => d.Id));
        if (departmentsSignature != _departmentsSignature)
        {
            _departmentsSignature = departmentsSignature;
            var selected = _department.SelectedId >= 0 && _department.SelectedId < _departmentIds.Count
                ? _departmentIds[_department.SelectedId]
                : null;
            _department.Clear();
            _departmentIds.Clear();
            _department.AddItem(Loc.GetString("dystopia-registry-filter-all"), AllId);
            foreach (var entry in _state.Departments)
            {
                _department.AddItem(entry.Name, _departmentIds.Count);
                _departmentIds.Add(entry.Id);
            }

            if (selected != null && _departmentIds.IndexOf(selected) is var index and >= 0)
                _department.SelectId(index);
        }

        var search = _search.Text.Trim();
        var department = _department.SelectedId >= 0 && _department.SelectedId < _departmentIds.Count
            ? _departmentIds[_department.SelectedId]
            : null;
        var status = _statusFilter.SelectedId;

        var shown = citizens.Where(c =>
                (!wantedOnly || c.Status == CityCitizenStatus.Wanted) &&
                (department == null || c.Department == department) &&
                (status == AllId || (int) c.Status == status) &&
                (search.Length == 0 ||
                 c.Number.ToString().Contains(search) ||
                 c.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                 c.JobTitle.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();

        _listFooter.Text = Loc.GetString("dystopia-registry-list-footer",
            ("shown", shown.Count),
            ("total", citizens.Count),
            ("wanted", citizens.Count(c => c.Status == CityCitizenStatus.Wanted)),
            ("supervised", citizens.Count(c => c.Status == CityCitizenStatus.Supervised)));

        var signature = string.Join(";", shown.Select(c => $"{c.Number}:{c.Name}:{c.Status}:{c.Loyalty}:{c.JobTitle}"));
        if (!force && signature == _listSignature)
            return;
        _listSignature = signature;

        _rows.RemoveAllChildren();
        if (shown.Count == 0)
        {
            var empty = CityUi.MakeLabel(Loc.GetString("dystopia-registry-list-empty"), CityUi.Dim);
            empty.Margin = new Thickness(8, 6);
            _rows.AddChild(empty);
            return;
        }

        foreach (var citizen in shown)
        {
            var number = citizen.Number;
            var row = new CityRegistryRow();
            row.OnPressed += _ => OnSelect?.Invoke(number);

            var outside = citizen.Loyalty == null;
            var statusLabel = CityUi.MakeLabel(
                outside ? Loc.GetString("dystopia-registry-head") : StatusText(citizen.Status),
                outside ? Gold : StatusColor(citizen.Status),
                CityUi.Regular(12));
            var loyaltyLabel = CityUi.MakeLabel(outside ? "★" : citizen.Loyalty.ToString() ?? string.Empty,
                outside ? Gold : CityUi.Text, CityUi.Regular(12));

            row.AddChild(ListColumns(
                CityUi.MakeLabel(citizen.Number.ToString(), CityUi.Text, CityUi.Regular(12)),
                CityUi.MakeLabel(citizen.Name, CityUi.Glow, CityUi.Regular(12)),
                CityUi.MakeLabel(citizen.JobTitle, CityUi.Text, CityUi.Regular(12)),
                statusLabel,
                loyaltyLabel));
            _rows.AddChild(row);
        }
    }

    /// <summary>Строка таблицы: №, имя (тянется), должность, статус, лояльность.</summary>
    private static BoxContainer ListColumns(Label number, Label name, Label job, Label status, Label loyalty)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10, HorizontalExpand = true };
        number.MinWidth = 70;
        name.HorizontalExpand = true;
        name.ClipText = true;
        job.MinWidth = 230;
        job.MaxWidth = 230;
        job.ClipText = true;
        status.MinWidth = 160;
        loyalty.MinWidth = 60;
        loyalty.Align = Label.AlignMode.Right;
        row.AddChild(number);
        row.AddChild(name);
        row.AddChild(job);
        row.AddChild(status);
        row.AddChild(loyalty);
        return row;
    }

    // ======================= Личное дело =======================

    private void RedrawDossier(CityRegistryDossier dossier)
    {
        var state = _state!;
        var outside = dossier.Loyalty == null;

        _dossierPrompt.Text = Loc.GetString("dystopia-registry-dossier-prompt", ("number", dossier.Number.ToString()));
        _number.Text = $"№ {dossier.Number}";

        // фото — только когда открыли другое дело (собирать куклу дорого)
        if (_photoNumber != dossier.Number)
        {
            _photoNumber = dossier.Number;
            if (dossier.Appearance is { } appearance)
                _photo.LoadPreview(appearance, null, false);
            else
                _photo.ClearPreview();
        }
        _photoCaption.Text = Loc.GetString("dystopia-registry-photo-caption", ("time", Clock(dossier.JoinedAt)));

        // статус
        var statusColor = outside ? Gold : StatusColor(dossier.Status);
        _statusText.Text = (outside ? Loc.GetString("dystopia-registry-head") : StatusText(dossier.Status)).ToUpperInvariant();
        _statusText.FontColorOverride = statusColor;
        _statusBadge.PanelOverride = CityUi.Box(statusColor.WithAlpha(0.12f), statusColor, 1, 8, 2);
        _dismissed.Visible = dossier.Dismissed;
        _statusButton.Text = Loc.GetString(outside ? "dystopia-registry-status-locked" : "dystopia-registry-change-status").ToUpperInvariant();
        _statusButton.Disabled = outside || !state.CanEdit;
        _rightsHint.Visible = !outside && !state.CanEdit;

        // лояльность
        _loyaltyBox.Visible = !outside;
        _outsideBox.Visible = outside;
        if (dossier.Loyalty is { } loyalty)
        {
            _loyaltyValue.Text = $"{loyalty} / {dossier.MaxLoyalty}";
            _loyaltyBar.Set(loyalty, dossier.MaxLoyalty, dossier.LoyaltyNorm, LoyaltyColor(loyalty, dossier.LoyaltyNorm));
            var norm = dossier.LoyaltyNorm ?? 0;
            var delta = loyalty - norm;
            _loyaltyHint.Text = Loc.GetString("dystopia-registry-loyalty-norm",
                ("norm", norm), ("delta", delta > 0 ? $"+{delta}" : delta.ToString()));
        }

        // вкладки: у Консула журнал и банк закрыты
        _subJournal.Disabled = outside;
        _subRoute.Disabled = outside;
        _subBank.Disabled = outside;
        _subJournal.Text = (outside
            ? Loc.GetString("dystopia-registry-subtab-journal-closed")
            : Loc.GetString("dystopia-registry-subtab-journal-count", ("count", dossier.Journal.Count(e => e.Kind != CityRegistryEntryKind.Service)))).ToUpperInvariant();
        _subBank.Text = Loc.GetString(outside ? "dystopia-registry-subtab-bank-closed" : "dystopia-registry-subtab-bank").ToUpperInvariant();
        if (outside && _tab is DossierTab.Journal or DossierTab.Route or DossierTab.Bank)
            SetTab(DossierTab.Summary);

        RedrawFields(dossier, outside);
        RedrawNotes(dossier, outside, state.CanEdit);
        _printButton.Disabled = outside || !state.CanEdit || dossier.Status != CityCitizenStatus.Wanted;

        RedrawJournal(dossier, state);
        RedrawLastSeen(dossier, outside, state.CanEdit);
        RedrawCards(dossier);
        RedrawRoute(dossier, state.CanEdit);
        RedrawBank(dossier);
    }

    private void RedrawFields(CityRegistryDossier dossier, bool outside)
    {
        var signature = $"{dossier.Number}:{dossier.Name}:{dossier.JobTitle}:{dossier.Department}:{outside}";
        if (signature == _fieldsSignature)
            return;
        _fieldsSignature = signature;

        _fields.RemoveAllChildren();
        var classified = Loc.GetString("dystopia-registry-classified");
        Field("dystopia-registry-field-name", dossier.Name, CityUi.Glow);
        Field("dystopia-registry-field-age", dossier.Age.ToString());
        Field("dystopia-registry-field-species", $"{dossier.Species} · {GenderText(dossier.Gender)}");
        Field("dystopia-registry-field-joined", Loc.GetString("dystopia-registry-joined", ("time", Clock(dossier.JoinedAt))));
        Field("dystopia-registry-field-job", dossier.JobTitle, outside ? Gold : null);
        Field("dystopia-registry-field-department", dossier.Department);
        Field("dystopia-registry-field-fingerprint", outside ? classified : dossier.Fingerprint ?? "—");
        Field("dystopia-registry-field-dna", outside ? classified : dossier.Dna ?? "—");
    }

    private void Field(string caption, string value, Color? color = null)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        box.AddChild(Small(caption));
        var label = CityUi.MakeLabel(value, color ?? CityUi.Text, CityUi.Regular(12));
        label.ClipText = true;
        box.AddChild(label);
        _fields.AddChild(box);
    }

    private void RedrawNotes(CityRegistryDossier dossier, bool outside, bool canEdit)
    {
        _notesText.Text = dossier.Notes.Length > 0 ? dossier.Notes : Loc.GetString("dystopia-registry-notes-empty");
        _notesText.FontColorOverride = dossier.Notes.Length > 0 ? CityUi.Text : CityUi.Dim;
        _notesEditButton.Visible = canEdit && !outside && !_notesEdit.Visible;
        if (!canEdit || outside)
            SetNotesEditing(false);
    }

    private void SetNotesEditing(bool editing)
    {
        if (editing && Dossier is { } dossier)
            _notesEdit.Text = dossier.Notes;

        _notesEdit.Visible = editing;
        _notesSave.Visible = editing;
        _notesCancel.Visible = editing;
        _notesText.Visible = !editing;
        _notesEditButton.Visible = !editing && _state is { CanEdit: true } && Dossier is { Loyalty: not null };
        if (editing)
            _notesEdit.GrabKeyboardFocus();
    }

    private void SetTab(DossierTab tab)
    {
        _tab = tab;
        _subSummary.Active = tab == DossierTab.Summary;
        _subJournal.Active = tab == DossierTab.Journal;
        _subRoute.Active = tab == DossierTab.Route;
        _subBank.Active = tab == DossierTab.Bank;
        _summaryView.Visible = tab == DossierTab.Summary;
        _journalView.Visible = tab == DossierTab.Journal;
        _routeView.Visible = tab == DossierTab.Route;
        _bankView.Visible = tab == DossierTab.Bank;
    }

    // ======================= Журнал =======================

    private void RedrawJournal(CityRegistryDossier dossier, CityRegistryConsoleUiState state)
    {
        var journal = dossier.Journal;
        _chipAll.Text = ChipText(JournalFilter.All, journal.Count);
        _chipViolations.Text = ChipText(JournalFilter.Violations, journal.Count(e => e.Kind is CityRegistryEntryKind.Violation or CityRegistryEntryKind.Fine));
        _chipRewards.Text = ChipText(JournalFilter.Rewards, journal.Count(e => e.Kind == CityRegistryEntryKind.Reward));
        _chipService.Text = ChipText(JournalFilter.Service, journal.Count(e => e.Kind == CityRegistryEntryKind.Service));
        _chipAll.Active = _filter == JournalFilter.All;
        _chipViolations.Active = _filter == JournalFilter.Violations;
        _chipRewards.Active = _filter == JournalFilter.Rewards;
        _chipService.Active = _filter == JournalFilter.Service;

        _addViolation.Visible = state.CanEdit;
        _addReward.Visible = state.CanReward;
        if (!state.CanEdit)
            _violationForm.Visible = false;
        if (!state.CanReward)
            _rewardForm.Visible = false;
        _rewardHint.Text = Loc.GetString("dystopia-registry-form-reward-range", ("max", state.MaxReward));

        RefreshLaws(state);
        RefreshClasses(false);

        var signature = $"{dossier.Number}:{_filter}:{journal.Count}:{(journal.Count > 0 ? journal[^1].Time : TimeSpan.Zero)}";
        if (signature == _journalSignature)
            return;
        _journalSignature = signature;

        _journalRows.RemoveAllChildren();
        var shown = journal.Where(e => _filter switch
            {
                // штрафы — среди нарушений
                JournalFilter.Violations => e.Kind is CityRegistryEntryKind.Violation or CityRegistryEntryKind.Fine,
                JournalFilter.Rewards => e.Kind == CityRegistryEntryKind.Reward,
                JournalFilter.Service => e.Kind == CityRegistryEntryKind.Service,
                _ => true,
            })
            .Reverse()
            .ToList();

        if (shown.Count == 0)
        {
            var empty = CityUi.MakeLabel(Loc.GetString("dystopia-registry-journal-empty"), CityUi.Dim);
            empty.Margin = new Thickness(8, 6);
            _journalRows.AddChild(empty);
            return;
        }

        foreach (var entry in shown)
        {
            var color = entry.Kind switch
            {
                CityRegistryEntryKind.Violation => Danger,
                CityRegistryEntryKind.Reward => CityUi.Accent,
                CityRegistryEntryKind.Fine => Gold,
                _ => CityUi.Text,
            };

            var row = new PanelContainer { PanelOverride = CityUi.Box(Color.Transparent, CityUi.Line, 0, 8, 5) };
            ((StyleBoxFlat) row.PanelOverride).BorderThickness = new Thickness(0, 0, 0, 1);
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };

            var time = CityUi.MakeLabel(Clock(entry.Time), CityUi.Dim, CityUi.Regular(11));
            time.MinWidth = 48;
            line.AddChild(time);

            var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
            text.AddChild(new CityWrapLabel(entry.Text, color));
            if (entry.Details.Length > 0)
                text.AddChild(CityUi.Hint(entry.Details));
            text.AddChild(CityUi.MakeLabel(entry.Author, CityUi.Dim, CityUi.Regular(10)));
            line.AddChild(text);

            var delta = CityUi.MakeLabel(entry.LoyaltyDelta == 0 ? "—" : entry.LoyaltyDelta > 0 ? $"+{entry.LoyaltyDelta}" : entry.LoyaltyDelta.ToString(),
                entry.LoyaltyDelta < 0 ? Danger : entry.LoyaltyDelta > 0 ? CityUi.Accent : CityUi.Dim, CityUi.Bold(12));
            delta.MinWidth = 40;
            delta.Align = Label.AlignMode.Right;
            line.AddChild(delta);

            row.AddChild(line);
            _journalRows.AddChild(row);
        }
    }

    private void ToggleForm(bool violation)
    {
        var form = violation ? _violationForm : _rewardForm;
        var other = violation ? _rewardForm : _violationForm;
        form.Visible = !form.Visible;
        other.Visible = false;
    }

    private void RefreshLaws(CityRegistryConsoleUiState state)
    {
        var signature = string.Join(";", state.Laws.Select(l => $"{l.Id}:{l.Number}:{l.Title}:{string.Join(",", l.Classes)}"));
        if (signature == _lawsSignature)
            return;
        _lawsSignature = signature;

        var selected = _law.SelectedId;
        _law.Clear();
        foreach (var law in state.Laws)
        {
            _law.AddItem(Loc.GetString("dystopia-registry-form-law-item", ("number", law.Number), ("title", law.Title)), law.Id);
        }

        if (state.Laws.Any(l => l.Id == selected))
            _law.SelectId(selected);
        _classesSignature = string.Empty;
    }

    private CityRegistryLawEntry? SelectedLaw()
    {
        return _state?.Laws.FirstOrDefault(l => l.Id == _law.SelectedId);
    }

    /// <summary>Классы для выбранной статьи; повторное нарушение открывает класс выше.</summary>
    private void RefreshClasses(bool force)
    {
        if (_state == null || Dossier is not { } dossier || SelectedLaw() is not { } law)
        {
            _class.Clear();
            _classIds.Clear();
            _violationWrite.Disabled = true;
            return;
        }

        var repeat = CityRegistryRules.IsRepeat(dossier.Journal, law.Id);
        var allowed = CityRegistryRules.AllowedClasses(_state.Sanctions, law, repeat);
        var signature = $"{law.Id}:{repeat}:{string.Join(",", allowed)}";
        if (!force && signature == _classesSignature)
            return;
        _classesSignature = signature;

        var previous = _class.SelectedId >= 0 && _class.SelectedId < _classIds.Count ? _classIds[_class.SelectedId] : null;
        _class.Clear();
        _classIds.Clear();
        foreach (var cls in allowed)
        {
            var raised = law.Classes.Count > 0 && !law.Classes.Contains(cls);
            _class.AddItem(Loc.GetString(raised ? "dystopia-registry-form-class-repeat" : "dystopia-registry-form-class-item",
                ("class", cls)), _classIds.Count);
            _classIds.Add(cls);
        }

        if (previous != null && _classIds.IndexOf(previous) is var index and >= 0)
            _class.SelectId(index);

        _violationWrite.Disabled = _classIds.Count == 0;
        RefreshClassHint(true);
    }

    private CitySanctionClass? SelectedSanction()
    {
        if (_state == null || _class.SelectedId < 0 || _class.SelectedId >= _classIds.Count)
            return null;

        var cls = _classIds[_class.SelectedId];
        return _state.Sanctions.FirstOrDefault(s => s.Class == cls);
    }

    private void RefreshClassHint(bool resetValue)
    {
        if (SelectedSanction() is not { } sanction)
        {
            _classHint.Text = string.Empty;
            return;
        }

        if (sanction.Dismissal)
        {
            _classHint.Text = Loc.GetString("dystopia-registry-form-dismissal");
            _classHint.FontColorOverride = Danger;
            _violationLoyalty.Text = "—";
            _violationLoyalty.Editable = false;
            return;
        }

        _classHint.Text = Loc.GetString("dystopia-registry-form-range", ("min", sanction.LoyaltyMin), ("max", sanction.LoyaltyMax));
        _classHint.FontColorOverride = CityUi.Dim;
        _violationLoyalty.Editable = true;
        if (resetValue || !int.TryParse(_violationLoyalty.Text, out var value) ||
            value < sanction.LoyaltyMin || value > sanction.LoyaltyMax)
        {
            _violationLoyalty.Text = sanction.LoyaltyMin.ToString();
        }
    }

    private void SendViolation()
    {
        if (Dossier is not { } dossier || SelectedLaw() is not { } law || SelectedSanction() is not { } sanction)
            return;

        var loyalty = 0;
        if (!sanction.Dismissal && !int.TryParse(_violationLoyalty.Text.Trim().TrimStart('-', '−'), out loyalty))
            loyalty = sanction.LoyaltyMin;

        OnViolation?.Invoke(dossier.Number, law.Id, sanction.Class, loyalty, _violationDetails.Text.Trim());
        _violationDetails.Text = string.Empty;
        _violationForm.Visible = false;
    }

    private void SendReward()
    {
        if (Dossier is not { } dossier || _state == null)
            return;

        if (!int.TryParse(_rewardAmount.Text.Trim().TrimStart('+'), out var amount))
            amount = 1;
        amount = Math.Clamp(amount, 1, _state.MaxReward);

        OnReward?.Invoke(dossier.Number, amount, _rewardText.Text.Trim());
        _rewardText.Text = string.Empty;
        _rewardForm.Visible = false;
    }

    // ======================= Неопознанные =======================

    private void RedrawUnidentified(bool force)
    {
        if (_state == null)
            return;

        var list = _state.Unidentified;
        _unidentifiedFooter.Text = Loc.GetString("dystopia-registry-unidentified-footer",
            ("count", list.Count), ("restricted", list.Count(u => u.Restricted)));

        // поля номера не должны сбрасываться, пока человек стоит в кадре: строки пересобираются только при смене
        // состава, а время обновляется на месте
        var signature = $"{_state.CanEdit}:{string.Join(";", list.Select(u => u.Id))}";
        if (force || signature != _unidentifiedSignature)
        {
            _unidentifiedSignature = signature;
            _unidentifiedRows.RemoveAllChildren();
            _unidentifiedTimes.Clear();

            if (list.Count == 0)
            {
                var empty = CityUi.MakeLabel(Loc.GetString("dystopia-registry-unidentified-empty"), CityUi.Dim);
                empty.Margin = new Thickness(8, 6);
                _unidentifiedRows.AddChild(empty);
            }

            foreach (var sighting in list)
            {
                _unidentifiedRows.AddChild(UnidentifiedRow(sighting, _state.CanEdit));
            }
        }

        foreach (var sighting in list)
        {
            if (_unidentifiedTimes.TryGetValue(sighting.Id, out var time))
                time.Text = Span(sighting.Time, sighting.LastTime);
        }
    }

    private Control UnidentifiedRow(CityUnidentifiedSighting sighting, bool canEdit)
    {
        var row = RowPanel();
        var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };

        var time = CityUi.MakeLabel(Span(sighting.Time, sighting.LastTime), CityUi.Dim, CityUi.Regular(11));
        time.MinWidth = 96;
        _unidentifiedTimes[sighting.Id] = time;
        line.AddChild(time);

        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        head.AddChild(CityUi.MakeLabel(sighting.Description, CityUi.Glow, CityUi.Regular(12)));
        if (sighting.Restricted)
            head.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-registry-unidentified-restricted"), Danger, CityUi.Bold(11)));
        text.AddChild(head);
        text.AddChild(CityUi.Hint(Where(sighting.Place, sighting.Camera, sighting.Mask)));
        line.AddChild(text);

        var watch = CityUi.MakeButton(Loc.GetString("dystopia-registry-watch"));
        watch.Disabled = !canEdit;
        var camera = sighting.CameraEntity;
        watch.OnPressed += _ => OnWatch?.Invoke(camera);
        watch.VerticalAlignment = VAlignment.Center;
        line.AddChild(watch);

        var number = CityUi.Field(Loc.GetString("dystopia-registry-identify-placeholder"), 90);
        number.Editable = canEdit;
        number.VerticalAlignment = VAlignment.Center;
        line.AddChild(number);

        var identify = CityUi.MakeButton(Loc.GetString("dystopia-registry-identify"), CityButtonStyle.Primary);
        identify.Disabled = !canEdit;
        identify.VerticalAlignment = VAlignment.Center;
        var id = sighting.Id;
        identify.OnPressed += _ =>
        {
            if (int.TryParse(number.Text.Trim().TrimStart('№', '#'), out var citizen))
                OnIdentify?.Invoke(id, citizen);
        };
        line.AddChild(identify);

        row.AddChild(line);
        return row;
    }

    // ======================= Карты =======================

    /// <summary>Карты гражданина (только просмотр: выдаёт и аннулирует Паспортный стол).</summary>
    private void RedrawCards(CityRegistryDossier dossier)
    {
        var signature = $"{dossier.Number}:" +
                        string.Join(";", dossier.Cards.Select(c => $"{c.Index}:{c.Annulled}:{c.Destroyed}:{c.Suspended}"));
        if (signature == _cardsSignature)
            return;
        _cardsSignature = signature;

        _cardList.RemoveAllChildren();
        if (dossier.Cards.Count == 0)
        {
            _cardList.AddChild(CityUi.Hint(Loc.GetString("dystopia-registry-cards-none")));
            return;
        }

        // в сводке — последние карты; весь список — в Паспортном столе
        const int shown = 4;
        foreach (var card in Enumerable.Reverse(dossier.Cards).Take(shown))
        {
            var (key, color) = card.Annulled ? ("dystopia-registry-card-annulled", Danger)
                : card.Destroyed ? ("dystopia-registry-card-destroyed", CityUi.Dim)
                : card.Suspended ? ("dystopia-registry-card-suspended", Detained)
                : ("dystopia-registry-card-live", CityUi.Accent);

            var line = Row(10);
            var serial = CityUi.MakeLabel(card.Serial, card.Annulled ? CityUi.Dim : CityUi.Glow, CityUi.Regular(12));
            serial.MinWidth = 130;
            line.AddChild(serial);
            line.AddChild(CityUi.MakeLabel(Loc.GetString(key), color, CityUi.Regular(11)));
            _cardList.AddChild(line);
        }

        if (dossier.Cards.Count > shown)
            _cardList.AddChild(CityUi.Hint(Loc.GetString("dystopia-registry-cards-more", ("count", dossier.Cards.Count - shown))));
    }

    // ======================= Маршрут =======================

    private void RedrawLastSeen(CityRegistryDossier dossier, bool outside, bool canEdit)
    {
        // последнее известное местонахождение — отметка, продлённая позже всех
        var last = dossier.Route.Count > 0 ? dossier.Route.MaxBy(r => r.LastTime) : null;
        var signature = last == null
            ? $"{dossier.Number}:{outside}:none"
            : $"{dossier.Number}:{canEdit}:{last.Time}:{last.LastTime}:{last.Camera}:{last.Method}";
        if (signature == _lastSeenSignature)
            return;
        _lastSeenSignature = signature;

        _lastSeen.RemoveAllChildren();
        if (last == null)
        {
            _lastSeen.AddChild(CityUi.Hint(Loc.GetString(outside
                ? "dystopia-registry-classified"
                : "dystopia-registry-last-seen-none")));
            return;
        }

        var line = Row(10);
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        text.AddChild(CityUi.MakeLabel(last.Place, CityUi.Glow, CityUi.Bold(12)));
        text.AddChild(CityUi.Hint(
            $"{Span(last.Time, last.LastTime)} · {Where(null, last.Camera, last.Mask)} · {MethodText(last.Method)}"));
        line.AddChild(text);
        line.AddChild(WatchButton(last.CameraEntity, canEdit));
        _lastSeen.AddChild(line);
    }

    private void RedrawRoute(CityRegistryDossier dossier, bool canEdit)
    {
        var route = dossier.Route;
        var signature = $"{dossier.Number}:{canEdit}:" +
                        string.Join(";", route.Select(s => $"{s.Time}:{s.LastTime}:{s.Camera}:{s.Method}"));
        if (signature == _routeSignature)
            return;
        _routeSignature = signature;

        _routeRows.RemoveAllChildren();
        if (route.Count == 0)
        {
            var empty = CityUi.Hint(Loc.GetString("dystopia-registry-route-empty"));
            empty.Margin = new Thickness(8, 6);
            _routeRows.AddChild(empty);
            return;
        }

        // новые — сверху
        for (var i = route.Count - 1; i >= 0; i--)
        {
            var sighting = route[i];
            var row = RowPanel();
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };

            var time = CityUi.MakeLabel(Span(sighting.Time, sighting.LastTime), CityUi.Dim, CityUi.Regular(11));
            time.MinWidth = 96;
            line.AddChild(time);

            var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
            text.AddChild(CityUi.MakeLabel(sighting.Place, i == route.Count - 1 ? CityUi.Glow : CityUi.Text, CityUi.Regular(12)));
            text.AddChild(CityUi.Hint(Where(null, sighting.Camera, sighting.Mask)));
            line.AddChild(text);

            var method = CityUi.MakeLabel(MethodText(sighting.Method),
                sighting.Method == CitySightingMethod.Manual ? Watch : CityUi.Dim, CityUi.Regular(11));
            method.MinWidth = 120;
            method.VerticalAlignment = VAlignment.Center;
            line.AddChild(method);
            line.AddChild(WatchButton(sighting.CameraEntity, canEdit));

            row.AddChild(line);
            _routeRows.AddChild(row);
        }
    }

    private CityButton WatchButton(NetEntity camera, bool canEdit)
    {
        var button = CityUi.MakeButton(Loc.GetString("dystopia-registry-watch"));
        button.Disabled = !canEdit;
        button.VerticalAlignment = VAlignment.Center;
        button.OnPressed += _ => OnWatch?.Invoke(camera);
        return button;
    }

    /// <summary>«камера «Рынок-2» · Рынок» / «маска Стражи · №102628».</summary>
    private static string Where(string? place, string camera, bool mask)
    {
        var source = Loc.GetString(mask ? "dystopia-registry-route-mask" : "dystopia-registry-route-camera", ("camera", camera));
        return place == null ? source : $"{source} · {place}";
    }

    private static string MethodText(CitySightingMethod method)
    {
        return Loc.GetString($"dystopia-registry-method-{method.ToString().ToLowerInvariant()}");
    }

    /// <summary>«12:03» или «12:03–12:15».</summary>
    private static string Span(TimeSpan from, TimeSpan to)
    {
        var start = Clock(from);
        var end = Clock(to);
        return start == end ? start : $"{start}–{end}";
    }

    private static PanelContainer RowPanel()
    {
        var row = new PanelContainer { PanelOverride = CityUi.Box(Color.Transparent, CityUi.Line, 0, 8, 5) };
        ((StyleBoxFlat) row.PanelOverride).BorderThickness = new Thickness(0, 0, 0, 1);
        return row;
    }

    // ======================= Банк =======================

    private void RedrawBank(CityRegistryDossier dossier)
    {
        var bank = dossier.Bank;
        _bankCards.Visible = bank != null;
        _bankNone.Visible = bank == null;
        if (bank == null)
        {
            _bankHistory.RemoveAllChildren();
            _bankSignature = string.Empty;
            return;
        }

        _bankAccount.Text = $"№{bank.Account}";
        _bankAccountState.Text = Loc.GetString(bank.Frozen ? "dystopia-registry-bank-frozen" : "dystopia-registry-bank-active");
        _bankAccountState.FontColorOverride = bank.Frozen ? Danger : CityUi.Dim;
        _bankBalance.Text = bank.Balance.ToString();
        _bankDebt.Text = bank.Debt.ToString();
        _bankDebt.FontColorOverride = bank.Debt > 0 ? Danger : CityUi.Glow;
        _bankTax.Text = $"{bank.TaxRate}%";

        var signature = $"{bank.Account}:{bank.History.Count}:{(bank.History.Count > 0 ? bank.History[0] : string.Empty)}";
        if (signature == _bankSignature)
            return;
        _bankSignature = signature;

        _bankHistory.RemoveAllChildren();
        foreach (var line in bank.History)
        {
            _bankHistory.AddChild(CityUi.Hint(line, CityUi.Text));
        }
    }

    private static (Label Value, Label Hint) BankCard(BoxContainer parent, string caption, string? hint = null)
    {
        var card = new CityCard { HorizontalExpand = true };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        box.AddChild(Small(caption));
        var value = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Title(11));
        box.AddChild(value);
        var hintLabel = CityUi.MakeLabel(hint != null ? Loc.GetString(hint) : string.Empty, CityUi.Dim, CityUi.Regular(10));
        box.AddChild(hintLabel);
        card.AddChild(box);
        parent.AddChild(card);
        return (value, hintLabel);
    }

    // ======================= Статус =======================

    private void OpenStatusWindow()
    {
        if (Dossier is not { } dossier || _state == null)
            return;

        if (_statusWindow == null || _statusWindow.Disposed)
        {
            _statusWindow = new CityRegistryStatusWindow();
            _statusWindow.OnApply += (number, status, reason) => OnSetStatus?.Invoke(number, status, reason);
        }

        _statusWindow.Update(dossier, _state.CanEdit, _state.CanSupreme);
        _statusWindow.Reset(dossier);
        _statusWindow.OpenCentered();
    }

    // ======================= Помощники =======================

    public static readonly Color Watch = Color.FromHex("#E8B84E");
    public static readonly Color Danger = Color.FromHex("#FF7A6E");
    public static readonly Color Detained = Color.FromHex("#F0954A");
    public static readonly Color Stripped = Color.FromHex("#B5A3DA");
    public static readonly Color Gold = Color.FromHex("#E8C66A");
    public static readonly Color GoldDark = Color.FromHex("#B8962E");

    public static Color StatusColor(CityCitizenStatus status)
    {
        return status switch
        {
            CityCitizenStatus.Supervised => Watch,
            CityCitizenStatus.Wanted => Danger,
            CityCitizenStatus.Detained => Detained,
            CityCitizenStatus.Disenfranchised => Stripped,
            CityCitizenStatus.Dead => CityUi.Dim,
            _ => CityUi.Accent,
        };
    }

    public static string StatusText(CityCitizenStatus status)
    {
        return Loc.GetString($"dystopia-registry-status-{status.ToString().ToLowerInvariant()}");
    }

    public static Color LoyaltyColor(int loyalty, int? norm)
    {
        if (loyalty <= 0)
            return Danger;

        return norm is { } n && loyalty < n ? Watch : CityUi.Accent;
    }

    private static string GenderText(Gender gender)
    {
        return Loc.GetString($"dystopia-registry-gender-{gender.ToString().ToLowerInvariant()}");
    }

    private static string Clock(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}";
    }

    private string ChipText(JournalFilter filter, int count)
    {
        return Loc.GetString($"dystopia-registry-chip-{filter.ToString().ToLowerInvariant()}", ("count", count)).ToUpperInvariant();
    }

    private CityButton Chip(JournalFilter filter)
    {
        var chip = new CityButton(string.Empty, CityButtonStyle.Tab) { MinHeight = 26 };
        // у вкладок Города текст обрезается по ширине кнопки, а ширину тут задаёт сам текст — обрезку снимаем
        chip.Label.ClipText = false;
        chip.OnPressed += _ =>
        {
            _filter = filter;
            _journalSignature = string.Empty;
            if (Dossier is { } dossier && _state != null)
                RedrawJournal(dossier, _state);
        };
        return chip;
    }

    private CityButton SubTab(string key, DossierTab tab)
    {
        var button = new CityButton(Loc.GetString(key).ToUpperInvariant(), CityButtonStyle.Tab) { MinWidth = 120 };
        button.Label.ClipText = false;
        button.OnPressed += _ => SetTab(tab);
        return button;
    }

    private static BoxContainer Row(int separation)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = separation,
        };
    }

    private static BoxContainer Column()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
    }

    private static Label Caption(string key)
    {
        var label = CityUi.MakeLabel(Loc.GetString(key).ToUpperInvariant(), CityUi.Dim, CityUi.Regular(11));
        label.VerticalAlignment = VAlignment.Center;
        return label;
    }

    private static Label Small(string key)
    {
        return CityUi.MakeLabel(Loc.GetString(key).ToUpperInvariant(), CityUi.Dim, CityUi.Regular(10));
    }
}

/// <summary>Строка списка граждан: подсветка при наведении.</summary>
public sealed class CityRegistryRow : ContainerButton
{
    public CityRegistryRow()
    {
        HorizontalExpand = true;
        UpdateLook();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateLook();
    }

    private void UpdateLook()
    {
        var hover = DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed;
        var box = CityUi.Box(hover ? CityUi.HoverFill : Color.Transparent, CityUi.Line, 0, 8, 5);
        box.BorderThickness = new Thickness(0, 0, 0, 1);
        StyleBoxOverride = box;
    }
}

/// <summary>Полоса лояльности с отметкой нормы должности.</summary>
public sealed class CityLoyaltyBar : Control
{
    private int _value;
    private int _max = 50;
    private int? _norm;
    private Color _color = CityUi.Accent;

    public CityLoyaltyBar()
    {
        MinHeight = 12;
        HorizontalExpand = true;
    }

    public void Set(int value, int max, int? norm, Color color)
    {
        _value = value;
        _max = Math.Max(1, max);
        _norm = norm;
        _color = color;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var w = PixelSize.X;
        var h = PixelSize.Y;
        var t = MathF.Max(1f, MathF.Round(UIScale));
        handle.DrawRect(new UIBox2(0, 0, w, h), CityUi.Input);
        var fill = w * Math.Clamp(_value / (float) _max, 0f, 1f);
        if (fill > 0)
            handle.DrawRect(new UIBox2(t, t, MathF.Max(t, fill - t), h - t), _color);
        handle.DrawRect(new UIBox2(0, 0, w, h), CityUi.Line, false);

        if (_norm is { } norm)
        {
            var x = MathF.Round(w * Math.Clamp(norm / (float) _max, 0f, 1f));
            handle.DrawRect(new UIBox2(x, -2, x + t, h + 2), CityUi.Glow);
        }
    }
}
