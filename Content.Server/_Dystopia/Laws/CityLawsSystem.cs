using System.Linq;
using Content.Shared._Dystopia.Laws;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.Laws;

/// <summary>
/// Свод законов Города: хранение и правка статей и шкалы санкций, программа КПК «Свод законов».
/// Правка идёт через Консоль Управления Городом (CityConsoleSystem).
/// </summary>
public sealed partial class CityLawsSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;

    public static readonly EntProtoId CartridgePrototype = "DystopiaLawsCartridge";

    public const int MaxTitleLength = 100;
    public const int MaxTextLength = 2000;
    public const int MaxSanctionLength = 300;
    public const int MaxLaws = 100;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityLawsPreinstallComponent, MapInitEvent>(OnPreinstallMapInit);
        SubscribeLocalEvent<CityLawsCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
    }

    private void OnPreinstallMapInit(Entity<CityLawsPreinstallComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<CartridgeLoaderComponent>(ent.Owner, out var loader))
            _cartridgeLoader.InstallProgram((ent.Owner, loader), CartridgePrototype, deinstallable: false);
    }

    private void OnUiReady(Entity<CityLawsCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        _cartridgeLoader.UpdateCartridgeUiState(args.Loader, GetUiState());
    }

    /// <summary>Свод законов Города (у нас одна станция).</summary>
    public bool TryGetLaws(out Entity<CityLawsComponent> laws)
    {
        var query = EntityQueryEnumerator<CityLawsComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            EnsureIds(comp);
            laws = (uid, comp);
            return true;
        }

        laws = default;
        return false;
    }

    /// <summary>Статьям Основного закона из YAML назначаются внутренние идентификаторы.</summary>
    private static void EnsureIds(CityLawsComponent comp)
    {
        foreach (var law in comp.Laws)
        {
            if (law.Id != 0)
                continue;

            law.Id = comp.NextId++;
        }
    }

    public List<CityLaw> GetSortedLaws(CityLawsComponent comp)
    {
        return comp.Laws.OrderBy(l => l.Number).ThenBy(l => l.Id).Select(l => l.Clone()).ToList();
    }

    public CityLawsUiState GetUiState()
    {
        if (!TryGetLaws(out var laws))
            return new CityLawsUiState(new(), new(), string.Empty);

        return new CityLawsUiState(
            GetSortedLaws(laws.Comp),
            laws.Comp.Sanctions.Select(s => s.Clone()).ToList(),
            laws.Comp.GeneralProvision);
    }

    public CityLaw? CreateLaw(Entity<CityLawsComponent> laws, TimeSpan now)
    {
        if (laws.Comp.Laws.Count >= MaxLaws)
            return null;

        var number = laws.Comp.Laws.Count == 0 ? 1 : laws.Comp.Laws.Max(l => l.Number) + 1;
        var law = new CityLaw
        {
            Id = laws.Comp.NextId++,
            Number = number,
            Title = Loc.GetString("dystopia-laws-new-title"),
            EnactedAt = now,
        };

        laws.Comp.Laws.Add(law);
        UpdateAllCartridges();
        return law;
    }

    public CityLaw? UpdateLaw(Entity<CityLawsComponent> laws, int id, int number, string title, string text, string sanction, TimeSpan now)
    {
        var law = laws.Comp.Laws.FirstOrDefault(l => l.Id == id);
        if (law == null)
            return null;

        law.Number = Math.Clamp(number, 1, 999);
        law.Title = Trim(title, MaxTitleLength);
        law.Text = Trim(text, MaxTextLength);
        law.Sanction = Trim(sanction, MaxSanctionLength);
        law.EnactedAt = now;

        UpdateAllCartridges();
        return law;
    }

    public CityLaw? DeleteLaw(Entity<CityLawsComponent> laws, int id)
    {
        var law = laws.Comp.Laws.FirstOrDefault(l => l.Id == id);
        if (law == null)
            return null;

        laws.Comp.Laws.Remove(law);
        UpdateAllCartridges();
        return law;
    }

    public void SetSanctions(Entity<CityLawsComponent> laws, List<CitySanctionClass> sanctions, string generalProvision)
    {
        // Меняются только тексты наказаний; набор классов остаётся тем, что задан в прототипе.
        foreach (var incoming in sanctions)
        {
            var existing = laws.Comp.Sanctions.FirstOrDefault(s => s.Class == incoming.Class);
            if (existing == null)
                continue;

            existing.Legal = Trim(incoming.Legal, MaxSanctionLength);
            existing.Disciplinary = Trim(incoming.Disciplinary, MaxSanctionLength);
        }

        laws.Comp.GeneralProvision = Trim(generalProvision, MaxSanctionLength);
        UpdateAllCartridges();
    }

    /// <summary>Обновить открытые программы «Свод законов» во всех КПК.</summary>
    public void UpdateAllCartridges()
    {
        var state = GetUiState();
        var query = EntityQueryEnumerator<CityLawsCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out _, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loader ||
                !TryComp<CartridgeLoaderComponent>(loader, out var loaderComp) ||
                loaderComp.ActiveProgram != uid)
            {
                continue;
            }

            _cartridgeLoader.UpdateCartridgeUiState(loader, state, loaderComp);
        }
    }

    private static string Trim(string value, int max)
    {
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}
