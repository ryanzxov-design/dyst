// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: горн — топливо, розжиг, нагрев стоящего на нём тигля. Раздувают его отдельные меха (DystopiaBellowsSystem).
// Электрический горн (после комплекта ТЭНов): без топлива и огня, греет, пока включён и есть ток.

using Content.Server.Power.EntitySystems;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Temperature;
using Content.Shared.Verbs;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaForgeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private DystopiaCrucibleSystem _crucible = default!;
    [Dependency] private DystopiaForgeMountSystem _mount = default!;
    [Dependency] private PowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaForgeComponent, MapInitEvent>(OnMapInit);
        // InteractUsing идёт раньше, чем горн-поверхность ставит предмет на себя (AfterInteractUsing):
        // большой тигель ставится установкой, топливо уходит в топку.
        SubscribeLocalEvent<DystopiaForgeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaForgeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaForgeComponent, DystopiaConveyorInsertEvent>(OnConveyorInsert);
        SubscribeLocalEvent<DystopiaForgeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnMapInit(Entity<DystopiaForgeComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent, false);
        if (ent.Comp.Electric)
            _power.SetLoad(ent.Owner, ent.Comp.On ? ent.Comp.PowerLoad : ent.Comp.IdlePowerLoad);
    }

    // ------------------------------------------------------------------ электрический: включить/выключить

    private void OnGetVerbs(Entity<DystopiaForgeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ent.Comp.Electric || !args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.On ? "dystopia-forge-electric-off-verb" : "dystopia-forge-electric-on-verb"),
            Act = () => SetOn(ent, !ent.Comp.On, user),
            Priority = 3,
        });
    }

    public void SetOn(Entity<DystopiaForgeComponent> ent, bool on, EntityUid? user)
    {
        ent.Comp.On = on;
        _power.SetLoad(ent.Owner, on ? ent.Comp.PowerLoad : ent.Comp.IdlePowerLoad);
        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString(on ? "dystopia-forge-electric-turned-on" : "dystopia-forge-electric-turned-off"),
                ent, user.Value);
        }
    }

    /// <summary>Меха раздули горн: seconds секунд он держит жар до BellowsMaxTemperature.</summary>
    public bool Blow(Entity<DystopiaForgeComponent> ent, float seconds)
    {
        if (!ent.Comp.Lit || ent.Comp.Electric) // электрическому меха ни к чему
            return false;

        var until = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        if (until > ent.Comp.BellowsUntil)
            ent.Comp.BellowsUntil = until;

        UpdateVisuals(ent, true);
        return true;
    }

    // ------------------------------------------------------------------ топливо и розжиг

    private void OnInteractUsing(Entity<DystopiaForgeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Большой тигель — установка на горн.
        if (HasComp<DystopiaForgeMountableComponent>(args.Used))
        {
            args.Handled = true;
            _mount.TryStartMount(ent, args.Used, args.User);
            return;
        }

        // Комплект ТЭНов — горн станет электрическим.
        if (HasComp<DystopiaForgeUpgradeComponent>(args.Used))
        {
            args.Handled = true;
            _mount.TryStartUpgrade(ent, args.Used, args.User);
            return;
        }

        // Электрическому горну ни огонь, ни топливо не нужны.
        if (ent.Comp.Electric)
            return;

        // Огонь (зажигалка, спичка, горелка) — разжечь.
        var hot = new IsHotEvent();
        RaiseLocalEvent(args.Used, hot);
        if (hot.IsHot)
        {
            args.Handled = true;
            if (ent.Comp.Lit)
                return;

            if (ent.Comp.Fuel <= 0)
            {
                _popup.PopupEntity(Loc.GetString("dystopia-forge-no-fuel"), ent, args.User);
                return;
            }

            ent.Comp.Lit = true;
            UpdateVisuals(ent, false);
            _popup.PopupEntity(Loc.GetString("dystopia-forge-lit"), ent, args.User);
            return;
        }

        // Топливо — по материалу (уголь, доски...).
        if (GetFuelValue(ent.Comp, args.Used) <= 0)
            return; // не топливо: пусть поставят на горн (PlaceableSurface)

        args.Handled = true;
        TryAddFuel(ent, args.Used, args.User);
    }

    // Конвейерный вставщик подкидывает топливо в обычный горн (в горн с тиглем всё идёт в тигель).
    private void OnConveyorInsert(Entity<DystopiaForgeComponent> ent, ref DystopiaConveyorInsertEvent args)
    {
        if (args.Handled || ent.Comp.Electric || HasComp<DystopiaCrucibleComponent>(ent) ||
            GetFuelValue(ent.Comp, args.Item) <= 0)
            return;

        args.Handled = true;
        args.Inserted = TryAddFuel(ent, args.Item, null);
    }

    /// <summary>Закинуть топливо (сколько влезет из стопки). Возвращает, закинуто ли.</summary>
    public bool TryAddFuel(Entity<DystopiaForgeComponent> ent, EntityUid item, EntityUid? user)
    {
        var perUnit = GetFuelValue(ent.Comp, item);
        if (perUnit <= 0)
            return false;

        var space = ent.Comp.MaxFuel - ent.Comp.Fuel;
        var count = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
        var take = Math.Min(count, (int) MathF.Floor(space / perUnit));
        if (take <= 0)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dystopia-forge-fuel-full"), ent, user.Value);
            return false;
        }

        if (stack != null && take < count)
            _stack.SetCount((item, stack), count - take);
        else
            QueueDel(item);

        ent.Comp.Fuel += take * perUnit;
        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-forge-fuel-added",
                ("seconds", (int) MathF.Round(ent.Comp.Fuel))), ent, user.Value);
        }

        return true;
    }

    /// <summary>Секунд горения в одной единице предмета (одном куске стопки).</summary>
    private float GetFuelValue(DystopiaForgeComponent forge, EntityUid item)
    {
        return TryComp<PhysicalCompositionComponent>(item, out var composition) ? FuelValue(forge, composition) : 0f;
    }

    /// <summary>Секунд горения на единицу предмета такого состава (0 — не топливо).</summary>
    public static float FuelValue(DystopiaForgeComponent forge, PhysicalCompositionComponent composition)
    {
        var total = 0f;
        foreach (var (material, amount) in composition.MaterialComposition)
        {
            if (forge.FuelMaterials.TryGetValue(material, out var per100))
                total += per100 * amount / 100f;
        }

        return total;
    }

    // ------------------------------------------------------------------ горение

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaForgeComponent>();
        while (query.MoveNext(out var uid, out var forge))
        {
            if (now < forge.NextUpdate)
                continue;

            float target;
            if (forge.Electric)
            {
                // Греет, пока включён и есть ток.
                var lit = forge.On && _power.IsPowered(uid);
                if (lit != forge.Lit)
                {
                    forge.Lit = lit;
                    UpdateVisuals((uid, forge), false);
                }

                if (!lit)
                    continue;

                forge.NextUpdate = now + TimeSpan.FromSeconds(forge.UpdateInterval);
                target = forge.BaseMaxTemperature;
            }
            else
            {
                if (!forge.Lit)
                    continue;

                forge.NextUpdate = now + TimeSpan.FromSeconds(forge.UpdateInterval);
                var bellows = now < forge.BellowsUntil;
                forge.Fuel -= forge.UpdateInterval * (bellows ? forge.BellowsFuelMultiplier : 1f);
                if (forge.Fuel <= 0)
                {
                    forge.Fuel = 0;
                    forge.Lit = false;
                    UpdateVisuals((uid, forge), false);
                    continue;
                }

                UpdateVisuals((uid, forge), bellows);
                target = bellows ? forge.BellowsMaxTemperature : forge.BaseMaxTemperature;
            }

            var until = now + TimeSpan.FromSeconds(forge.UpdateInterval * 2);
            // Тигель, поставленный на горн, и тигель, встроенный в горн (горн с большим тиглем).
            if (TryComp<ItemPlacerComponent>(uid, out var placer))
            {
                foreach (var placed in placer.PlacedEntities)
                {
                    if (TryComp<DystopiaCrucibleComponent>(placed, out var crucible))
                        _crucible.ApplyHeat((placed, crucible), target, forge.HeatRate, until);
                }
            }

            if (TryComp<DystopiaCrucibleComponent>(uid, out var own))
                _crucible.ApplyHeat((uid, own), target, forge.HeatRate, until);
        }
    }

    private void UpdateVisuals(Entity<DystopiaForgeComponent> ent, bool bellows)
    {
        _appearance.SetData(ent, DystopiaForgeVisuals.Lit, ent.Comp.Lit);
        _appearance.SetData(ent, DystopiaForgeVisuals.Bellows, ent.Comp.Lit && bellows);

        if (_light.TryGetLight(ent, out var light))
            _light.SetEnabled(ent, ent.Comp.Lit, light);

        _ambient.SetAmbience(ent, ent.Comp.Lit);
    }

    private const int ExamineForge = 20;
    private const int ExamineWarning = 25;

    private void OnExamined(Entity<DystopiaForgeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        ExamineHeatWarning(ent, ref args);

        if (ent.Comp.Electric)
        {
            args.PushMarkup(Loc.GetString(ent.Comp.Lit ? "dystopia-forge-electric-examine-working"
                : ent.Comp.On ? "dystopia-forge-electric-examine-unpowered"
                : "dystopia-forge-electric-examine-off"), ExamineForge);
            args.PushMarkup(Loc.GetString("dystopia-forge-electric-examine-max",
                ("max", MathF.Round(ent.Comp.BaseMaxTemperature - DystopiaCrucibleSystem.KelvinOffset))), ExamineForge - 1);
            return;
        }

        var bellows = ent.Comp.Lit && _timing.CurTime < ent.Comp.BellowsUntil;
        args.PushMarkup(Loc.GetString(ent.Comp.Lit ? "dystopia-forge-examine-lit" : "dystopia-forge-examine-cold"),
            ExamineForge);
        args.PushMarkup(Loc.GetString("dystopia-forge-examine-fuel", ("seconds", (int) MathF.Round(ent.Comp.Fuel))),
            ExamineForge - 1);
        args.PushMarkup(Loc.GetString("dystopia-forge-examine-max",
            ("base", MathF.Round(ent.Comp.BaseMaxTemperature - DystopiaCrucibleSystem.KelvinOffset)),
            ("bellows", MathF.Round(ent.Comp.BellowsMaxTemperature - DystopiaCrucibleSystem.KelvinOffset))),
            ExamineForge - 2);
        if (bellows)
            args.PushMarkup(Loc.GetString("dystopia-forge-examine-bellows"), ExamineForge - 3);
    }

    /// <summary>Тигель на горне (или в горне) ждёт жара, которого горн сам не даст, — подсказать, что делать.</summary>
    private void ExamineHeatWarning(Entity<DystopiaForgeComponent> ent, ref ExaminedEvent args)
    {
        float? needed = null;
        if (TryComp<DystopiaCrucibleComponent>(ent, out var own))
            needed = _crucible.RequiredTemperature((ent.Owner, own));

        if (TryComp<ItemPlacerComponent>(ent, out var placer))
        {
            foreach (var placed in placer.PlacedEntities)
            {
                if (TryComp<DystopiaCrucibleComponent>(placed, out var crucible) &&
                    _crucible.RequiredTemperature((placed, crucible)) is { } t)
                {
                    needed = MathF.Max(needed ?? 0f, t);
                }
            }
        }

        if (needed is not { } need)
            return;

        var max = ent.Comp.Electric ? ent.Comp.BaseMaxTemperature : ent.Comp.BellowsMaxTemperature;
        var celsius = MathF.Round(need - DystopiaCrucibleSystem.KelvinOffset);
        if (need > max)
        {
            args.PushMarkup(Loc.GetString("dystopia-forge-examine-too-cold", ("temperature", celsius)), ExamineWarning);
        }
        else if (!ent.Comp.Electric && need > ent.Comp.BaseMaxTemperature)
        {
            args.PushMarkup(Loc.GetString("dystopia-forge-examine-need-bellows", ("temperature", celsius)), ExamineWarning);
        }
    }
}
