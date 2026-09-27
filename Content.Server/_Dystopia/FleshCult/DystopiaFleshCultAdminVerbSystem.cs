using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Antag;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Dystopia.FleshCult;

/// <summary>
/// Админ-действие «Антагонисты → Сделать Проповедником Плоти» (для проверки и ивентов).
/// </summary>
public sealed partial class DystopiaFleshCultAdminVerbSystem : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;

    private static readonly EntProtoId CultRule = "DystopiaFleshCultRule";
    private static readonly ProtoId<AntagSpecifierPrototype> Preacher = "DystopiaFleshPreacher";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !_adminManager.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
        {
            return;
        }

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        var target = targetActor.PlayerSession;
        var name = Loc.GetString("dystopia-admin-verb-make-flesh-preacher");
        args.Verbs.Add(new Verb
        {
            Text = name,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Dystopia/Interface/job_icons.rsi"), "FleshPreacher"),
            Act = () => _antag.ForceMakeAntag<DystopiaFleshCultRuleComponent>(target, CultRule, Preacher),
            Impact = LogImpact.High,
            Message = string.Join(": ", name, Loc.GetString("dystopia-admin-verb-make-flesh-preacher-desc")),
        });
    }
}
