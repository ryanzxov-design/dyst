// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: программа КПК «Мой статус». Показывает дело владельца карты, лежащей в КПК (как «Банк» — счёт этой
// карты): статус, лояльность, должность и карту.

using Content.Shared._Dystopia.Economy;
using Content.Shared._Dystopia.Registry;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Registry;

public sealed partial class CityStatusCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private CityRegistrySystem _registry = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityStatusCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // открытые программы обновляются сами: статус, лояльность и журнал меняются в консолях
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CityStatusCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var status, out var cartridge))
        {
            if (now < status.NextRefresh)
                continue;
            status.NextRefresh = now + status.RefreshInterval;

            if (cartridge.LoaderUid is not { } loader ||
                !TryComp<CartridgeLoaderComponent>(loader, out var loaderComp) ||
                loaderComp.ActiveProgram != uid)
            {
                continue;
            }

            UpdateUi(status, loader);
        }
    }

    private void OnUiReady(Entity<CityStatusCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUi(ent.Comp, args.Loader);
    }

    private void UpdateUi(CityStatusCartridgeComponent comp, EntityUid loader)
    {
        _cartridgeLoader.UpdateCartridgeUiState(loader, BuildState(comp, loader));
    }

    private CityStatusUiState BuildState(CityStatusCartridgeComponent comp, EntityUid loader)
    {
        var state = new CityStatusUiState();

        if (!TryComp<PdaComponent>(loader, out var pda) || pda.ContainedId is not { } card ||
            !TryComp<CityBankCardComponent>(card, out var bankCard) || bankCard.AccountId is not { } number ||
            !_registry.TryGetRegistry(out var registry) || !_registry.TryGetRecord(number, out var record))
        {
            return state;
        }

        var outside = CityRegistrySystem.IsOutsideLoyalty(registry, record);

        state.HasRecord = true;
        state.Number = record.Number;
        state.Name = record.Name;
        state.JobTitle = record.Job is { } job && _prototype.TryIndex(job, out var jobProto) ? jobProto.LocalizedName : "—";
        state.Department = record.Department is { } dep && _prototype.TryIndex(dep, out var depProto)
            ? Loc.GetString(depProto.Name)
            : "—";
        state.Status = record.Status;
        state.StatusReason = comp.ShowStatusReason ? record.StatusReason : string.Empty;
        state.Dismissed = record.Dismissed;
        state.Outside = outside;
        state.Loyalty = record.Loyalty;
        state.LoyaltyNorm = record.LoyaltyNorm;
        state.MaxLoyalty = registry.Comp.MaxLoyalty;

        if (TryComp<CityIdCardComponent>(card, out var idCard))
        {
            state.CardSerial = idCard.Serial;
            state.CardAnnulled = idCard.Annulled;
        }

        return state;
    }
}
