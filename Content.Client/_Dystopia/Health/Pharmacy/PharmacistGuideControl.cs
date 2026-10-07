// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: содержимое «Справочника фармацевта»: слева список препаратов с поиском, в центре древо синтеза
// (сырьё слева, готовые препараты справа), справа карточка выбранного вещества — сырьё на одну реакцию и
// порядок синтеза с переключением рецептов.

using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Health.Pharmacy;

public sealed class PharmacistGuideControl : BoxContainer
{
    private static readonly Color Warning = Color.FromHex("#E0B060");

    private readonly PharmacistGuideData _data;

    /// <summary>
    /// Выбранный рецепт для веществ с несколькими рецептами (индекс в <see cref="GuideReagent.Recipes"/>).
    /// </summary>
    private readonly Dictionary<string, int> _choices = new();

    private string? _selected;
    private string _search = string.Empty;
    private bool _onlyChain;

    // слева
    private readonly LineEdit _searchEdit;
    private readonly Label _listCaption;
    private readonly BoxContainer _list;

    // центр
    private readonly Label _stats;
    private readonly CityButton _onlyButton;
    private readonly ScrollContainer _treeScroll;
    private readonly PharmacistTreeCanvas _canvas;
    private readonly Dictionary<string, PharmacistTreeNode> _nodes = new();

    // справа
    private readonly BoxContainer _details;

    // текущая раскладка
    private Dictionary<string, int> _depth = new();
    private Dictionary<string, Vector2> _positions = new();
    private bool _scrollPending;
    private Vector2? _scrollTarget;
    private Vector2 _canvasTarget;
    private int _scrollWait;

    // масштаб полотна (колесо мыши) и размер полотна в масштабе 1
    private float _zoom = 1f;
    private Vector2 _baseSize;

    public PharmacistGuideControl(PharmacistGuideData data)
    {
        _data = data;
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 10;

        // ===== Слева: препараты =====
        var left = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 250,
            MaxWidth = 250,
            SeparationOverride = 6,
        };
        AddChild(left);

        left.AddChild(CityUi.SectionHeader(Loc.GetString("pharm-guide-section-list")));
        left.AddChild(CityUi.MakeLabel(Loc.GetString("pharm-guide-search-label").ToUpperInvariant(), CityUi.Dim, CityUi.Regular(10)));
        _searchEdit = CityUi.Field(Loc.GetString("pharm-guide-search-placeholder"));
        _searchEdit.OnTextChanged += args =>
        {
            _search = args.Text.Trim();
            RebuildList();
            UpdateLooks();
        };
        left.AddChild(_searchEdit);

        _listCaption = CityUi.MakeLabel(string.Empty, CityUi.Muted, CityUi.Mono(10));
        left.AddChild(_listCaption);

        var listPanel = new PanelContainer
        {
            PanelOverride = CityUi.Box(CityUi.Panel, CityUi.Line),
            VerticalExpand = true,
        };
        var listScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _list = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 1 };
        listScroll.AddChild(_list);
        listPanel.AddChild(listScroll);
        left.AddChild(listPanel);

        // ===== Центр: древо =====
        var center = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        AddChild(center);

        var toolbar = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(0, 6, 0, 2),
        };
        toolbar.AddChild(CityUi.MakeLabel(Loc.GetString("pharm-guide-section-tree").ToUpperInvariant(), CityUi.Accent, CityUi.Bold(12)));
        toolbar.AddChild(CityUi.Separator());
        center.AddChild(toolbar);

        var controls = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _stats = CityUi.MakeLabel(string.Empty, CityUi.Dim, CityUi.Mono(10));
        _stats.HorizontalExpand = true;
        _stats.VerticalAlignment = VAlignment.Center;
        controls.AddChild(_stats);
        _onlyButton = CityUi.MakeButton(Loc.GetString("pharm-guide-only-chain-off"));
        _onlyButton.OnPressed += _ =>
        {
            _onlyChain = !_onlyChain;
            Rebuild();
        };
        controls.AddChild(_onlyButton);
        var reset = CityUi.MakeButton(Loc.GetString("pharm-guide-reset"));
        reset.OnPressed += _ =>
        {
            _selected = null;
            _onlyChain = false;
            _choices.Clear();
            _searchEdit.SetText(string.Empty, false);
            _search = string.Empty;
            Rebuild();
        };
        controls.AddChild(reset);
        center.AddChild(controls);

        var treePanel = new PanelContainer
        {
            PanelOverride = CityUi.Box(CityUi.Bg, CityUi.Line),
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _treeScroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        _canvas = new PharmacistTreeCanvas();
        _canvas.Dragged += delta => _treeScroll.SetScrollValue(_treeScroll.GetScrollValue() - delta);
        _canvas.Zoomed += OnZoom;
        _treeScroll.AddChild(_canvas);
        treePanel.AddChild(_treeScroll);
        center.AddChild(treePanel);

        // ===== Справа: карточка =====
        var card = new CityCard
        {
            MinWidth = 320,
            MaxWidth = 320,
        };
        AddChild(card);
        var detailsScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _details = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(2, 4, 6, 4),
        };
        detailsScroll.AddChild(_details);
        card.AddChild(detailsScroll);

        Rebuild();
    }

    #region Граф

    private GuideRecipe? RecipeOf(string id)
    {
        if (!_data.Reagents.TryGetValue(id, out var reagent) || !reagent.Craftable)
            return null;

        var index = _choices.GetValueOrDefault(id);
        return reagent.Recipes[Math.Clamp(index, 0, reagent.Recipes.Count - 1)];
    }

    private IEnumerable<string> InputsOf(string id)
    {
        var recipe = RecipeOf(id);
        if (recipe == null)
            yield break;

        foreach (var input in recipe.Inputs)
        {
            if (_data.Reagents.ContainsKey(input.Reagent))
                yield return input.Reagent;
        }
    }

    /// <summary>
    /// Всё, что нужно для вещества, включая его самого.
    /// </summary>
    private HashSet<string> ChainOf(string id)
    {
        var result = new HashSet<string>();
        var stack = new Stack<string>();
        stack.Push(id);
        while (stack.TryPop(out var current))
        {
            if (!result.Add(current))
                continue;

            foreach (var input in InputsOf(current))
            {
                stack.Push(input);
            }
        }

        return result;
    }

    private int CraftSteps(HashSet<string> chain)
    {
        return chain.Count(id => RecipeOf(id) != null);
    }

    private int DepthOf(string id, HashSet<string> stack)
    {
        if (_depth.TryGetValue(id, out var known))
            return known;

        stack.Add(id);
        var depth = 0;
        foreach (var input in InputsOf(id))
        {
            if (stack.Contains(input))
                continue; // цикл через альтернативные рецепты — считаем сырьём

            depth = Math.Max(depth, DepthOf(input, stack) + 1);
        }

        stack.Remove(id);
        _depth[id] = depth;
        return depth;
    }

    /// <summary>
    /// Раскладка как у древа исследований: колонка — глубина рецепта, порядок в колонке — по среднему
    /// положению связанных узлов, чтобы линии меньше пересекались.
    /// </summary>
    private Dictionary<string, Vector2> Layout(ICollection<string> ids, out Vector2 size)
    {
        var columns = new SortedDictionary<int, List<string>>();
        foreach (var id in ids)
        {
            var depth = _depth[id];
            if (!columns.TryGetValue(depth, out var column))
                columns[depth] = column = new List<string>();
            column.Add(id);
        }

        var order = new Dictionary<string, int>();
        void Apply(List<string> column)
        {
            for (var i = 0; i < column.Count; i++)
            {
                order[column[i]] = i;
            }
        }

        foreach (var column in columns.Values)
        {
            column.Sort((a, b) =>
            {
                var da = _data.Reagents[a].Dispenser ? 0 : 1;
                var db = _data.Reagents[b].Dispenser ? 0 : 1;
                return da != db ? da.CompareTo(db) : string.Compare(_data.Reagents[a].Name, _data.Reagents[b].Name, StringComparison.CurrentCulture);
            });
            Apply(column);
        }

        var keys = columns.Keys.ToList();
        for (var iteration = 0; iteration < 4; iteration++)
        {
            // назад: сырьё ближе к тем, кому оно нужно
            var children = new Dictionary<string, List<int>>();
            foreach (var id in ids)
            {
                foreach (var input in InputsOf(id))
                {
                    if (!order.ContainsKey(input))
                        continue;
                    if (!children.TryGetValue(input, out var list))
                        children[input] = list = new List<int>();
                    list.Add(order[id]);
                }
            }

            for (var k = 0; k < keys.Count - 1; k++)
            {
                var column = columns[keys[k]];
                var key = column.ToDictionary(id => id, id => children.TryGetValue(id, out var list) ? (float) list.Average() : order[id]);
                column.Sort((a, b) => key[a].CompareTo(key[b]));
                Apply(column);
            }

            // вперёд: продукт ближе к своим реагентам
            for (var k = 1; k < keys.Count; k++)
            {
                var column = columns[keys[k]];
                var key = column.ToDictionary(id => id, id =>
                {
                    var parents = InputsOf(id).Where(order.ContainsKey).Select(p => order[p]).ToList();
                    return parents.Count > 0 ? (float) parents.Average() : order[id];
                });
                column.Sort((a, b) => key[a].CompareTo(key[b]));
                Apply(column);
            }
        }

        var positions = new Dictionary<string, Vector2>();
        var rows = 0;
        for (var ci = 0; ci < keys.Count; ci++)
        {
            var column = columns[keys[ci]];
            rows = Math.Max(rows, column.Count);
            for (var i = 0; i < column.Count; i++)
            {
                positions[column[i]] = new Vector2(
                    PharmacistTreeCanvas.Padding + ci * (PharmacistTreeCanvas.NodeWidth + PharmacistTreeCanvas.ColumnGap),
                    PharmacistTreeCanvas.Padding + i * (PharmacistTreeCanvas.NodeHeight + PharmacistTreeCanvas.RowGap));
            }
        }

        size = new Vector2(
            PharmacistTreeCanvas.Padding * 2 + Math.Max(1, keys.Count) * (PharmacistTreeCanvas.NodeWidth + PharmacistTreeCanvas.ColumnGap) - PharmacistTreeCanvas.ColumnGap,
            PharmacistTreeCanvas.Padding * 2 + Math.Max(1, rows) * (PharmacistTreeCanvas.NodeHeight + PharmacistTreeCanvas.RowGap) - PharmacistTreeCanvas.RowGap);
        return positions;
    }

    #endregion

    #region Отрисовка

    /// <summary>
    /// Полная перестройка: состав древа зависит от выбранных рецептов и режима «только цепочка».
    /// </summary>
    private void Rebuild()
    {
        if (_selected != null && !_data.Reagents.ContainsKey(_selected))
            _selected = null;

        var all = new HashSet<string>();
        foreach (var medicine in _data.Medicines)
        {
            all.UnionWith(ChainOf(medicine));
        }

        var chain = _selected != null ? ChainOf(_selected) : null;
        if (_selected != null)
            all.UnionWith(chain!);

        var shown = _onlyChain && chain != null ? chain : all;

        _depth = new Dictionary<string, int>();
        var stack = new HashSet<string>();
        foreach (var id in shown)
        {
            DepthOf(id, stack);
        }

        _positions = Layout(shown, out var size);

        _canvas.RemoveAllChildren();
        _nodes.Clear();
        _baseSize = size;
        foreach (var (id, _) in _positions)
        {
            var reagent = _data.Reagents[id];
            var node = new PharmacistTreeNode(id, reagent.Name);
            node.OnPressed += _ => Select(id);
            node.ToolTip = NodeTooltip(reagent);
            _canvas.AddChild(node);
            _nodes[id] = node;
        }

        ApplyZoom();

        _onlyButton.Text = Loc.GetString(_onlyChain ? "pharm-guide-only-chain-on" : "pharm-guide-only-chain-off").ToUpperInvariant();
        _stats.Text = Loc.GetString("pharm-guide-stats",
            ("meds", _data.Medicines.Count),
            ("nodes", all.Count),
            ("crafts", all.Count(id => RecipeOf(id) != null)));

        RebuildList();
        UpdateLooks();
        RebuildDetails();
        _scrollPending = true;
    }

    private string NodeTooltip(GuideReagent reagent)
    {
        var lines = new List<string> { reagent.Name };
        if (reagent.Description.Length > 0)
            lines.Add(reagent.Description);

        var recipe = RecipeOf(reagent.Id);
        if (recipe != null)
        {
            lines.Add(Loc.GetString("pharm-guide-node-tip-recipe", ("inputs", IngredientsText(recipe))));
            if (reagent.Recipes.Count > 1)
                lines.Add(Loc.GetString("pharm-guide-node-tip-alts", ("count", reagent.Recipes.Count)));
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Цвета узлов и линий: выбранная цепочка подсвечена, остальное приглушено, совпадения поиска обведены.
    /// </summary>
    private void UpdateLooks()
    {
        var chain = _selected != null ? ChainOf(_selected) : null;
        var query = _search.ToLowerInvariant();

        foreach (var (id, node) in _nodes)
        {
            var reagent = _data.Reagents[id];
            var inChain = chain != null && chain.Contains(id);
            var hit = query.Length > 0 && reagent.Name.ToLowerInvariant().Contains(query);
            var recipe = RecipeOf(id);

            var fill = CityUi.Panel;
            var border = CityUi.Line;
            var text = CityUi.Text;
            var thickness = 1f;
            var bold = false;

            if (recipe == null)
            {
                fill = CityUi.Bg2;
                text = CityUi.Dim;
                if (!reagent.Dispenser)
                    border = CityUi.Muted;
            }

            if (reagent.Medicine)
            {
                fill = CityUi.Panel2;
                border = CityUi.Frame;
                text = CityUi.Glow;
                bold = true;
            }

            if (inChain)
            {
                fill = CityUi.ActiveFill;
                border = CityUi.Accent;
                text = CityUi.Glow;
            }

            if (id == _selected)
            {
                fill = CityUi.HoverFill;
                thickness = 2;
                bold = true;
            }

            if (hit)
            {
                border = CityUi.Glow;
                thickness = 2;
            }

            var alpha = 1f;
            if (chain != null && !inChain)
                alpha = hit ? 0.7f : 0.25f;
            else if (chain == null && query.Length > 0 && !hit)
                alpha = 0.4f;

            string badge;
            if (recipe == null)
                badge = Loc.GetString(reagent.Dispenser ? "pharm-guide-badge-dispenser" : "pharm-guide-badge-other");
            else if (reagent.Recipes.Count > 1)
                badge = $"{_choices.GetValueOrDefault(id) + 1}/{reagent.Recipes.Count}";
            else
                badge = "×" + Amount(recipe.Output);

            node.SetLook(fill, border, text, thickness, bold, reagent.Dispenser ? CityUi.Accent : Color.Transparent, badge, alpha);
        }

        var edges = new List<TreeEdge>();
        foreach (var (id, position) in _positions)
        {
            foreach (var input in InputsOf(id))
            {
                if (!_positions.TryGetValue(input, out var from))
                    continue;

                var highlighted = chain != null && chain.Contains(id) && chain.Contains(input);
                edges.Add(new TreeEdge(from, position, highlighted, chain != null && !highlighted));
            }
        }

        _canvas.SetEdges(edges);
    }

    private void RebuildList()
    {
        _list.RemoveAllChildren();
        var query = _search.ToLowerInvariant();
        var found = 0;

        foreach (var id in _data.Medicines)
        {
            var reagent = _data.Reagents[id];
            if (query.Length > 0 && !reagent.Name.ToLowerInvariant().Contains(query))
                continue;

            found++;
            var steps = reagent.Craftable
                ? Loc.GetString("pharm-guide-steps-short", ("count", CraftSteps(ChainOf(id))))
                : "—";
            var item = new PharmacistListItem(reagent.Name, steps, id == _selected);
            item.OnPressed += _ => Select(id);
            _list.AddChild(item);
        }

        if (found == 0)
        {
            var empty = CityUi.MakeLabel(Loc.GetString("pharm-guide-list-empty"), CityUi.Dim, CityUi.Regular(11));
            empty.Margin = new Thickness(10, 12);
            _list.AddChild(empty);
        }

        _listCaption.Text = query.Length > 0
            ? Loc.GetString("pharm-guide-list-found", ("found", found), ("count", _data.Medicines.Count))
            : Loc.GetString("pharm-guide-list-total", ("count", _data.Medicines.Count));
    }

    private void Select(string id)
    {
        _selected = _selected == id ? null : id;

        // в режиме «только цепочка» состав древа зависит от выбора
        if (_onlyChain)
        {
            Rebuild();
            return;
        }

        RebuildList();
        UpdateLooks();
        RebuildDetails();
        _scrollPending = true;
    }

    private void SwitchRecipe(string id, int delta)
    {
        if (!_data.Reagents.TryGetValue(id, out var reagent) || reagent.Recipes.Count < 2)
            return;

        var count = reagent.Recipes.Count;
        _choices[id] = ((_choices.GetValueOrDefault(id) + delta) % count + count) % count;
        Rebuild();
    }

    /// <summary>
    /// Размеры и положения узлов под текущий масштаб.
    /// </summary>
    private void ApplyZoom()
    {
        var size = _baseSize * _zoom;
        _canvas.Zoom = _zoom;
        _canvas.MinSize = size;
        _canvas.SetSize = size;
        _canvasTarget = size;
        _scrollWait = 0;

        foreach (var (id, node) in _nodes)
        {
            node.SetZoom(_zoom);
            LayoutContainer.SetPosition(node, _positions[id] * _zoom);
        }
    }

    /// <summary>
    /// Колесо мыши: масштаб вокруг точки под курсором.
    /// </summary>
    private void OnZoom(int step, Vector2 cursor)
    {
        var zoom = Math.Clamp(_zoom * (step > 0 ? 1.15f : 1f / 1.15f), PharmacistTreeCanvas.MinZoom, PharmacistTreeCanvas.MaxZoom);
        if (MathF.Abs(zoom - _zoom) < 0.001f)
            return;

        var scroll = _treeScroll.GetScrollValue();
        var inView = cursor - scroll;
        var factor = zoom / _zoom;
        _zoom = zoom;
        ApplyZoom();

        // точка под курсором остаётся на месте
        _scrollTarget = cursor * factor - inView;
        _scrollPending = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_scrollPending && _scrollTarget == null)
            return;

        // FrameUpdate идёт до раскладки: ждём, пока полотно примет новый размер (не дольше нескольких кадров),
        // иначе прокрутка упрётся в границы старого размера
        if (Vector2.Distance(_canvas.Size, _canvasTarget) > 1f && _scrollWait++ < 10)
            return;

        if (_scrollTarget is { } target)
        {
            _scrollTarget = null;
            _treeScroll.SetScrollValue(Vector2.Max(Vector2.Zero, target));
            return;
        }

        // прокрутка к выбранному узлу
        _scrollPending = false;
        if (_selected == null || !_positions.TryGetValue(_selected, out var position))
            return;

        var view = _treeScroll.Size;
        var center = (position + new Vector2(PharmacistTreeCanvas.NodeWidth, PharmacistTreeCanvas.NodeHeight) / 2) * _zoom;
        _treeScroll.SetScrollValue(Vector2.Max(Vector2.Zero, center - view / 2));
    }

    #endregion

    #region Карточка

    private void RebuildDetails()
    {
        _details.RemoveAllChildren();

        if (_selected == null || !_data.Reagents.TryGetValue(_selected, out var reagent))
        {
            _details.AddChild(CityUi.MakeLabel(Loc.GetString("pharm-guide-card-title").ToUpperInvariant(), CityUi.Accent, CityUi.Bold(12)));
            _details.AddChild(Text(Loc.GetString("pharm-guide-card-hint"), CityUi.Text));
            return;
        }

        var recipe = RecipeOf(reagent.Id);
        var kind = reagent.Medicine ? "pharm-guide-kind-medicine"
            : recipe != null ? "pharm-guide-kind-craft"
            : reagent.Dispenser ? "pharm-guide-kind-dispenser"
            : "pharm-guide-kind-other";

        _details.AddChild(CityUi.MakeLabel(Loc.GetString(kind).ToUpperInvariant(), CityUi.Dim, CityUi.Regular(10)));
        var title = CityUi.MakeLabel(reagent.Name, CityUi.Glow, CityUi.Title(18));
        _details.AddChild(title);
        if (reagent.Description.Length > 0)
            _details.AddChild(Text(reagent.Description, CityUi.Text));

        if (recipe == null)
        {
            var source = reagent.Dispenser
                ? Loc.GetString("pharm-guide-no-recipe-dispenser")
                : Loc.GetString("pharm-guide-no-recipe-other", ("source", reagent.SourceHint ?? Loc.GetString("pharm-guide-source-unknown")));
            _details.AddChild(Text(source, CityUi.Dim));
            return;
        }

        var chain = ChainOf(reagent.Id);
        var outLabel = CityUi.MakeLabel(
            Loc.GetString("pharm-guide-out", ("amount", Amount(recipe.Output)), ("steps", CraftSteps(chain))).ToUpperInvariant(),
            CityUi.Accent,
            CityUi.Mono(10));
        _details.AddChild(outLabel);

        // --- сырьё на одну реакцию ---
        var need = new Dictionary<string, float>();
        var catalysts = new Dictionary<string, float>();
        Totals(reagent.Id, recipe.Output, need, catalysts, new HashSet<string>());

        _details.AddChild(CityUi.SectionHeader(Loc.GetString("pharm-guide-section-raw")));
        foreach (var (id, amount) in need
                     .OrderBy(p => _data.Reagents.TryGetValue(p.Key, out var r) && r.Dispenser ? 0 : 1)
                     .ThenBy(p => NameOf(p.Key), StringComparer.CurrentCulture))
        {
            var isDispenser = _data.Reagents.TryGetValue(id, out var raw) && raw.Dispenser;
            var source = isDispenser
                ? Loc.GetString("pharm-guide-raw-dispenser")
                : raw?.SourceHint ?? Loc.GetString("pharm-guide-raw-other");
            _details.AddChild(RawRow(NameOf(id), source.ToUpperInvariant(), Amount(amount), isDispenser ? CityUi.Accent : CityUi.Muted));
        }

        foreach (var (id, amount) in catalysts)
        {
            _details.AddChild(RawRow(NameOf(id), Loc.GetString("pharm-guide-raw-catalyst"), Amount(amount), CityUi.Frame));
        }

        // --- порядок синтеза: от простого к выбранному ---
        _details.AddChild(CityUi.SectionHeader(Loc.GetString("pharm-guide-section-steps")));
        var crafts = chain
            .Where(id => RecipeOf(id) != null)
            .OrderBy(id => _depth.GetValueOrDefault(id))
            .ThenBy(NameOf, StringComparer.CurrentCulture)
            .ToList();

        for (var i = 0; i < crafts.Count; i++)
        {
            _details.AddChild(StepCard(i + 1, crafts[i], crafts[i] == reagent.Id));
        }
    }

    /// <summary>
    /// Разворачивает рецепт до сырья: сколько чего уйдёт на <paramref name="amount"/> единиц вещества.
    /// Катализаторы не расходуются — их нужно столько, сколько требует самая жадная реакция.
    /// </summary>
    private void Totals(string id, float amount, Dictionary<string, float> need, Dictionary<string, float> catalysts, HashSet<string> stack)
    {
        var recipe = RecipeOf(id);
        if (recipe == null || stack.Contains(id) || recipe.Output <= 0)
        {
            need[id] = need.GetValueOrDefault(id) + amount;
            return;
        }

        stack.Add(id);
        var batches = amount / recipe.Output;
        foreach (var input in recipe.Inputs)
        {
            if (input.Catalyst)
                catalysts[input.Reagent] = MathF.Max(catalysts.GetValueOrDefault(input.Reagent), input.Amount);
            else
                Totals(input.Reagent, input.Amount * batches, need, catalysts, stack);
        }

        stack.Remove(id);
    }

    private Control StepCard(int number, string id, bool target)
    {
        var reagent = _data.Reagents[id];
        var recipe = RecipeOf(id)!;

        var panel = new PanelContainer
        {
            PanelOverride = CityUi.Box(target ? CityUi.ActiveFill : CityUi.Bg2, target ? CityUi.Accent : CityUi.Line, 1, 8, 6),
        };
        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 10 };
        panel.AddChild(row);

        var numberLabel = CityUi.MakeLabel(number.ToString("00"), CityUi.Accent, CityUi.Mono(11));
        numberLabel.MinWidth = 18;
        numberLabel.VerticalAlignment = VAlignment.Top;
        row.AddChild(numberLabel);

        var body = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2, HorizontalExpand = true };
        row.AddChild(body);

        var head = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var name = CityUi.MakeLabel(reagent.Name, CityUi.Glow, CityUi.Bold(12));
        name.ClipText = true;
        name.HorizontalExpand = true;
        head.AddChild(name);
        head.AddChild(CityUi.MakeLabel(Loc.GetString("pharm-guide-step-out", ("amount", Amount(recipe.Output))), CityUi.Dim, CityUi.Mono(10)));
        body.AddChild(head);

        body.AddChild(Text(IngredientsText(recipe), CityUi.Text));

        var conditions = new List<string>(recipe.Conditions);
        foreach (var input in recipe.Inputs)
        {
            if (input.Catalyst)
                conditions.Add(Loc.GetString("pharm-guide-catalyst-mark", ("name", NameOf(input.Reagent))));
        }

        if (conditions.Count > 0)
            body.AddChild(Text(string.Join("  ·  ", conditions), Warning));

        // переключение рецептов
        if (reagent.Recipes.Count > 1)
        {
            var switcher = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6, Margin = new Thickness(0, 3, 0, 0) };
            var prev = CityUi.MakeButton("◀");
            prev.MinWidth = 26;
            prev.OnPressed += _ => SwitchRecipe(id, -1);
            var next = CityUi.MakeButton("▶");
            next.MinWidth = 26;
            next.OnPressed += _ => SwitchRecipe(id, 1);
            var label = CityUi.MakeLabel(
                Loc.GetString("pharm-guide-recipe-switch", ("index", _choices.GetValueOrDefault(id) + 1), ("count", reagent.Recipes.Count)),
                CityUi.Accent,
                CityUi.Mono(10));
            label.VerticalAlignment = VAlignment.Center;
            switcher.AddChild(prev);
            switcher.AddChild(label);
            switcher.AddChild(next);
            body.AddChild(switcher);
        }

        return panel;
    }

    #endregion

    #region Помощники

    private string NameOf(string id)
    {
        return _data.Reagents.TryGetValue(id, out var reagent) ? reagent.Name : id;
    }

    private string IngredientsText(GuideRecipe recipe)
    {
        return string.Join("  +  ", recipe.Inputs.Select(i => $"{NameOf(i.Reagent)} {Amount(i.Amount)}"));
    }

    private static string Amount(float value)
    {
        return MathF.Round(value, 1).ToString("0.#", CultureInfo.CurrentCulture);
    }

    private static RichTextLabel Text(string text, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();

        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);
        return label;
    }

    private static Control RawRow(string name, string source, string amount, Color mark)
    {
        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = mark },
            SetSize = new Vector2(6, 6),
            VerticalAlignment = VAlignment.Center,
        });
        var nameLabel = CityUi.MakeLabel(name, CityUi.Glow, CityUi.Regular(12));
        nameLabel.HorizontalExpand = true;
        nameLabel.ClipText = true;
        row.AddChild(nameLabel);
        var sourceLabel = CityUi.MakeLabel(source, CityUi.Dim, CityUi.Regular(9));
        sourceLabel.VerticalAlignment = VAlignment.Center;
        row.AddChild(sourceLabel);
        var amountLabel = CityUi.MakeLabel(Loc.GetString("pharm-guide-amount", ("amount", amount)), CityUi.Accent, CityUi.Mono(11));
        amountLabel.MinWidth = 56;
        amountLabel.Align = Label.AlignMode.Right;
        row.AddChild(amountLabel);
        return row;
    }

    #endregion
}

/// <summary>
/// Строка списка препаратов: название и число шагов синтеза; выбранная — с голубой полосой слева.
/// </summary>
public sealed class PharmacistListItem : ContainerButton
{
    private readonly bool _active;
    private readonly Label _name;

    public PharmacistListItem(string name, string steps, bool active)
    {
        _active = active;
        MinHeight = 30;
        HorizontalExpand = true;

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(active ? 6 : 10, 0, 10, 0),
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(row);

        _name = CityUi.MakeLabel(name, active ? CityUi.Glow : CityUi.Text, CityUi.Bold(12));
        _name.HorizontalExpand = true;
        _name.ClipText = true;
        row.AddChild(_name);
        row.AddChild(CityUi.MakeLabel(steps, CityUi.Dim, CityUi.Mono(10)));

        UpdateLook();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateLook();
    }

    private void UpdateLook()
    {
        var hover = DrawMode == DrawModeEnum.Hover;
        var box = CityUi.Box(_active ? CityUi.ActiveFill : hover ? CityUi.Panel2 : CityUi.Panel, _active ? CityUi.Accent : CityUi.Panel, 0);
        if (_active)
        {
            box.BorderThickness = new Thickness(4, 0, 0, 0);
            box.BorderColor = CityUi.Accent;
        }

        StyleBoxOverride = box;
    }
}
