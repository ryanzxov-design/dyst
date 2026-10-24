// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: паровой котёл с топкой (см. DystopiaBoilerComponent).
// Раз в такт: топка жжёт топливо по режиму и прогревает котёл; прогретый котёл варит из воды пар нужной температуры
// в барабан; из барабана пар уходит в газовую трубу, пока давление в ней ниже предела. Некуда уйти — давление растёт:
// тревога, скрип, урон, на пределе — взрыв (Destructible + Explosive в прототипе). Стравить пар — вручную, в воздух
// под котлом. Горит без воды — урон трубам.

using Content.Server.Atmos.EntitySystems;
using Content.Server.Destructible;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Temperature;
using Content.Shared.Tools.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaBoilerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private DystopiaMachinePortsSystem _ports = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaBoilerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaBoilerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaBoilerComponent, DystopiaConveyorInsertEvent>(OnConveyorInsert);
        SubscribeLocalEvent<DystopiaBoilerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaBoilerComponent, BoundUIOpenedEvent>(OnUiOpened);
        Subs.BuiEvents<DystopiaBoilerComponent>(DystopiaBoilerUiKey.Key, subs =>
        {
            subs.Event<DystopiaBoilerSetModeMessage>(OnSetMode);
            subs.Event<DystopiaBoilerToggleVentMessage>(OnToggleVent);
            subs.Event<DystopiaBoilerExtinguishMessage>(OnExtinguish);
        });
    }

    private void OnMapInit(Entity<DystopiaBoilerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Mode = Math.Clamp(ent.Comp.Mode, 0, Math.Max(0, ent.Comp.Modes.Count - 1));
        UpdateVisuals(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaBoilerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var boiler, out var xform))
        {
            if (now < boiler.NextUpdate)
                continue;

            boiler.NextUpdate = now + TimeSpan.FromSeconds(boiler.Interval);
            Tick((uid, boiler), xform, boiler.Interval);

            if (_ui.IsUiOpen(uid, DystopiaBoilerUiKey.Key))
                UpdateUi((uid, boiler));
        }
    }

    // ------------------------------------------------------------------ работа

    private DystopiaBoilerMode? CurrentMode(DystopiaBoilerComponent boiler)
    {
        if (boiler.Modes.Count == 0)
            return null;

        return boiler.Modes[Math.Clamp(boiler.Mode, 0, boiler.Modes.Count - 1)];
    }

    /// <summary>Температура пара в барабане, K.</summary>
    private float SteamKelvin(DystopiaBoilerComponent boiler)
    {
        return (CurrentMode(boiler)?.Temperature ?? 100f) + Atmospherics.T0C;
    }

    /// <summary>Давление в барабане, кПа (P = nRT/V).</summary>
    public float Pressure(DystopiaBoilerComponent boiler)
    {
        return boiler.SteamMoles * Atmospherics.R * SteamKelvin(boiler) / MathF.Max(1f, boiler.DrumVolume);
    }

    private void Tick(Entity<DystopiaBoilerComponent> ent, TransformComponent xform, float dt)
    {
        var boiler = ent.Comp;
        boiler.ProducedRate = 0f;
        boiler.WaterRate = 0f;
        boiler.FuelRate = 0f;
        boiler.Dry = false;
        var wasLit = boiler.Lit;

        if (CurrentMode(boiler) is not { } mode)
            return;

        // Топка
        if (boiler.Lit)
        {
            var burn = mode.Fuel * dt;
            boiler.FuelRate = mode.Fuel;
            boiler.Fuel -= burn;
            if (boiler.Fuel <= 0f)
            {
                boiler.Fuel = 0f;
                boiler.Lit = false;
            }

            boiler.Heat += dt / MathF.Max(1f, boiler.WarmupTime);
        }
        else
        {
            boiler.Heat -= dt / MathF.Max(1f, boiler.CooldownTime);
        }

        boiler.Heat = Math.Clamp(boiler.Heat, 0f, 1f);

        // Пар
        var factor = Math.Clamp((boiler.Heat - boiler.SteamFromHeat) / MathF.Max(0.01f, 1f - boiler.SteamFromHeat), 0f, 1f);
        if (boiler.Lit && factor > 0f)
        {
            var want = mode.Steam * factor * dt;
            var needUnits = want / MathF.Max(0.001f, boiler.SteamPerWaterUnit);
            var units = 0f;
            if (_solution.TryGetSolution(ent.Owner, boiler.WaterSolution, out var waterEnt, out var water))
            {
                units = MathF.Min(needUnits, water.GetTotalPrototypeQuantity(boiler.Water).Float());
                if (units > 0.01f)
                    _solution.RemoveReagent(waterEnt.Value, boiler.Water.Id, FixedPoint2.New(units));
                else
                    units = 0f;
            }

            var moles = units * boiler.SteamPerWaterUnit;
            boiler.SteamMoles += moles;
            boiler.ProducedRate = moles / dt;
            boiler.WaterRate = units / dt;
            boiler.Dry = units < needUnits * 0.5f;
        }

        if (boiler.Dry)
            boiler.PendingDry += boiler.DryDamageRate * dt;

        ToPipe(ent, dt);
        Vent(ent, xform, dt);
        CheckPressure(ent, dt);
        ApplyDamage(ent);

        if (wasLit != boiler.Lit)
            UpdateVisuals(ent);
    }

    /// <summary>Пар из барабана — в газовую трубу, пока давление в ней ниже предела.</summary>
    private void ToPipe(Entity<DystopiaBoilerComponent> ent, float dt)
    {
        var boiler = ent.Comp;
        boiler.SteamRate = 0f;
        if (boiler.SteamMoles <= 0.001f || !TryGetPipe(ent, out var pipe) || pipe.Air.Pressure >= boiler.OutletPressure)
            return;

        var steam = new GasMixture { Temperature = SteamKelvin(boiler) };
        steam.AdjustMoles(Gas.WaterVapor, boiler.SteamMoles);
        _atmos.Merge(pipe.Air, steam);
        boiler.SteamRate = boiler.SteamMoles / dt;
        boiler.SteamMoles = 0f;
    }

    private bool TryGetPipe(Entity<DystopiaBoilerComponent> ent, out PipeNode pipe)
    {
        pipe = default!;
        if (!_ports.TryGetPort(ent, ent.Comp.SteamPort, out var port) ||
            !_nodes.TryGetNode<PipeNode>(port, ent.Comp.SteamNode, out var node))
        {
            return false;
        }

        pipe = node;
        return true;
    }

    /// <summary>Стравливание: пар — в воздух под котлом.</summary>
    private void Vent(Entity<DystopiaBoilerComponent> ent, TransformComponent xform, float dt)
    {
        var boiler = ent.Comp;
        if (!boiler.Venting || boiler.SteamMoles <= 0.001f)
            return;

        var moles = MathF.Min(boiler.SteamMoles, boiler.VentRate * dt);
        if (_atmos.GetContainingMixture((ent.Owner, xform), false, true) is { } air)
        {
            var steam = new GasMixture { Temperature = SteamKelvin(boiler) };
            steam.AdjustMoles(Gas.WaterVapor, moles);
            _atmos.Merge(air, steam);
        }

        boiler.SteamMoles -= moles;
    }

    /// <summary>Давление: выше тревожного — сирена, выше предела — скрип и урон барабану.</summary>
    private void CheckPressure(Entity<DystopiaBoilerComponent> ent, float dt)
    {
        var boiler = ent.Comp;
        var pressure = Pressure(boiler);
        var now = _timing.CurTime;

        var warn = pressure > boiler.WarnPressure;
        if (warn && !boiler.Warned)
        {
            _audio.PlayPvs(boiler.AlarmSound, ent);
            _appearance.SetData(ent, DystopiaBoilerVisuals.Alarm, true);
        }
        else if (!warn && boiler.Warned)
        {
            _appearance.SetData(ent, DystopiaBoilerVisuals.Alarm, false);
        }

        boiler.Warned = warn;

        if (pressure <= boiler.DamagePressure)
            return;

        boiler.PendingDamage += (pressure - boiler.DamagePressure) / 100f * boiler.PressureDamageRate * dt;
        if (now >= boiler.NextCreak)
        {
            _audio.PlayPvs(boiler.CreakSound, ent);
            boiler.NextCreak = now + TimeSpan.FromSeconds(4);
        }
    }

    /// <summary>Урон хранится с точностью до сотых: мелочь копим и наносим, когда наберётся.</summary>
    private void ApplyDamage(Entity<DystopiaBoilerComponent> ent)
    {
        var boiler = ent.Comp;
        if (boiler.PendingDamage >= 0.1f && !boiler.PressureDamage.Empty)
        {
            _damageable.TryChangeDamage(ent.Owner, boiler.PressureDamage * boiler.PendingDamage, interruptsDoAfters: false);
            boiler.PendingDamage = 0f;
        }

        if (boiler.PendingDry >= 0.1f && !boiler.DryDamage.Empty)
        {
            _damageable.TryChangeDamage(ent.Owner, boiler.DryDamage * boiler.PendingDry, interruptsDoAfters: false);
            boiler.PendingDry = 0f;
        }
    }

    private void UpdateVisuals(Entity<DystopiaBoilerComponent> ent)
    {
        _appearance.SetData(ent, DystopiaBoilerVisuals.Lit, ent.Comp.Lit);
        _appearance.SetData(ent, DystopiaBoilerVisuals.Venting, ent.Comp.Venting);
        if (_light.TryGetLight(ent, out var light))
            _light.SetEnabled(ent, ent.Comp.Lit, light);

        _ambient.SetAmbience(ent, ent.Comp.Lit);
    }

    // ------------------------------------------------------------------ топливо и розжиг

    private void OnInteractUsing(Entity<DystopiaBoilerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Огонь (зажигалка, спичка, горелка) — разжечь. Сваркой повреждённый котёл чинят, а не разжигают.
        var hot = new IsHotEvent();
        RaiseLocalEvent(args.Used, hot);
        if (hot.IsHot && !(HasComp<WelderComponent>(args.Used) && Damaged(ent)))
        {
            if (ent.Comp.Lit)
                return;

            args.Handled = true;
            if (ent.Comp.Fuel <= 0f)
            {
                _popup.PopupEntity(Loc.GetString("dystopia-boiler-no-fuel"), ent, args.User);
                return;
            }

            SetLit(ent, true);
            _popup.PopupEntity(Loc.GetString("dystopia-boiler-lit"), ent, args.User);
            return;
        }

        if (FuelValue(ent.Comp, args.Used) <= 0f)
            return;

        args.Handled = true;
        TryAddFuel(ent, args.Used, args.User);
    }

    private bool Damaged(EntityUid uid)
    {
        return TryComp<DamageableComponent>(uid, out var damageable) && TotalDamage((uid, damageable)) > FixedPoint2.Zero;
    }

    private void OnConveyorInsert(Entity<DystopiaBoilerComponent> ent, ref DystopiaConveyorInsertEvent args)
    {
        if (args.Handled || FuelValue(ent.Comp, args.Item) <= 0f)
            return;

        args.Handled = true;
        args.Inserted = TryAddFuel(ent, args.Item, null);
    }

    /// <summary>Секунд горения в одной единице предмета (0 — не топливо).</summary>
    private float FuelValue(DystopiaBoilerComponent boiler, EntityUid item)
    {
        if (!TryComp<PhysicalCompositionComponent>(item, out var composition))
            return 0f;

        var total = 0f;
        foreach (var (material, amount) in composition.MaterialComposition)
        {
            if (boiler.FuelMaterials.TryGetValue(material, out var per100))
                total += per100 * amount / 100f;
        }

        return total;
    }

    /// <summary>Закинуть топливо (сколько влезет из стопки).</summary>
    public bool TryAddFuel(Entity<DystopiaBoilerComponent> ent, EntityUid item, EntityUid? user)
    {
        var perUnit = FuelValue(ent.Comp, item);
        if (perUnit <= 0f)
            return false;

        var count = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
        var take = Math.Min(count, (int) MathF.Floor((ent.Comp.MaxFuel - ent.Comp.Fuel) / perUnit));
        if (take <= 0)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dystopia-boiler-fuel-full"), ent, user.Value);
            return false;
        }

        if (stack != null && take < count)
            _stack.SetCount((item, stack), count - take);
        else
            QueueDel(item);

        ent.Comp.Fuel += take * perUnit;
        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-boiler-fuel-added",
                ("seconds", (int) MathF.Round(ent.Comp.Fuel))), ent, user.Value);
        }

        return true;
    }

    /// <summary>
    /// Жидкое топливо от мазутной форсунки: добавить секунды горения (не больше места) и разжечь. Возвращает,
    /// сколько секунд принято.
    /// </summary>
    public float Feed(Entity<DystopiaBoilerComponent> ent, float seconds)
    {
        var take = MathF.Max(0f, MathF.Min(seconds, ent.Comp.MaxFuel - ent.Comp.Fuel));
        ent.Comp.Fuel += take;
        if (!ent.Comp.Lit && ent.Comp.Fuel > 0f)
            SetLit(ent, true);

        return take;
    }

    public void SetLit(Entity<DystopiaBoilerComponent> ent, bool lit)
    {
        if (ent.Comp.Lit == lit)
            return;

        ent.Comp.Lit = lit;
        UpdateVisuals(ent);
    }

    // ------------------------------------------------------------------ меню

    private void OnUiOpened(Entity<DystopiaBoilerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSetMode(Entity<DystopiaBoilerComponent> ent, ref DystopiaBoilerSetModeMessage args)
    {
        if (args.Mode < 0 || args.Mode >= ent.Comp.Modes.Count)
            return;

        ent.Comp.Mode = args.Mode;
        UpdateUi(ent);
    }

    private void OnToggleVent(Entity<DystopiaBoilerComponent> ent, ref DystopiaBoilerToggleVentMessage args)
    {
        ent.Comp.Venting = !ent.Comp.Venting;
        UpdateVisuals(ent);
        UpdateUi(ent);
    }

    private void OnExtinguish(Entity<DystopiaBoilerComponent> ent, ref DystopiaBoilerExtinguishMessage args)
    {
        SetLit(ent, false);
        UpdateUi(ent);
    }

    private DystopiaBoilerStatus Status(DystopiaBoilerComponent boiler)
    {
        if (Pressure(boiler) > boiler.WarnPressure)
            return DystopiaBoilerStatus.Danger;
        if (boiler.Dry)
            return DystopiaBoilerStatus.NoWater;
        if (boiler.ProducedRate > 0f)
            return DystopiaBoilerStatus.Producing;
        if (boiler.Lit)
            return DystopiaBoilerStatus.Warming;

        return DystopiaBoilerStatus.Cold;
    }

    /// <summary>Повреждение, доля до разрушения (порог берём у Destructible).</summary>
    private float Wear(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return 0f;

        var max = _destructible.DestroyedAt(uid);
        return max > FixedPoint2.Zero && max < FixedPoint2.MaxValue
            ? MathF.Min(1f, TotalDamage((uid, damageable)).Float() / max.Float())
            : 0f;
    }

#pragma warning disable CS0618 // Do not rely on a numerically quantifiable amount of damage — здесь это и есть повреждение
    private FixedPoint2 TotalDamage(Entity<DamageableComponent> ent)
    {
        return _damageable.GetTotalDamage((ent.Owner, ent.Comp));
    }
#pragma warning restore CS0618

    private void UpdateUi(Entity<DystopiaBoilerComponent> ent)
    {
        var boiler = ent.Comp;
        float water = 0f, waterMax = 0f;
        if (_solution.TryGetSolution(ent.Owner, boiler.WaterSolution, out _, out var waterSol))
        {
            water = waterSol.GetTotalPrototypeQuantity(boiler.Water).Float();
            waterMax = waterSol.MaxVolume.Float();
        }

        var connected = TryGetPipe(ent, out var pipe) && pipe.NodeGroup is { } group && group.Nodes.Count > 1;
        var pipePressure = TryGetPipe(ent, out pipe) ? pipe.Air.Pressure : 0f;

        _ui.SetUiState(ent.Owner, DystopiaBoilerUiKey.Key, new DystopiaBoilerUiState(Status(boiler), boiler.Lit,
            boiler.Fuel, boiler.MaxFuel, boiler.Modes, boiler.Mode, boiler.Heat, Pressure(boiler), boiler.WarnPressure,
            boiler.DamagePressure, boiler.SteamMoles, boiler.SteamRate, boiler.ProducedRate, connected, pipePressure,
            water, waterMax, boiler.WaterRate, boiler.FuelRate, boiler.Venting, Wear(ent)));
    }

    // ------------------------------------------------------------------ осмотр

    private void OnExamined(Entity<DystopiaBoilerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var boiler = ent.Comp;
        using (args.PushGroup(nameof(DystopiaBoilerSystem)))
        {
            args.PushMarkup(Loc.GetString(boiler.Lit ? "dystopia-boiler-examine-lit" : "dystopia-boiler-examine-cold",
                ("fuel", (int) MathF.Round(boiler.Fuel))));
            args.PushMarkup(Loc.GetString("dystopia-boiler-examine-state",
                ("temperature", (int) (CurrentMode(boiler)?.Temperature ?? 0f)),
                ("pressure", (int) MathF.Round(Pressure(boiler))),
                ("heat", (int) MathF.Round(boiler.Heat * 100f))));
            if (Pressure(boiler) > boiler.WarnPressure)
                args.PushMarkup(Loc.GetString("dystopia-boiler-examine-danger"));
        }
    }
}
