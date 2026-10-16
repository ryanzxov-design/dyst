// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: жидкостные трубы — доступ к сети, лужа при снятии или разборе трубы, отсечки цвета смеси, осмотр.

using System.Linq;
using Content.Server._Dystopia.Industry;
using Content.Server.Fluids.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared._Dystopia.Liquids;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.NodeContainer;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Liquids;

public sealed partial class DystopiaLiquidSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    /// <summary>Цвет каждой сети считается один раз за обновление.</summary>
    private readonly Dictionary<DystopiaLiquidNet, Color> _colorCache = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaLiquidPipeComponent, ExaminedEvent>(OnPipeExamined);
        SubscribeLocalEvent<DystopiaLiquidPipeComponent, AnchorStateChangedEvent>(OnPipeUnanchored);
        SubscribeLocalEvent<DystopiaLiquidPipeComponent, EntityTerminatingEvent>(OnPipeTerminating);
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, AnchorStateChangedEvent>(OnDeviceUnanchored);
        SubscribeLocalEvent<DystopiaLiquidDeviceComponent, EntityTerminatingEvent>(OnDeviceTerminating);
    }

    // ------------------------------------------------------------------ сеть

    /// <summary>Сеть узла сущности (null — узла нет или он сейчас ни к чему не подключён).</summary>
    public DystopiaLiquidNet? GetNet(EntityUid uid, string node)
    {
        return _nodes.TryGetNode<DystopiaLiquidPipeNode>(uid, node, out var pipe) ? pipe.Net : null;
    }

    /// <summary>Перелить из раствора в раствор не больше <paramref name="max"/> единиц (с учётом места).</summary>
    public FixedPoint2 Move(Solution from, Solution to, FixedPoint2 max)
    {
        var amount = FixedPoint2.Min(max, FixedPoint2.Min(from.Volume, to.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        to.AddSolution(from.SplitSolution(amount), _proto);
        return amount;
    }

    // ------------------------------------------------------------------ снятие и разбор: доля трубы — лужей

    private void OnPipeUnanchored(Entity<DystopiaLiquidPipeComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored && !args.Detaching)
            SpillShares(ent);
    }

    private void OnPipeTerminating(Entity<DystopiaLiquidPipeComponent> ent, ref EntityTerminatingEvent args)
    {
        if (Transform(ent).Anchored)
            SpillShares(ent);
    }

    private void OnDeviceUnanchored(Entity<DystopiaLiquidDeviceComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored && !args.Detaching)
            SpillShares(ent);
    }

    private void OnDeviceTerminating(Entity<DystopiaLiquidDeviceComponent> ent, ref EntityTerminatingEvent args)
    {
        if (Transform(ent).Anchored)
            SpillShares(ent);
    }

    /// <summary>
    /// Вылить на пол то, что приходится на участки этой сущности (сеть ещё целая — потом она поделит остаток).
    /// При выгрузке карты и конце раунда не льём.
    /// </summary>
    private void SpillShares(EntityUid uid)
    {
        var xform = Transform(uid);
        if (TerminatingOrDeleted(xform.GridUid) || TerminatingOrDeleted(xform.MapUid))
            return;

        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return;

        Solution? spilled = null;
        foreach (var node in container.Nodes.Values)
        {
            if (node is not DystopiaLiquidPipeNode { Net: { } net } pipe ||
                net.Solution.Volume <= FixedPoint2.Zero ||
                net.Solution.MaxVolume <= FixedPoint2.Zero)
            {
                continue;
            }

            var share = Math.Min(1f, pipe.Volume / net.Solution.MaxVolume.Float());
            var part = net.Solution.SplitSolution(FixedPoint2.New(net.Solution.Volume.Float() * share));
            if (spilled == null)
                spilled = part;
            else
                spilled.AddSolution(part, _proto);
        }

        if (spilled != null && spilled.Volume > FixedPoint2.Zero)
            _puddle.TrySpillAt(xform.Coordinates, spilled, out _);
    }

    // ------------------------------------------------------------------ отсечки цвета смеси

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _colorCache.Clear();

        var query = EntityQueryEnumerator<DystopiaLiquidPipeComponent>();
        while (query.MoveNext(out var uid, out var pipe))
        {
            if (now < pipe.NextColorUpdate)
                continue;

            pipe.NextColorUpdate = now + TimeSpan.FromSeconds(pipe.ColorInterval);

            var color = GetNet(uid, pipe.Node) is { } net ? NetColor(net, pipe) : pipe.EmptyColor;
            if (pipe.LastColor == color)
                continue;

            pipe.LastColor = color;
            _appearance.SetData(uid, DystopiaLiquidVisuals.Color, color);
        }
    }

    private Color NetColor(DystopiaLiquidNet net, DystopiaLiquidPipeComponent pipe)
    {
        if (net.Solution.Volume <= FixedPoint2.Zero)
            return pipe.EmptyColor;

        if (_colorCache.TryGetValue(net, out var cached))
            return cached;

        var color = Quantize(net.Solution.GetColor(_proto), pipe.ColorStep);
        _colorCache[net] = color;
        return color;
    }

    private static Color Quantize(Color color, int step)
    {
        step = Math.Max(1, step);

        static byte Channel(float value, int step)
        {
            var scaled = (int) MathF.Round(value * 255f / step) * step;
            return (byte) Math.Clamp(scaled, 0, 255);
        }

        return new Color(Channel(color.R, step), Channel(color.G, step), Channel(color.B, step), (byte) 255);
    }

    // ------------------------------------------------------------------ осмотр

    private void OnPipeExamined(Entity<DystopiaLiquidPipeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        PushNetInfo(args, GetNet(ent, ent.Comp.Node));
    }

    /// <summary>Заполненность, температура и состав сети.</summary>
    public void PushNetInfo(ExaminedEvent args, DystopiaLiquidNet? net, int priority = 0)
    {
        if (net == null)
        {
            args.PushMarkup(Loc.GetString("dystopia-liquid-examine-disconnected"), priority);
            return;
        }

        var solution = net.Solution;
        if (solution.Volume <= FixedPoint2.Zero)
        {
            args.PushMarkup(Loc.GetString("dystopia-liquid-examine-empty",
                ("max", solution.MaxVolume)), priority);
            return;
        }

        var percent = solution.MaxVolume > FixedPoint2.Zero
            ? (int) MathF.Round(solution.Volume.Float() / solution.MaxVolume.Float() * 100f)
            : 0;

        args.PushMarkup(Loc.GetString("dystopia-liquid-examine-fill",
            ("percent", percent),
            ("volume", solution.Volume),
            ("max", solution.MaxVolume),
            ("temperature", MathF.Round(solution.Temperature - DystopiaCrucibleSystem.KelvinOffset))), priority);

        foreach (var (reagent, quantity) in solution.GetReagentPrototypes(_proto).OrderByDescending(p => p.Value.Value))
        {
            args.PushMarkup(Loc.GetString("dystopia-liquid-examine-entry",
                ("color", reagent.SubstanceColor.ToHexNoAlpha()),
                ("name", reagent.LocalizedName),
                ("amount", quantity)), priority);
        }
    }
}
