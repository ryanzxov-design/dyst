// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: повышающий трансформатор. Снизу берёт ток как потребитель (PowerConsumer на входном узле), наверх отдаёт
// как генератор (PowerSupplier на выходном). Между ними буфер: копится полученное (с потерями), наверх уходит не
// больше Buffer / BufferSeconds — то есть только уже полученное, энергия не берётся из ниоткуда. Снизу берёт столько,
// сколько расходуют наверху, с запасом, чтобы мощность могла расти, но не больше MaxPower / Efficiency.

using Content.Server.Power.Components;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Examine;

namespace Content.Server._Dystopia.Energy;

public sealed partial class DystopiaTransformerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaTransformerComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (frameTime <= 0f)
            return;

        var query = EntityQueryEnumerator<DystopiaTransformerComponent, PowerConsumerComponent, PowerSupplierComponent>();
        while (query.MoveNext(out _, out var tr, out var input, out var output))
        {
            // что отдали наверх и что получили снизу за прошлый расчёт сети
            var supplied = output.CurrentSupply;
            var received = float.IsFinite(input.ReceivedPower) ? input.ReceivedPower : 0f;
            var capacity = tr.MaxPower * MathF.Max(0.1f, tr.BufferSeconds);
            tr.Buffer = Math.Clamp(tr.Buffer + (received * tr.Efficiency - supplied) * frameTime, 0f, capacity);

            output.MaxSupply = MathF.Min(tr.MaxPower, tr.Buffer / MathF.Max(0.1f, tr.BufferSeconds));
            input.DrawRate = MathF.Min(tr.MaxPower / tr.Efficiency,
                MathF.Max(tr.MinDraw, supplied * (1f + tr.Headroom) / tr.Efficiency + tr.MinDraw));
        }
    }

    private void OnExamined(Entity<DystopiaTransformerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<PowerSupplierComponent>(ent, out var output))
            return;

        args.PushMarkup(Loc.GetString("dystopia-transformer-examine",
            ("power", MathF.Round(output.CurrentSupply / 1000f, 1)), ("max", MathF.Round(ent.Comp.MaxPower / 1000f)),
            ("loss", (int) MathF.Round((1f - ent.Comp.Efficiency) * 100f))));
    }
}
