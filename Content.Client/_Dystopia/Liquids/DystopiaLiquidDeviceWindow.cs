// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню жидкостного устройства в ванильном стиле (как у газовых насоса и фильтра): переключатель вкл/выкл,
// скорость (ед./с) с «Установить» и «Макс.»; у фильтра — список всех веществ с поиском (то, что сейчас во входной
// трубе, — сверху), «Выбрать вещество» и «Не отводить»; у качалки — запас выхода; внизу — что в трубах у узлов.

using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Dystopia.Liquids;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Liquids;

public sealed class DystopiaLiquidDeviceWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<bool>? OnToggle;
    public event Action<float>? OnSetRate;
    public event Action<string?>? OnSetFilter;

    private static readonly Color Dim = Color.FromHex("#A0A0A0");
    private static readonly Color Bad = Color.FromHex("#E05050");
    private static readonly Color Good = Color.FromHex("#78E070");

    private readonly SwitchButton _toggle;
    private readonly Label _status;
    private readonly RichTextLabel _hint;
    private readonly LineEdit _rateInput;
    private readonly Button _setRate;
    private readonly Label _rateMax;

    private readonly BoxContainer _filterBox;
    private readonly Label _filterCurrent;
    private readonly LineEdit _search;
    private readonly ItemList _reagents;
    private readonly Button _select;

    private readonly RichTextLabel _source;
    private readonly BoxContainer _nets;

    private DystopiaLiquidDeviceUiState? _state;
    private string? _selected;
    private string _listKey = string.Empty;
    private string _netsKey = string.Empty;

    /// <summary>Все вещества игры: id → название (по алфавиту).</summary>
    private readonly List<(string Id, string Name)> _all = new();

    public DystopiaLiquidDeviceWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(420, 260);

        foreach (var reagent in _proto.EnumeratePrototypes<ReagentPrototype>())
            _all.Add((reagent.ID, reagent.LocalizedName));
        _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(5),
            SeparationOverride = 8,
            VerticalExpand = true,
        };
        Contents.AddChild(root);

        // Вкл/выкл и состояние
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        _toggle = new SwitchButton { HorizontalAlignment = HAlignment.Left };
        _toggle.OnToggled += _ => OnToggle?.Invoke(_toggle.Pressed);
        _status = new Label { HorizontalExpand = true, HorizontalAlignment = HAlignment.Right };
        top.AddChild(_toggle);
        top.AddChild(_status);
        root.AddChild(top);

        _hint = new RichTextLabel { HorizontalExpand = true };
        root.AddChild(_hint);

        // Скорость
        var rate = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        rate.AddChild(new Label { Text = Loc.GetString("dystopia-liquid-ui-rate-label") });
        _rateInput = new LineEdit { MinSize = new Vector2(70, 0) };
        _rateInput.OnTextChanged += _ => _setRate!.Disabled = false;
        _rateInput.OnTextEntered += _ => SubmitRate();
        _setRate = new Button { Text = Loc.GetString("dystopia-liquid-ui-set"), Disabled = true };
        _setRate.OnPressed += _ => SubmitRate();
        var max = new Button { Text = Loc.GetString("dystopia-liquid-ui-max") };
        max.OnPressed += _ =>
        {
            if (_state != null)
                OnSetRate?.Invoke(_state.MaxRate);
        };
        _rateMax = new Label { FontColorOverride = Dim };
        rate.AddChild(_rateInput);
        rate.AddChild(_setRate);
        rate.AddChild(max);
        rate.AddChild(_rateMax);
        root.AddChild(rate);

        // Фильтр
        _filterBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            VerticalExpand = true,
            Visible = false,
        };
        _filterCurrent = new Label();
        _filterBox.AddChild(_filterCurrent);
        _filterBox.AddChild(new Label { Text = Loc.GetString("dystopia-liquid-ui-filter-choose") });
        _search = new LineEdit { PlaceHolder = Loc.GetString("dystopia-liquid-ui-filter-search") };
        _search.OnTextChanged += _ => RebuildList(force: true);
        _filterBox.AddChild(_search);
        _reagents = new ItemList { SelectMode = ItemList.ItemListSelectMode.Single, VerticalExpand = true, MinSize = new Vector2(0, 180) };
        _reagents.OnItemSelected += args =>
        {
            _selected = (string?) args.ItemList[args.ItemIndex].Metadata;
            _select!.Disabled = _selected == null || _selected == _state?.FilterReagent;
        };
        _reagents.OnItemDeselected += _ =>
        {
            _selected = null;
            _select!.Disabled = true;
        };
        _filterBox.AddChild(_reagents);
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        _select = new Button { Text = Loc.GetString("dystopia-liquid-ui-filter-select"), HorizontalExpand = true, Disabled = true };
        _select.OnPressed += _ =>
        {
            if (_selected != null)
                OnSetFilter?.Invoke(_selected);
        };
        var none = new Button { Text = Loc.GetString("dystopia-liquid-filter-none"), HorizontalExpand = true };
        none.OnPressed += _ => OnSetFilter?.Invoke(null);
        buttons.AddChild(_select);
        buttons.AddChild(none);
        _filterBox.AddChild(buttons);
        root.AddChild(_filterBox);

        // Качалка
        _source = new RichTextLabel { HorizontalExpand = true, Visible = false };
        root.AddChild(_source);

        // Трубы у узлов
        root.AddChild(new PanelContainer { MinSize = new Vector2(0, 2), StyleClasses = { "LowDivider" } });
        _nets = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        root.AddChild(_nets);
    }

    private void SubmitRate()
    {
        var text = _rateInput.Text.Trim().Replace(',', '.');
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate))
            return;

        _setRate.Disabled = true;
        OnSetRate?.Invoke(rate);
    }

    public void UpdateState(DystopiaLiquidDeviceUiState state)
    {
        var first = _state == null;
        _state = state;

        _toggle.Visible = state.Toggleable;
        _toggle.Pressed = state.On;

        (string status, Color color) = state.Toggleable && !state.On ? ("dystopia-liquid-ui-status-off", Dim)
            : state.RequirePower && !state.Powered ? ("dystopia-liquid-ui-status-unpowered", Bad)
            : state.Working ? ("dystopia-liquid-ui-status-working", Good)
            : ("dystopia-liquid-ui-status-idle", Dim);
        _status.Text = Loc.GetString(status);
        _status.FontColorOverride = color;

        _hint.SetMessage(FormattedMessage.FromUnformatted(
            Loc.GetString($"dystopia-liquid-ui-hint-{state.Mode.ToString().ToLowerInvariant()}")), Dim);

        // Поле скорости не трогаем, пока в нём печатают.
        if (!_rateInput.HasKeyboardFocus())
        {
            _rateInput.Text = Fmt(state.Rate);
            _setRate.Disabled = true;
        }

        _rateMax.Text = Loc.GetString("dystopia-liquid-ui-rate-max", ("max", Fmt(state.MaxRate)));

        var isFilter = state.Mode == DystopiaLiquidDeviceMode.Filter;
        _filterBox.Visible = isFilter;
        if (isFilter)
        {
            var current = state.FilterReagent != null && _proto.TryIndex<ReagentPrototype>(state.FilterReagent, out var proto)
                ? proto.LocalizedName
                : Loc.GetString("dystopia-liquid-ui-filter-nothing");
            _filterCurrent.Text = Loc.GetString("dystopia-liquid-ui-filter-current", ("reagent", current));
            RebuildList(force: false);
        }

        _source.Visible = state.Source != null;
        if (state.Source != null)
            _source.SetMessage(FormattedMessage.FromMarkupOrThrow(state.Source));

        RebuildNets(state);

        if (first)
            SetSize = isFilter ? new Vector2(440, 620) : new Vector2(440, 320);
    }

    // ------------------------------------------------------------------ список веществ фильтра

    private void RebuildList(bool force)
    {
        if (_state == null)
            return;

        // То, что сейчас во входной трубе, — сверху; список перестраиваем, только если он поменялся.
        var present = _state.FilterOptions.Select(o => o.Id).ToHashSet();
        var key = $"{_search.Text}|{_state.FilterReagent}|{string.Join(",", present.OrderBy(x => x))}";
        if (!force && key == _listKey)
            return;

        _listKey = key;
        var query = _search.Text.Trim();
        var keep = _selected;          // очистка списка снимает выделение — запоминаем его
        _reagents.Clear();
        _selected = keep;

        var shown = _all
            .Where(r => query.Length == 0 ||
                        r.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                        r.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => present.Contains(r.Id) ? 0 : 1);

        foreach (var (id, name) in shown)
        {
            var text = present.Contains(id) ? $"{name} {Loc.GetString("dystopia-liquid-ui-filter-present")}" : name;
            if (id == _state.FilterReagent)
                text = "► " + text;

            var item = new ItemList.Item(_reagents) { Text = text, Metadata = id };
            _reagents.Add(item);
            if (id == keep)
                item.Selected = true;
        }

        _selected = keep;

        _select.Disabled = _selected == null || _selected == _state.FilterReagent;
    }

    // ------------------------------------------------------------------ трубы

    private void RebuildNets(DystopiaLiquidDeviceUiState state)
    {
        var key = string.Join(";", state.Nets.Select(n => $"{n.Label}:{n.Connected}:{Fmt(n.Volume)}:{Fmt(n.Max)}:" +
            $"{(int) MathF.Round(n.Celsius)}:" + string.Join(",", n.Reagents.Select(r => $"{r.Id}={Fmt(r.Amount)}"))));
        if (key == _netsKey)
            return;

        _netsKey = key;
        _nets.RemoveAllChildren();
        foreach (var net in state.Nets)
        {
            string line;
            if (!net.Connected)
                line = Loc.GetString("dystopia-liquid-ui-disconnected");
            else if (net.Volume <= 0f)
                line = Loc.GetString("dystopia-liquid-ui-empty", ("max", Fmt(net.Max)));
            else
                line = Loc.GetString("dystopia-liquid-ui-fill",
                    ("percent", net.Max > 0f ? (int) MathF.Round(net.Volume / net.Max * 100f) : 0),
                    ("volume", Fmt(net.Volume)), ("max", Fmt(net.Max)), ("temperature", (int) MathF.Round(net.Celsius)));

            _nets.AddChild(new Label { Text = $"{net.Label}: {line}" });
            foreach (var reagent in net.Reagents)
            {
                _nets.AddChild(new Label
                {
                    Text = $"    {reagent.Name} — {Loc.GetString("dystopia-liquid-ui-amount", ("amount", Fmt(reagent.Amount)))}",
                    FontColorOverride = reagent.Color,
                });
            }
        }
    }

    private static string Fmt(float value)
    {
        return Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
    }
}
