// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: консоль Гражданской Инспекции в оформлении Города. Та же ванильная консоль ID-карт (те же сообщения
// серверу): карта инспектора и карта гражданина в слотах, имя и должность на карте, профессия (иконка и набор
// доступов), доступы плитками. Используется только консолью Города (ComputerIdDystopia).

using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared.Access;
using Content.Shared.CCVar;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using static Content.Shared.Access.Components.IdCardConsoleComponent;

namespace Content.Client._Dystopia.Registry;

public sealed class CityIdConsoleWindow : CityWindow
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>Профессия, если у карты её нет в учёте.</summary>
    private static readonly ProtoId<JobPrototype> DefaultJob = "DystopiaProletarian";

    public event Action? OnPrivilegedSlot;
    public event Action? OnTargetSlot;
    public event Action? OnManifest;
    public event Action<string, string, List<ProtoId<AccessLevelPrototype>>, ProtoId<JobPrototype>?>? OnSubmit;

    private readonly CityButton _privilegedButton;
    private readonly Label _privilegedName;
    private readonly CityButton _targetButton;
    private readonly Label _targetName;
    private readonly CityWrapLabel _status;

    private readonly LineEdit _fullName;
    private readonly CityButton _fullNameSave;
    private readonly LineEdit _jobTitle;
    private readonly CityButton _jobTitleSave;
    private readonly CityDropdown _job;
    private readonly List<ProtoId<JobPrototype>> _jobIds = new();

    private readonly CityButton _selectAll;
    private readonly CityButton _deselectAll;
    private readonly GridContainer _grid;
    private readonly Dictionary<ProtoId<AccessLevelPrototype>, CityButton> _access = new();
    private readonly HashSet<ProtoId<AccessLevelPrototype>> _pressed = new();

    private string? _lastFullName;
    private string? _lastJobTitle;
    private string? _lastJobProto;
    private bool _enabled;

    public CityIdConsoleWindow()
    {
        IoCManager.InjectDependencies(this);

        WindowTitle = Loc.GetString("dystopia-id-console-title");
        Subtitle = Loc.GetString("dystopia-id-console-subtitle");
        Slogan = Loc.GetString("dystopia-id-console-slogan");
        MinSize = new Vector2(820, 600);
        SetSize = new Vector2(880, 680);

        // ---------- карты в слотах ----------
        var slots = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        (_privilegedButton, _privilegedName) = SlotCard(slots, "dystopia-id-console-privileged");
        (_targetButton, _targetName) = SlotCard(slots, "dystopia-id-console-target");
        _privilegedButton.OnPressed += _ => OnPrivilegedSlot?.Invoke();
        _targetButton.OnPressed += _ => OnTargetSlot?.Invoke();
        var manifest = CityUi.MakeButton(Loc.GetString("dystopia-id-console-manifest"));
        manifest.VerticalAlignment = VAlignment.Center;
        manifest.OnPressed += _ => OnManifest?.Invoke();
        slots.AddChild(manifest);
        Contents.AddChild(slots);

        _status = CityUi.Hint(string.Empty);
        Contents.AddChild(_status);

        // ---------- данные карты ----------
        var card = new CityCard();
        var fields = new GridContainer { Columns = 3, HorizontalExpand = true, HSeparationOverride = 10, VSeparationOverride = 6 };
        fields.AddChild(Caption("dystopia-id-console-full-name"));
        _fullName = CityUi.Field();
        _fullName.HorizontalExpand = true;
        _fullName.IsValid = s => s.Length <= _cfg.GetCVar(CCVars.MaxNameLength);
        _fullName.OnTextChanged += _ => _fullNameSave!.Disabled = !_enabled || _fullName.Text == _lastFullName;
        _fullName.OnTextEntered += _ => Submit();
        fields.AddChild(_fullName);
        _fullNameSave = CityUi.MakeButton(Loc.GetString("dystopia-id-console-save"), CityButtonStyle.Primary);
        _fullNameSave.OnPressed += _ => Submit();
        fields.AddChild(_fullNameSave);

        fields.AddChild(Caption("dystopia-id-console-job-title"));
        _jobTitle = CityUi.Field();
        _jobTitle.HorizontalExpand = true;
        _jobTitle.IsValid = s => s.Length <= _cfg.GetCVar(CCVars.MaxIdJobLength);
        _jobTitle.OnTextChanged += _ => _jobTitleSave!.Disabled = !_enabled || _jobTitle.Text == _lastJobTitle;
        _jobTitle.OnTextEntered += _ => Submit();
        fields.AddChild(_jobTitle);
        _jobTitleSave = CityUi.MakeButton(Loc.GetString("dystopia-id-console-save"), CityButtonStyle.Primary);
        _jobTitleSave.OnPressed += _ => Submit();
        fields.AddChild(_jobTitleSave);

        fields.AddChild(Caption("dystopia-id-console-job"));
        _job = new CityDropdown { HorizontalExpand = true };
        _job.OnItemSelected += SelectJob;
        fields.AddChild(_job);
        fields.AddChild(new Control());
        card.AddChild(fields);
        Contents.AddChild(card);
        Contents.AddChild(CityUi.Hint(Loc.GetString("dystopia-id-console-job-hint")));

        // ---------- доступы ----------
        var accessHead = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var accessCaption = CityUi.MakeLabel(Loc.GetString("dystopia-id-console-access").ToUpperInvariant(), CityUi.Dim, CityUi.Regular(11));
        accessCaption.HorizontalExpand = true;
        accessCaption.VerticalAlignment = VAlignment.Center;
        accessHead.AddChild(accessCaption);
        _selectAll = CityUi.MakeButton(Loc.GetString("dystopia-id-console-select-all"));
        _selectAll.OnPressed += _ => SetAll(true);
        accessHead.AddChild(_selectAll);
        _deselectAll = CityUi.MakeButton(Loc.GetString("dystopia-id-console-deselect-all"), CityButtonStyle.Danger);
        _deselectAll.OnPressed += _ => SetAll(false);
        accessHead.AddChild(_deselectAll);
        Contents.AddChild(accessHead);

        var accessPanel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 8);
        accessPanel.VerticalExpand = true;
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _grid = new GridContainer { Columns = 3, HorizontalExpand = true, HSeparationOverride = 6, VSeparationOverride = 6 };
        scroll.AddChild(_grid);
        accessPanel.AddChild(scroll);
        Contents.AddChild(accessPanel);

        FillJobs();
    }

    /// <summary>Плитки доступов, которыми распоряжается консоль (из её прототипа).</summary>
    public void SetAccessLevels(List<ProtoId<AccessLevelPrototype>> accessLevels)
    {
        _grid.RemoveAllChildren();
        _access.Clear();
        foreach (var id in accessLevels)
        {
            if (!_prototype.TryIndex(id, out var level))
                continue;

            var name = level.GetAccessLevelName();
            var button = new CityButton(name, CityButtonStyle.Tab)
            {
                HorizontalExpand = true,
                Marquee = true,
            };
            button.SetFullText(name);
            var access = id;
            button.OnPressed += _ => Toggle(access);
            _access[id] = button;
            _grid.AddChild(button);
        }
    }

    private void FillJobs()
    {
        // только профессии из служб, видимых в редакторе персонажа (как в ванильной консоли с правкой Dystopia)
        var visible = new HashSet<ProtoId<JobPrototype>>();
        foreach (var department in _prototype.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (!department.EditorHidden)
                visible.UnionWith(department.Roles);
        }

        var jobs = _prototype.EnumeratePrototypes<JobPrototype>()
            .Where(j => j.OverrideConsoleVisibility.GetValueOrDefault(j.SetPreference) && visible.Contains(j.ID))
            .OrderBy(j => j.LocalizedName, StringComparer.CurrentCulture);

        foreach (var job in jobs)
        {
            _job.AddItem(job.LocalizedName, _jobIds.Count);
            _jobIds.Add(job.ID);
        }
    }

    public void UpdateState(IdCardConsoleBoundUserInterfaceState state)
    {
        _privilegedButton.Text = Loc.GetString(state.IsPrivilegedIdPresent ? "dystopia-id-console-eject" : "dystopia-id-console-insert");
        _privilegedName.Text = state.IsPrivilegedIdPresent ? state.PrivilegedIdName : Loc.GetString("dystopia-id-console-empty");
        _targetButton.Text = Loc.GetString(state.IsTargetIdPresent ? "dystopia-id-console-eject" : "dystopia-id-console-insert");
        _targetName.Text = state.IsTargetIdPresent ? state.TargetIdName : Loc.GetString("dystopia-id-console-empty");

        _enabled = state.IsPrivilegedIdPresent && state.IsPrivilegedIdAuthorized && state.IsTargetIdPresent;
        _status.Text = Loc.GetString(!state.IsPrivilegedIdPresent ? "dystopia-id-console-status-no-privileged"
            : !state.IsPrivilegedIdAuthorized ? "dystopia-id-console-status-unauthorized"
            : !state.IsTargetIdPresent ? "dystopia-id-console-status-no-target"
            : "dystopia-id-console-status-ready");
        _status.FontColorOverride = _enabled ? CityUi.Accent
            : state.IsPrivilegedIdPresent && !state.IsPrivilegedIdAuthorized ? CityRegistryConsoleWindow.Danger
            : CityUi.Dim;

        var fullNameDirty = _lastFullName != null && _fullName.Text != state.TargetIdFullName;
        var jobTitleDirty = _lastJobTitle != null && _jobTitle.Text != state.TargetIdJobTitle;

        _fullName.Editable = _enabled;
        if (!fullNameDirty)
            _fullName.Text = state.TargetIdFullName ?? string.Empty;
        _fullNameSave.Disabled = !_enabled || !fullNameDirty;

        _jobTitle.Editable = _enabled;
        if (!jobTitleDirty)
            _jobTitle.Text = state.TargetIdJobTitle ?? string.Empty;
        _jobTitleSave.Disabled = !_enabled || !jobTitleDirty;

        _job.Disabled = !_enabled;
        _selectAll.Disabled = !_enabled;
        _deselectAll.Disabled = !_enabled;

        _pressed.Clear();
        if (state.TargetIdAccessList != null)
            _pressed.UnionWith(state.TargetIdAccessList);
        var allowed = state.AllowedModifyAccessList;
        foreach (var (id, button) in _access)
        {
            button.Active = _pressed.Contains(id);
            button.Disabled = !_enabled || allowed != null && !allowed.Contains(id);
        }

        var jobIndex = _jobIds.IndexOf(state.TargetIdJobPrototype);
        if (jobIndex < 0)
            jobIndex = _jobIds.IndexOf(DefaultJob);
        if (jobIndex >= 0)
            _job.SelectId(jobIndex);

        _lastFullName = state.TargetIdFullName;
        _lastJobTitle = state.TargetIdJobTitle;
        _lastJobProto = state.TargetIdJobPrototype;
    }

    private void Toggle(ProtoId<AccessLevelPrototype> access)
    {
        if (!_access.TryGetValue(access, out var button) || button.Disabled)
            return;

        if (!_pressed.Remove(access))
            _pressed.Add(access);
        button.Active = _pressed.Contains(access);
        Submit();
    }

    private void SetAll(bool enabled)
    {
        foreach (var (id, button) in _access)
        {
            if (button.Disabled)
                continue;

            if (enabled)
                _pressed.Add(id);
            else
                _pressed.Remove(id);
            button.Active = enabled;
        }

        Submit();
    }

    /// <summary>Профессия: должность на карте, иконка и набор доступов профессии.</summary>
    private void SelectJob(int index)
    {
        if (index < 0 || index >= _jobIds.Count || !_prototype.TryIndex(_jobIds[index], out var job))
            return;

        _job.SelectId(index);
        _jobTitle.Text = job.LocalizedName;

        foreach (var (id, button) in _access)
        {
            if (!button.Disabled)
                _pressed.Remove(id);
        }

        var tags = new HashSet<ProtoId<AccessLevelPrototype>>(job.Access);
        foreach (var group in job.AccessGroups)
        {
            if (_prototype.TryIndex(group, out var groupProto))
                tags.UnionWith(groupProto.Tags);
        }

        foreach (var tag in tags)
        {
            if (_access.TryGetValue(tag, out var button) && !button.Disabled)
                _pressed.Add(tag);
        }

        foreach (var (id, button) in _access)
        {
            button.Active = _pressed.Contains(id);
        }

        Submit();
    }

    private void Submit()
    {
        if (!_enabled)
            return;

        ProtoId<JobPrototype>? job = null;
        if (_job.SelectedId >= 0 && _job.SelectedId < _jobIds.Count && _jobIds[_job.SelectedId].Id != _lastJobProto)
            job = _jobIds[_job.SelectedId];

        OnSubmit?.Invoke(_fullName.Text, _jobTitle.Text, _pressed.ToList(), job);
    }

    private static (CityButton Button, Label Name) SlotCard(BoxContainer parent, string caption)
    {
        var card = new CityCard { HorizontalExpand = true };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        box.AddChild(CityUi.MakeLabel(Loc.GetString(caption).ToUpperInvariant(), CityUi.Dim, CityUi.Regular(10)));
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var name = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Regular(12));
        name.HorizontalExpand = true;
        name.ClipText = true;
        name.VerticalAlignment = VAlignment.Center;
        row.AddChild(name);
        var button = CityUi.MakeButton(string.Empty);
        row.AddChild(button);
        box.AddChild(row);
        card.AddChild(box);
        parent.AddChild(card);
        return (button, name);
    }

    private static Label Caption(string key)
    {
        var label = CityUi.MakeLabel(Loc.GetString(key).ToUpperInvariant(), CityUi.Dim, CityUi.Regular(11));
        label.VerticalAlignment = VAlignment.Center;
        return label;
    }
}
