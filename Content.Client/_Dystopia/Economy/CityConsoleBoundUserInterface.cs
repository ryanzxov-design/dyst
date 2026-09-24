using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Economy;

public sealed partial class CityConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CityConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CityConsoleWindow>();
        _window.OnSetRates += (job, salary, tax) => SendMessage(new CityConsoleSetRatesMessage(job, salary, tax));
        _window.OnBonus += (id, amount, reason) => SendMessage(new CityConsoleBonusMessage(id, amount, reason));
        _window.OnSeize += (id, amount, reason) => SendMessage(new CityConsoleSeizeMessage(id, amount, reason));
        _window.OnSetMode += mode => SendMessage(new CityConsoleSetModeMessage(mode));
        _window.OnAnnounce += text => SendMessage(new CityConsoleAnnounceMessage(text));
        _window.OnNewLaw += () => SendMessage(new CityConsoleNewLawMessage());
        _window.OnSaveLaw += (id, number, title, text, sanction) =>
            SendMessage(new CityConsoleSaveLawMessage(id, number, title, text, sanction));
        _window.OnDeleteLaw += id => SendMessage(new CityConsoleDeleteLawMessage(id));
        _window.OnSaveSanctions += (sanctions, provision) =>
            SendMessage(new CityConsoleSaveSanctionsMessage(sanctions, provision));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CityConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
