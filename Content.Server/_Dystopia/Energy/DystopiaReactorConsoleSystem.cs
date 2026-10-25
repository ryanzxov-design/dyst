// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: пульт реактора (стиль Города). Связь — колонна вплотную или кабель управления до любой колонны. Карта
// каналов с температурой, мощностью, выгоранием и стержнями; управление стержнями по одному и группами, АЗ-5 и её
// снятие, авторегулятор (держит тепловую мощность группой 1), автоматическая защита по температуре.

using Content.Shared._Dystopia.Industry;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaReactorConsoleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private DystopiaReactorSystem _reactor = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaReactorConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        Subs.BuiEvents<DystopiaReactorConsoleComponent>(DystopiaReactorConsoleUiKey.Key, subs =>
        {
            subs.Event<DystopiaReactorSetRodMessage>(OnSetRod);
            subs.Event<DystopiaReactorSetGroupMessage>(OnSetGroup);
            subs.Event<DystopiaReactorStepGroupMessage>(OnStepGroup);
            subs.Event<DystopiaReactorAssignGroupMessage>(OnAssignGroup);
            subs.Event<DystopiaReactorScramMessage>(OnScram);
            subs.Event<DystopiaReactorResetScramMessage>(OnResetScram);
            subs.Event<DystopiaReactorAutoRegulatorMessage>(OnAutoRegulator);
            subs.Event<DystopiaReactorAutoScramMessage>(OnAutoScram);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<DystopiaReactorConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, DystopiaReactorConsoleUiKey.Key))
                UpdateUi((uid, console));
        }
    }

    private void OnOpened(Entity<DystopiaReactorConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private DystopiaReactorSystem.Reactor? Reactor(Entity<DystopiaReactorConsoleComponent> ent)
    {
        return _reactor.FindReactor(ent, ent.Comp.ControlNode);
    }

    /// <summary>Колонна-стержень этого реактора по сетевому id (чужие и не стержни — мимо).</summary>
    private DystopiaReactorColumnComponent? ControlRod(DystopiaReactorSystem.Reactor reactor, NetEntity net)
    {
        var uid = GetEntity(net);
        return reactor.TileOf.ContainsKey(uid) && TryComp<DystopiaReactorColumnComponent>(uid, out var col) &&
               col.Kind == DystopiaReactorColumnKind.Control
            ? col
            : null;
    }

    private void OnSetRod(Entity<DystopiaReactorConsoleComponent> ent, ref DystopiaReactorSetRodMessage args)
    {
        if (!float.IsFinite(args.Target) || Reactor(ent) is not { } reactor || ControlRod(reactor, args.Rod) is not { } col)
            return;

        col.Target = Math.Clamp(args.Target, 0f, 1f);
        UpdateUi(ent);
    }

    private void OnSetGroup(Entity<DystopiaReactorConsoleComponent> ent, ref DystopiaReactorSetGroupMessage args)
    {
        if (!float.IsFinite(args.Target) || args.Group < 0 || args.Group > 4 || Reactor(ent) is not { } reactor)
            return;

        foreach (var col in _reactor.GroupRods(reactor, args.Group))
            col.Target = Math.Clamp(args.Target, 0f, 1f);
        UpdateUi(ent);
    }

    private void OnStepGroup(Entity<DystopiaReactorConsoleComponent> ent, ref DystopiaReactorStepGroupMessage args)
    {
        if (!float.IsFinite(args.Delta) || args.Group < 0 || args.Group > 4 || Reactor(ent) is not { } reactor)
            return;

        foreach (var col in _reactor.GroupRods(reactor, args.Group))
            col.Target = Math.Clamp(col.Target + args.Delta, 0f, 1f);
        UpdateUi(ent);
    }

    private void OnAssignGroup(Entity<DystopiaReactorConsoleComponent> ent, ref DystopiaReactorAssignGroupMessage args)
    {
        if (args.Group < 1 || args.Group > 4 || Reactor(ent) is not { } reactor || ControlRod(reactor, args.Rod) is not { } col)
            return;

        col.Group = args.Group;
        UpdateUi(ent);
    }

    private void OnScram(Entity<DystopiaReactorConsoleComponent> ent, ref DystopiaReactorScramMessage args)
    {
        if (Reactor(ent) is { } reactor)
            _reactor.Scram(reactor);
        UpdateUi(ent);
    }

    private void OnResetScram(Entity<DystopiaReactorConsoleComponent> ent, ref DystopiaReactorResetScramMessage args)
    {
        if (Reactor(ent) is { } reactor)
            _reactor.ResetScram(reactor);
        UpdateUi(ent);
    }

    private void OnAutoRegulator(Entity<DystopiaReactorConsoleComponent> ent, ref DystopiaReactorAutoRegulatorMessage args)
    {
        if (!float.IsFinite(args.Setpoint))
            return;

        ent.Comp.AutoRegulator = args.On;
        ent.Comp.Setpoint = Math.Clamp(args.Setpoint, 0f, 1_000_000f);
        UpdateUi(ent);
    }

    private void OnAutoScram(Entity<DystopiaReactorConsoleComponent> ent, ref DystopiaReactorAutoScramMessage args)
    {
        ent.Comp.AutoScram = args.On;
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DystopiaReactorConsoleComponent> ent)
    {
        var console = ent.Comp;
        var cells = new List<DystopiaReactorCell>();
        var cfg = _reactor.Settings();
        var alarmT = cfg?.HotLevel2 ?? 800f;
        var damageT = cfg?.DamageTemperature ?? 1200f;
        if (Reactor(ent) is not { } reactor)
        {
            _ui.SetUiState(ent.Owner, DystopiaReactorConsoleUiKey.Key, new DystopiaReactorConsoleUiState(false, cells, 0, 0,
                0f, 0f, 0f, 0f, 0f, 0f, 0, 0f, console.AutoRegulator, console.Setpoint, console.AutoScram,
                console.AutoScramTemperature, false, 1f, alarmT, damageT, 1f, 1f));
            return;
        }

        // пороги давления — по первому барабану (у всех одинаковые, если не правили прототип)
        var warnP = 6500f;
        var damageP = 8000f;
        foreach (var sepUid in reactor.Separators)
        {
            if (!TryComp<DystopiaReactorSeparatorComponent>(sepUid, out var sep))
                continue;

            warnP = sep.WarnPressure;
            damageP = sep.DamagePressure;
            break;
        }

        foreach (var (uid, tile) in reactor.TileOf)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<DystopiaReactorColumnComponent>(uid, out var col))
                continue;

            cells.Add(new DystopiaReactorCell(GetNetEntity(uid), tile.X - reactor.Min.X, tile.Y - reactor.Min.Y, col.Kind,
                col.Temperature, col.Power, col.Flux, _reactor.RodBurnup(uid, col), _reactor.ColumnRodGrade(uid, col),
                col.Insertion, col.Target, col.Group, col.Xenon, col.Scrammed, _reactor.ColumnHealth(uid)));
        }

        _ui.SetUiState(ent.Owner, DystopiaReactorConsoleUiKey.Key, new DystopiaReactorConsoleUiState(true, cells,
            reactor.Max.X - reactor.Min.X + 1, reactor.Max.Y - reactor.Min.Y + 1, reactor.ThermalPower, reactor.SteamRate,
            reactor.Water, reactor.WaterMax, reactor.MaxTemperature, reactor.AverageXenon, reactor.Separators.Count,
            _reactor.MaxSeparatorPressure(reactor), console.AutoRegulator, console.Setpoint, console.AutoScram,
            console.AutoScramTemperature, reactor.Alarm, MathF.Max(1f, _reactor.CoolingCapacity(reactor)), alarmT, damageT,
            warnP, damageP));
    }
}
