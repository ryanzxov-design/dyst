// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: горн — топливо, розжиг, меха, нагрев стоящего на нём тигля.

using Content.Shared._Dystopia.Industry;
using Content.Shared.Audio;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Temperature;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaForgeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private DystopiaCrucibleSystem _crucible = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaForgeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaForgeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaForgeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<DystopiaForgeComponent, DystopiaForgeBellowsDoAfterEvent>(OnBellows);
        SubscribeLocalEvent<DystopiaForgeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<DystopiaForgeComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent, false);
    }

    // ------------------------------------------------------------------ топливо и розжиг

    private void OnInteractUsing(Entity<DystopiaForgeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
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
        var perUnit = GetFuelValue(ent.Comp, args.Used);
        if (perUnit <= 0)
            return; // не топливо: пусть поставят на горн (PlaceableSurface)

        args.Handled = true;
        var space = ent.Comp.MaxFuel - ent.Comp.Fuel;
        var count = TryComp<StackComponent>(args.Used, out var stack) ? stack.Count : 1;
        var take = Math.Min(count, (int) MathF.Floor(space / perUnit));
        if (take <= 0)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-forge-fuel-full"), ent, args.User);
            return;
        }

        if (stack != null && take < count)
            _stack.SetCount((args.Used, stack), count - take);
        else
            QueueDel(args.Used);

        ent.Comp.Fuel += take * perUnit;
        _popup.PopupEntity(Loc.GetString("dystopia-forge-fuel-added",
            ("seconds", (int) MathF.Round(ent.Comp.Fuel))), ent, args.User);
    }

    /// <summary>Секунд горения в одной единице предмета (одном куске стопки).</summary>
    private float GetFuelValue(DystopiaForgeComponent forge, EntityUid item)
    {
        if (!TryComp<PhysicalCompositionComponent>(item, out var composition))
            return 0f;

        var total = 0f;
        foreach (var (material, amount) in composition.MaterialComposition)
        {
            if (forge.FuelMaterials.TryGetValue(material, out var per100))
                total += per100 * amount / 100f;
        }

        return total;
    }

    // ------------------------------------------------------------------ меха

    private void OnInteractHand(Entity<DystopiaForgeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!ent.Comp.Lit)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-forge-not-lit"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.BellowsDelay,
            new DystopiaForgeBellowsDoAfterEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnBellows(Entity<DystopiaForgeComponent> ent, ref DystopiaForgeBellowsDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !ent.Comp.Lit)
            return;

        args.Handled = true;
        ent.Comp.BellowsUntil = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.BellowsDuration);
        UpdateVisuals(ent, true);
        args.Repeat = true; // качают, пока не отойдут
    }

    // ------------------------------------------------------------------ горение

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaForgeComponent, ItemPlacerComponent>();
        while (query.MoveNext(out var uid, out var forge, out var placer))
        {
            if (!forge.Lit || now < forge.NextUpdate)
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

            var target = bellows ? forge.BellowsMaxTemperature : forge.BaseMaxTemperature;
            var until = now + TimeSpan.FromSeconds(forge.UpdateInterval * 2);
            foreach (var placed in placer.PlacedEntities)
            {
                if (TryComp<DystopiaCrucibleComponent>(placed, out var crucible))
                    _crucible.ApplyHeat((placed, crucible), target, forge.HeatRate, until);
            }
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

    private void OnExamined(Entity<DystopiaForgeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var bellows = ent.Comp.Lit && _timing.CurTime < ent.Comp.BellowsUntil;
        args.PushMarkup(Loc.GetString(ent.Comp.Lit ? "dystopia-forge-examine-lit" : "dystopia-forge-examine-cold"));
        args.PushMarkup(Loc.GetString("dystopia-forge-examine-fuel", ("seconds", (int) MathF.Round(ent.Comp.Fuel))));
        args.PushMarkup(Loc.GetString("dystopia-forge-examine-max",
            ("base", MathF.Round(ent.Comp.BaseMaxTemperature - DystopiaCrucibleSystem.KelvinOffset)),
            ("bellows", MathF.Round(ent.Comp.BellowsMaxTemperature - DystopiaCrucibleSystem.KelvinOffset))));
        if (bellows)
            args.PushMarkup(Loc.GetString("dystopia-forge-examine-bellows"));
    }
}
