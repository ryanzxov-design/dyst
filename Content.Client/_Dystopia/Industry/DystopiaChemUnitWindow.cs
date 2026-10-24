// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню химической установки (стиль Города). Слева — что производить: все реакции, которые установка умеет
// вести, с поиском и фильтром «можно сейчас». Справа — выбранная реакция (формула, на сколько хватит), контейнер
// (ёмкость в слоте — оттуда в буфер), сколько переливать за раз, буфер (вещества не смешиваются) и бак продукции.
// Переливание: контейнер ↔ буфер ↔ бак продукции, из буфера и бака — в контейнер, «Вылить».

using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaChemUnitWindow : CityWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action? OnToggle;
    public event Action<string?>? OnSelect;
    public event Action<DystopiaChemUnitTank, DystopiaChemUnitTank, string, float>? OnTransfer;
    public event Action? OnEject;

    /// <summary>Сколько переливать за раз (0 — всё), как в химмастере.</summary>
    private static readonly float[] Amounts = { 1, 5, 10, 15, 20, 30, 40, 60, 120, 0 };

    private static readonly Color Ready = Color.FromHex("#78E070");
    private const int MaxShown = 120;

    private readonly Label _status;
    private readonly CityButton _toggle;
    private readonly Label _speed;
    private readonly LineEdit _search;
    private readonly CityButton _all;
    private readonly CityButton _possible;
    private readonly BoxContainer _recipes;
    private readonly BoxContainer _current;
    private readonly BoxContainer _container;
    private readonly BoxContainer _buffer;
    private readonly BoxContainer _output;
    private readonly List<(float Amount, CityButton Button)> _amountButtons = new();
    private float _amount;

    /// <summary>Реакции установки: прототип, название (по продуктам), формула.</summary>
    private readonly List<(ReactionPrototype Proto, string Name, string Formula)> _reactions = new();

    private DystopiaChemUnitUiState? _state;
    private bool _onlyPossible;
    private string _recipesKey = string.Empty;
    private string _tanksKey = string.Empty;

    public DystopiaChemUnitWindow()
    {
        IoCManager.InjectDependencies(this);

        WindowTitle = Loc.GetString("dystopia-chem-unit-ui-title");
        Subtitle = Loc.GetString("dystopia-industry-ui-sub");
        Slogan = Loc.GetString("dystopia-industry-ui-slogan");
        MinSize = new Vector2(760, 560);
        SetSize = new Vector2(860, 660);

        foreach (var reaction in _proto.EnumeratePrototypes<ReactionPrototype>())
        {
            if (DystopiaChemUnit.Allowed(reaction))
                _reactions.Add((reaction, Products(reaction), Formula(reaction)));
        }

        _reactions.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        // Питание
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _status = CityUi.MakeLabel(string.Empty, CityUi.Accent, CityUi.Bold(13));
        _status.HorizontalExpand = true;
        _toggle = CityUi.MakeButton(Loc.GetString("dystopia-chem-unit-ui-turn-on"), CityButtonStyle.Primary);
        _toggle.OnPressed += _ => OnToggle?.Invoke();
        top.AddChild(_status);
        top.AddChild(_toggle);
        root.AddChild(top);
        _speed = CityUi.MakeLabel(string.Empty, CityUi.Dim);
        root.AddChild(_speed);

        var columns = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12,
        };
        root.AddChild(columns);

        // Слева — рецепты
        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
        };
        left.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-chem-unit-ui-recipes")));
        var tabs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _all = CityUi.MakeButton(Loc.GetString("dystopia-chem-unit-ui-all"), CityButtonStyle.Tab);
        _possible = CityUi.MakeButton(Loc.GetString("dystopia-chem-unit-ui-possible"), CityButtonStyle.Tab);
        _all.HorizontalExpand = _possible.HorizontalExpand = true;
        _all.Active = true;
        _all.OnPressed += _ => SetOnlyPossible(false);
        _possible.OnPressed += _ => SetOnlyPossible(true);
        tabs.AddChild(_all);
        tabs.AddChild(_possible);
        left.AddChild(tabs);
        _search = CityUi.Field(Loc.GetString("dystopia-chem-unit-ui-search"));
        _search.HorizontalExpand = true;
        _search.OnTextChanged += _ => RebuildRecipes(force: true);
        left.AddChild(_search);
        var recipeScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _recipes = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        recipeScroll.AddChild(_recipes);
        left.AddChild(recipeScroll);
        columns.AddChild(left);

        // Справа — выбранное, буфер, продукция
        var rightScroll = new ScrollContainer { HScrollEnabled = false, HorizontalExpand = true, VerticalExpand = true };
        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        rightScroll.AddChild(right);
        columns.AddChild(rightScroll);

        right.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-chem-unit-ui-current")));
        _current = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        right.AddChild(_current);
        _container = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        right.AddChild(_container);

        // Сколько переливать за раз
        right.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-chem-unit-ui-amount-header")));
        var amounts = new GridContainer { Columns = 5, HorizontalExpand = true };
        foreach (var amount in Amounts)
        {
            var button = CityUi.MakeButton(amount > 0f ? Fmt(amount) : Loc.GetString("dystopia-chem-unit-ui-all-amount"),
                CityButtonStyle.Tab);
            button.HorizontalExpand = true;
            var value = amount;
            button.OnPressed += _ => SetAmount(value);
            _amountButtons.Add((amount, button));
            amounts.AddChild(button);
        }

        right.AddChild(amounts);
        SetAmount(0f);

        _buffer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        right.AddChild(_buffer);
        _output = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        right.AddChild(_output);
    }

    // ------------------------------------------------------------------ подписи

    private string ReagentName(string id)
    {
        return _proto.TryIndex<ReagentPrototype>(id, out var proto) ? proto.LocalizedName : id;
    }

    private Color ReagentColor(string id)
    {
        return _proto.TryIndex<ReagentPrototype>(id, out var proto) ? proto.SubstanceColor : CityUi.Text;
    }

    private string Products(ReactionPrototype reaction)
    {
        return string.Join(", ", reaction.Products.Keys.Select(p => ReagentName(p.Id)));
    }

    /// <summary>«2 этанол + 1 сера → 3 …»; катализаторы — отдельно в скобках.</summary>
    private string Formula(ReactionPrototype reaction)
    {
        var inputs = reaction.Reactants.Where(r => !r.Value.Catalyst)
            .Select(r => $"{Fmt(r.Value.Amount.Float())} {ReagentName(r.Key.Id)}");
        var outputs = reaction.Products.Select(p => $"{Fmt(p.Value.Float())} {ReagentName(p.Key.Id)}");
        var text = $"{string.Join(" + ", inputs)} → {string.Join(" + ", outputs)}";

        var catalysts = reaction.Reactants.Where(r => r.Value.Catalyst).Select(r => ReagentName(r.Key.Id)).ToList();
        if (catalysts.Count > 0)
            text += " " + Loc.GetString("dystopia-chem-unit-ui-catalyst", ("list", string.Join(", ", catalysts)));

        return text;
    }

    private float Portions(ReactionPrototype reaction)
    {
        if (_state == null)
            return 0f;

        var buffer = _state.Buffer.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        return DystopiaChemUnit.Portions(reaction,
            id => FixedPoint2.New(buffer.TryGetValue(id, out var amount) ? amount : 0f));
    }

    private static string Fmt(float value)
    {
        return Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------ состояние

    public void UpdateState(DystopiaChemUnitUiState state)
    {
        _state = state;

        _status.Text = Loc.GetString(!state.On ? "dystopia-chem-unit-ui-status-off"
            : !state.Powered ? "dystopia-chem-unit-ui-status-unpowered"
            : state.Working ? "dystopia-chem-unit-ui-status-working"
            : "dystopia-chem-unit-ui-status-idle");
        _status.FontColorOverride = !state.On ? CityUi.Dim : !state.Powered ? CityUi.Danger : CityUi.Accent;
        _toggle.Text = Loc.GetString(state.On ? "dystopia-chem-unit-ui-turn-off" : "dystopia-chem-unit-ui-turn-on")
            .ToUpperInvariant();
        _speed.Text = Loc.GetString("dystopia-chem-unit-ui-speed",
            ("units", Fmt(state.UnitsPerCycle)), ("seconds", Fmt(state.CycleTime)));

        var key = $"{state.Reaction}|{state.Container}|" +
                  string.Join(",", state.ContainerReagents.Select(r => $"{r.Id}={Fmt(r.Amount)}")) + "|" +
                  string.Join(",", state.Buffer.Select(r => $"{r.Id}={Fmt(r.Amount)}")) + "|" +
                  string.Join(",", state.Output.Select(r => $"{r.Id}={Fmt(r.Amount)}"));
        if (key != _tanksKey)
        {
            _tanksKey = key;
            RebuildCurrent();
            RebuildTanks();
        }

        RebuildRecipes(force: false);
    }

    private void SetAmount(float amount)
    {
        _amount = amount;
        foreach (var (value, button) in _amountButtons)
            button.Active = value == amount;
    }

    private void SetOnlyPossible(bool value)
    {
        _onlyPossible = value;
        _all.Active = !value;
        _possible.Active = value;
        RebuildRecipes(force: true);
    }

    // ------------------------------------------------------------------ рецепты

    private void RebuildRecipes(bool force)
    {
        if (_state == null)
            return;

        var query = _search.Text.Trim();
        var possible = _reactions.Where(r => Portions(r.Proto) > 0f).Select(r => r.Proto.ID).ToHashSet();
        var key = $"{query}|{_onlyPossible}|{_state.Reaction}|{string.Join(",", possible.OrderBy(x => x))}";
        if (!force && key == _recipesKey)
            return;

        _recipesKey = key;
        _recipes.RemoveAllChildren();

        var shown = _reactions
            .Where(r => !_onlyPossible || possible.Contains(r.Proto.ID))
            .Where(r => query.Length == 0 ||
                        r.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                        r.Formula.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(r => possible.Contains(r.Proto.ID) ? 0 : 1)
            .ToList();

        if (shown.Count == 0)
        {
            _recipes.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-chem-unit-ui-nothing-found"), CityUi.Muted));
            return;
        }

        // Карточек сотни — показываем первые MaxShown, остальное находится поиском.
        if (shown.Count > MaxShown)
        {
            _recipes.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-chem-unit-ui-too-many",
                ("shown", MaxShown), ("total", shown.Count)), CityUi.Dim));
            shown = shown.Take(MaxShown).ToList();
        }

        foreach (var (proto, name, formula) in shown)
        {
            var card = new CityCard();
            var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
            var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var title = CityUi.MakeLabel((possible.Contains(proto.ID) ? "● " : string.Empty) + name,
                possible.Contains(proto.ID) ? Ready : CityUi.Text, CityUi.Bold(12));
            title.HorizontalExpand = true;
            title.ClipText = true;
            head.AddChild(title);
            var select = CityUi.MakeButton(Loc.GetString(proto.ID == _state.Reaction
                ? "dystopia-chem-unit-ui-selected"
                : "dystopia-chem-unit-ui-select"), CityButtonStyle.Primary);
            select.Disabled = proto.ID == _state.Reaction;
            var id = proto.ID;
            select.OnPressed += _ => OnSelect?.Invoke(id);
            head.AddChild(select);
            box.AddChild(head);
            var formulaLabel = CityUi.MakeLabel(formula, CityUi.Dim);
            formulaLabel.ClipText = true;
            box.AddChild(formulaLabel);
            card.AddChild(box);
            _recipes.AddChild(card);
        }
    }

    // ------------------------------------------------------------------ выбранное, буфер, продукция

    private void RebuildCurrent()
    {
        _current.RemoveAllChildren();
        if (_state?.Reaction == null || !_proto.TryIndex<ReactionPrototype>(_state.Reaction, out var reaction))
        {
            _current.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-chem-unit-ui-none"), CityUi.Muted));
            return;
        }

        _current.AddChild(CityUi.MakeLabel(Products(reaction), CityUi.Glow, CityUi.Bold(13)));
        var formula = new RichTextLabel { HorizontalExpand = true };
        formula.SetMessage(Formula(reaction), CityUi.Text);
        _current.AddChild(formula);

        var portions = Portions(reaction);
        var output = reaction.Products.Values.Sum(v => v.Float()) * portions;
        _current.AddChild(CityUi.MakeLabel(portions > 0f
            ? Loc.GetString("dystopia-chem-unit-ui-enough", ("amount", Fmt(output)))
            : Loc.GetString("dystopia-chem-unit-ui-missing"), portions > 0f ? Ready : CityUi.Danger));

        // Чего не хватает до одной порции.
        if (portions <= 0f)
        {
            foreach (var (id, info) in reaction.Reactants)
            {
                var have = _state.Buffer.Where(r => r.Id == id.Id).Sum(r => r.Amount);
                if (have < info.Amount.Float())
                {
                    _current.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-chem-unit-ui-need",
                        ("reagent", ReagentName(id.Id)), ("need", Fmt(info.Amount.Float())), ("have", Fmt(have))),
                        CityUi.Dim));
                }
            }
        }

        var clear = CityUi.MakeButton(Loc.GetString("dystopia-chem-unit-ui-clear"), CityButtonStyle.Danger);
        clear.OnPressed += _ => OnSelect?.Invoke(null);
        _current.AddChild(clear);
    }

    private void RebuildTanks()
    {
        if (_state == null)
            return;

        // Контейнер
        _container.RemoveAllChildren();
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var title = CityUi.SectionHeader(_state.Container == null
            ? Loc.GetString("dystopia-chem-unit-ui-container")
            : Loc.GetString("dystopia-chem-unit-ui-container-full", ("name", _state.Container),
                ("volume", Fmt(_state.ContainerReagents.Sum(r => r.Amount))), ("max", Fmt(_state.ContainerMax))));
        title.HorizontalExpand = true;
        head.AddChild(title);
        if (_state.Container != null)
        {
            var eject = CityUi.MakeButton(Loc.GetString("dystopia-chem-unit-ui-eject"));
            eject.OnPressed += _ => OnEject?.Invoke();
            head.AddChild(eject);
        }

        _container.AddChild(head);
        if (_state.Container == null)
            _container.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-chem-unit-ui-container-hint"), CityUi.Muted));
        else
            FillRows(_container, _state.ContainerReagents, DystopiaChemUnitTank.Container);

        // Буфер и бак продукции
        _buffer.RemoveAllChildren();
        _buffer.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-chem-unit-ui-buffer",
            ("volume", Fmt(_state.Buffer.Sum(r => r.Amount))), ("max", Fmt(_state.BufferMax)))));
        FillRows(_buffer, _state.Buffer, DystopiaChemUnitTank.Buffer);

        _output.RemoveAllChildren();
        _output.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-chem-unit-ui-output",
            ("volume", Fmt(_state.Output.Sum(r => r.Amount))), ("max", Fmt(_state.OutputMax)))));
        FillRows(_output, _state.Output, DystopiaChemUnitTank.Output);
    }

    /// <summary>Строки веществ с кнопками «куда перелить»: из контейнера — в буфер; из буфера — в контейнер и в
    /// выход; из бака продукции — в контейнер и обратно в буфер; везде, кроме контейнера, — «Вылить».</summary>
    private void FillRows(BoxContainer box, List<DystopiaChemUnitReagent> reagents, DystopiaChemUnitTank from)
    {
        if (reagents.Count == 0)
        {
            box.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-chem-unit-ui-empty"), CityUi.Muted));
            return;
        }

        var targets = from switch
        {
            DystopiaChemUnitTank.Container => new[] { (DystopiaChemUnitTank.Buffer, "dystopia-chem-unit-ui-to-buffer") },
            DystopiaChemUnitTank.Buffer => new[]
            {
                (DystopiaChemUnitTank.Container, "dystopia-chem-unit-ui-to-container"),
                (DystopiaChemUnitTank.Output, "dystopia-chem-unit-ui-to-output"),
                (DystopiaChemUnitTank.Discard, "dystopia-chem-unit-ui-discard"),
            },
            _ => new[]
            {
                (DystopiaChemUnitTank.Container, "dystopia-chem-unit-ui-to-container"),
                (DystopiaChemUnitTank.Buffer, "dystopia-chem-unit-ui-to-buffer"),
                (DystopiaChemUnitTank.Discard, "dystopia-chem-unit-ui-discard"),
            },
        };

        foreach (var group in reagents.GroupBy(r => r.Id).OrderByDescending(g => g.Sum(r => r.Amount)))
        {
            var id = group.Key;
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            var name = CityUi.MakeLabel("■ " + ReagentName(id), ReagentColor(id));
            name.HorizontalExpand = true;
            name.ClipText = true;
            row.AddChild(name);
            row.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-chem-unit-ui-amount", ("amount", Fmt(group.Sum(r => r.Amount)))),
                CityUi.Text, CityUi.Mono()));
            foreach (var (to, text) in targets)
            {
                var button = CityUi.MakeButton(Loc.GetString(text),
                    to == DystopiaChemUnitTank.Discard ? CityButtonStyle.Danger : CityButtonStyle.Normal);
                button.Disabled = to == DystopiaChemUnitTank.Container && _state?.Container == null;
                var target = to;
                button.OnPressed += _ => OnTransfer?.Invoke(from, target, id, _amount);
                row.AddChild(button);
            }

            box.AddChild(row);
        }
    }
}
