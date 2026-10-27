// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: окошко смены статуса гражданина (из консоли социоучёта). Шесть статусов; поражение в правах — только
// для высшей власти; для розыска нужно основание.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Registry;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Registry;

public sealed class CityRegistryStatusWindow : CityWindow
{
    public event Action<int, CityCitizenStatus, string>? OnApply;

    private readonly Label _citizen;
    private readonly Dictionary<CityCitizenStatus, CityButton> _options = new();
    private readonly LineEdit _reason;
    private readonly CityWrapLabel _wantedHint;
    private readonly CityWrapLabel _outlawHint;
    private readonly CityButton _apply;

    private int _number;
    private CityCitizenStatus _current;
    private CityCitizenStatus _chosen;
    private bool _canEdit;
    private bool _canSupreme;

    public CityRegistryStatusWindow()
    {
        Subtitle = Loc.GetString("dystopia-registry-status-subtitle");
        Slogan = Loc.GetString("dystopia-registry-console-slogan");
        MinSize = new Vector2(520, 420);
        SetSize = new Vector2(560, 470);

        _citizen = CityUi.MakeLabel(string.Empty, CityUi.Text, CityUi.Regular(12));
        _citizen.ClipText = true;
        Contents.AddChild(_citizen);

        Contents.AddChild(CityUi.SectionHeader(Loc.GetString("dystopia-registry-status-new")));
        var grid = new GridContainer { Columns = 2, HorizontalExpand = true, HSeparationOverride = 6, VSeparationOverride = 6 };
        foreach (var status in Enum.GetValues<CityCitizenStatus>())
        {
            var option = new CityButton(string.Empty, CityButtonStyle.Tab) { HorizontalExpand = true, MinHeight = 30 };
            option.TextColor = CityRegistryConsoleWindow.StatusColor(status);
            var value = status;
            option.OnPressed += _ => Choose(value);
            _options[status] = option;
            grid.AddChild(option);
        }
        Contents.AddChild(grid);

        Contents.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-registry-status-reason").ToUpperInvariant(), CityUi.Dim, CityUi.Regular(11)));
        _reason = CityUi.Field(Loc.GetString("dystopia-registry-status-reason-placeholder"));
        _reason.HorizontalExpand = true;
        _reason.OnTextChanged += _ => RefreshApply();
        Contents.AddChild(_reason);

        _wantedHint = CityUi.Hint(Loc.GetString("dystopia-registry-status-wanted-hint"), CityRegistryConsoleWindow.Danger);
        Contents.AddChild(_wantedHint);
        _outlawHint = CityUi.Hint(Loc.GetString("dystopia-registry-status-outlaw-hint"), CityRegistryConsoleWindow.Stripped);
        Contents.AddChild(_outlawHint);
        Contents.AddChild(CityUi.Hint(Loc.GetString("dystopia-registry-status-sign")));

        Contents.AddChild(new Control { VerticalExpand = true });
        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            SeparationOverride = 8,
        };
        var cancel = CityUi.MakeButton(Loc.GetString("dystopia-registry-status-cancel"));
        cancel.OnPressed += _ => Close();
        _apply = CityUi.MakeButton(Loc.GetString("dystopia-registry-status-apply"), CityButtonStyle.Primary);
        _apply.OnPressed += _ =>
        {
            OnApply?.Invoke(_number, _chosen, _reason.Text.Trim());
            Close();
        };
        buttons.AddChild(cancel);
        buttons.AddChild(_apply);
        Contents.AddChild(buttons);
    }

    /// <summary>Свежие данные дела и права (окно может висеть открытым, пока приходят обновления).</summary>
    public void Update(CityRegistryDossier dossier, bool canEdit, bool canSupreme)
    {
        _number = dossier.Number;
        _current = dossier.Status;
        _canEdit = canEdit;
        _canSupreme = canSupreme;

        WindowTitle = Loc.GetString("dystopia-registry-status-title", ("number", dossier.Number.ToString()));
        _citizen.Text = Loc.GetString("dystopia-registry-status-window-citizen",
            ("name", dossier.Name), ("job", dossier.JobTitle), ("status", CityRegistryConsoleWindow.StatusText(dossier.Status)));

        // поражение в правах ставит и снимает только высшая власть
        var supremeLocked = !canSupreme && _current == CityCitizenStatus.Disenfranchised;
        foreach (var (status, option) in _options)
        {
            var locked = status == CityCitizenStatus.Disenfranchised && !canSupreme;
            option.Text = (locked
                ? Loc.GetString("dystopia-registry-status-supreme", ("status", CityRegistryConsoleWindow.StatusText(status)))
                : CityRegistryConsoleWindow.StatusText(status)).ToUpperInvariant();
            option.Disabled = !canEdit || locked || supremeLocked;
        }

        RefreshApply();
    }

    /// <summary>Открытие заново: выбран текущий статус, основание пустое.</summary>
    public void Reset(CityRegistryDossier dossier)
    {
        _reason.Text = string.Empty;
        Choose(dossier.Status);
    }

    private void Choose(CityCitizenStatus status)
    {
        _chosen = status;
        foreach (var (value, option) in _options)
        {
            option.Active = value == status;
        }

        RefreshApply();
    }

    private void RefreshApply()
    {
        var needsReason = _chosen == CityCitizenStatus.Wanted;
        _wantedHint.Visible = needsReason;
        _outlawHint.Visible = _chosen == CityCitizenStatus.Disenfranchised && _current != CityCitizenStatus.Disenfranchised;
        _apply.Disabled = !_canEdit ||
                          _chosen == _current ||
                          needsReason && _reason.Text.Trim().Length == 0 ||
                          !_canSupreme && (_chosen == CityCitizenStatus.Disenfranchised || _current == CityCitizenStatus.Disenfranchised);
    }
}
