using System.Linq;
using Content.Server.Popups;
using Content.Shared._Dystopia.Economy;
using Content.Shared.CartridgeLoader;

namespace Content.Server._Dystopia.Economy;

/// <summary>
/// Программа КПК «Банк». Показывает счёт карты, лежащей в КПК, и делает переводы.
/// Кто держит КПК с картой — тот и распоряжается счётом.
/// </summary>
public sealed partial class CityBankCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private CityBankSystem _bank = default!;
    [Dependency] private PopupSystem _popup = default!;

    private const int MaxCommentLength = 80;
    private float _refreshAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityBankCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<CityBankCartridgeComponent, CartridgeMessageEvent>(OnMessage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Раз в 2 секунды обновляем открытые программы: зарплаты, входящие переводы, штрафы.
        _refreshAccumulator += frameTime;
        if (_refreshAccumulator < 2f)
            return;
        _refreshAccumulator = 0f;

        var query = EntityQueryEnumerator<CityBankCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out _, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loader ||
                !TryComp<CartridgeLoaderComponent>(loader, out var loaderComp) ||
                loaderComp.ActiveProgram != uid)
            {
                continue;
            }

            UpdateUi(loader);
        }
    }

    private void OnUiReady(Entity<CityBankCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUi(args.Loader);
    }

    private void OnMessage(EntityUid uid, CityBankCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not CityBankTransferMessageEvent transfer)
            return;

        var loader = GetEntity(args.LoaderUid);
        if (!_bank.TryGetAccount(loader, out var bank, out var account))
            return;

        var comment = transfer.Comment.Trim();
        if (comment.Length > MaxCommentLength)
            comment = comment[..MaxCommentLength];

        var result = _bank.Transfer(bank, account, transfer.ToAccount, transfer.Amount, comment);
        var popup = result switch
        {
            CityBankSystem.TransferResult.Success => Loc.GetString("dystopia-bank-transfer-success",
                ("amount", transfer.Amount), ("id", transfer.ToAccount)),
            CityBankSystem.TransferResult.Frozen => Loc.GetString("dystopia-bank-transfer-frozen"),
            CityBankSystem.TransferResult.RecipientFrozen => Loc.GetString("dystopia-bank-transfer-recipient-frozen"),
            CityBankSystem.TransferResult.NotEnoughMoney => Loc.GetString("dystopia-bank-transfer-no-money"),
            CityBankSystem.TransferResult.NoRecipient => Loc.GetString("dystopia-bank-transfer-no-recipient"),
            CityBankSystem.TransferResult.SameAccount => Loc.GetString("dystopia-bank-transfer-same"),
            _ => Loc.GetString("dystopia-bank-transfer-bad-amount"),
        };

        _popup.PopupEntity(popup, loader, args.Actor);
        UpdateUi(loader);
    }

    private void UpdateUi(EntityUid loader)
    {
        CityBankUiState state;
        if (_bank.TryGetAccount(loader, out _, out var account))
        {
            state = new CityBankUiState(true, account.Id, account.Name, account.Balance, account.Debt, account.Frozen,
                account.History.AsEnumerable().Reverse().ToList());
        }
        else
        {
            state = new CityBankUiState(false, 0, string.Empty, 0, 0, false, new());
        }

        _cartridgeLoader.UpdateCartridgeUiState(loader, state);
    }
}
