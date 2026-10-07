// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: окно отладочного инструмента «Болезни» (собирается в коде).

using System.Globalization;
using System.Linq;
using System.Numerics;
using Robust.Client.UserInterface;
using Content.Shared._Dystopia.Health.Disease;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Dystopia.Health.Disease;

public sealed class DiseaseDebugWindow : DefaultWindow
{
    public event Action<string, bool>? OnInfect;
    public event Action<float>? OnInfectRandom;
    public event Action<NetEntity>? OnCure;
    public event Action? OnCureAll;
    public event Action<NetEntity, float, float>? OnProgress;
    public event Action<NetEntity>? OnMutate;
    public event Action? OnClearImmunity;
    public event Action? OnSpread;
    public event Action? OnRefresh;

    private readonly Label _target;
    private readonly OptionButton _presets;
    private readonly CheckBox _force;
    private readonly LineEdit _complexity;
    private readonly Label _immunity;
    private readonly BoxContainer _diseases;

    private List<DiseaseDebugPreset> _presetList = new();

    public DiseaseDebugWindow()
    {
        Title = Loc.GetString("disease-debug-title");
        MinSize = new Vector2(520, 560);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        Contents.AddChild(root);

        _target = new Label();
        root.AddChild(_target);

        // Заразить готовой болезнью
        var infectRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        _presets = new OptionButton { HorizontalExpand = true };
        _presets.OnItemSelected += args => _presets.SelectId(args.Id);
        _force = new CheckBox { Text = Loc.GetString("disease-debug-force") };
        var infect = new Button { Text = Loc.GetString("disease-debug-infect") };
        infect.OnPressed += _ =>
        {
            var id = _presets.SelectedId;
            if (id >= 0 && id < _presetList.Count)
                OnInfect?.Invoke(_presetList[id].Id, _force.Pressed);
        };
        infectRow.AddChild(_presets);
        infectRow.AddChild(_force);
        infectRow.AddChild(infect);
        root.AddChild(infectRow);

        // Случайная болезнь заданной сложности
        var randomRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        randomRow.AddChild(new Label { Text = Loc.GetString("disease-debug-complexity") });
        _complexity = new LineEdit { Text = "20", MinWidth = 60 };
        randomRow.AddChild(_complexity);
        var random = new Button { Text = Loc.GetString("disease-debug-infect-random"), HorizontalExpand = true };
        random.OnPressed += _ =>
        {
            if (float.TryParse(_complexity.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                OnInfectRandom?.Invoke(value);
        };
        randomRow.AddChild(random);
        root.AddChild(randomRow);

        // Общие действия
        var actions = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        actions.AddChild(MakeButton("disease-debug-cure-all", () => OnCureAll?.Invoke()));
        actions.AddChild(MakeButton("disease-debug-clear-immunity", () => OnClearImmunity?.Invoke()));
        actions.AddChild(MakeButton("disease-debug-spread", () => OnSpread?.Invoke()));
        actions.AddChild(MakeButton("disease-debug-refresh", () => OnRefresh?.Invoke()));
        root.AddChild(actions);

        _immunity = new Label { FontColorOverride = Color.LightGray };
        root.AddChild(_immunity);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _diseases = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        scroll.AddChild(_diseases);
        root.AddChild(scroll);
    }

    private static Button MakeButton(string loc, Action act)
    {
        var button = new Button { Text = Loc.GetString(loc), HorizontalExpand = true };
        button.OnPressed += _ => act();
        return button;
    }

    public void UpdateState(DiseaseDebugBuiState state)
    {
        _target.Text = state.HasTarget
            ? Loc.GetString(state.CanCarry ? "disease-debug-target" : "disease-debug-target-no-carrier", ("name", state.TargetName))
            : Loc.GetString("disease-debug-no-target");

        // Список болезней перестраиваем, только если он поменялся — иначе сбросится выбор
        if (_presetList.Count != state.Presets.Count || !_presetList.Select(p => p.Id).SequenceEqual(state.Presets.Select(p => p.Id)))
        {
            var selected = _presets.SelectedId;
            _presetList = state.Presets;
            _presets.Clear();
            for (var i = 0; i < _presetList.Count; i++)
            {
                _presets.AddItem(_presetList[i].Name, i);
            }

            if (selected >= 0 && selected < _presetList.Count)
                _presets.SelectId(selected);
        }

        _immunity.Text = state.ImmuneTo.Count == 0
            ? Loc.GetString("disease-debug-immunity-none")
            : Loc.GetString("disease-debug-immunity", ("list", string.Join(", ", state.ImmuneTo)));

        _diseases.RemoveAllChildren();
        if (state.Diseases.Count == 0)
        {
            _diseases.AddChild(new Label { Text = Loc.GetString("disease-debug-healthy") });
            return;
        }

        foreach (var disease in state.Diseases)
        {
            _diseases.AddChild(MakeDiseaseBlock(disease));
        }
    }

    private Control MakeDiseaseBlock(DiseaseDebugEntry disease)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        box.AddChild(new Label
        {
            Text = Loc.GetString("disease-debug-entry-header", ("name", disease.Name), ("type", disease.Type), ("genotype", disease.Genotype)),
            FontColorOverride = Color.Orange,
        });
        box.AddChild(new Label
        {
            Text = Loc.GetString("disease-debug-entry-progress",
                ("infection", Percent(disease.Infection)), ("immunity", Percent(disease.Immunity))),
        });
        box.AddChild(new Label
        {
            Text = Loc.GetString("disease-debug-entry-params",
                ("rate", disease.InfectionRate.ToString("0.#####", CultureInfo.InvariantCulture)),
                ("mutation", disease.MutationRate.ToString("0.###", CultureInfo.InvariantCulture)),
                ("complexity", disease.Complexity.ToString("0.#", CultureInfo.InvariantCulture))),
            FontColorOverride = Color.LightGray,
        });

        foreach (var effect in disease.Effects)
        {
            box.AddChild(new Label
            {
                Text = Loc.GetString("disease-debug-entry-effect", ("name", effect.Name),
                    ("severity", effect.Severity.ToString("0.##", CultureInfo.InvariantCulture))),
            });
        }

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        buttons.AddChild(MakeButton("disease-debug-infection-up", () => OnProgress?.Invoke(disease.Disease, 0.1f, 0f)));
        buttons.AddChild(MakeButton("disease-debug-infection-down", () => OnProgress?.Invoke(disease.Disease, -0.1f, 0f)));
        buttons.AddChild(MakeButton("disease-debug-immunity-up", () => OnProgress?.Invoke(disease.Disease, 0f, 0.1f)));
        buttons.AddChild(MakeButton("disease-debug-immunity-down", () => OnProgress?.Invoke(disease.Disease, 0f, -0.1f)));
        box.AddChild(buttons);

        var buttons2 = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        buttons2.AddChild(MakeButton("disease-debug-mutate", () => OnMutate?.Invoke(disease.Disease)));
        buttons2.AddChild(MakeButton("disease-debug-cure", () => OnCure?.Invoke(disease.Disease)));
        box.AddChild(buttons2);

        return box;
    }

    private static int Percent(float value)
    {
        return (int) MathF.Round(value * 100f);
    }
}
