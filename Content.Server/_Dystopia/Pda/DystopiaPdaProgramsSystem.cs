using Content.Shared._Dystopia.Pda;
using Content.Shared.CartridgeLoader;

namespace Content.Server._Dystopia.Pda;

/// <summary>Ставит программы Города в КПК при его появлении.</summary>
public sealed partial class DystopiaPdaProgramsSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaPdaProgramsComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DystopiaPdaProgramsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<CartridgeLoaderComponent>(ent.Owner, out var loader))
            return;

        foreach (var program in ent.Comp.Programs)
        {
            _cartridgeLoader.InstallProgram((ent.Owner, loader), program, deinstallable: false);
        }
    }
}
