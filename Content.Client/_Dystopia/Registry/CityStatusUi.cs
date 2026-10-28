// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: программа КПК «Мой статус» — дело владельца карты в КПК глазами самого гражданина.

using System.Linq;
using Content.Client._Dystopia.UserInterface;
using Content.Client.UserInterface.Fragments;
using Content.Shared._Dystopia.Registry;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Registry;

public sealed partial class CityStatusUi : UIFragment
{
    private CityStatusUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new CityStatusUiFragment();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CityStatusUiState cast)
            _fragment?.UpdateState(cast);
    }
}

public sealed class CityStatusUiFragment : BoxContainer
{
    private readonly BoxContainer _content;
    private string _signature = string.Empty;

    public CityStatusUiFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        Margin = new Thickness(4);

        var scroll = new ScrollContainer { HScrollEnabled = false, HorizontalExpand = true, VerticalExpand = true };
        _content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        scroll.AddChild(_content);
        AddChild(scroll);
    }

    public void UpdateState(CityStatusUiState state)
    {
        // перестраиваем только когда что-то поменялось: иначе список прыгает каждые 2 секунды
        var signature = Signature(state);
        if (signature == _signature)
            return;
        _signature = signature;

        _content.RemoveAllChildren();
        if (!state.HasRecord)
        {
            _content.AddChild(CityUi.Hint(Loc.GetString("dystopia-status-no-card")));
            return;
        }

        // кто
        _content.AddChild(CityUi.MakeLabel($"№{state.Number} · {state.Name}", CityUi.Glow, CityUi.Bold(14)));
        _content.AddChild(CityUi.MakeLabel($"{state.JobTitle} · {state.Department}", CityUi.Dim, CityUi.Regular(11)));
        if (state.Dismissed)
            _content.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-status-dismissed"), CityRegistryConsoleWindow.Danger, CityUi.Bold(12)));

        // статус
        _content.AddChild(Section("dystopia-status-section-status"));
        _content.AddChild(CityUi.MakeLabel(CityRegistryConsoleWindow.StatusText(state.Status).ToUpperInvariant(),
            CityRegistryConsoleWindow.StatusColor(state.Status), CityUi.Bold(14)));
        if (state.StatusReason.Length > 0)
            _content.AddChild(CityUi.Hint(Loc.GetString("dystopia-status-reason", ("reason", state.StatusReason))));

        // лояльность
        _content.AddChild(Section("dystopia-status-section-loyalty"));
        if (state.Outside || state.Loyalty is not { } loyalty)
        {
            _content.AddChild(CityUi.Hint(Loc.GetString("dystopia-status-outside"), CityRegistryConsoleWindow.Gold));
        }
        else
        {
            var color = CityRegistryConsoleWindow.LoyaltyColor(loyalty, state.LoyaltyNorm);
            _content.AddChild(CityUi.MakeLabel($"{loyalty} / {state.MaxLoyalty}", color, CityUi.Bold(14)));
            var bar = new CityLoyaltyBar();
            bar.Set(loyalty, state.MaxLoyalty, state.LoyaltyNorm, color);
            _content.AddChild(bar);
            if (state.LoyaltyNorm is { } norm)
            {
                var key = loyalty <= 0 ? "dystopia-status-loyalty-zero"
                    : loyalty < norm ? "dystopia-status-loyalty-below"
                    : "dystopia-status-loyalty-ok";
                _content.AddChild(CityUi.Hint(Loc.GetString(key, ("norm", norm))));
            }
        }

        // карта
        _content.AddChild(Section("dystopia-status-section-card"));
        if (state.CardSerial.Length > 0)
        {
            _content.AddChild(CityUi.MakeLabel(state.CardSerial, CityUi.Text, CityUi.Mono(12)));
            if (state.CardAnnulled)
                _content.AddChild(CityUi.MakeLabel(Loc.GetString("dystopia-status-card-annulled"), CityRegistryConsoleWindow.Danger, CityUi.Bold(11)));
        }
        else
        {
            _content.AddChild(CityUi.Hint(Loc.GetString("dystopia-status-card-unknown")));
        }

        // журнал
        if (state.Outside)
            return;

        _content.AddChild(Section("dystopia-status-section-journal"));
        if (state.Journal.Count == 0)
        {
            _content.AddChild(CityUi.Hint(Loc.GetString("dystopia-status-journal-empty")));
            return;
        }

        foreach (var entry in state.Journal)
        {
            _content.AddChild(EntryRow(entry));
        }
    }

    private static Control Section(string key)
    {
        var label = CityUi.MakeLabel(Loc.GetString(key).ToUpperInvariant(), CityUi.Accent, CityUi.Bold(11));
        label.Margin = new Thickness(0, 8, 0, 0);
        return label;
    }

    private static Control EntryRow(CityRegistryEntry entry)
    {
        var color = entry.Kind switch
        {
            CityRegistryEntryKind.Violation => CityRegistryConsoleWindow.Danger,
            CityRegistryEntryKind.Reward => CityUi.Accent,
            CityRegistryEntryKind.Fine => CityRegistryConsoleWindow.Gold,
            _ => CityUi.Text,
        };

        var row = new PanelContainer { PanelOverride = CityUi.Box(Color.Transparent, CityUi.Line, 0, 0, 4) };
        ((StyleBoxFlat) row.PanelOverride).BorderThickness = new Thickness(0, 0, 0, 1);
        var line = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };

        var time = CityUi.MakeLabel($"{(int) entry.Time.TotalHours:00}:{entry.Time.Minutes:00}", CityUi.Dim, CityUi.Regular(11));
        time.MinWidth = 40;
        time.VerticalAlignment = VAlignment.Top;
        line.AddChild(time);

        var text = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        text.AddChild(new CityWrapLabel(entry.Text, color));
        if (entry.Details.Length > 0)
            text.AddChild(CityUi.Hint(entry.Details));
        text.AddChild(CityUi.MakeLabel(entry.Author, CityUi.Dim, CityUi.Regular(10)));
        line.AddChild(text);

        var delta = CityUi.MakeLabel(entry.LoyaltyDelta == 0 ? "—" : entry.LoyaltyDelta > 0 ? $"+{entry.LoyaltyDelta}" : entry.LoyaltyDelta.ToString(),
            entry.LoyaltyDelta < 0 ? CityRegistryConsoleWindow.Danger : entry.LoyaltyDelta > 0 ? CityUi.Accent : CityUi.Dim,
            CityUi.Bold(12));
        delta.MinWidth = 32;
        delta.Align = Label.AlignMode.Right;
        delta.VerticalAlignment = VAlignment.Top;
        line.AddChild(delta);

        row.AddChild(line);
        return row;
    }

    private static string Signature(CityStatusUiState s)
    {
        return $"{s.HasRecord}:{s.Number}:{s.Name}:{s.JobTitle}:{s.Department}:{s.Status}:{s.StatusReason}:{s.Dismissed}:" +
               $"{s.Outside}:{s.Loyalty}:{s.LoyaltyNorm}:{s.MaxLoyalty}:{s.CardSerial}:{s.CardAnnulled}:" +
               string.Join("|", s.Journal.Select(e => $"{e.Time.Ticks}:{e.Kind}:{e.Text}"));
    }
}
