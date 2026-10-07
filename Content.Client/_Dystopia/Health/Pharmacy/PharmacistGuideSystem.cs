// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: «Справочник фармацевта». Собирает рецепты из прототипов реакций (кешируется до перезагрузки
// прототипов) и открывает окно справочника — из предмета-справочника или из программы КПК.

using System.Linq;
using Content.Shared._Dystopia.Health.Pharmacy;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers;
using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Health.Pharmacy;

public sealed partial class PharmacistGuideSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;

    private static readonly ProtoId<PharmacistGuidePrototype> ConfigId = "Default";

    private PharmacistGuideData? _cache;
    private PharmacistGuideWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
        _window = null;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReactionPrototype>() || args.WasModified<ReagentPrototype>()
            || args.WasModified<PharmacistGuidePrototype>() || args.WasModified<EntityPrototype>())
            _cache = null;
    }

    /// <summary>
    /// Открыть отдельное окно справочника (из программы КПК). Повторный вызов поднимает уже открытое окно.
    /// </summary>
    public void OpenWindow()
    {
        if (_window is { Disposed: false })
        {
            _window.MoveToFront();
            return;
        }

        _window = new PharmacistGuideWindow();
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }

    public PharmacistGuideData GetData()
    {
        return _cache ??= Build();
    }

    private PharmacistGuideData Build()
    {
        _proto.TryIndex(ConfigId, out var config);
        var groups = config?.Groups ?? new List<string> { "Medicine" };

        // --- сырьё из раздатчиков: всё, чем наполнены их канистры ---
        var dispenser = new HashSet<string>();
        if (config != null)
        {
            foreach (var dispenserId in config.Dispensers)
            {
                if (!_proto.TryIndex(dispenserId, out var dispenserProto)
                    || !dispenserProto.TryComp<EntityTableContainerFillComponent>(out var fill, EntityManager.ComponentFactory))
                    continue;

                foreach (var selector in fill.Containers.Values)
                {
                    foreach (var (spawn, _) in _entityTable.ListSpawns(selector))
                    {
                        if (!_proto.TryIndex(spawn, out var jug)
                            || !jug.TryComp<SolutionComponent>(out var solution, EntityManager.ComponentFactory))
                            continue;

                        foreach (var quantity in solution.Solution.Contents)
                        {
                            dispenser.Add(quantity.Reagent.Prototype.Id);
                        }
                    }
                }
            }
        }

        var raw = config?.Raw.Select(r => r.Id).ToHashSet() ?? new HashSet<string>();

        // --- рецепты: каждая реакция, создающая новое вещество (без распада и без «самого себя» в реагентах) ---
        var recipes = new Dictionary<string, List<(ReactionPrototype Reaction, GuideRecipe Recipe)>>();
        foreach (var reaction in _proto.EnumeratePrototypes<ReactionPrototype>())
        {
            if (reaction.Source)
                continue;

            foreach (var (product, amount) in reaction.Products)
            {
                if (reaction.Reactants.ContainsKey(product) || !_proto.HasIndex(product))
                    continue;

                var recipe = new GuideRecipe { ReactionId = reaction.ID, Output = amount.Float() };
                foreach (var (reactant, info) in reaction.Reactants)
                {
                    recipe.Inputs.Add(new GuideIngredient(reactant.Id, info.Amount.Float(), info.Catalyst));
                }

                if (reaction.MinimumTemperature > 0f)
                    recipe.Conditions.Add(Loc.GetString("pharm-guide-cond-min-temp", ("temp", MathF.Round(reaction.MinimumTemperature, 1))));
                if (!float.IsPositiveInfinity(reaction.MaximumTemperature))
                    recipe.Conditions.Add(Loc.GetString("pharm-guide-cond-max-temp", ("temp", MathF.Round(reaction.MaximumTemperature, 1))));
                if (reaction.MixingCategories != null)
                {
                    foreach (var category in reaction.MixingCategories)
                    {
                        if (_proto.TryIndex(category, out var categoryProto))
                            recipe.Conditions.Add(Loc.GetString("pharm-guide-cond-mixer", ("verb", Loc.GetString(categoryProto.VerbText))));
                    }
                }

                if (!recipes.TryGetValue(product.Id, out var list))
                    recipes[product.Id] = list = new();
                list.Add((reaction, recipe));
            }
        }

        // --- вещества ---
        var reagents = new Dictionary<string, GuideReagent>();
        foreach (var reagent in _proto.EnumeratePrototypes<ReagentPrototype>())
        {
            var entry = new GuideReagent
            {
                Id = reagent.ID,
                Name = Capitalize(reagent.LocalizedName),
                Description = FormattedMessage.RemoveMarkupPermissive(reagent.LocalizedDescription),
                Medicine = groups.Contains(reagent.Group),
                Dispenser = dispenser.Contains(reagent.ID),
            };

            if (!entry.Dispenser && !raw.Contains(reagent.ID) && recipes.TryGetValue(reagent.ID, out var list))
            {
                // сначала простые: без центрифуги/электролиза, меньше реагентов
                entry.Recipes = list
                    .OrderBy(r => r.Reaction.MixingCategories is { Count: > 0 } ? 1 : 0)
                    .ThenBy(r => r.Reaction.Reactants.Count)
                    .ThenBy(r => r.Reaction.ID, StringComparer.Ordinal)
                    .Select(r => r.Recipe)
                    .ToList();
            }

            if (config != null && config.Sources.TryGetValue(reagent.ID, out var source))
                entry.SourceHint = Loc.GetString(source);

            reagents[reagent.ID] = entry;
        }

        var medicines = reagents.Values
            .Where(r => r.Medicine)
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(r => r.Id)
            .ToList();

        return new PharmacistGuideData(reagents, medicines);
    }

    private static string Capitalize(string text)
    {
        // без char + string и диапазонов: компилятор превращает их в ReadOnlySpan, а песочница клиента это запрещает
        return text.Length == 0 ? text : text.Substring(0, 1).ToUpperInvariant() + text.Substring(1);
    }
}

public readonly record struct GuideIngredient(string Reagent, float Amount, bool Catalyst);

public sealed class GuideRecipe
{
    public string ReactionId = string.Empty;
    public float Output;
    public List<GuideIngredient> Inputs = new();
    public List<string> Conditions = new();
}

public sealed class GuideReagent
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public bool Medicine;
    public bool Dispenser;
    public string? SourceHint;

    /// <summary>
    /// Все известные рецепты, первый — самый простой. Пусто — вещество не синтезируется.
    /// </summary>
    public List<GuideRecipe> Recipes = new();

    public bool Craftable => Recipes.Count > 0;
}

public sealed class PharmacistGuideData(Dictionary<string, GuideReagent> reagents, List<string> medicines)
{
    public readonly Dictionary<string, GuideReagent> Reagents = reagents;

    /// <summary>
    /// Препараты по алфавиту.
    /// </summary>
    public readonly List<string> Medicines = medicines;
}
