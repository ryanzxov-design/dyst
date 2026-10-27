// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: окно консоли соционадзора. Вкладки: «Камеры» (список или карта, картинка, эфир — речь у камеры,
// «Голос Стражи»), «Тревоги», «Слежка», «Патрули», «Маркеры». Картинка маски Стражи — с её синим визором (его
// рисует наложение визора для глаза маски). Списки перерисовываются только при изменениях, чтобы набранный текст
// не сбрасывался обновлениями с сервера.

using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Client.Eye;
using Content.Client.Viewport;
using Content.Shared._Dystopia.Registry;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Registry;

public sealed class CitySurveillanceConsoleWindow : CityWindow
{
    [Dependency] private IEntityManager _entMan = default!;

    public event Action<NetEntity?>? OnSelect;
    public event Action<int>? OnAccept;
    public event Action<int?>? OnDismiss;
    public event Action<int?>? OnTrack;
    public event Action<string, bool>? OnKeyword;
    public event Action<string, bool>? OnVoice;

    private enum Tab : byte
    {
        Cameras,
        Alerts,
        Track,
        Dozor,
        Keywords,
    }

    private CitySurveillanceUiState? _state;
    private Tab _tab = Tab.Cameras;

    private readonly Dictionary<Tab, CityButton> _tabs = new();
    private readonly Dictionary<Tab, Control> _views = new();

    // камеры
    private readonly CityButton _listButton;
    private readonly CityButton _mapButton;
    private readonly ScrollContainer _listScroll;
    private readonly BoxContainer _cameraRows;
    private readonly CitySurveillanceMapControl _map;
    private readonly Label _cameraTitle;
    private readonly CityButton _cameraOff;
    private readonly ScalingViewport _view;
    private readonly Label _noSignal;
    private readonly FixedEye _blank = new();
    private EntityUid? _viewCamera;
    private readonly BoxContainer _speechRows;
    private readonly ScrollContainer _speechScroll;
    private readonly LineEdit _voiceText;
    private readonly CityButton _voiceLoud;
    private readonly CityButton _voicePersonal;
    private string _camerasSignature = string.Empty;
    private string _speechSignature = string.Empty;
    private int _speechToBottom;

    // тревоги
    private readonly BoxContainer _alertRows;
    private readonly CityButton _dismissAll;
    private string _alertsSignature = string.Empty;

    // слежка
    private readonly LineEdit _trackNumber;
    private readonly BoxContainer _trackInfo;
    private string _trackSignature = string.Empty;

    // патрули
    private readonly BoxContainer _patrolRows;
    private readonly CityDozorMapControl _dozorMap;
    private readonly CityButton _targetsButton;
    private readonly CityButton _trailsButton;
    private readonly Dictionary<int, CityHeartbeatControl> _beats = new();
    private int? _selectedGuard;
    private string _patrolsSignature = string.Empty;

    // маркеры
    private readonly BoxContainer _keywordRows;
    private readonly LineEdit _keywordText;
    private string _keywordsSignature = string.Empty;

    public CitySurveillanceConsoleWindow()
    {
        IoCManager.InjectDependencies(this);

        WindowTitle = Loc.GetString("dystopia-surveillance-title");
        Subtitle = Loc.GetString("dystopia-surveillance-subtitle");
        Slogan = Loc.GetString("dystopia-surveillance-slogan");
        MinSize = new Vector2(1000, 680);
        SetSize = new Vector2(1080, 740);

        var tabs = Row(4);
        foreach (var tab in Enum.GetValues<Tab>())
        {
            var button = new CityButton(string.Empty, CityButtonStyle.Tab) { MinWidth = 150 };
            button.Label.ClipText = false;
            var t = tab;
            button.OnPressed += _ => SetTab(t);
            _tabs[tab] = button;
            tabs.AddChild(button);
        }
        Contents.AddChild(tabs);
        Contents.AddChild(CityUi.Separator(CityUi.Accent));

        // ================= Камеры =================
        var cameras = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12,
        };
        _views[Tab.Cameras] = cameras;
        Contents.AddChild(cameras);

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 320,
            MaxWidth = 320,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        cameras.AddChild(left);

        var mode = Row(4);
        _listButton = new CityButton(Loc.GetString("dystopia-surveillance-list").ToUpperInvariant(), CityButtonStyle.Tab) { MinHeight = 28 };
        _mapButton = new CityButton(Loc.GetString("dystopia-surveillance-map").ToUpperInvariant(), CityButtonStyle.Tab) { MinHeight = 28 };
        _listButton.Label.ClipText = false;
        _mapButton.Label.ClipText = false;
        _listButton.OnPressed += _ => SetMapMode(false);
        _mapButton.OnPressed += _ => SetMapMode(true);
        mode.AddChild(_listButton);
        mode.AddChild(_mapButton);
        left.AddChild(mode);

        var listPanel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        listPanel.VerticalExpand = true;
        _listScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _cameraRows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        _listScroll.AddChild(_cameraRows);
        listPanel.AddChild(_listScroll);
        left.AddChild(listPanel);

        _map = new CitySurveillanceMapControl { VerticalExpand = true, HorizontalExpand = true, Visible = false };
        _map.CameraSelected += net => OnSelect?.Invoke(net);
        listPanel.AddChild(_map);
        left.AddChild(CityUi.Hint(Loc.GetString("dystopia-surveillance-legend")));

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        cameras.AddChild(right);

        var head = Row(8);
        _cameraTitle = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Regular(12));
        _cameraTitle.HorizontalExpand = true;
        _cameraTitle.ClipText = true;
        _cameraTitle.VerticalAlignment = VAlignment.Center;
        head.AddChild(_cameraTitle);
        _cameraOff = CityUi.MakeButton(Loc.GetString("dystopia-surveillance-off"));
        _cameraOff.OnPressed += _ => OnSelect?.Invoke(null);
        head.AddChild(_cameraOff);
        right.AddChild(head);

        var screen = CityUi.MakePanel(CityUi.Input, CityUi.Frame, 2);
        screen.MinSize = new Vector2(480, 330);
        var screenBox = new Control { HorizontalExpand = true, VerticalExpand = true };
        _view = new ScalingViewport
        {
            ViewportSize = new Vector2i(500, 500),
            MinSize = new Vector2(470, 320),
            MouseFilter = MouseFilterMode.Ignore,
            HorizontalExpand = true,
            VerticalExpand = true,
            Eye = _blank,
            Visible = false,
        };
        screenBox.AddChild(_view);
        _noSignal = CityUi.MakeLabel(Loc.GetString("dystopia-surveillance-no-signal").ToUpperInvariant(), CityUi.Dim, CityUi.Title(14));
        _noSignal.HorizontalAlignment = HAlignment.Center;
        _noSignal.VerticalAlignment = VAlignment.Center;
        screenBox.AddChild(_noSignal);
        screen.AddChild(screenBox);
        right.AddChild(screen);

        right.AddChild(Small("dystopia-surveillance-speech"));
        var speechPanel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 6);
        speechPanel.MinHeight = 130;
        speechPanel.VerticalExpand = true;
        _speechScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _speechRows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 2 };
        _speechScroll.AddChild(_speechRows);
        speechPanel.AddChild(_speechScroll);
        right.AddChild(speechPanel);

        var voice = Row(6);
        voice.AddChild(Caption("dystopia-surveillance-voice"));
        _voiceText = CityUi.Field(Loc.GetString("dystopia-surveillance-voice-placeholder"));
        _voiceText.HorizontalExpand = true;
        _voiceText.OnTextEntered += _ => SendVoice(false);
        voice.AddChild(_voiceText);
        _voiceLoud = CityUi.MakeButton(Loc.GetString("dystopia-surveillance-voice-loud"), CityButtonStyle.Primary);
        _voiceLoud.OnPressed += _ => SendVoice(false);
        voice.AddChild(_voiceLoud);
        _voicePersonal = CityUi.MakeButton(Loc.GetString("dystopia-surveillance-voice-personal"));
        _voicePersonal.OnPressed += _ => SendVoice(true);
        voice.AddChild(_voicePersonal);
        right.AddChild(voice);

        // ================= Тревоги =================
        var alerts = Column();
        var alertsHead = Row(8);
        var alertsHint = CityUi.Hint(Loc.GetString("dystopia-surveillance-alerts-hint"));
        alertsHint.HorizontalExpand = true;
        alertsHead.AddChild(alertsHint);
        _dismissAll = CityUi.MakeButton(Loc.GetString("dystopia-surveillance-dismiss-all"));
        _dismissAll.VerticalAlignment = VAlignment.Center;
        _dismissAll.OnPressed += _ => OnDismiss?.Invoke(null);
        alertsHead.AddChild(_dismissAll);
        alerts.AddChild(alertsHead);
        _alertRows = ScrollList(alerts);
        _views[Tab.Alerts] = alerts;
        Contents.AddChild(alerts);

        // ================= Слежка =================
        var track = Column();
        var trackRow = Row(8);
        trackRow.AddChild(Caption("dystopia-surveillance-track-number"));
        _trackNumber = CityUi.Field(Loc.GetString("dystopia-surveillance-track-placeholder"), 120);
        _trackNumber.OnTextEntered += _ => SendTrack();
        trackRow.AddChild(_trackNumber);
        var trackStart = CityUi.MakeButton(Loc.GetString("dystopia-surveillance-track-start"), CityButtonStyle.Primary);
        trackStart.OnPressed += _ => SendTrack();
        trackRow.AddChild(trackStart);
        var trackStop = CityUi.MakeButton(Loc.GetString("dystopia-surveillance-track-stop"));
        trackStop.OnPressed += _ => OnTrack?.Invoke(null);
        trackRow.AddChild(trackStop);
        track.AddChild(trackRow);
        track.AddChild(CityUi.Hint(Loc.GetString("dystopia-surveillance-track-hint")));
        _trackInfo = ScrollList(track);
        _views[Tab.Track] = track;
        Contents.AddChild(track);

        // ================= Патрули =================
        var dozor = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
        };
        var mapColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
        };
        var toggles = Row(6);
        _targetsButton = new CityButton(Loc.GetString("dystopia-dozor-targets").ToUpperInvariant(), CityButtonStyle.Tab) { MinHeight = 26, Active = true };
        _trailsButton = new CityButton(Loc.GetString("dystopia-dozor-trails").ToUpperInvariant(), CityButtonStyle.Tab) { MinHeight = 26, Active = true };
        _targetsButton.Label.ClipText = false;
        _trailsButton.Label.ClipText = false;
        _targetsButton.OnPressed += _ =>
        {
            _dozorMap!.ShowTargets = !_dozorMap.ShowTargets;
            _targetsButton.Active = _dozorMap.ShowTargets;
        };
        _trailsButton.OnPressed += _ =>
        {
            _dozorMap!.ShowTrails = !_dozorMap.ShowTrails;
            _trailsButton.Active = _dozorMap.ShowTrails;
        };
        toggles.AddChild(_targetsButton);
        toggles.AddChild(_trailsButton);
        mapColumn.AddChild(toggles);
        var dozorPanel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        dozorPanel.VerticalExpand = true;
        dozorPanel.HorizontalExpand = true;
        _dozorMap = new CityDozorMapControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            FlashOf = number => _beats.TryGetValue(number, out var beat) ? beat.Flash : 0f,
        };
        _dozorMap.GuardSelected += SelectGuard;
        dozorPanel.AddChild(_dozorMap);
        mapColumn.AddChild(dozorPanel);
        dozor.AddChild(mapColumn);

        var listColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 290,
            MaxWidth = 290,
            VerticalExpand = true,
            SeparationOverride = 4,
        };
        listColumn.AddChild(Small("dystopia-dozor-list"));
        _patrolRows = ScrollList(listColumn);
        dozor.AddChild(listColumn);
        _views[Tab.Dozor] = dozor;
        Contents.AddChild(dozor);

        // ================= Маркеры =================
        var keywords = Column();
        keywords.AddChild(CityUi.Hint(Loc.GetString("dystopia-surveillance-keywords-hint")));
        var addRow = Row(8);
        _keywordText = CityUi.Field(Loc.GetString("dystopia-surveillance-keyword-placeholder"), 200);
        _keywordText.OnTextEntered += _ => SendKeyword();
        addRow.AddChild(_keywordText);
        var add = CityUi.MakeButton(Loc.GetString("dystopia-surveillance-keyword-add"), CityButtonStyle.Primary);
        add.OnPressed += _ => SendKeyword();
        addRow.AddChild(add);
        keywords.AddChild(addRow);
        _keywordRows = ScrollList(keywords);
        _views[Tab.Keywords] = keywords;
        Contents.AddChild(keywords);

        SetTab(Tab.Cameras);
        SetMapMode(false);
    }

    /// <summary>Сетка Города для карты (её берёт окно консоли от самой консоли).</summary>
    public void SetGrid(EntityUid? grid)
    {
        _map.MapUid = grid;
        _map.ForceNavMapUpdate();
        _dozorMap.MapUid = grid;
        _dozorMap.ForceNavMapUpdate();
    }

    public void UpdateState(CitySurveillanceUiState state)
    {
        _state = state;

        var unaccepted = state.Alerts.Count(a => a.AcceptedBy.Length == 0 && !a.Resolved);
        _tabs[Tab.Cameras].Text = Loc.GetString("dystopia-surveillance-tab-cameras", ("count", state.Cameras.Count(c => c.Active))).ToUpperInvariant();
        _tabs[Tab.Alerts].Text = Loc.GetString("dystopia-surveillance-tab-alerts", ("count", unaccepted)).ToUpperInvariant();
        _tabs[Tab.Alerts].TextColor = unaccepted > 0 ? CityRegistryConsoleWindow.Danger : null;
        _tabs[Tab.Track].Text = Loc.GetString(state.Tracking != null ? "dystopia-surveillance-tab-track-on" : "dystopia-surveillance-tab-track").ToUpperInvariant();
        _tabs[Tab.Dozor].Text = Loc.GetString("dystopia-dozor-tab",
            ("up", state.Patrols.Count(p => p.Health != CityGuardHealth.NoData)), ("total", state.Patrols.Count)).ToUpperInvariant();
        var trouble = state.Patrols.Any(p => p.Distress || p.Health is CityGuardHealth.Critical or CityGuardHealth.Dead);
        _tabs[Tab.Dozor].TextColor = trouble ? CityRegistryConsoleWindow.Danger : null;
        _tabs[Tab.Keywords].Text = Loc.GetString("dystopia-surveillance-tab-keywords", ("count", state.Keywords.Count)).ToUpperInvariant();

        RedrawCameras(state);
        RedrawScreen(state);
        RedrawSpeech(state);
        RedrawAlerts(state);
        RedrawTrack(state);
        RedrawDozor(state);
        RedrawKeywords(state);
    }

    private void SetTab(Tab tab)
    {
        _tab = tab;
        foreach (var (key, button) in _tabs)
        {
            button.Active = key == tab;
            _views[key].Visible = key == tab;
        }
    }

    private void SetMapMode(bool map)
    {
        _listButton.Active = !map;
        _mapButton.Active = map;
        _listScroll.Visible = !map;
        _map.Visible = map;
    }

    // ======================= Камеры =======================

    private CitySurveillanceCamera? Current()
    {
        return _state?.Camera is { } camera ? _state.Cameras.FirstOrDefault(c => c.Entity == camera) : null;
    }

    private void RedrawCameras(CitySurveillanceUiState state)
    {
        _map.SetCameras(state.Cameras, state.Camera);

        var signature = $"{state.Camera}:" + string.Join(";", state.Cameras.Select(c => $"{c.Entity}:{c.Name}:{c.Active}:{c.Place}"));
        if (signature == _camerasSignature)
            return;
        _camerasSignature = signature;

        _cameraRows.RemoveAllChildren();
        if (state.Cameras.Count == 0)
        {
            var empty = CityUi.Hint(Loc.GetString("dystopia-surveillance-no-cameras"));
            empty.Margin = new Thickness(8, 6);
            _cameraRows.AddChild(empty);
            return;
        }

        CityCameraKind? group = null;
        foreach (var camera in state.Cameras)
        {
            if (group != camera.Kind)
            {
                group = camera.Kind;
                var caption = Small($"dystopia-surveillance-group-{camera.Kind.ToString().ToLowerInvariant()}");
                caption.Margin = new Thickness(8, 6, 8, 2);
                _cameraRows.AddChild(caption);
            }

            var net = camera.Entity;
            var row = new CityRegistryRow();
            row.OnPressed += _ => OnSelect?.Invoke(net);
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8, HorizontalExpand = true };
            var mark = camera.Kind == CityCameraKind.Mask ? "◆" : "●";
            var color = !camera.Active ? CitySurveillanceMapControl.OffColor
                : camera.Entity == state.Camera ? CityUi.Glow
                : camera.Kind switch
                {
                    CityCameraKind.Mask => CitySurveillanceMapControl.MaskColor,
                    CityCameraKind.Restricted => CitySurveillanceMapControl.RestrictedColor,
                    _ => CityUi.Text,
                };
            var name = CityUi.MakeLabel($"{(camera.Active ? mark : "○")} {camera.Name}", color, CityUi.Regular(12));
            name.HorizontalExpand = true;
            name.ClipText = true;
            line.AddChild(name);
            var place = CityUi.MakeLabel(camera.Active ? camera.Place : camera.Status,
                camera.Active ? CityUi.Dim : CityRegistryConsoleWindow.Danger, CityUi.Regular(11));
            place.MaxWidth = 120;
            place.ClipText = true;
            line.AddChild(place);
            row.AddChild(line);
            _cameraRows.AddChild(row);
        }
    }

    private void RedrawScreen(CitySurveillanceUiState state)
    {
        var current = Current();
        var canUse = state.CanUse;

        _cameraTitle.Text = current == null
            ? Loc.GetString("dystopia-surveillance-screen-off")
            : Loc.GetString(current.Active ? "dystopia-surveillance-screen-on" : "dystopia-surveillance-screen-dark",
                ("camera", current.Name), ("place", current.Place.Length > 0 ? current.Place : "—"));
        _cameraOff.Disabled = current == null;

        var live = current is { Active: true };
        _voiceText.Editable = canUse && live;
        _voiceLoud.Disabled = !canUse || !live;
        _voicePersonal.Visible = current?.Kind == CityCameraKind.Mask;
        _voicePersonal.Disabled = !canUse || !live;

        var camera = live ? _entMan.GetEntity(state.Camera) : null;
        if (camera != _viewCamera)
        {
            if (_viewCamera is { } old)
                _entMan.System<EyeLerpingSystem>().RemoveEye(old);
            _viewCamera = camera;
            _view.Eye = _blank;
            _view.Visible = false;
        }

        _noSignal.Visible = !_view.Visible;
        _noSignal.Text = Loc.GetString(current == null
            ? "dystopia-surveillance-no-camera"
            : live ? "dystopia-surveillance-connecting" : "dystopia-surveillance-no-signal").ToUpperInvariant();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_speechToBottom > 0)
        {
            _speechToBottom--;
            _speechScroll.SetScrollValue(new Vector2(0, float.MaxValue));
        }

        // камера может дойти до клиента не сразу — ждём её глаз
        if (_viewCamera is not { } camera || _view.Eye != _blank)
            return;

        if (!_entMan.TryGetComponent<EyeComponent>(camera, out var eye) || eye.Eye == null)
            return;

        _entMan.System<EyeLerpingSystem>().AddEye(camera, eye);
        _view.Eye = eye.Eye;
        _view.Visible = true;
        _noSignal.Visible = false;
    }

    public override void Close()
    {
        if (_viewCamera is { } camera)
            _entMan.System<EyeLerpingSystem>().RemoveEye(camera);
        _viewCamera = null;
        base.Close();
    }

    private void RedrawSpeech(CitySurveillanceUiState state)
    {
        var lines = state.Speech;
        var signature = $"{state.Camera}:{lines.Count}:{(lines.Count > 0 ? lines[^1].Time : TimeSpan.Zero)}:{(lines.Count > 0 ? lines[^1].Text : string.Empty)}";
        if (signature == _speechSignature)
            return;
        _speechSignature = signature;

        _speechRows.RemoveAllChildren();
        if (lines.Count == 0)
        {
            _speechRows.AddChild(CityUi.Hint(Loc.GetString(state.Camera == null
                ? "dystopia-surveillance-speech-off"
                : "dystopia-surveillance-speech-empty")));
            return;
        }

        foreach (var line in lines)
        {
            var color = line.Voice ? CitySurveillanceMapControl.MaskColor
                : line.Keyword ? CityRegistryConsoleWindow.Danger
                : CityUi.Text;
            _speechRows.AddChild(CityUi.Hint(Loc.GetString("dystopia-surveillance-speech-line",
                ("time", Clock(line.Time)), ("name", line.Name), ("text", line.Text)), color));
        }

        // новые реплики — внизу: прокручиваем к ним, когда строки разложатся
        _speechToBottom = 2;
    }

    private void SendVoice(bool personal)
    {
        var text = _voiceText.Text.Trim();
        if (text.Length == 0)
            return;

        OnVoice?.Invoke(text, personal);
        _voiceText.Text = string.Empty;
    }

    // ======================= Тревоги =======================

    private void RedrawAlerts(CitySurveillanceUiState state)
    {
        var signature = $"{state.CanUse}:" + string.Join(";", state.Alerts.Select(a => $"{a.Id}:{a.AcceptedBy}:{a.Resolved}"));
        _dismissAll.Disabled = !state.CanUse || !state.Alerts.Any(a => a.AcceptedBy.Length > 0 || a.Resolved);
        if (signature == _alertsSignature)
            return;
        _alertsSignature = signature;

        _alertRows.RemoveAllChildren();
        if (state.Alerts.Count == 0)
        {
            AddEmpty(_alertRows, "dystopia-surveillance-alerts-empty");
            return;
        }

        foreach (var alert in state.Alerts)
        {
            var row = RowPanel();
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };

            var time = CityUi.MakeLabel(Clock(alert.Time), CityUi.Dim, CityUi.Regular(11));
            time.MinWidth = 48;
            line.AddChild(time);

            var kind = CityUi.MakeLabel(
                Loc.GetString($"dystopia-surveillance-kind-{alert.Kind.ToString().ToLowerInvariant()}").ToUpperInvariant(),
                KindColor(alert.Kind), CityUi.Bold(11));
            kind.MinWidth = 150;
            line.AddChild(kind);

            var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, MinWidth = 0 };
            text.AddChild(new CityWrapLabel(alert.Text, alert.Resolved || alert.AcceptedBy.Length > 0 ? CityUi.Dim : CityUi.Glow));
            var note = alert.Place;
            if (alert.AcceptedBy.Length > 0)
                note += " · " + Loc.GetString("dystopia-surveillance-accepted", ("author", alert.AcceptedBy), ("time", Clock(alert.AcceptedAt)));
            if (alert.Resolved)
                note += " · " + Loc.GetString("dystopia-surveillance-resolved", ("time", Clock(alert.ResolvedAt)));
            text.AddChild(CityUi.Hint(note));
            line.AddChild(text);

            // кнопки — в блоке постоянной ширины: длинный текст тревоги их не сжимает
            var actions = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                MinWidth = 280,
                VerticalAlignment = VAlignment.Center,
            };
            actions.AddChild(new Control { HorizontalExpand = true });
            var id = alert.Id;

            if (alert.Camera is { } camera)
            {
                var watch = ActionButton("dystopia-registry-watch", CityButtonStyle.Normal, 110);
                watch.OnPressed += _ =>
                {
                    OnSelect?.Invoke(camera);
                    SetTab(Tab.Cameras);
                };
                actions.AddChild(watch);
            }

            if (alert.AcceptedBy.Length == 0 && !alert.Resolved)
            {
                var accept = ActionButton("dystopia-surveillance-accept", CityButtonStyle.Danger, 110);
                accept.Disabled = !state.CanUse;
                accept.OnPressed += _ => OnAccept?.Invoke(id);
                actions.AddChild(accept);
            }

            var dismiss = ActionButton("dystopia-surveillance-dismiss", CityButtonStyle.Normal, 34);
            dismiss.Disabled = !state.CanUse;
            dismiss.ToolTip = Loc.GetString("dystopia-surveillance-dismiss-tooltip");
            dismiss.OnPressed += _ => OnDismiss?.Invoke(id);
            actions.AddChild(dismiss);
            line.AddChild(actions);

            row.AddChild(line);
            _alertRows.AddChild(row);
        }
    }

    private static Color KindColor(CityAlertKind kind)
    {
        return kind switch
        {
            CityAlertKind.Outlaw => CityRegistryConsoleWindow.Stripped,
            CityAlertKind.Wanted => CityRegistryConsoleWindow.Danger,
            CityAlertKind.Restricted => CityRegistryConsoleWindow.Detained,
            CityAlertKind.Keyword => CityRegistryConsoleWindow.Watch,
            CityAlertKind.GuardCritical or CityAlertKind.GuardDead or CityAlertKind.Distress => CityDozorMapControl.DistressColor,
            _ => CityUi.Dim,
        };
    }

    // ======================= Слежка =======================

    private void SendTrack()
    {
        if (int.TryParse(_trackNumber.Text.Trim().TrimStart('№', '#'), out var number))
            OnTrack?.Invoke(number);
    }

    private void RedrawTrack(CitySurveillanceUiState state)
    {
        var track = state.Track;
        var signature = track == null
            ? $"none:{state.Tracking}"
            : $"{track.Number}:{track.Status}:{track.Current}:" + string.Join(";", track.Path.Select(p => $"{p.Time}:{p.LastTime}:{p.Camera}"));
        if (signature == _trackSignature)
            return;
        _trackSignature = signature;

        _trackInfo.RemoveAllChildren();
        if (track == null)
        {
            AddEmpty(_trackInfo, "dystopia-surveillance-track-none");
            return;
        }

        var card = new CityCard();
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        box.AddChild(CityUi.MakeLabel($"№{track.Number} · {track.Name}", CityUi.Glow, CityUi.Bold(13)));
        var status = CityUi.MakeLabel(CityRegistryConsoleWindow.StatusText(track.Status).ToUpperInvariant(),
            CityRegistryConsoleWindow.StatusColor(track.Status), CityUi.Bold(11));
        box.AddChild(status);

        var now = track.Current is { } current && state.Cameras.FirstOrDefault(c => c.Entity == current) is { } seen
            ? Loc.GetString("dystopia-surveillance-track-now", ("camera", seen.Name), ("place", seen.Place))
            : Loc.GetString("dystopia-surveillance-track-lost");
        box.AddChild(CityUi.Hint(now, track.Current != null ? CityUi.Accent : CityRegistryConsoleWindow.Watch));
        card.AddChild(box);
        _trackInfo.AddChild(card);

        _trackInfo.AddChild(Small("dystopia-surveillance-track-path"));
        if (track.Path.Count == 0)
        {
            AddEmpty(_trackInfo, "dystopia-surveillance-track-path-empty");
            return;
        }

        foreach (var sighting in Enumerable.Reverse(track.Path))
        {
            var row = RowPanel();
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
            var time = CityUi.MakeLabel(Span(sighting.Time, sighting.LastTime), CityUi.Dim, CityUi.Regular(11));
            time.MinWidth = 96;
            line.AddChild(time);
            var place = CityUi.MakeLabel($"{sighting.Place} · {sighting.Camera}", CityUi.Text, CityUi.Regular(12));
            place.HorizontalExpand = true;
            place.ClipText = true;
            line.AddChild(place);
            var watch = CityUi.MakeButton(Loc.GetString("dystopia-registry-watch"));
            var camera = sighting.CameraEntity;
            watch.OnPressed += _ =>
            {
                OnSelect?.Invoke(camera);
                SetTab(Tab.Cameras);
            };
            line.AddChild(watch);
            row.AddChild(line);
            _trackInfo.AddChild(row);
        }
    }

    // ======================= Дозор =======================

    private void RedrawDozor(CitySurveillanceUiState state)
    {
        _dozorMap.Selected = _selectedGuard;
        _dozorMap.SetData(state.Patrols, state.Targets);

        // кардиограммы живут дольше строк списка: при перестройке не сбиваются
        foreach (var patrol in state.Patrols)
        {
            if (!_beats.TryGetValue(patrol.Number, out var beat))
                _beats[patrol.Number] = beat = new CityHeartbeatControl();
            beat.Health = patrol.Health;
        }

        var signature = $"{_selectedGuard}:" + string.Join(";", state.Patrols.Select(p =>
            $"{p.Number}:{p.Health}:{p.Mask}:{p.OnShift}:{p.Place}:{p.Camera}:{p.HasPosition}:{p.Distress}:" +
            $"{(p.DarkFor is { } d ? (int) d.TotalMinutes : -1)}"));
        if (signature == _patrolsSignature)
            return;
        _patrolsSignature = signature;

        _patrolRows.RemoveAllChildren();
        if (state.Patrols.Count == 0)
        {
            AddEmpty(_patrolRows, "dystopia-surveillance-patrols-empty");
            return;
        }

        foreach (var patrol in state.Patrols)
        {
            var number = patrol.Number;
            var row = new CityRegistryRow();
            if (number == _selectedGuard)
                row.StyleBoxOverride = CityUi.Box(CityUi.ActiveFill, CityUi.Accent, 1, 6, 4);
            row.OnPressed += _ => SelectGuard(number);

            var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
            var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var noData = patrol.Health == CityGuardHealth.NoData;
            var who = CityUi.MakeLabel($"{(noData ? "◇" : "◆")} №{patrol.Number}",
                noData ? CityUi.Dim : CityHeartbeatControl.ColorOf(patrol.Health), CityUi.Bold(12));
            var whoBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
            whoBox.AddChild(who);
            var job = CityUi.MakeLabel(patrol.JobTitle, CityUi.Dim, CityUi.Regular(10));
            job.ClipText = true;
            whoBox.AddChild(job);
            head.AddChild(whoBox);

            var beat = _beats[number];
            beat.Orphan();
            beat.VerticalAlignment = VAlignment.Center;
            head.AddChild(beat);
            box.AddChild(head);

            box.AddChild(CityUi.Hint(StatusLine(patrol), patrol.Distress ? CityDozorMapControl.DistressColor : CityUi.Dim));

            var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            if (patrol.HasPosition)
            {
                var onMap = ActionButton("dystopia-dozor-on-map", CityButtonStyle.Normal, 0);
                onMap.OnPressed += _ => SelectGuard(number);
                buttons.AddChild(onMap);
            }
            if (patrol.Camera is { } camera)
            {
                var mask = ActionButton("dystopia-dozor-mask", CityButtonStyle.Normal, 0);
                mask.OnPressed += _ =>
                {
                    OnSelect?.Invoke(camera);
                    SetTab(Tab.Cameras);
                };
                buttons.AddChild(mask);
            }
            if (buttons.ChildCount > 0)
                box.AddChild(buttons);

            row.AddChild(box);
            _patrolRows.AddChild(row);
        }
    }

    /// <summary>Строка под номером: сигнал бедствия, не в смене, маска снята или где он сейчас.</summary>
    private static string StatusLine(CityPatrolEntry patrol)
    {
        if (patrol.Distress)
            return Loc.GetString("dystopia-dozor-distress");

        var place = patrol.Place.Length > 0 ? patrol.Place : "—";
        if (patrol.Health == CityGuardHealth.NoData)
        {
            var line = patrol.DarkFor is { } dark
                ? Loc.GetString("dystopia-dozor-no-data-for", ("minutes", (int) dark.TotalMinutes), ("place", place))
                : Loc.GetString("dystopia-dozor-no-data");
            return patrol.OnShift ? line : Loc.GetString("dystopia-surveillance-off-shift") + " · " + line;
        }

        var status = Loc.GetString($"dystopia-dozor-health-{patrol.Health.ToString().ToLowerInvariant()}") + " · " + place;
        if (patrol.Mask == CityMaskState.Lowered)
            status += " · " + Loc.GetString("dystopia-surveillance-mask-lowered");
        return status;
    }

    private void SelectGuard(int? number)
    {
        _selectedGuard = number;
        _patrolsSignature = string.Empty;
        _dozorMap.Selected = number;

        if (number != null && _state?.Patrols.FirstOrDefault(p => p.Number == number) is { HasPosition: true } patrol &&
            _entMan.GetEntity(patrol.Grid) is { Valid: true } grid)
        {
            _dozorMap.CenterToCoordinates(new EntityCoordinates(grid, patrol.Position));
        }

        if (_state != null)
            RedrawDozor(_state);
    }

    // ======================= Маркеры =======================

    private void SendKeyword()
    {
        var word = _keywordText.Text.Trim();
        if (word.Length == 0)
            return;

        OnKeyword?.Invoke(word, true);
        _keywordText.Text = string.Empty;
    }

    private void RedrawKeywords(CitySurveillanceUiState state)
    {
        _keywordText.Editable = state.CanUse;
        var signature = $"{state.CanUse}:" + string.Join(";", state.Keywords);
        if (signature == _keywordsSignature)
            return;
        _keywordsSignature = signature;

        _keywordRows.RemoveAllChildren();
        if (state.Keywords.Count == 0)
        {
            AddEmpty(_keywordRows, "dystopia-surveillance-keywords-empty");
            return;
        }

        foreach (var word in state.Keywords)
        {
            var row = RowPanel();
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
            var label = CityUi.MakeLabel(Loc.GetString("dystopia-surveillance-keyword-line", ("word", word)), CityUi.Glow, CityUi.Regular(12));
            label.HorizontalExpand = true;
            line.AddChild(label);
            var remove = CityUi.MakeButton(Loc.GetString("dystopia-surveillance-keyword-remove"), CityButtonStyle.Danger);
            remove.Disabled = !state.CanUse;
            var w = word;
            remove.OnPressed += _ => OnKeyword?.Invoke(w, false);
            line.AddChild(remove);
            row.AddChild(line);
            _keywordRows.AddChild(row);
        }
    }

    // ======================= Помощники =======================

    private static CityButton ActionButton(string key, CityButtonStyle style, float width)
    {
        var button = CityUi.MakeButton(Loc.GetString(key), style);
        button.MinWidth = width;
        button.Label.ClipText = false;
        return button;
    }

    private static BoxContainer ScrollList(BoxContainer parent)
    {
        var panel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        panel.VerticalExpand = true;
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 2 };
        scroll.AddChild(rows);
        panel.AddChild(scroll);
        parent.AddChild(panel);
        return rows;
    }

    private static void AddEmpty(BoxContainer rows, string key)
    {
        var empty = CityUi.Hint(Loc.GetString(key));
        empty.Margin = new Thickness(8, 6);
        rows.AddChild(empty);
    }

    private static PanelContainer RowPanel()
    {
        var row = new PanelContainer { PanelOverride = CityUi.Box(Color.Transparent, CityUi.Line, 0, 8, 5) };
        ((StyleBoxFlat) row.PanelOverride).BorderThickness = new Thickness(0, 0, 0, 1);
        return row;
    }

    private static BoxContainer Row(int separation)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, SeparationOverride = separation };
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

    private static string Clock(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}";
    }

    private static string Span(TimeSpan from, TimeSpan to)
    {
        var start = Clock(from);
        var end = Clock(to);
        return start == end ? start : $"{start}–{end}";
    }
}
