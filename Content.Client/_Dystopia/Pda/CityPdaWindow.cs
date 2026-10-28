// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: окно КПК Города — старый янтарный терминал гражданина в корпусе своей службы (CityPdaCaseComponent:
// казённая сталь у всех, служебный планшет у Стражи). Вместо вкладок и иконок ванильного КПК — клавиши
// («1 ГЛАВН · 2 ПРОГР · 3 НАСТР · 4 ФОНАРЬ · 5 ВЫН.ПЕРО · 6 ВЫН.КАРТУ»): у стали строкой внизу экрана, у планшета —
// кнопками на корпусе;
// вместо плиток программ — нумерованный список. Всё, что умеет ванильное окно (данные карты, режим тревоги,
// время смены, программы, настройки, аплинк), здесь же; копирование строки главного экрана щелчком тоже осталось.

using System.Numerics;
using Content.Shared._Dystopia.Pda;
using Content.Shared.AlertLevel;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Pda;

public sealed class CityPdaWindow : BaseWindow
{
    [Dependency] private IClipboardManager _clipboard = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private enum View : byte
    {
        Home,
        Programs,
        Settings,
        Program,
    }

    private enum KeyId : byte
    {
        Home,
        Programs,
        Settings,
        Light,
        Pen,
        Id,
        Pai,
        Exit,
    }

    private static readonly (KeyId Id, string Number, string Caption)[] KeyLayout =
    {
        (KeyId.Home, "1", "dystopia-pda-key-home"),
        (KeyId.Programs, "2", "dystopia-pda-key-programs"),
        (KeyId.Settings, "3", "dystopia-pda-key-settings"),
        (KeyId.Light, "4", "dystopia-pda-key-light"),
        (KeyId.Pen, "5", "dystopia-pda-key-pen"),
        (KeyId.Id, "6", "dystopia-pda-key-id"),
        (KeyId.Pai, "7", "dystopia-pda-key-pai"),
        (KeyId.Exit, "0", "dystopia-pda-key-exit"),
    };

    private readonly AlertLevelSystem _alert;
    private readonly GameTicker _ticker;

    private readonly PanelContainer _screen;
    private readonly PanelContainer _keysBar;
    private readonly BoxContainer _screenKeys;
    private readonly Dictionary<KeyId, CityPdaKeyBase> _keys = new();
    private readonly Label _title;
    private readonly Label _clock;
    private readonly Label _address;
    private readonly Dictionary<View, Control> _views = new();

    // главный экран
    private readonly CityPdaRow _ownerRow;
    private readonly Label _owner;
    private readonly Label _card;
    private readonly Label _job;
    private readonly Label _city;
    private readonly BoxContainer _alertBox;
    private readonly Label _time;
    private readonly RichTextLabel _instructions;

    // программы и настройки
    private readonly BoxContainer _programList;
    private readonly CityPdaRow _musicRow;
    private readonly CityPdaRow _showUplinkRow;
    private readonly CityPdaRow _lockUplinkRow;

    /// <summary>Сюда встаёт открытая программа.</summary>
    public readonly BoxContainer ProgramView;

    public event Action? OnRingtone;
    public event Action? OnMusic;
    public event Action? OnShowUplink;
    public event Action? OnLockUplink;
    public event Action? OnExitProgram;
    public event Action? OnLight;
    public event Action? OnEjectPen;
    public event Action? OnEjectId;
    public event Action? OnEjectPai;
    public event Action<EntityUid>? OnProgramItemPressed;
    public event Action<EntityUid>? OnInstallButtonPressed;
    public event Action<EntityUid>? OnUninstallButtonPressed;

    private View _view = View.Home;
    private bool _programOpen;
    private string _programTitle = string.Empty;
    private int _programCount;

    // что показывают клавиши
    private bool _light;
    private bool _hasPen;
    private bool _hasId;
    private bool _hasPai;

    private string _ownerText = string.Empty;
    private string _cardText = string.Empty;
    private string _cityText = string.Empty;
    private string _alertText = string.Empty;
    private string _instructionsText = string.Empty;

    public CityPdaWindow()
    {
        IoCManager.InjectDependencies(this);
        _alert = _entMan.System<AlertLevelSystem>();
        _ticker = _entMan.System<GameTicker>();

        Stylesheet = CityPdaUi.Sheet;
        MouseFilter = MouseFilterMode.Stop;

        // ===== экран (корпус вокруг него собирает ApplyCase) =====
        _screen = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CityPdaUi.Screen },
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _screen.AddChild(content);
        _screen.AddChild(new CityCrtScanlines());

        // строка заголовка — инверсией
        var titleBar = new PanelContainer { PanelOverride = CityPdaUi.Fill(CityPdaUi.Amber, 6, 1) };
        var titleRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _title = CityPdaUi.Text(string.Empty, CityPdaUi.Ink);
        _title.HorizontalExpand = true;
        _title.ClipText = true;
        _clock = CityPdaUi.Text("00:00:00", CityPdaUi.Ink);
        titleRow.AddChild(_title);
        titleRow.AddChild(_clock);
        titleBar.AddChild(titleRow);
        content.AddChild(titleBar);

        var views = new Control { VerticalExpand = true, HorizontalExpand = true, Margin = new Thickness(8, 6), RectClipContent = true };
        content.AddChild(views);

        // ----- главный экран -----
        var home = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 1 };
        _owner = CityPdaUi.Text(string.Empty);
        _ownerRow = CopyRow(home, "dystopia-pda-field-owner", _owner, () => _ownerText);
        _card = CityPdaUi.Text(string.Empty);
        CopyRow(home, "dystopia-pda-field-card", _card, () => _cardText);
        _job = CityPdaUi.Text(string.Empty);
        CopyRow(home, "dystopia-pda-field-job", _job, () => _cardText);
        _city = CityPdaUi.Text(string.Empty);
        CopyRow(home, "dystopia-pda-field-city", _city, () => _cityText);

        _alertBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var alertRow = new CityPdaRow();
        alertRow.AddChild(CityPdaUi.Field(Loc.GetString("dystopia-pda-field-alert"), _alertBox));
        alertRow.OnPressed += _ => _clipboard.SetText(_alertText);
        home.AddChild(alertRow);

        _time = CityPdaUi.Text("00:00:00");
        CopyRow(home, "dystopia-pda-field-time", _time, () => RoundTime().ToString("hh\\:mm\\:ss"));

        home.AddChild(CityPdaUi.Rule());
        _instructions = CityPdaUi.Wrap(string.Empty, CityPdaUi.Dim);
        var instructionsRow = new CityPdaRow();
        instructionsRow.AddChild(_instructions);
        instructionsRow.OnPressed += _ => _clipboard.SetText(_instructionsText);
        home.AddChild(instructionsRow);
        home.AddChild(new Control { VerticalExpand = true });
        home.AddChild(Prompt("> "));
        AddView(views, View.Home, home);

        // ----- программы -----
        var programs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var programScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _programList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 1 };
        programScroll.AddChild(_programList);
        programs.AddChild(programScroll);
        programs.AddChild(Prompt(Loc.GetString("dystopia-pda-choice") + " "));
        AddView(views, View.Programs, programs);

        // ----- настройки -----
        var settings = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        settings.AddChild(SettingRow("comp-pda-ui-ringtone-button", "comp-pda-ui-ringtone-button-description", () => OnRingtone?.Invoke()));
        _musicRow = SettingRow("pda-bound-user-interface-music-button", "pda-bound-user-interface-music-button-description", () => OnMusic?.Invoke());
        _showUplinkRow = SettingRow("pda-bound-user-interface-show-uplink-title", "pda-bound-user-interface-show-uplink-description", () => OnShowUplink?.Invoke());
        _lockUplinkRow = SettingRow("pda-bound-user-interface-lock-uplink-title", "pda-bound-user-interface-lock-uplink-description", () => OnLockUplink?.Invoke());
        _musicRow.Visible = false;
        _showUplinkRow.Visible = false;
        _lockUplinkRow.Visible = false;
        settings.AddChild(_musicRow);
        settings.AddChild(_showUplinkRow);
        settings.AddChild(_lockUplinkRow);
        AddView(views, View.Settings, settings);

        // ----- открытая программа -----
        ProgramView = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true, HorizontalExpand = true };
        AddView(views, View.Program, ProgramView);

        // ===== строка клавиш на экране (у стали; у планшета клавиши на корпусе) =====
        _keysBar = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BorderColor = CityPdaUi.Faint,
                BorderThickness = new Thickness(0, 1, 0, 0),
            },
        };
        _screenKeys = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8, Margin = new Thickness(6, 4) };
        _keysBar.AddChild(_screenKeys);
        content.AddChild(_keysBar);

        // ===== подвал =====
        var footer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(6, 1, 6, 2),
        };
        footer.AddChild(CityPdaUi.Text(Loc.GetString("dystopia-pda-footer"), CityPdaUi.Dim, 12));
        footer.AddChild(new Control { HorizontalExpand = true });
        _address = CityPdaUi.Text(" - ", CityPdaUi.Dim, 12);
        footer.AddChild(_address);
        var footerBar = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BorderColor = CityPdaUi.Faint,
                BorderThickness = new Thickness(0, 1, 0, 0),
            },
        };
        footerBar.AddChild(footer);
        content.AddChild(footerBar);

        ApplyCase(new CityPdaCaseComponent());
        ToHomeScreen();
    }

    #region Корпус

    /// <summary>
    /// Собрать корпус вокруг экрана: казённая сталь (клавиши строкой на экране, трафарет снизу) или служебный
    /// планшет (клавиши на корпусе). Цвета — из CityPdaCaseComponent КПК.
    /// </summary>
    public void ApplyCase(CityPdaCaseComponent data)
    {
        var tablet = data.Style == CityPdaCaseStyle.Tablet;

        _screen.Orphan();
        foreach (var key in _keys.Values)
        {
            key.Orphan();
        }
        _keys.Clear();
        RemoveAllChildren();

        MinSize = tablet ? new Vector2(610, 500) : new Vector2(620, 450);
        SetSize = tablet ? new Vector2(610, 530) : new Vector2(620, 490);

        var root = new CityPdaCaseControl(data);
        AddChild(root);

        var trim = CityPdaCaseControl.TrimSize;
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            Margin = tablet ? new Thickness(16, 4, 16, 8) : new Thickness(trim + 12, 3, 14, 5),
        };
        root.AddChild(box);

        // шапка: трафарет службы, огоньки со спрайта, закрыть
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 5, MinHeight = 18 };
        if (tablet)
            header.Margin = new Thickness(CityPdaCaseControl.BumperSize - 12, 0);
        header.AddChild(CityPdaUi.Text(Loc.GetString("dystopia-pda-case", ("label", Loc.GetString(data.Label))), data.Stencil, 12));
        header.AddChild(new Control { HorizontalExpand = true });
        foreach (var light in data.Lights)
        {
            header.AddChild(new CityPdaLight(light));
        }
        var close = new CityPdaRow { HorizontalExpand = false, Margin = new Thickness(6, 0, 0, 0) };
        close.AddChild(close.Track(CityPdaUi.Text("×", data.Stencil, 16)));
        close.OnPressed += _ => Close();
        header.AddChild(close);
        box.AddChild(header);

        // экран утоплен в корпус: тёмная рамка (у планшета — глубже, со светлой нижней кромкой)
        var recess = new PanelContainer
        {
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = tablet ? Color.Black : data.Edge,
                BorderColor = tablet ? Color.InterpolateBetween(data.Body, Color.White, 0.15f) : Color.Black.WithAlpha(0.6f),
                BorderThickness = tablet ? new Thickness(0, 0, 2, 2) : new Thickness(1),
            },
        };
        var inset = new Control { Margin = tablet ? new Thickness(6, 6, 4, 4) : new Thickness(4) };
        inset.AddChild(_screen);
        recess.AddChild(inset);
        box.AddChild(recess);

        if (tablet)
        {
            // клавиши на корпусе
            _keysBar.Visible = false;
            var hard = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4, Margin = new Thickness(8, 2, 8, 0) };
            foreach (var (id, number, caption) in KeyLayout)
            {
                AddKey(id, new CityPdaHardKey(number, Loc.GetString(caption), data.Keycap, data.Stencil), hard);
            }
            box.AddChild(hard);
        }
        else
        {
            // клавиши строкой на экране, трафарет внизу корпуса
            _keysBar.Visible = true;
            foreach (var (id, number, caption) in KeyLayout)
            {
                AddKey(id, new CityPdaKey(number, Loc.GetString(caption)), _screenKeys);
            }

            var bottom = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, MinHeight = 16 };
            bottom.AddChild(CityPdaUi.Text(Loc.GetString("dystopia-pda-made"), data.Stencil, 12));
            bottom.AddChild(new Control { HorizontalExpand = true });
            var plate = new PanelContainer { PanelOverride = CityPdaUi.Fill(data.Stencil, 4) };
            plate.AddChild(CityPdaUi.Text(Loc.GetString("dystopia-pda-property"), data.Body, 12));
            bottom.AddChild(plate);
            box.AddChild(bottom);
        }

        RefreshKeys();
    }

    private void AddKey(KeyId id, CityPdaKeyBase key, BoxContainer parent)
    {
        key.OnPressed += _ => KeyPressed(id);
        parent.AddChild(key);
        _keys[id] = key;
    }

    private void KeyPressed(KeyId id)
    {
        switch (id)
        {
            case KeyId.Home:
                ToHomeScreen();
                break;
            case KeyId.Programs:
                ShowView(_programOpen ? View.Program : View.Programs);
                break;
            case KeyId.Settings:
                ShowView(View.Settings);
                break;
            case KeyId.Light:
                OnLight?.Invoke();
                break;
            case KeyId.Pen:
                OnEjectPen?.Invoke();
                break;
            case KeyId.Id:
                OnEjectId?.Invoke();
                break;
            case KeyId.Pai:
                OnEjectPai?.Invoke();
                break;
            case KeyId.Exit:
                OnExitProgram?.Invoke();
                HideProgramHeader();
                ToHomeScreen();
                break;
        }
    }

    /// <summary>Клавиши показывают текущий экран, фонарь, есть ли что вынимать; «3» и «0» — по открытой программе.</summary>
    private void RefreshKeys()
    {
        foreach (var (id, key) in _keys)
        {
            key.Current = id switch
            {
                KeyId.Home => _view == View.Home,
                KeyId.Programs => _view is View.Programs or View.Program,
                KeyId.Settings => _view == View.Settings,
                _ => false,
            };
            key.Toggled = id == KeyId.Light && _light;
            key.Inactive = id switch
            {
                KeyId.Pen => !_hasPen,
                KeyId.Id => !_hasId,
                _ => false,
            };
            key.Visible = id switch
            {
                KeyId.Settings => !_programOpen,
                KeyId.Exit => _programOpen,
                KeyId.Pai => _hasPai,
                _ => true,
            };
        }
    }

    #endregion

    #region Сборка

    private void AddView(Control parent, View view, Control control)
    {
        control.Visible = false;
        control.HorizontalExpand = true;
        control.VerticalExpand = true;
        parent.AddChild(control);
        _views[view] = control;
    }

    private CityPdaRow CopyRow(BoxContainer parent, string key, Label value, Func<string> copy)
    {
        var row = new CityPdaRow();
        var field = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        field.AddChild(row.Track(CityPdaUi.Text(CityPdaUi.Leader(Loc.GetString(key)), CityPdaUi.Dim)));
        value.HorizontalExpand = true;
        value.ClipText = true;
        field.AddChild(row.Track(value));
        row.AddChild(field);
        row.OnPressed += _ => _clipboard.SetText(copy());
        parent.AddChild(row);
        return row;
    }

    private static BoxContainer Prompt(string text)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 0 };
        row.AddChild(CityPdaUi.Text(text));
        row.AddChild(new CityPdaCursor());
        return row;
    }

    private static CityPdaRow SettingRow(string title, string description, Action pressed)
    {
        var row = new CityPdaRow();
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(row.Track(CityPdaUi.Text("> " + Loc.GetString(title).ToUpperInvariant())));
        box.AddChild(row.Track(CityPdaUi.Text("  " + Loc.GetString(description), CityPdaUi.Dim, 12)));
        row.AddChild(box);
        row.OnPressed += _ => pressed();
        return row;
    }

    #endregion

    #region Экраны

    private void ShowView(View view)
    {
        foreach (var (key, control) in _views)
        {
            control.Visible = key == view;
        }

        _view = view;
        RefreshKeys();

        _title.Text = view switch
        {
            View.Programs => Loc.GetString("dystopia-pda-screen-programs", ("count", _programCount)),
            View.Settings => Loc.GetString("dystopia-pda-screen-settings"),
            View.Program => Loc.GetString("dystopia-pda-screen-program", ("title", _programTitle.ToUpperInvariant())),
            _ => Loc.GetString("dystopia-pda-screen-home"),
        };
    }

    public void ToHomeScreen()
    {
        ShowView(View.Home);
    }

    /// <summary>Открыта программа: клавиша «2» ведёт в неё, «3» прячется, появляется «0 ВЫХОД».</summary>
    public void ToProgramView(string title)
    {
        _programOpen = true;
        _programTitle = title;
        ShowView(View.Program);
    }

    public void HideProgramHeader()
    {
        _programOpen = false;
        _programTitle = string.Empty;
        if (_view == View.Program)
            ShowView(View.Home);
        else
            RefreshKeys();
    }

    #endregion

    #region Данные

    public void UpdateState(PdaUpdateState state)
    {
        var unknown = Loc.GetString("dystopia-pda-unknown");
        var info = state.PdaOwnerInfo;

        _light = state.FlashlightEnabled;

        _ownerText = info.ActualOwnerName ?? string.Empty;
        _ownerRow.Visible = info.ActualOwnerName != null;
        _owner.Text = _ownerText.ToUpperInvariant();

        var hasCard = info.IdOwner != null || info.JobTitle != null;
        var cardOwner = info.IdOwner ?? unknown;
        var job = info.JobTitle ?? Loc.GetString("comp-pda-ui-unassigned");
        _cardText = hasCard ? $"{cardOwner}, {job}" : string.Empty;
        _card.Text = hasCard ? cardOwner.ToUpperInvariant() : Loc.GetString("dystopia-pda-no-card");
        _job.Text = hasCard ? job.ToUpperInvariant() : "—";

        _cityText = state.StationName ?? unknown;
        _city.Text = _cityText.ToUpperInvariant();

        (_alertText, _, _instructionsText) = _alert.AlertLevelData(info.StationAlertLevel);
        _alertBox.RemoveAllChildren();
        _alertBox.AddChild(CityPdaUi.Inverse(_alertText.ToUpperInvariant()));
        _instructions.SetMessage(Robust.Shared.Utility.FormattedMessage.FromUnformatted(_instructionsText), defaultColor: CityPdaUi.Dim);

        _address.Text = state.Address?.ToUpperInvariant() ?? " - ";

        _hasId = hasCard;
        _hasPen = state.HasPen;
        _hasPai = state.HasPai;
        RefreshKeys();
        _musicRow.Visible = state.CanPlayMusic;
        _showUplinkRow.Visible = state.HasUplink;
        _lockUplinkRow.Visible = state.HasUplink;
    }

    public void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
    {
        _programCount = programs.Count;
        if (_view == View.Programs)
            ShowView(View.Programs);

        _programList.RemoveAllChildren();
        if (programs.Count == 0)
        {
            _programList.AddChild(CityPdaUi.Text(Loc.GetString("dystopia-pda-no-programs"), CityPdaUi.Dim));
            return;
        }

        for (var i = 0; i < programs.Count; i++)
        {
            var (uid, cartridge) = programs[i];
            var row = new CityPdaRow();
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            line.AddChild(row.Track(CityPdaUi.Text($"[{i + 1}]")));
            var name = row.Track(CityPdaUi.Text(Loc.GetString(cartridge.ProgramName).ToUpperInvariant()));
            name.HorizontalExpand = true;
            name.ClipText = true;
            line.AddChild(name);

            var action = cartridge.InstallationStatus switch
            {
                InstallationStatus.Cartridge => "dystopia-pda-install",
                InstallationStatus.Installed => "dystopia-pda-uninstall",
                _ => null,
            };
            if (action != null)
            {
                var button = new CityPdaRow { HorizontalExpand = false };
                button.AddChild(button.Track(CityPdaUi.Text(Loc.GetString(action), CityPdaUi.Dim)));
                var status = cartridge.InstallationStatus;
                button.OnPressed += _ =>
                {
                    if (status == InstallationStatus.Cartridge)
                        OnInstallButtonPressed?.Invoke(uid);
                    else
                        OnUninstallButtonPressed?.Invoke(uid);
                };
                line.AddChild(button);
            }

            row.AddChild(line);
            row.OnPressed += _ => OnProgramItemPressed?.Invoke(uid);
            _programList.AddChild(row);
        }
    }

    private TimeSpan RoundTime()
    {
        return _timing.CurTime.Subtract(_ticker.RoundStartTimeSpan);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // текст меняется раз в секунду — не трогаем надписи каждый кадр
        var time = RoundTime().ToString("hh\\:mm\\:ss");
        if (_clock.Text == time)
            return;
        _clock.Text = time;
        _time.Text = time;
    }

    #endregion

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }
}
