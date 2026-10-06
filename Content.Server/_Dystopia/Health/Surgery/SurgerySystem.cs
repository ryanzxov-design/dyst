// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос хирургии Shitmed из Goob-Station (AGPL-3.0): серверная часть — список доступных операций
// для окна, урон от шагов (заражение, разрезы) и крик пациента в сознании.

using Content.Server.Chat.Systems;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared._Dystopia.Health.Surgery;
using Content.Shared._Dystopia.Health.Surgery.Effects;
using Content.Shared._Dystopia.Health.Surgery.Steps;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Dystopia.Health.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private SharedBodySystem _bodyParts = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private WoundSystem _woundsServer = default!;
    [Dependency] private UserInterfaceSystem _uiServer = default!;
    [Dependency] private MobStateSystem _mobStateServer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryStepDamageEvent>(OnSurgeryStepDamage);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepEmote);
    }

    protected override void RefreshUI(EntityUid body)
    {
        if (!_uiServer.IsUiOpen(body, SurgeryUIKey.Key))
            return;

        var surgeries = new Dictionary<NetEntity, List<EntProtoId>>();
        foreach (var (part, _) in _bodyParts.GetBodyChildren(body))
        {
            var valid = new List<EntProtoId>();
            foreach (var surgery in AllSurgeries)
            {
                if (IsSurgeryAvailable(body, part, surgery))
                    valid.Add(surgery);
            }

            surgeries[GetNetEntity(part)] = valid;
        }

        _uiServer.SetUiState(body, SurgeryUIKey.Key, new SurgeryBuiState(surgeries));
        // Состояние окна откатывает предсказание как раз во время проверки шагов — отдельное сообщение
        // заставляет клиента перерисовать кнопки уже с новыми данными (так в Shitmed).
        _uiServer.ServerSendUiMessage(body, SurgeryUIKey.Key, new SurgeryBuiRefreshMessage());
    }

    private void OnSurgeryStepDamage(Entity<SurgeryTargetComponent> ent, ref SurgeryStepDamageEvent args)
    {
        if (args.Damage.Empty)
            return;

        _woundsServer.DamagePart(args.Body, args.Part, args.Damage, args.User);
    }

    private void OnStepEmote(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (HasComp<SleepingComponent>(args.Body) || _mobStateServer.IsIncapacitated(args.Body))
            return;

        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote.Id, ignoreActionBlocker: true);
    }
}
