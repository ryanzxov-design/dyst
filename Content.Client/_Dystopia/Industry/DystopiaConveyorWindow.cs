// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню конвейерных устройств. Извлекатель/вставщик: режим (белый/чёрный список), список, поиск предметов.
// Сортировщик: зелёный и синий выходы со своими списками, жёлтый — всё остальное.

using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Item;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaConveyorWindow : CityWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    public event Action<bool>? OnSetWhitelist;
    public event Action<DystopiaConveyorList, string>? OnAdd;
    public event Action<DystopiaConveyorList, string>? OnRemove;
    public event Action<DystopiaConveyorList>? OnAddHeld;
    public event Action<DystopiaConveyorList>? OnClear;

    private static readonly Color Green = Color.FromHex("#50C050");
    private static readonly Color Blue = Color.FromHex("#5080E0");
    private static readonly Color Yellow = Color.FromHex("#E0C040");
    private const int MaxResults = 40;

    private readonly BoxContainer _top;
    private readonly BoxContainer _lists;
    private readonly LineEdit _search;
    private readonly BoxContainer _results;

    /// <summary>Все предметы игры для поиска: ключ фильтра → название (стопки одного вида — один ключ).</summary>
    private readonly List<(string Key, string Name)> _index = new();

    private DystopiaConveyorUiState? _state;

    public DystopiaConveyorWindow()
    {
        IoCManager.InjectDependencies(this);

        WindowTitle = Loc.GetString("dystopia-conveyor-ui-title");
        Subtitle = Loc.GetString("dystopia-industry-ui-sub");
        Slogan = Loc.GetString("dystopia-industry-ui-slogan");
        MinSize = new Vector2(560, 560);
        SetSize = new Vector2(620, 660);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        _top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        root.AddChild(_top);

        var listScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinHeight = 200 };
        _lists = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        listScroll.AddChild(_lists);
        root.AddChild(listScroll);

        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-conveyor-ui-search")));
        _search = CityUi.Field(Loc.GetString("dystopia-conveyor-ui-search-placeholder"));
        _search.HorizontalExpand = true;
        _search.OnTextChanged += _ => RebuildResults();
        root.AddChild(_search);

        var resultScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinHeight = 180 };
        _results = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        resultScroll.AddChild(_results);
        root.AddChild(resultScroll);

        BuildIndex();
    }

    private void BuildIndex()
    {
        var seen = new HashSet<string>();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || proto.HideSpawnMenu || !proto.TryGetComponent<ItemComponent>(out _, _factory))
                continue;

            var key = DystopiaConveyorKeys.KeyFor(proto, _factory);
            if (!seen.Add(key))
                continue;

            _index.Add((key, DystopiaConveyorKeys.Name(_proto, key)));
        }

        _index.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    public void UpdateState(DystopiaConveyorUiState state)
    {
        _state = state;
        RebuildTop();
        RebuildLists();
        RebuildResults();
    }

    private bool IsSorter => _state?.Mode == DystopiaConveyorDeviceMode.Sort;

    // ------------------------------------------------------------------ верх: режим и подсказка

    private void RebuildTop()
    {
        _top.RemoveAllChildren();
        if (_state == null)
            return;

        if (IsSorter)
        {
            _top.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-conveyor-ui-sort-hint"), CityUi.Dim));
            return;
        }

        _top.AddChild(CityUi.MakeLabel(Loc.GetString(_state.Mode == DystopiaConveyorDeviceMode.Extract
            ? "dystopia-conveyor-ui-extract-hint"
            : "dystopia-conveyor-ui-insert-hint"), CityUi.Dim));

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var white = CityUi.MakeButton(Loc.GetString("dystopia-conveyor-ui-whitelist"), CityButtonStyle.Tab);
        var black = CityUi.MakeButton(Loc.GetString("dystopia-conveyor-ui-blacklist"), CityButtonStyle.Tab);
        white.HorizontalExpand = black.HorizontalExpand = true;
        white.Active = _state.Whitelist;
        black.Active = !_state.Whitelist;
        white.OnPressed += _ => OnSetWhitelist?.Invoke(true);
        black.OnPressed += _ => OnSetWhitelist?.Invoke(false);
        row.AddChild(white);
        row.AddChild(black);
        _top.AddChild(row);
    }

    // ------------------------------------------------------------------ списки

    private void RebuildLists()
    {
        _lists.RemoveAllChildren();
        if (_state == null)
            return;

        if (IsSorter)
        {
            AddListSection(DystopiaConveyorList.Green, Loc.GetString("dystopia-conveyor-ui-green"), Green, _state.Green);
            AddListSection(DystopiaConveyorList.Blue, Loc.GetString("dystopia-conveyor-ui-blue"), Blue, _state.Blue);
            _lists.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-conveyor-ui-yellow")));
            _lists.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-conveyor-ui-yellow-hint"), Yellow));
            return;
        }

        AddListSection(DystopiaConveyorList.Filter, Loc.GetString(_state.Whitelist
            ? "dystopia-conveyor-ui-list-white"
            : "dystopia-conveyor-ui-list-black"), CityUi.Accent, _state.Filter);
    }

    private void AddListSection(DystopiaConveyorList list, string title, Color color, List<string> keys)
    {
        _lists.AddChild(CityUi.SectionHeader(title));

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var held = CityUi.MakeButton(Loc.GetString("dystopia-conveyor-ui-add-held"));
        held.HorizontalExpand = true;
        held.OnPressed += _ => OnAddHeld?.Invoke(list);
        buttons.AddChild(held);
        if (keys.Count > 0)
        {
            var clear = CityUi.MakeButton(Loc.GetString("dystopia-conveyor-ui-clear"), CityButtonStyle.Danger);
            clear.OnPressed += _ => OnClear?.Invoke(list);
            buttons.AddChild(clear);
        }

        _lists.AddChild(buttons);

        if (keys.Count == 0)
        {
            _lists.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-conveyor-ui-empty"), CityUi.Muted));
            return;
        }

        foreach (var key in keys)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var name = CityUi.MakeLabel(DystopiaConveyorKeys.Name(_proto, key), color);
            name.HorizontalExpand = true;
            name.ClipText = true;
            row.AddChild(name);
            var remove = CityUi.MakeButton(Loc.GetString("dystopia-conveyor-ui-remove"));
            remove.OnPressed += _ => OnRemove?.Invoke(list, key);
            row.AddChild(remove);
            _lists.AddChild(row);
        }
    }

    // ------------------------------------------------------------------ поиск

    private void RebuildResults()
    {
        _results.RemoveAllChildren();
        if (_state == null)
            return;

        var query = _search.Text.Trim();
        if (query.Length < 2)
        {
            _results.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-conveyor-ui-search-hint"), CityUi.Muted));
            return;
        }

        var found = _index
            .Where(e => e.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                        e.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(MaxResults)
            .ToList();

        if (found.Count == 0)
        {
            _results.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-conveyor-ui-nothing"), CityUi.Muted));
            return;
        }

        foreach (var (key, name) in found)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var label = CityUi.MakeLabel(name);
            label.HorizontalExpand = true;
            label.ClipText = true;
            row.AddChild(label);

            if (IsSorter)
            {
                AddResultButton(row, DystopiaConveyorList.Green, key, "dystopia-conveyor-ui-to-green", _state.Green);
                AddResultButton(row, DystopiaConveyorList.Blue, key, "dystopia-conveyor-ui-to-blue", _state.Blue);
            }
            else
            {
                AddResultButton(row, DystopiaConveyorList.Filter, key, "dystopia-conveyor-ui-add", _state.Filter);
            }

            _results.AddChild(row);
        }
    }

    private void AddResultButton(BoxContainer row, DystopiaConveyorList list, string key, string text, List<string> current)
    {
        var button = CityUi.MakeButton(Loc.GetString(text), CityButtonStyle.Primary);
        button.Disabled = current.Contains(key);
        button.OnPressed += _ => OnAdd?.Invoke(list, key);
        row.AddChild(button);
    }
}
