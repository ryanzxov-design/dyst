// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: меню дуговой сталеплавильной печи — питание, жар, загрузка, состав расплава, выбор сплава для разлива,
// снятый шлак.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Dystopia.Industry;

public sealed class DystopiaArcFurnaceWindow : CityWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action? OnToggle;
    public event Action<string?>? OnSelect;
    public event Action? OnEjectSlag;

    private readonly Label _status;
    private readonly CityButton _toggle;
    private readonly Label _temperature;
    private readonly Label _charge;
    private readonly Label _channel;
    private readonly BoxContainer _contents;
    private readonly GridContainer _alloys;
    private readonly Label _slag;
    private readonly CityButton _ejectSlag;

    public DystopiaArcFurnaceWindow()
    {
        IoCManager.InjectDependencies(this);

        WindowTitle = Loc.GetString("dystopia-arc-furnace-ui-title");
        Subtitle = Loc.GetString("dystopia-industry-ui-sub");
        Slogan = Loc.GetString("dystopia-industry-ui-slogan");
        MinSize = new Vector2(560, 560);
        SetSize = new Vector2(600, 640);

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
        _toggle = CityUi.MakeButton(Loc.GetString("dystopia-arc-furnace-ui-turn-on"), CityButtonStyle.Primary);
        _toggle.OnPressed += _ => OnToggle?.Invoke();
        top.AddChild(_status);
        top.AddChild(_toggle);
        root.AddChild(top);

        _temperature = CityUi.MakeLabel(string.Empty);
        _charge = CityUi.MakeLabel(string.Empty);
        _channel = CityUi.MakeLabel(string.Empty, CityUi.Dim);
        root.AddChild(_temperature);
        root.AddChild(_charge);
        root.AddChild(_channel);

        // Расплав
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-arc-furnace-ui-melt")));
        var contentsScroll = new ScrollContainer { HScrollEnabled = false, MinHeight = 110 };
        _contents = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        contentsScroll.AddChild(_contents);
        root.AddChild(contentsScroll);

        // Разлив
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-arc-furnace-ui-pour")));
        root.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-arc-furnace-ui-pour-hint"), CityUi.Dim));
        var alloyScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinHeight = 150 };
        _alloys = new GridContainer { Columns = 3, HorizontalExpand = true };
        alloyScroll.AddChild(_alloys);
        root.AddChild(alloyScroll);

        // Шлак
        root.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-arc-furnace-ui-slag")));
        var slagRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _slag = CityUi.MakeLabel(string.Empty);
        _slag.HorizontalExpand = true;
        _ejectSlag = CityUi.MakeButton(Loc.GetString("dystopia-arc-furnace-ui-eject-slag"));
        _ejectSlag.OnPressed += _ => OnEjectSlag?.Invoke();
        slagRow.AddChild(_slag);
        slagRow.AddChild(_ejectSlag);
        root.AddChild(slagRow);
    }

    private string ReagentName(string id)
    {
        return _proto.TryIndex<ReagentPrototype>(id, out var reagent) ? reagent.LocalizedName : id;
    }

    public void UpdateState(DystopiaArcFurnaceUiState state)
    {
        _status.Text = Loc.GetString(!state.On ? "dystopia-arc-furnace-ui-status-off"
            : state.Powered ? "dystopia-arc-furnace-ui-status-on"
            : "dystopia-arc-furnace-ui-status-unpowered");
        _status.FontColorOverride = state.On && state.Powered ? CityUi.Accent : state.On ? CityUi.Danger : CityUi.Dim;
        _toggle.Text = Loc.GetString(state.On ? "dystopia-arc-furnace-ui-turn-off" : "dystopia-arc-furnace-ui-turn-on")
            .ToUpperInvariant();

        _temperature.Text = Loc.GetString("dystopia-arc-furnace-ui-temperature",
            ("temperature", MathF.Round(state.Temperature)), ("max", MathF.Round(state.MaxTemperature)));
        _charge.Text = Loc.GetString("dystopia-arc-furnace-ui-charge",
            ("charge", state.Charge / 100), ("max", state.MaxCharge / 100),
            ("volume", MathF.Round(state.Volume)), ("maxVolume", MathF.Round(state.MaxVolume)));
        _channel.Text = Loc.GetString(state.ChannelAttached
            ? "dystopia-arc-furnace-ui-channel-yes"
            : "dystopia-arc-furnace-ui-channel-no");
        _channel.FontColorOverride = state.ChannelAttached ? CityUi.Dim : CityUi.Danger;

        // Состав расплава
        _contents.RemoveAllChildren();
        if (state.Contents.Count == 0)
            _contents.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-arc-furnace-ui-empty"), CityUi.Muted));

        foreach (var entry in state.Contents)
        {
            var color = _proto.TryIndex<ReagentPrototype>(entry.Id, out var reagent) ? reagent.SubstanceColor : CityUi.Text;
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var name = CityUi.MakeLabel(ReagentName(entry.Id), color);
            name.HorizontalExpand = true;
            row.AddChild(name);
            row.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-arc-furnace-ui-amount",
                ("amount", MathF.Round(entry.Amount, 1)))));
            _contents.AddChild(row);
        }

        // Выбор сплава
        _alloys.RemoveAllChildren();
        var none = CityUi.MakeButton(Loc.GetString("dystopia-arc-furnace-ui-no-pour"), CityButtonStyle.Tab);
        none.HorizontalExpand = true;
        none.Active = state.Selected == null;
        none.OnPressed += _ => OnSelect?.Invoke(null);
        _alloys.AddChild(none);

        foreach (var id in state.Castable)
        {
            var button = CityUi.MakeButton(ReagentName(id), CityButtonStyle.Tab);
            button.HorizontalExpand = true;
            button.Active = state.Selected == id;
            button.OnPressed += _ => OnSelect?.Invoke(id);
            _alloys.AddChild(button);
        }

        _slag.Text = Loc.GetString("dystopia-arc-furnace-ui-slag-count", ("count", state.Slag), ("max", state.SlagCapacity));
        _ejectSlag.Disabled = state.Slag <= 0;
    }
}
