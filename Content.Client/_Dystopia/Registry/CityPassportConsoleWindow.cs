// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: окно «Паспортного стола». Слева — граждане с поиском, справа — выбранный гражданин: фото (сверить лицо),
// должность и статус, восстановление в должности, выдача новой карты и все его карты с аннулированием. Списки
// перерисовываются только при изменениях, чтобы набранная причина аннулирования не сбрасывалась.

using System.Linq;
using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Client.Lobby.UI.ProfileEditorControls;
using Content.Shared._Dystopia.Registry;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Registry;

public sealed class CityPassportConsoleWindow : CityWindow
{
    public event Action<int?>? OnSelect;
    public event Action<int, int, string>? OnAnnul;
    public event Action<int>? OnIssue;
    public event Action<int>? OnReinstate;

    private CityPassportConsoleUiState? _state;

    private readonly LineEdit _search;
    private readonly BoxContainer _rows;
    private string _listSignature = string.Empty;

    private readonly Label _selectHint;
    private readonly BoxContainer _detail;
    private readonly ProfilePreviewSpriteView _photo;
    private readonly Label _name;
    private readonly Label _job;
    private readonly Label _status;
    private readonly Label _dismissed;
    private readonly CityWrapLabel _dismissedHint;
    private readonly CityWrapLabel _outlawHint;
    private readonly CityWrapLabel _readonlyHint;
    private readonly CityButton _reinstate;
    private readonly CityButton _issue;
    private readonly BoxContainer _cards;
    private string _cardsSignature = string.Empty;
    private int? _photoNumber;

    public CityPassportConsoleWindow()
    {
        WindowTitle = Loc.GetString("dystopia-passport-console-title");
        Subtitle = Loc.GetString("dystopia-passport-console-subtitle");
        Slogan = Loc.GetString("dystopia-passport-console-slogan");
        MinSize = new Vector2(880, 580);
        SetSize = new Vector2(960, 640);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 14,
        };
        Contents.AddChild(body);

        // ---------- список ----------
        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 360,
            MaxWidth = 360,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        body.AddChild(left);

        _search = CityUi.Field(Loc.GetString("dystopia-registry-search-placeholder"));
        _search.HorizontalExpand = true;
        _search.OnTextChanged += _ => RedrawList(true);
        left.AddChild(_search);

        var table = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        table.VerticalExpand = true;
        var tableBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        table.AddChild(tableBox);
        var header = new PanelContainer { PanelOverride = CityUi.Box(CityUi.Bg2, CityUi.Bg2, 0, 8, 4) };
        header.AddChild(Columns(
            Small("dystopia-registry-col-number"),
            Small("dystopia-registry-col-name"),
            Small("dystopia-passport-col-cards")));
        tableBox.AddChild(header);
        tableBox.AddChild(CityUi.Separator(CityUi.Frame));
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        scroll.AddChild(_rows);
        tableBox.AddChild(scroll);
        left.AddChild(table);

        // ---------- гражданин ----------
        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        body.AddChild(right);

        _selectHint = CityUi.MakeLabel(Loc.GetString("dystopia-passport-console-select"), CityUi.Dim);
        right.AddChild(_selectHint);

        _detail = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Visible = false,
        };
        right.AddChild(_detail);

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12 };
        var photoCard = new CityCard { MinSize = new Vector2(150, 160) };
        _photo = new ProfilePreviewSpriteView
        {
            OverrideDirection = Direction.South,
            Stretch = SpriteView.StretchMode.None,
            Scale = new Vector2(4, 4),
            SetSize = new Vector2(140, 140),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        photoCard.AddChild(_photo);
        top.AddChild(photoCard);

        var info = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        _name = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Title(13));
        info.AddChild(_name);
        _job = CityUi.MakeLabel(string.Empty, CityUi.Text, CityUi.Regular(12));
        info.AddChild(_job);
        _status = CityUi.MakeLabel(string.Empty, CityUi.Accent, CityUi.Bold(12));
        info.AddChild(_status);
        _dismissed = CityUi.MakeLabel(Loc.GetString("dystopia-passport-dismissed"), CityRegistryConsoleWindow.Danger, CityUi.Bold(11));
        info.AddChild(_dismissed);
        _dismissedHint = CityUi.Hint(Loc.GetString("dystopia-passport-dismissed-hint"));
        info.AddChild(_dismissedHint);
        _outlawHint = CityUi.Hint(Loc.GetString("dystopia-passport-outlaw-hint"), CityRegistryConsoleWindow.Stripped);
        info.AddChild(_outlawHint);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _reinstate = CityUi.MakeButton(Loc.GetString("dystopia-passport-reinstate"));
        _reinstate.OnPressed += _ =>
        {
            if (_state?.Dossier is { } d)
                OnReinstate?.Invoke(d.Number);
        };
        buttons.AddChild(_reinstate);
        _issue = CityUi.MakeButton(Loc.GetString("dystopia-passport-issue"), CityButtonStyle.Primary);
        _issue.OnPressed += _ =>
        {
            if (_state?.Dossier is { } d)
                OnIssue?.Invoke(d.Number);
        };
        buttons.AddChild(_issue);
        info.AddChild(new Control { VerticalExpand = true });
        info.AddChild(buttons);
        top.AddChild(info);
        _detail.AddChild(top);

        _detail.AddChild(CityUi.Hint(Loc.GetString("dystopia-passport-issue-hint")));
        _readonlyHint = CityUi.Hint(Loc.GetString("dystopia-passport-console-readonly"));
        _detail.AddChild(_readonlyHint);

        _detail.AddChild(Small("dystopia-passport-cards"));
        var cardsPanel = CityUi.MakePanel(CityUi.Bg2, CityUi.Line, 0);
        cardsPanel.VerticalExpand = true;
        var cardsScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _cards = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        cardsScroll.AddChild(_cards);
        cardsPanel.AddChild(cardsScroll);
        _detail.AddChild(cardsPanel);
    }

    public void UpdateState(CityPassportConsoleUiState state)
    {
        _state = state;
        RedrawList(false);
        RedrawDetail();
    }

    // ======================= Список =======================

    private void RedrawList(bool force)
    {
        if (_state == null)
            return;

        var search = _search.Text.Trim();
        var shown = _state.Citizens.Where(c =>
                search.Length == 0 ||
                c.Number.ToString().Contains(search) ||
                c.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                c.JobTitle.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        var selected = _state.Dossier?.Number;
        var signature = $"{selected}:" + string.Join(";", shown.Select(c => $"{c.Number}:{c.Name}:{c.LiveCards}:{c.Dismissed}:{c.Status}"));
        if (!force && signature == _listSignature)
            return;
        _listSignature = signature;

        _rows.RemoveAllChildren();
        if (shown.Count == 0)
        {
            var empty = CityUi.MakeLabel(Loc.GetString("dystopia-registry-list-empty"), CityUi.Dim);
            empty.Margin = new Thickness(8, 6);
            _rows.AddChild(empty);
            return;
        }

        foreach (var citizen in shown)
        {
            var number = citizen.Number;
            var row = new CityRegistryRow();
            row.OnPressed += _ => OnSelect?.Invoke(number);

            var nameColor = number == selected ? CityUi.Accent
                : citizen.Outside ? CityRegistryConsoleWindow.Gold
                : citizen.Dismissed ? CityRegistryConsoleWindow.Danger
                : CityUi.Glow;
            var cardsColor = citizen.LiveCards == 0 ? CityRegistryConsoleWindow.Danger : CityUi.Text;

            row.AddChild(Columns(
                CityUi.MakeLabel(citizen.Number.ToString(), CityUi.Text, CityUi.Regular(12)),
                CityUi.MakeLabel(citizen.Name, nameColor, CityUi.Regular(12)),
                CityUi.MakeLabel(citizen.LiveCards.ToString(), cardsColor, CityUi.Regular(12))));
            _rows.AddChild(row);
        }
    }

    private static BoxContainer Columns(Label number, Label name, Label cards)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10, HorizontalExpand = true };
        number.MinWidth = 64;
        name.HorizontalExpand = true;
        name.ClipText = true;
        cards.MinWidth = 44;
        cards.Align = Label.AlignMode.Right;
        row.AddChild(number);
        row.AddChild(name);
        row.AddChild(cards);
        return row;
    }

    // ======================= Гражданин =======================

    private void RedrawDetail()
    {
        var state = _state!;
        var dossier = state.Dossier;
        _selectHint.Visible = dossier == null;
        _detail.Visible = dossier != null;
        if (dossier == null)
        {
            _photoNumber = null;
            _cardsSignature = string.Empty;
            return;
        }

        var canEdit = state.CanEdit && (!dossier.Outside || state.CanSupreme);
        var outlaw = dossier.Status == CityCitizenStatus.Disenfranchised;

        if (_photoNumber != dossier.Number)
        {
            _photoNumber = dossier.Number;
            if (dossier.Appearance is { } appearance)
                _photo.LoadPreview(appearance, null, false);
            else
                _photo.ClearPreview();
        }

        _name.Text = dossier.Name;
        _job.Text = Loc.GetString("dystopia-passport-citizen", ("number", dossier.Number.ToString()), ("job", dossier.JobTitle));
        _status.Text = (dossier.Outside
            ? Loc.GetString("dystopia-registry-head")
            : CityRegistryConsoleWindow.StatusText(dossier.Status)).ToUpperInvariant();
        _status.FontColorOverride = dossier.Outside
            ? CityRegistryConsoleWindow.Gold
            : CityRegistryConsoleWindow.StatusColor(dossier.Status);
        _dismissed.Visible = dossier.Dismissed;
        _dismissedHint.Visible = dossier.Dismissed && !outlaw;
        _outlawHint.Visible = dossier.Dismissed && outlaw;
        _readonlyHint.Visible = !canEdit;

        _reinstate.Visible = dossier.Dismissed;
        _reinstate.Disabled = !canEdit || outlaw;
        _issue.Disabled = !canEdit;

        RedrawCards(dossier, canEdit);
    }

    private void RedrawCards(CityPassportDossier dossier, bool canEdit)
    {
        var signature = $"{dossier.Number}:{canEdit}:" +
                        string.Join(";", dossier.Cards.Select(c => $"{c.Index}:{c.Annulled}:{c.Destroyed}:{c.Suspended}"));
        if (signature == _cardsSignature)
            return;
        _cardsSignature = signature;

        _cards.RemoveAllChildren();
        if (dossier.Cards.Count == 0)
        {
            var empty = CityUi.MakeLabel(Loc.GetString("dystopia-registry-cards-none"), CityUi.Dim);
            empty.Margin = new Thickness(8, 6);
            _cards.AddChild(empty);
            return;
        }

        // новые — сверху
        foreach (var card in Enumerable.Reverse(dossier.Cards))
        {
            _cards.AddChild(CardRow(dossier.Number, card, canEdit));
        }
    }

    private Control CardRow(int number, CityIdCardEntry card, bool canEdit)
    {
        var row = new PanelContainer { PanelOverride = CityUi.Box(Color.Transparent, CityUi.Line, 0, 8, 6) };
        ((StyleBoxFlat) row.PanelOverride).BorderThickness = new Thickness(0, 0, 0, 1);
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };
        row.AddChild(box);

        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        var serial = CityUi.MakeLabel(card.Serial, card.Annulled ? CityUi.Dim : CityUi.Glow, CityUi.Bold(13));
        head.AddChild(serial);
        var (stateText, stateColor) = CardState(card);
        var stateLabel = CityUi.MakeLabel(stateText.ToUpperInvariant(), stateColor, CityUi.Bold(11));
        stateLabel.VerticalAlignment = VAlignment.Center;
        stateLabel.HorizontalExpand = true;
        head.AddChild(stateLabel);
        box.AddChild(head);

        box.AddChild(CityUi.Hint(Loc.GetString("dystopia-passport-card-issued",
            ("time", Clock(card.IssuedAt)), ("author", card.IssuedBy))));
        if (card.Annulled)
        {
            box.AddChild(CityUi.Hint(Loc.GetString("dystopia-passport-card-annulled",
                ("time", Clock(card.AnnulledAt)), ("author", card.AnnulledBy),
                ("reason", card.AnnulReason.Length > 0 ? card.AnnulReason : "—")), CityRegistryConsoleWindow.Danger));
            return row;
        }

        if (!canEdit)
            return row;

        // аннулирование: кнопка раскрывает поле причины и подтверждение
        var annul = CityUi.MakeButton(Loc.GetString("dystopia-passport-annul"), CityButtonStyle.Danger);
        annul.HorizontalAlignment = HAlignment.Left;
        var form = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8, Visible = false };
        var reason = CityUi.Field(Loc.GetString("dystopia-passport-annul-reason"));
        reason.HorizontalExpand = true;
        var confirm = CityUi.MakeButton(Loc.GetString("dystopia-passport-annul-confirm"), CityButtonStyle.Danger);
        var index = card.Index;
        confirm.OnPressed += _ =>
        {
            if (reason.Text.Trim().Length == 0)
                return;
            OnAnnul?.Invoke(number, index, reason.Text.Trim());
        };
        reason.OnTextChanged += _ => confirm.Disabled = reason.Text.Trim().Length == 0;
        confirm.Disabled = true;
        form.AddChild(reason);
        form.AddChild(confirm);

        annul.OnPressed += _ =>
        {
            form.Visible = !form.Visible;
            if (form.Visible)
                reason.GrabKeyboardFocus();
        };
        box.AddChild(annul);
        box.AddChild(form);
        return row;
    }

    private static (string Text, Color Color) CardState(CityIdCardEntry card)
    {
        if (card.Annulled)
            return (Loc.GetString("dystopia-registry-card-annulled"), CityRegistryConsoleWindow.Danger);
        if (card.Destroyed)
            return (Loc.GetString("dystopia-registry-card-destroyed"), CityUi.Dim);
        if (card.Suspended)
            return (Loc.GetString("dystopia-registry-card-suspended"), CityRegistryConsoleWindow.Detained);
        return (Loc.GetString("dystopia-registry-card-live"), CityUi.Accent);
    }

    private static string Clock(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}";
    }

    private static Label Small(string key)
    {
        return CityUi.MakeLabel(Loc.GetString(key).ToUpperInvariant(), CityUi.Dim, CityUi.Regular(10));
    }
}
