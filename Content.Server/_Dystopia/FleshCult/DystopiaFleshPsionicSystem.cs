using Content.Shared._Dystopia.FleshCult;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.FleshCult;

/// <summary>
/// Псионическая связь Культа: каждый культист без всякой гарнитуры слышит канал «Шёпот Плоти»
/// и говорит в него (префикс канала, по умолчанию :ш). В обычные каналы Города эта связь не пускает.
/// </summary>
public sealed partial class DystopiaFleshPsionicSystem : EntitySystem
{
    private static readonly ProtoId<RadioChannelPrototype> CultChannel = "DystopiaCult";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFleshCultistComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DystopiaFleshCultistComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(Entity<DystopiaFleshCultistComponent> ent, ref ComponentInit args)
    {
        // Передатчик: если его не было, по умолчанию в нём общий канал Города — убираем его,
        // чтобы культист не вещал в Город без гарнитуры.
        if (!HasComp<IntrinsicRadioTransmitterComponent>(ent))
        {
            var transmitter = AddComp<IntrinsicRadioTransmitterComponent>(ent);
            transmitter.Channels.Clear();
        }

        Comp<IntrinsicRadioTransmitterComponent>(ent).Channels.Add(CultChannel);
        EnsureComp<ActiveRadioComponent>(ent).Channels.Add(CultChannel);
        EnsureComp<IntrinsicRadioReceiverComponent>(ent);
    }

    private void OnRemove(Entity<DystopiaFleshCultistComponent> ent, ref ComponentRemove args)
    {
        // Отлучённый от Культа теряет связь с ним.
        if (TryComp<IntrinsicRadioTransmitterComponent>(ent, out var transmitter))
            transmitter.Channels.Remove(CultChannel);

        if (TryComp<ActiveRadioComponent>(ent, out var active))
            active.Channels.Remove(CultChannel);
    }
}
