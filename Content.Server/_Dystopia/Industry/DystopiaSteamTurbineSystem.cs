// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: паровая турбина (см. DystopiaSteamTurbineComponent).
// Раз в такт: если включена, не заклинена и выхлопу есть куда уйти — берёт из патрубка свежего пара до расхода
// по клапану, срабатывает перепад температур (не больше MaxDelta) и выпускает пар остывшим в патрубок выхлопа.
// Ротор раскручивается и выбегает не сразу; мощность ∝ расход × перепад × обороты → клемма генератора
// (PowerSupplier на сущности-клемме). Масло — на обороты, без него износ резко растёт; перегретый пар жжёт лопатки.

using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.NodeContainer;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaSteamTurbineSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DystopiaMachinePortsSystem _ports = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaSteamTurbineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DystopiaSteamTurbineComponent, BoundUIOpenedEvent>(OnUiOpened);
        Subs.BuiEvents<DystopiaSteamTurbineComponent>(DystopiaSteamTurbineUiKey.Key, subs =>
        {
            subs.Event<DystopiaSteamTurbineToggleMessage>(OnToggle);
            subs.Event<DystopiaSteamTurbineSetValveMessage>(OnSetValve);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaSteamTurbineComponent>();
        while (query.MoveNext(out var uid, out var turbine))
        {
            if (now < turbine.NextUpdate)
                continue;

            turbine.NextUpdate = now + TimeSpan.FromSeconds(turbine.Interval);
            Tick((uid, turbine), turbine.Interval);

            if (_ui.IsUiOpen(uid, DystopiaSteamTurbineUiKey.Key))
                UpdateUi((uid, turbine));
        }
    }

    private PipeNode? Pipe(EntityUid uid, string port, string node)
    {
        return _ports.TryGetPort(uid, port, out var portUid) && _nodes.TryGetNode<PipeNode>(portUid, node, out var pipe)
            ? pipe
            : null;
    }

    private void Tick(Entity<DystopiaSteamTurbineComponent> ent, float dt)
    {
        var turbine = ent.Comp;
        turbine.Flow = 0f;
        turbine.Power = 0f;
        turbine.OilUse = 0f;
        turbine.Blocked = false;

        var inlet = Pipe(ent, turbine.InletPort, turbine.PipeNode);
        var outlet = Pipe(ent, turbine.OutletPort, turbine.PipeNode);
        turbine.InletTemperature = inlet?.Air.Temperature ?? 0f;
        turbine.InletPressure = inlet?.Air.Pressure ?? 0f;
        turbine.ExhaustPressure = outlet?.Air.Pressure ?? 0f;

        var target = 0f;
        var delta = 0f;
        if (turbine.On && !Seized(ent) && inlet != null && outlet != null)
        {
            if (outlet.Air.Pressure >= turbine.MaxExhaustPressure)
            {
                turbine.Blocked = true;
            }
            else
            {
                var available = inlet.Air.TotalMoles;
                var take = MathF.Min(available, turbine.MaxFlow * turbine.Valve * dt);
                if (take > 0.001f)
                {
                    var gas = inlet.Air.Remove(take);
                    var inT = gas.Temperature;
                    delta = Math.Clamp(inT - turbine.ExhaustTemperature, 0f, turbine.MaxDelta);
                    if (delta > 0f)
                        gas.Temperature = inT - delta;

                    _atmos.Merge(outlet.Air, gas);
                    turbine.Flow = take / dt;
                    target = turbine.MaxFlow > 0f ? turbine.Flow / turbine.MaxFlow : 0f;

                    if (inT > turbine.MaxInletTemperature)
                        turbine.PendingHeat += (inT - turbine.MaxInletTemperature) * turbine.OverheatRate * dt;
                }
            }
        }

        // Ротор: раскрутка и выбег
        if (turbine.Speed < target)
            turbine.Speed = MathF.Min(target, turbine.Speed + turbine.SpinUp * dt);
        else
            turbine.Speed = MathF.Max(target, turbine.Speed - turbine.SpinDown * dt);

        // Мощность: от пара, но не быстрее, чем раскрутился ротор
        if (turbine.Flow > 0f && delta > 0f)
        {
            var spin = target > 0f ? MathF.Min(1f, turbine.Speed / target) : 0f;
            turbine.Power = turbine.Flow * delta * turbine.PowerPerMoleKelvin * spin;
        }

        Lubricate(ent, dt);
        ApplyDamage(ent);

        if (_ports.TryGetPort(ent, turbine.PowerPort, out var powerPort) &&
            TryComp<PowerSupplierComponent>(powerPort, out var supplier))
        {
            supplier.MaxSupply = turbine.Power;
        }

        var running = turbine.Speed > 0.02f;
        if (running != turbine.Running)
        {
            turbine.Running = running;
            _appearance.SetData(ent, DystopiaProcessorVisuals.Running, running);
            _ambient.SetAmbience(ent, running);
        }
    }

    /// <summary>Масло — на обороты; нет масла — износ во много раз выше.</summary>
    private void Lubricate(Entity<DystopiaSteamTurbineComponent> ent, float dt)
    {
        var turbine = ent.Comp;
        turbine.Dry = false;
        if (turbine.Speed <= 0.001f)
            return;

        var dry = true;
        if (_solution.TryGetSolution(ent.Owner, turbine.OilSolution, out var oilEnt, out var oil))
        {
            var need = FixedPoint2.New(turbine.OilRate * turbine.Speed * dt);
            if (need > FixedPoint2.Zero && oil.GetTotalPrototypeQuantity(turbine.Lubricant) >= need)
            {
                _solution.RemoveReagent(oilEnt.Value, turbine.Lubricant.Id, need);
                turbine.OilUse = need.Float() / dt;
                dry = false;
            }
        }

        turbine.Dry = dry;
        turbine.PendingWear += turbine.WearRate * turbine.Speed * (dry ? turbine.DryWear : 1f) * dt;
    }

    private void ApplyDamage(Entity<DystopiaSteamTurbineComponent> ent)
    {
        var turbine = ent.Comp;
        if (turbine.PendingWear >= 0.1f && !turbine.WearDamage.Empty)
        {
            _damageable.TryChangeDamage(ent.Owner, turbine.WearDamage * turbine.PendingWear, interruptsDoAfters: false);
            turbine.PendingWear = 0f;
        }

        if (turbine.PendingHeat >= 0.1f && !turbine.OverheatDamage.Empty)
        {
            _damageable.TryChangeDamage(ent.Owner, turbine.OverheatDamage * turbine.PendingHeat, interruptsDoAfters: false);
            turbine.PendingHeat = 0f;
        }
    }

#pragma warning disable CS0618 // Do not rely on a numerically quantifiable amount of damage — здесь это и есть износ
    private FixedPoint2 TotalDamage(Entity<DamageableComponent> ent)
    {
        return _damageable.GetTotalDamage((ent.Owner, ent.Comp));
    }
#pragma warning restore CS0618

    private bool Seized(Entity<DystopiaSteamTurbineComponent> ent)
    {
        return TryComp<DamageableComponent>(ent, out var damageable) &&
               TotalDamage((ent.Owner, damageable)) >= ent.Comp.SeizeDamage;
    }

    private DystopiaSteamTurbineStatus Status(Entity<DystopiaSteamTurbineComponent> ent)
    {
        if (Seized(ent))
            return DystopiaSteamTurbineStatus.Seized;
        if (!ent.Comp.On)
            return DystopiaSteamTurbineStatus.Off;
        if (ent.Comp.Blocked)
            return DystopiaSteamTurbineStatus.Blocked;
        if (ent.Comp.Flow <= 0f)
            return DystopiaSteamTurbineStatus.NoSteam;

        return DystopiaSteamTurbineStatus.Running;
    }

    // ------------------------------------------------------------------ заправка из труб (экспортер)

    // Экспортер наливает только масло — DystopiaLiquidInputFilterComponent в прототипе.

    // ------------------------------------------------------------------ меню

    private void OnUiOpened(Entity<DystopiaSteamTurbineComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnToggle(Entity<DystopiaSteamTurbineComponent> ent, ref DystopiaSteamTurbineToggleMessage args)
    {
        ent.Comp.On = !ent.Comp.On;
        ent.Comp.NextUpdate = TimeSpan.Zero;
        UpdateUi(ent);
    }

    private void OnSetValve(Entity<DystopiaSteamTurbineComponent> ent, ref DystopiaSteamTurbineSetValveMessage args)
    {
        if (!float.IsFinite(args.Valve))
            return;

        ent.Comp.Valve = Math.Clamp(args.Valve, 0f, 1f);
        UpdateUi(ent);
    }

    private bool Connected(EntityUid uid, string port, string node)
    {
        return _ports.TryGetPort(uid, port, out var portUid) &&
               _nodes.TryGetNode<Node>(portUid, node, out var found) &&
               found.NodeGroup is { } group && group.Nodes.Count > 1;
    }

    private void UpdateUi(Entity<DystopiaSteamTurbineComponent> ent)
    {
        var turbine = ent.Comp;
        float oil = 0f, oilMax = 0f;
        if (_solution.TryGetSolution(ent.Owner, turbine.OilSolution, out _, out var oilSol))
        {
            oil = oilSol.GetTotalPrototypeQuantity(turbine.Lubricant).Float();
            oilMax = oilSol.MaxVolume.Float();
        }

        var wear = TryComp<DamageableComponent>(ent, out var damageable) && turbine.SeizeDamage > FixedPoint2.Zero
            ? MathF.Min(1f, TotalDamage((ent.Owner, damageable)).Float() / turbine.SeizeDamage.Float())
            : 0f;

        // Клемма подключена — если в её сети есть ещё узлы (кабель под ней).
        var powerConnected = _ports.TryGetPort(ent, turbine.PowerPort, out var powerPort) &&
                             TryComp<NodeContainerComponent>(powerPort, out var container) &&
                             container.Nodes.Values.Any(n => n.NodeGroup is { } g && g.Nodes.Count > 1);

        _ui.SetUiState(ent.Owner, DystopiaSteamTurbineUiKey.Key, new DystopiaSteamTurbineUiState(Status(ent),
            turbine.On, turbine.Valve, turbine.Speed, turbine.Power,
            turbine.MaxFlow * turbine.MaxDelta * turbine.PowerPerMoleKelvin, turbine.Flow, turbine.MaxFlow,
            turbine.InletTemperature, turbine.InletPressure, turbine.ExhaustTemperature, turbine.ExhaustPressure,
            turbine.MaxExhaustPressure, turbine.MaxInletTemperature,
            Connected(ent, turbine.InletPort, turbine.PipeNode), Connected(ent, turbine.OutletPort, turbine.PipeNode),
            powerConnected, oil, oilMax, turbine.OilUse, wear));
    }

    // ------------------------------------------------------------------ осмотр

    private void OnExamined(Entity<DystopiaSteamTurbineComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(DystopiaSteamTurbineSystem)))
        {
            args.PushMarkup(Loc.GetString("dystopia-turbine-examine-state",
                ("speed", (int) MathF.Round(ent.Comp.Speed * 100f)),
                ("power", MathF.Round(ent.Comp.Power / 1000f, 1))));
            if (ent.Comp.Dry && ent.Comp.Speed > 0.01f)
                args.PushMarkup(Loc.GetString("dystopia-turbine-examine-dry"));
        }
    }
}
