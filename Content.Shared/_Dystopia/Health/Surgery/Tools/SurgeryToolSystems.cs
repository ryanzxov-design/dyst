// SPDX-License-Identifier: AGPL-3.0-or-later
// Перенос Shitmed (Goob-Station, AGPL-3.0): осмотр инструмента (чем он может служить) и запрет
// пользоваться выключенным прибором.

using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Dystopia.Health.Surgery.Tools;

public sealed partial class SurgeryToolExamineSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryToolComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<SurgeryToolComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var msg = FormattedMessage.FromMarkupOrThrow(Loc.GetString("surgery-tool-header"));
        AddTool<ScalpelComponent>(ent, msg);
        AddTool<RetractorComponent>(ent, msg);
        AddTool<HemostatComponent>(ent, msg);
        AddTool<TweezersComponent>(ent, msg);
        AddTool<TendingComponent>(ent, msg);
        AddTool<BoneSawComponent>(ent, msg);
        AddTool<DrillComponent>(ent, msg);
        AddTool<BoneSetterComponent>(ent, msg);
        AddTool<BoneGelComponent>(ent, msg);
        AddTool<CauteryComponent>(ent, msg);
        AddTool<StitchesComponent>(ent, msg);

        _examine.AddDetailedExamineVerb(args, ent.Comp, msg,
            Loc.GetString("surgery-tool-examinable-verb-text"), "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("surgery-tool-examinable-verb-message"));
    }

    private void AddTool<T>(EntityUid uid, FormattedMessage msg) where T : IComponent, ISurgeryToolComponent
    {
        if (!TryComp<T>(uid, out var comp))
            return;

        var color = comp.Speed switch
        {
            < 1f => "red",
            > 1f => "green",
            _ => "white",
        };

        msg.PushNewline();
        var key = "surgery-tool-" + (comp.Used == true ? "used" : "unlimited");
        msg.AddMarkupOrThrow(Loc.GetString(key, ("tool", Loc.GetString(comp.ToolName)),
            ("speed", comp.Speed.ToString("N2")), ("color", color)));
    }
}

/// <summary>Выключенным прибором (электроскальпель, электропила) оперировать нельзя.</summary>
public sealed partial class SurgeryToolConditionsSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemToggleComponent, SurgeryToolUsedEvent>(OnToggleUsed);
    }

    private void OnToggleUsed(Entity<ItemToggleComponent> ent, ref SurgeryToolUsedEvent args)
    {
        if (ent.Comp.Activated || args.IgnoreToggle)
            return;

        _popup.PopupClient(Loc.GetString("surgery-tool-turn-on"), ent, args.User);
        args.Cancelled = true;
    }
}
