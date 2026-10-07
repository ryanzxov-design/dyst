using Content.Shared._Goobstation.Disease;
using Robust.Shared.Timing;

namespace Content.Shared._Goobstation.Disease.Chemistry
{
    public sealed partial class MetabolismImmunityModifierSystem : EntitySystem
    {
        [Dependency] private IGameTiming _gameTiming = default!;

        private readonly List<Entity<ImmunityModifierMetabolismComponent>> _components = new();

        public override void Initialize()
        {
            base.Initialize();

            UpdatesOutsidePrediction = true;

            SubscribeLocalEvent<ImmunityModifierMetabolismComponent, ComponentStartup>(AddComponent);
            SubscribeLocalEvent<ImmunityModifierMetabolismComponent, GetImmunityEvent>(OnGetImmunity);
        }

        // Dystopia: событие передаётся по ссылке — обработчик тоже по ссылке
        private void OnGetImmunity(Entity<ImmunityModifierMetabolismComponent> ent, ref GetImmunityEvent args)
        {
            args.ImmunityGainRate += ent.Comp.GainRateModifier;
            args.ImmunityStrength += ent.Comp.StrengthModifier;
        }

        private void AddComponent(Entity<ImmunityModifierMetabolismComponent> metabolism, ref ComponentStartup args)
        {
            _components.Add(metabolism);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var currentTime = _gameTiming.CurTime;

            for (var i = _components.Count - 1; i >= 0; i--)
            {
                var metabolism = _components[i];

                if (metabolism.Comp.Deleted)
                {
                    _components.RemoveAt(i);
                    continue;
                }

                if (metabolism.Comp.ModifierTimer > currentTime)
                    continue;

                _components.RemoveAt(i);
                RemComp<ImmunityModifierMetabolismComponent>(metabolism);
            }
        }
    }
}
