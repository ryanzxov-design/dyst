using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private DamageExamineSystem _damageExamine = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Armor.Penetration.ArmorPenetrationSystem _armorPenetration = default!; // Dystopia-Health

    // needed for server system
    protected virtual void InitializeCartridge()
    {
        SubscribeLocalEvent<CartridgeAmmoComponent, ExaminedEvent>(OnCartridgeExamine);
        SubscribeLocalEvent<CartridgeAmmoComponent, DamageExamineEvent>(OnCartridgeDamageExamine);
    }

    private void OnCartridgeExamine(Entity<CartridgeAmmoComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Spent
            ? Loc.GetString("gun-cartridge-spent")
            : Loc.GetString("gun-cartridge-unspent"));
    }

    private void OnCartridgeDamageExamine(Entity<CartridgeAmmoComponent> ent, ref DamageExamineEvent args)
    {
        var damageSpec = GetProjectileDamage(ent.Comp.Prototype);

        if (damageSpec == null)
            return;

        _damageExamine.AddDamageExamine(args.Message, Damageable.ApplyUniversalAllModifiers(damageSpec), Loc.GetString("damage-projectile"));
        _armorPenetration.AddProjectileExamine(args.Message, ent.Comp.Prototype); // Dystopia-Health
    }

    private DamageSpecifier? GetProjectileDamage(EntProtoId proto)
    {
        if (!ProtoMan.TryIndex(proto, out var entityProto))
            return null;

        if (!entityProto.TryComp<ProjectileComponent>(out var projectile, Factory))
            return null;

        if (!projectile.Damage.Empty)
            return projectile.Damage * Damageable.UniversalProjectileDamageModifier;

        return null;
    }
}
