using Content.Shared._Goobstation.Disease.Chemistry;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Goobstation.EntityEffects.Disease;

/// <summary>
/// Modifies the entity's immunity's strength, with accumulation.
/// </summary>
// Dystopia: в Goob эффект ждал, что компонент модификатора уже есть на теле, но его никто не добавлял —
// иммурин и витамины не работали. Действуем на всех, у кого есть иммунитет, и добавляем компонент сами.
public sealed partial class ImmunityModifierSystem : EntityEffectSystem<Content.Shared._Goobstation.Disease.Components.ImmunityComponent, ImmunityModifier>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<Content.Shared._Goobstation.Disease.Components.ImmunityComponent> entity, ref EntityEffectEvent<ImmunityModifier> args)
    {
        var comp = EnsureComp<ImmunityModifierMetabolismComponent>(entity);

        comp.GainRateModifier = args.Effect.GainRateModifier;
        comp.StrengthModifier = args.Effect.StrengthModifier;

        var time = args.Effect.StatusLifetime * args.Scale;

        var offsetTime = Math.Max(comp.ModifierTimer.TotalSeconds, _timing.CurTime.TotalSeconds);
        comp.ModifierTimer = TimeSpan.FromSeconds(offsetTime + time);

        Dirty(entity.Owner, comp);
    }
}

public sealed partial class ImmunityModifier : EntityEffectBase<ImmunityModifier>
{
    /// <summary>
    /// How much to add to the immunity's gain rate.
    /// </summary>
    [DataField]
    public float GainRateModifier = 0.002f;

    /// <summary>
    /// How much to add to the immunity's strength.
    /// </summary>
    [DataField]
    public float StrengthModifier = 0.02f;

    /// <summary>
    /// How long the modifier applies (in seconds).
    /// </summary>
    [DataField]
    public float StatusLifetime = 2f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-immunity-modifier",
            ("chance", Probability),
            ("gainrate", GainRateModifier),
            ("strength", StrengthModifier),
            ("time", StatusLifetime));
    }
}
