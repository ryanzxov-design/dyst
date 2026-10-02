// SPDX-License-Identifier: AGPL-3.0-or-later
// Хирургия Dystopia — под новую систему тела.
// Операции и шаги — прототипы-сущности (как в ); состояние операции хранится компонентами на части тела.
// Органы достаются и вставляются через контейнер тела и связи органов (OrganRelationSystem).

using System.Linq;
using Content.Server.Popups;
using Content.Shared._Dystopia.Health.Surgery;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Dystopia.Health.Surgery;

public sealed partial class SurgerySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private OrganRelationSystem _relation = default!;
    [Dependency] private DetachableOrganSystem _detachable = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    /// <summary>Части тела, которые можно оперировать (порядок в окне).</summary>
    private static readonly string[] OperableParts =
    {
        "Torso", "Groin", "Head", "ArmLeft", "ArmRight", "HandLeft", "HandRight", "LegLeft", "LegRight", "FootLeft", "FootRight",
    };

    /// <summary>Конечности, которые можно отрезать и пришить (голову — нельзя).</summary>
    private static readonly HashSet<string> LimbCategories = new()
    {
        "ArmLeft", "ArmRight", "HandLeft", "HandRight", "LegLeft", "LegRight", "FootLeft", "FootRight",
    };

    /// <summary>Внешние части тела — их нет среди «внутренних органов» для вставки.</summary>
    private static readonly HashSet<string> ExternalCategories = new()
    {
        "Torso", "Groin", "Head", "ArmLeft", "ArmRight", "HandLeft", "HandRight", "LegLeft", "LegRight", "FootLeft", "FootRight",
    };

    private static readonly ProtoId<DamageTypePrototype> FailDamageType = "Slash";

    private const float ImprovisedSpeed = 0.6f;
    private const float ImprovisedFail = 0.2f;
    private const float FloorFail = 0.15f;
    private const float AwakeFail = 0.1f;
    private const float FloorSlowdown = 1.5f;
    private const float OrganActionTime = 5f;
    private const float AttachLimbTime = 7f;
    private const float AmputationBleed = 3f;

    private readonly List<SurgeryDef> _surgeries = new();

    private sealed record SurgeryDef(EntProtoId Id, string Name, SurgeryComponent Surgery, HashSet<string> Parts);

    public override void Initialize()
    {
        base.Initialize();
        LoadSurgeries();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => LoadSurgeries());

        SubscribeLocalEvent<SurgeryToolComponent, AfterInteractEvent>(OnToolAfterInteract);
        SubscribeLocalEvent<GetVerbsEvent<InteractionVerb>>(OnGetVerbs);

        SubscribeLocalEvent<SurgeryPatientComponent, SurgeryStepMessage>(OnStepMessage);
        SubscribeLocalEvent<SurgeryPatientComponent, SurgeryRemoveOrganMessage>(OnRemoveOrganMessage);
        SubscribeLocalEvent<SurgeryPatientComponent, SurgeryInsertOrganMessage>(OnInsertOrganMessage);
        SubscribeLocalEvent<SurgeryPatientComponent, SurgeryAttachLimbMessage>(OnAttachLimbMessage);
        SubscribeLocalEvent<SurgeryPatientComponent, SurgeryDoAfterEvent>(OnDoAfter);
    }

    private void LoadSurgeries()
    {
        _surgeries.Clear();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryComp<SurgeryComponent>(out var surgery, _factory))
                continue;

            var parts = new HashSet<string>();
            if (proto.TryComp<SurgeryPartConditionComponent>(out var cond, _factory))
            {
                foreach (var p in cond.Parts)
                {
                    parts.Add(p.Id);
                }
            }

            _surgeries.Add(new SurgeryDef(proto.ID, proto.Name, surgery, parts));
        }

        _surgeries.Sort((a, b) => a.Surgery.Priority.CompareTo(b.Surgery.Priority));
    }

    // ================= Открытие окна =================

    private void OnToolAfterInteract(Entity<SurgeryToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<BodyComponent>(target))
            return;

        args.Handled = TryOpen(args.User, target);
    }

    private void OnGetVerbs(GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.User == args.Target || !HasComp<BodyComponent>(args.Target))
            return;

        if (args.Using is not { } used || GetTool(used) is null)
            return;

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("surgery-verb-operate"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => TryOpen(user, target),
        });
    }

    public bool TryOpen(EntityUid user, EntityUid patient)
    {
        if (!CanOperate(user, patient, out var reason))
        {
            _popup.PopupEntity(reason, patient, user, PopupType.SmallCaution);
            return false;
        }

        if (!_ui.HasUi(patient, SurgeryUiKey.Key))
            _ui.SetUi(patient, SurgeryUiKey.Key, new InterfaceData("SurgeryBoundUserInterface", 2f));

        EnsureComp<SurgeryPatientComponent>(patient);
        _ui.OpenUi(patient, SurgeryUiKey.Key, user);
        UpdateUi(patient, user);
        return true;
    }

    private bool CanOperate(EntityUid user, EntityUid patient, out string reason)
    {
        reason = string.Empty;
        if (user == patient)
        {
            reason = Loc.GetString("surgery-self");
            return false;
        }

        if (!HasComp<BodyComponent>(patient))
            return false;

        if (!OnTable(patient) && !_standing.IsDown(patient))
        {
            reason = Loc.GetString("surgery-must-lie");
            return false;
        }

        return true;
    }

    private bool OnTable(EntityUid patient)
    {
        return TryComp<BuckleComponent>(patient, out var buckle) &&
               buckle.BuckledTo is { } strap &&
               HasComp<SurgeryOperatingTableComponent>(strap);
    }

    // ================= Инструменты =================

    private sealed record ToolInfo(HashSet<SurgeryToolKind> Kinds, float Speed, float Fail, bool Improvised);

    /// <summary>Что умеет предмет: настоящий инструмент или подручный (нож — скальпель, лом — ретрактор...).</summary>
    private ToolInfo? GetTool(EntityUid item)
    {
        if (TryComp<SurgeryToolComponent>(item, out var tool))
            return new ToolInfo(tool.Kinds.ToHashSet(), tool.Speed, tool.FailChance, false);

        if (!TryComp<ToolComponent>(item, out var generic))
            return null;

        var kinds = new HashSet<SurgeryToolKind>();
        foreach (var quality in generic.Qualities)
        {
            switch (quality.Id)
            {
                case "Slicing": kinds.Add(SurgeryToolKind.Scalpel); break;
                case "Sawing": kinds.Add(SurgeryToolKind.Saw); break;
                case "Prying": kinds.Add(SurgeryToolKind.Retractor); break;
                case "Cutting": kinds.Add(SurgeryToolKind.Hemostat); break;
                case "Welding": kinds.Add(SurgeryToolKind.Cautery); break;
            }
        }

        return kinds.Count == 0 ? null : new ToolInfo(kinds, ImprovisedSpeed, ImprovisedFail, true);
    }

    private float FailChance(EntityUid patient, ToolInfo tool)
    {
        var chance = tool.Fail;
        if (!OnTable(patient))
            chance += FloorFail;
        if (!HasComp<SleepingComponent>(patient) && !_mobState.IsIncapacitated(patient))
            chance += AwakeFail;
        return Math.Clamp(chance, 0f, 0.95f);
    }

    // ================= Состояние операций =================

    private bool HasAll(EntityUid part, ComponentRegistry registry)
    {
        foreach (var (_, entry) in registry)
        {
            if (!HasComp(part, entry.Component.GetType()))
                return false;
        }

        return true;
    }

    private bool HasNone(EntityUid part, ComponentRegistry registry)
    {
        foreach (var (_, entry) in registry)
        {
            if (HasComp(part, entry.Component.GetType()))
                return false;
        }

        return true;
    }

    private SurgeryStepComponent? Step(EntProtoId id)
    {
        return _proto.TryIndex(id, out var proto) && proto.TryComp<SurgeryStepComponent>(out var step, _factory) ? step : null;
    }

    private bool IsStepDone(EntityUid part, SurgeryStepComponent step)
    {
        if (step.Add.Count == 0 && step.Remove.Count == 0)
            return false;

        return HasAll(part, step.Add) && HasNone(part, step.Remove);
    }

    private bool IsComplete(EntityUid part, SurgeryDef surgery)
    {
        foreach (var id in surgery.Surgery.Steps)
        {
            if (Step(id) is not { } step || !IsStepDone(part, step))
                return false;
        }

        return true;
    }

    private bool IsAvailable(EntityUid part, string category, SurgeryDef surgery)
    {
        if (surgery.Parts.Count > 0 && !surgery.Parts.Contains(category))
            return false;

        if (surgery.Surgery.Requirement is not { } req)
            return true;

        var required = _surgeries.FirstOrDefault(s => s.Id == req);
        return required != null && IsComplete(part, required);
    }

    /// <summary>Текущий (первый невыполненный) шаг операции.</summary>
    private EntProtoId? CurrentStep(EntityUid part, SurgeryDef surgery)
    {
        foreach (var id in surgery.Surgery.Steps)
        {
            if (Step(id) is not { } step || !IsStepDone(part, step))
                return id;
        }

        return null;
    }

    private IEnumerable<(EntityUid Uid, string Category)> Parts(EntityUid patient)
    {
        var found = new Dictionary<string, EntityUid>();
        foreach (var organ in _body.EnumerateOrgans<OrganComponent>(patient))
        {
            if (organ.Comp1.Category is { } cat && OperableParts.Contains(cat.Id))
                found[cat.Id] = organ.Owner;
        }

        foreach (var cat in OperableParts)
        {
            if (found.TryGetValue(cat, out var uid))
                yield return (uid, cat);
        }
    }

    private string? PartCategory(EntityUid part)
    {
        return TryComp<OrganComponent>(part, out var organ) ? organ.Category?.Id : null;
    }

    /// <summary>Внутренние органы, прикреплённые к этой части тела.</summary>
    private IEnumerable<EntityUid> InternalOrgans(EntityUid part)
    {
        if (!TryComp<ParentOrganComponent>(part, out var parent))
            yield break;

        var children = parent.Children;
        foreach (var child in children)
        {
            if (HasComp<InternalChildOrganComponent>(child))
                yield return child;
        }
    }

    /// <summary>Каких внутренних органов не хватает в части тела (по устройству тела вида).</summary>
    private List<string> MissingOrgans(EntityUid patient, EntityUid part, string category)
    {
        var result = new List<string>();
        if (!TryComp<InitialBodyComponent>(patient, out var initial))
            return result;

        // Поле читаем в локальную переменную: вызывать методы прямо на чужом поле запрещает анализатор доступа
        var relationships = initial.Relationships;
        if (relationships is null || !relationships.TryGetValue(category, out var expected))
            return result;

        var present = new HashSet<string>();
        foreach (var organ in InternalOrgans(part))
        {
            if (PartCategory(organ) is { } cat)
                present.Add(cat);
        }

        foreach (var cat in expected)
        {
            if (!ExternalCategories.Contains(cat.Id) && !present.Contains(cat.Id))
                result.Add(cat.Id);
        }

        return result;
    }

    /// <summary>Каких конечностей не хватает у этой части тела (по устройству тела вида).</summary>
    private List<string> MissingLimbs(EntityUid patient, EntityUid part, string category)
    {
        var result = new List<string>();
        if (!TryComp<InitialBodyComponent>(patient, out var initial))
            return result;

        var relationships = initial.Relationships;
        if (relationships is null || !relationships.TryGetValue(category, out var expected))
            return result;

        var present = new HashSet<string>();
        if (TryComp<ParentOrganComponent>(part, out var parent))
        {
            var children = parent.Children;
            foreach (var child in children)
            {
                if (PartCategory(child) is { } cat)
                    present.Add(cat);
            }
        }

        foreach (var cat in expected)
        {
            if (LimbCategories.Contains(cat.Id) && !present.Contains(cat.Id))
                result.Add(cat.Id);
        }

        return result;
    }

    // ================= Окно =================

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SurgeryPatientComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextRefresh)
                continue;

            comp.NextRefresh = now + TimeSpan.FromSeconds(1);
            if (_ui.IsUiOpen(uid, SurgeryUiKey.Key))
                UpdateUi(uid, _ui.GetActors(uid, SurgeryUiKey.Key).FirstOrDefault());
        }
    }

    private void UpdateUi(EntityUid patient, EntityUid? viewer)
    {
        ToolInfo? tool = null;
        var held = Loc.GetString("surgery-held-nothing");
        if (viewer is { } user && _hands.GetActiveItem(user) is { } item)
        {
            tool = GetTool(item);
            held = tool is null
                ? Loc.GetString("surgery-held-useless", ("item", Name(item)))
                : Loc.GetString("surgery-held-tool", ("item", Name(item)),
                    ("kinds", string.Join(", ", tool.Kinds.Select(ToolName))));
        }

        var conditions = Loc.GetString(OnTable(patient) ? "surgery-cond-table" : "surgery-cond-floor") + " · " +
                         Loc.GetString(HasComp<SleepingComponent>(patient) || _mobState.IsIncapacitated(patient)
                             ? "surgery-cond-asleep" : "surgery-cond-awake");
        if (tool != null)
            conditions += " · " + Loc.GetString("surgery-cond-fail", ("chance", (int) (FailChance(patient, tool) * 100)));

        var parts = new List<SurgeryPartState>();
        foreach (var (part, category) in Parts(patient))
        {
            var entries = new List<SurgeryEntryState>();
            foreach (var surgery in _surgeries)
            {
                if (!IsAvailable(part, category, surgery))
                    continue;

                var current = CurrentStep(part, surgery);
                var steps = new List<SurgeryStepState>();
                foreach (var stepId in surgery.Surgery.Steps)
                {
                    if (Step(stepId) is not { } step)
                        continue;

                    var name = _proto.TryIndex(stepId, out var sp) ? sp.Name : stepId.Id;
                    steps.Add(new SurgeryStepState(stepId, name, string.Join(" / ", step.Tools.Select(ToolName)),
                        IsStepDone(part, step), current == stepId));
                }

                entries.Add(new SurgeryEntryState(surgery.Id, surgery.Name, steps));
            }

            var cavity = HasComp<SurgeryBonesOpenComponent>(part);
            var organs = new List<SurgeryOrganState>();
            var missing = new List<string>();
            if (cavity)
            {
                foreach (var organ in InternalOrgans(part))
                {
                    organs.Add(new SurgeryOrganState(GetNetEntity(organ), Name(organ)));
                }

                missing = MissingOrgans(patient, part, category).Select(c => Loc.GetString($"surgery-organ-{c}")).ToList();
            }

            var open = HasComp<SurgeryIncisionOpenComponent>(part);
            var missingLimbs = MissingLimbs(patient, part, category)
                .Select(c => Loc.GetString($"surgery-part-{c}")).ToList();

            parts.Add(new SurgeryPartState(GetNetEntity(part), Loc.GetString($"surgery-part-{category}"), PartStatus(part),
                entries, cavity, organs, missing, open, missingLimbs));
        }

        _ui.SetUiState(patient, SurgeryUiKey.Key, new SurgeryBuiState(Name(patient), conditions, held, parts));
    }

    private string PartStatus(EntityUid part)
    {
        if (HasComp<SurgeryBonesOpenComponent>(part))
            return Loc.GetString("surgery-status-cavity");
        if (HasComp<SurgeryIncisionOpenComponent>(part))
            return Loc.GetString("surgery-status-incision");
        return Loc.GetString("surgery-status-closed");
    }

    private string ToolName(SurgeryToolKind kind) => Loc.GetString($"surgery-tool-{kind}");

    // ================= Действия =================

    private void OnStepMessage(Entity<SurgeryPatientComponent> ent, ref SurgeryStepMessage args)
    {
        var user = args.Actor;
        if (!CanOperate(user, ent, out var reason))
        {
            _popup.PopupEntity(reason, ent, user, PopupType.SmallCaution);
            return;
        }

        var part = GetEntity(args.Part);
        var surgeryId = args.Surgery; // ref-параметр нельзя использовать внутри лямбды
        var stepId = args.Step;
        if (PartCategory(part) is not { } category ||
            _surgeries.FirstOrDefault(s => s.Id == surgeryId) is not { } surgery ||
            !IsAvailable(part, category, surgery) ||
            CurrentStep(part, surgery) is not { } current || current.Id != stepId ||
            Step(current) is not { } step)
        {
            return;
        }

        if (_hands.GetActiveItem(user) is not { } item || GetTool(item) is not { } tool || !step.Tools.Any(tool.Kinds.Contains))
        {
            _popup.PopupEntity(Loc.GetString("surgery-need-tool", ("tools", string.Join(" / ", step.Tools.Select(ToolName)))),
                ent, user, PopupType.SmallCaution);
            return;
        }

        var time = step.Duration / MathF.Max(0.1f, tool.Speed) * (OnTable(ent) ? 1f : FloorSlowdown);
        StartDoAfter(user, ent, item, time, new SurgeryDoAfterEvent(SurgeryActionType.Step, args.Part, surgeryId, stepId));
    }

    private void OnRemoveOrganMessage(Entity<SurgeryPatientComponent> ent, ref SurgeryRemoveOrganMessage args)
    {
        var user = args.Actor;
        var part = GetEntity(args.Part);
        var organ = GetEntity(args.Organ);
        if (!CanOperate(user, ent, out _) || !HasComp<SurgeryBonesOpenComponent>(part) || !InternalOrgans(part).Contains(organ))
            return;

        if (_hands.GetActiveItem(user) is not { } item || GetTool(item) is not { } tool || !tool.Kinds.Contains(SurgeryToolKind.Hemostat))
        {
            _popup.PopupEntity(Loc.GetString("surgery-need-tool", ("tools", ToolName(SurgeryToolKind.Hemostat))), ent, user, PopupType.SmallCaution);
            return;
        }

        StartDoAfter(user, ent, item, OrganActionTime / MathF.Max(0.1f, tool.Speed),
            new SurgeryDoAfterEvent(SurgeryActionType.RemoveOrgan, args.Part, organ: args.Organ));
    }

    private void OnInsertOrganMessage(Entity<SurgeryPatientComponent> ent, ref SurgeryInsertOrganMessage args)
    {
        var user = args.Actor;
        var part = GetEntity(args.Part);
        if (!CanOperate(user, ent, out _) || !HasComp<SurgeryBonesOpenComponent>(part) || PartCategory(part) is not { } category)
            return;

        if (_hands.GetActiveItem(user) is not { } organ || PartCategory(organ) is not { } organCat ||
            !MissingOrgans(ent, part, category).Contains(organCat))
        {
            _popup.PopupEntity(Loc.GetString("surgery-need-organ"), ent, user, PopupType.SmallCaution);
            return;
        }

        StartDoAfter(user, ent, organ, OrganActionTime,
            new SurgeryDoAfterEvent(SurgeryActionType.InsertOrgan, args.Part, organ: GetNetEntity(organ)));
    }

    private void OnAttachLimbMessage(Entity<SurgeryPatientComponent> ent, ref SurgeryAttachLimbMessage args)
    {
        var user = args.Actor;
        var part = GetEntity(args.Part);
        if (!CanOperate(user, ent, out _) || !HasComp<SurgeryIncisionOpenComponent>(part))
            return;

        if (_hands.GetActiveItem(user) is not { } limb || FindLimbRoot(ent, part, limb) is null)
        {
            _popup.PopupEntity(Loc.GetString("surgery-need-limb"), ent, user, PopupType.SmallCaution);
            return;
        }

        StartDoAfter(user, ent, limb, AttachLimbTime,
            new SurgeryDoAfterEvent(SurgeryActionType.AttachLimb, args.Part, organ: GetNetEntity(limb)));
    }

    /// <summary>
    /// Корневой орган конечности в отделённом теле (отрезанная рука, протез), которую можно пришить к этой части.
    /// </summary>
    private EntityUid? FindLimbRoot(EntityUid patient, EntityUid part, EntityUid limbItem)
    {
        if (limbItem == patient || !HasComp<BodyComponent>(limbItem) || PartCategory(part) is not { } category)
            return null;

        var missing = MissingLimbs(patient, part, category);
        foreach (var organ in _body.EnumerateOrgans<OrganComponent>(limbItem))
        {
            if (organ.Comp1.Category is not { } cat || !missing.Contains(cat.Id))
                continue;

            // корень — орган без родителя в отделённом теле
            if (TryComp<ChildOrganComponent>(organ, out var child) && child.Parent != null)
                continue;

            return organ.Owner;
        }

        return null;
    }

    private void StartDoAfter(EntityUid user, EntityUid patient, EntityUid used, float seconds, SurgeryDoAfterEvent ev)
    {
        var args = new DoAfterArgs(EntityManager, user, seconds, ev, patient, target: patient, used: used)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("surgery-start", ("user", user), ("patient", patient)), patient, PopupType.Small);
    }

    private void OnDoAfter(Entity<SurgeryPatientComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } used)
            return;

        args.Handled = true;
        var user = args.User;
        var patient = ent.Owner;
        var part = GetEntity(args.Part);
        if (!CanOperate(user, patient, out _) || !Exists(part))
            return;

        switch (args.Action)
        {
            case SurgeryActionType.Step:
                DoStep(user, patient, part, used, args.Surgery, args.Step);
                break;
            case SurgeryActionType.RemoveOrgan:
                DoRemoveOrgan(user, patient, part, used, GetEntity(args.Organ));
                break;
            case SurgeryActionType.InsertOrgan:
                DoInsertOrgan(user, patient, part, GetEntity(args.Organ));
                break;
            case SurgeryActionType.AttachLimb:
                DoAttachLimb(user, patient, part, GetEntity(args.Organ));
                break;
        }

        UpdateUi(patient, user);
    }

    /// <summary>Провал шага: рука соскальзывает — порез и кровь.</summary>
    private bool RollFail(EntityUid user, EntityUid patient, EntityUid tool)
    {
        if (GetTool(tool) is not { } info || !_random.Prob(FailChance(patient, info)))
            return false;

        var damage = new DamageSpecifier(_proto.Index(FailDamageType), 5);
        _damageable.TryChangeDamage(patient, damage, origin: user);
        _bloodstream.TryModifyBleedAmount(patient, 1f);
        _popup.PopupEntity(Loc.GetString("surgery-fail", ("user", user), ("patient", patient)), patient, PopupType.MediumCaution);
        return true;
    }

    private void DoStep(EntityUid user, EntityUid patient, EntityUid part, EntityUid tool, string surgeryId, string stepId)
    {
        if (PartCategory(part) is not { } category ||
            _surgeries.FirstOrDefault(s => s.Id == surgeryId) is not { } surgery ||
            !IsAvailable(part, category, surgery) ||
            CurrentStep(part, surgery) is not { } current || current.Id != stepId ||
            Step(current) is not { } step)
        {
            return;
        }

        if (RollFail(user, patient, tool))
            return;

        EntityManager.RemoveComponents(part, step.Remove);
        EntityManager.AddComponents(part, step.Add);
        if (step.Bleed != 0)
            _bloodstream.TryModifyBleedAmount(patient, step.Bleed);

        if (step.Amputate)
        {
            Amputate(user, patient, part, category);
            return;
        }

        var stepName = _proto.TryIndex(current, out var sp) ? sp.Name : stepId;
        _popup.PopupEntity(Loc.GetString("surgery-step-done", ("user", user), ("step", stepName),
            ("part", Loc.GetString($"surgery-part-{category}")), ("patient", patient)), patient, PopupType.Small);
    }

    /// <summary>
    /// Отделить конечность: она падает вместе с кистью или стопой, у родительской части остаётся открытая культя
    /// (её надо закрыть — «Закрыть» у этой части тела) и сильное кровотечение.
    /// </summary>
    private void Amputate(EntityUid user, EntityUid patient, EntityUid part, string category)
    {
        if (!LimbCategories.Contains(category))
            return;

        EntityUid? parentPart = TryComp<ChildOrganComponent>(part, out var child) ? child.Parent : null;

        // Состояния операции остаются на самой конечности — снимаем их
        RemComp<SurgeryIncisionOpenComponent>(part);
        RemComp<SurgeryBleedersClampedComponent>(part);
        RemComp<SurgerySkinRetractedComponent>(part);
        RemComp<SurgeryBonesSawedComponent>(part);
        RemComp<SurgeryBonesOpenComponent>(part);

        EnsureComp<SurgeryLimbLossComponent>(patient);
        if (_detachable.Detach(part) is not { } limb)
            return;

        if (parentPart is { } stump && Exists(stump))
        {
            EnsureComp<SurgeryIncisionOpenComponent>(stump);
            EnsureComp<SurgerySkinRetractedComponent>(stump);
            EnsureComp<SurgeryBleedersClampedComponent>(stump);
        }

        // У отделённого тела нет своего имени — называем по отрезанной части и хозяину
        _metaData.SetEntityName(limb, Loc.GetString("surgery-severed-limb",
            ("part", Loc.GetString($"surgery-severed-{category}")), ("patient", Name(patient))));

        _bloodstream.TryModifyBleedAmount(patient, AmputationBleed);
        _popup.PopupEntity(Loc.GetString("surgery-amputated", ("user", user), ("part", Loc.GetString($"surgery-part-{category}")),
            ("patient", patient)), patient, PopupType.MediumCaution);
        _hands.PickupOrDrop(user, limb);
    }

    /// <summary>Пришить конечность (отрезанную или протез): органы переходят в тело и привязываются к части.</summary>
    private void DoAttachLimb(EntityUid user, EntityUid patient, EntityUid part, EntityUid limbItem)
    {
        if (!HasComp<SurgeryIncisionOpenComponent>(part) || FindLimbRoot(patient, part, limbItem) is not { } root)
            return;

        if (!_container.TryGetContainer(patient, BodyComponent.ContainerID, out var container))
            return;

        var moving = new List<EntityUid> { root };
        foreach (var descendant in _relation.AllChildren(root))
        {
            moving.Add(descendant.Owner);
        }

        _hands.TryDrop(user, limbItem, checkActionBlocker: false);
        foreach (var organ in moving)
        {
            _container.Insert(organ, container, force: true);
        }

        _relation.Relate(part, root);
        QueueDel(limbItem);

        // Свежий шов: рану на пришитой конечности нужно закрыть
        EnsureComp<SurgeryIncisionOpenComponent>(root);
        EnsureComp<SurgerySkinRetractedComponent>(root);
        EnsureComp<SurgeryBleedersClampedComponent>(root);
        EnsureComp<SurgeryLimbLossComponent>(patient);
        _bloodstream.TryModifyBleedAmount(patient, 2f);

        _popup.PopupEntity(Loc.GetString("surgery-limb-attached", ("user", user), ("limb", root), ("patient", patient)),
            patient, PopupType.Medium);
    }

    private void DoRemoveOrgan(EntityUid user, EntityUid patient, EntityUid part, EntityUid tool, EntityUid organ)
    {
        if (!HasComp<SurgeryBonesOpenComponent>(part) || !InternalOrgans(part).Contains(organ))
            return;

        if (RollFail(user, patient, tool))
            return;

        if (!_container.TryGetContainer(patient, BodyComponent.ContainerID, out var container))
            return;

        _relation.Orphan(organ);
        _container.Remove(organ, container);
        _hands.PickupOrDrop(user, organ);
        _popup.PopupEntity(Loc.GetString("surgery-organ-removed", ("user", user), ("organ", organ), ("patient", patient)),
            patient, PopupType.Medium);
    }

    private void DoInsertOrgan(EntityUid user, EntityUid patient, EntityUid part, EntityUid organ)
    {
        if (!HasComp<SurgeryBonesOpenComponent>(part) || PartCategory(part) is not { } category ||
            PartCategory(organ) is not { } organCat || !MissingOrgans(patient, part, category).Contains(organCat) ||
            !HasComp<ChildOrganComponent>(organ))
        {
            return;
        }

        if (!_container.TryGetContainer(patient, BodyComponent.ContainerID, out var container))
            return;

        _hands.TryDrop(user, organ, checkActionBlocker: false);
        if (!_container.Insert(organ, container))
            return;

        _relation.Relate(part, organ);
        _popup.PopupEntity(Loc.GetString("surgery-organ-inserted", ("user", user), ("organ", organ), ("patient", patient)),
            patient, PopupType.Medium);
    }
}
