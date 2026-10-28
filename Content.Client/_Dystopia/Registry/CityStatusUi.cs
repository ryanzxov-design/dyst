// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: программа КПК «Мой статус» — дело владельца карты в КПК глазами самого гражданина. Экран КПК Города:
// один янтарный цвет, важное — инверсией, тревожное (розыск, аннулированная карта) мигает.

using Content.Client._Dystopia.Pda;
using Content.Client.UserInterface.Fragments;
using Content.Shared._Dystopia.Registry;
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

        var scroll = new ScrollContainer { HScrollEnabled = false, HorizontalExpand = true, VerticalExpand = true };
        _content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };
        scroll.AddChild(_content);
        AddChild(scroll);
    }

    public void UpdateState(CityStatusUiState state)
    {
        // перестраиваем только когда что-то поменялось
        var signature = Signature(state);
        if (signature == _signature)
            return;
        _signature = signature;

        _content.RemoveAllChildren();
        if (!state.HasRecord)
        {
            _content.AddChild(CityPdaUi.Wrap(Loc.GetString("dystopia-status-no-card"), CityPdaUi.Dim));
            return;
        }

        // кто
        _content.AddChild(CityPdaUi.Text($"№{state.Number}  {state.Name.ToUpperInvariant()}"));
        _content.AddChild(CityPdaUi.Text($"{state.JobTitle} · {state.Department}".ToUpperInvariant(), CityPdaUi.Dim));
        if (state.Dismissed)
            _content.AddChild(Blink(CityPdaUi.Inverse(Loc.GetString("dystopia-status-dismissed"))));

        // статус
        _content.AddChild(CityPdaUi.Section(Loc.GetString("dystopia-status-section-status")));
        var status = CityPdaUi.Inverse(CityRegistryConsoleWindow.StatusText(state.Status).ToUpperInvariant());
        var alarming = state.Status is CityCitizenStatus.Wanted or CityCitizenStatus.Disenfranchised;
        _content.AddChild(CityPdaUi.Field(Loc.GetString("dystopia-status-field-status"), alarming ? Blink(status) : status));
        if (state.StatusReason.Length > 0)
            _content.AddChild(CityPdaUi.Wrap(Loc.GetString("dystopia-status-reason", ("reason", state.StatusReason)), CityPdaUi.Dim));

        // лояльность
        _content.AddChild(CityPdaUi.Section(Loc.GetString("dystopia-status-section-loyalty")));
        if (state.Outside || state.Loyalty is not { } loyalty)
        {
            _content.AddChild(CityPdaUi.Text(Loc.GetString("dystopia-status-outside").ToUpperInvariant(), CityPdaUi.Dim));
        }
        else
        {
            _content.AddChild(CityPdaUi.Field(Loc.GetString("dystopia-status-field-loyalty"), $"{loyalty} / {state.MaxLoyalty}"));
            var bar = new CityPdaBar { Margin = new Thickness(0, 2) };
            bar.Set(loyalty, state.MaxLoyalty, state.LoyaltyNorm);
            _content.AddChild(bar);
            if (state.LoyaltyNorm is { } norm)
            {
                var key = loyalty <= 0 ? "dystopia-status-loyalty-zero"
                    : loyalty < norm ? "dystopia-status-loyalty-below"
                    : "dystopia-status-loyalty-ok";
                _content.AddChild(CityPdaUi.Wrap(Loc.GetString(key, ("norm", norm)), CityPdaUi.Dim));
            }
        }

        // карта
        _content.AddChild(CityPdaUi.Section(Loc.GetString("dystopia-status-section-card")));
        if (state.CardSerial.Length == 0)
        {
            _content.AddChild(CityPdaUi.Text(Loc.GetString("dystopia-status-card-unknown"), CityPdaUi.Dim));
            return;
        }

        _content.AddChild(CityPdaUi.Field(Loc.GetString("dystopia-status-field-serial"), state.CardSerial));
        if (state.CardAnnulled)
        {
            _content.AddChild(CityPdaUi.Field(Loc.GetString("dystopia-status-field-card-state"),
                Blink(CityPdaUi.Inverse(Loc.GetString("dystopia-status-card-annulled")))));
            _content.AddChild(CityPdaUi.Text(Loc.GetString("dystopia-status-card-annulled-hint"), CityPdaUi.Dim));
        }
        else
        {
            _content.AddChild(CityPdaUi.Field(Loc.GetString("dystopia-status-field-card-state"),
                Loc.GetString("dystopia-status-card-valid")));
        }
    }

    private static Control Blink(Control inner)
    {
        var blink = new CityPdaBlink();
        blink.AddChild(inner);
        return blink;
    }

    private static string Signature(CityStatusUiState s)
    {
        return $"{s.HasRecord}:{s.Number}:{s.Name}:{s.JobTitle}:{s.Department}:{s.Status}:{s.StatusReason}:{s.Dismissed}:" +
               $"{s.Outside}:{s.Loyalty}:{s.LoyaltyNorm}:{s.MaxLoyalty}:{s.CardSerial}:{s.CardAnnulled}";
    }
}
