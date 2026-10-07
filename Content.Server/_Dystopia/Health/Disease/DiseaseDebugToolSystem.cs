// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: отладочный инструмент «Болезни» (сервер): заражение, лечение, прогресс, мутации, иммунитет, «чих».

using System.Linq;
using Content.Server._Goobstation.Disease;
using Content.Shared._Dystopia.Health.Disease;
using Content.Shared._Goobstation.Disease;
using Content.Shared._Goobstation.Disease.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Health.Disease;

public sealed partial class DiseaseDebugToolSystem : EntitySystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Основа случайной болезни: бактериальная, с кашлем (как у чумных мышей).</summary>
    private static readonly EntProtoId RandomBase = "DiseaseBaseMouse";

    private static readonly ProtoId<DiseaseSpreadPrototype> AerialSpread = "Aerial";

    private TimeSpan _nextRefresh;
    private readonly List<DiseaseDebugPreset> _presets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseDebugToolComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DiseaseDebugToolComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _presets.Clear());

        Subs.BuiEvents<DiseaseDebugToolComponent>(DiseaseDebugUiKey.Key, subs =>
        {
            subs.Event<DiseaseDebugInfectMessage>(OnInfect);
            subs.Event<DiseaseDebugInfectRandomMessage>(OnInfectRandom);
            subs.Event<DiseaseDebugCureMessage>(OnCure);
            subs.Event<DiseaseDebugCureAllMessage>(OnCureAll);
            subs.Event<DiseaseDebugProgressMessage>(OnProgress);
            subs.Event<DiseaseDebugMutateMessage>(OnMutate);
            subs.Event<DiseaseDebugClearImmunityMessage>(OnClearImmunity);
            subs.Event<DiseaseDebugSpreadMessage>(OnSpread);
            subs.Event<DiseaseDebugRefreshMessage>(OnRefresh);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Пока окно открыто — прогресс болезней обновляется вживую
        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<DiseaseDebugToolComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, DiseaseDebugUiKey.Key))
                UpdateUi((uid, comp));
        }
    }

    private void OnAfterInteract(Entity<DiseaseDebugToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        Open(ent, args.User, target);
    }

    private void OnUseInHand(Entity<DiseaseDebugToolComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Open(ent, args.User, args.User);
    }

    private void Open(Entity<DiseaseDebugToolComponent> ent, EntityUid user, EntityUid target)
    {
        ent.Comp.Target = target;
        _ui.OpenUi(ent.Owner, DiseaseDebugUiKey.Key, user);
        UpdateUi(ent);
    }

    private bool TryTarget(Entity<DiseaseDebugToolComponent> ent, out EntityUid target)
    {
        target = ent.Comp.Target ?? EntityUid.Invalid;
        return ent.Comp.Target != null && !TerminatingOrDeleted(target);
    }

    // ===================== Действия =====================

    private void OnInfect(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugInfectMessage args)
    {
        if (!TryTarget(ent, out var target) || !_proto.HasIndex<EntityPrototype>(args.Proto))
            return;

        if (!_disease.TryInfect(target, new EntProtoId(args.Proto), out _, args.Force))
            _popup.PopupEntity(Loc.GetString("disease-debug-infect-failed"), ent, args.Actor);

        UpdateUi(ent);
    }

    private void OnInfectRandom(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugInfectRandomMessage args)
    {
        if (!TryTarget(ent, out var target))
            return;

        var disease = _disease.MakeRandomDisease(RandomBase, Math.Clamp(args.Complexity, 1f, 200f));
        if (disease == null)
            return;

        if (!_disease.TryInfect(target, disease.Value, force: true))
            QueueDel(disease);

        UpdateUi(ent);
    }

    private void OnCure(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugCureMessage args)
    {
        if (!TryTarget(ent, out var target))
            return;

        _disease.TryCure(target, GetEntity(args.Disease));
        UpdateUi(ent);
    }

    private void OnCureAll(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugCureAllMessage args)
    {
        if (!TryTarget(ent, out var target))
            return;

        _disease.TryCureAll(target);
        UpdateUi(ent);
    }

    private void OnProgress(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugProgressMessage args)
    {
        var disease = GetEntity(args.Disease);
        if (!HasComp<DiseaseComponent>(disease))
            return;

        if (args.Infection != 0f)
            _disease.ChangeInfectionProgress(disease, args.Infection);
        if (args.Immunity != 0f)
            _disease.ChangeImmunityProgress(disease, args.Immunity);

        UpdateUi(ent);
    }

    private void OnMutate(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugMutateMessage args)
    {
        var disease = GetEntity(args.Disease);
        if (!HasComp<DiseaseComponent>(disease))
            return;

        // Как несколько передач подряд — чтобы мутация была заметна
        _disease.MutateDisease(disease, 0.5f);
        UpdateUi(ent);
    }

    private void OnClearImmunity(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugClearImmunityMessage args)
    {
        if (!TryTarget(ent, out var target) || !TryComp<ImmunityComponent>(target, out var immunity))
            return;

        immunity.ImmuneTo.Clear();
        UpdateUi(ent);
    }

    private void OnSpread(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugSpreadMessage args)
    {
        if (!TryTarget(ent, out var target) || !TryComp<DiseaseCarrierComponent>(target, out var carrier))
            return;

        var diseases = carrier.Diseases.ContainedEntities.ToList();
        var infected = 0;
        var tried = 0;
        foreach (var other in _lookup.GetEntitiesInRange<DiseaseCarrierComponent>(Transform(target).Coordinates, ent.Comp.SpreadRange))
        {
            if (other.Owner == target)
                continue;

            tried++;
            foreach (var disease in diseases)
            {
                // Сила и шанс 1 — дальше решают маски, костюмы и иммунитет цели
                if (_disease.DoInfectionAttempt(other.Owner, disease, 1f, 1f, AerialSpread))
                    infected++;
            }
        }

        _popup.PopupEntity(Loc.GetString("disease-debug-spread-result", ("tried", tried), ("infected", infected)), ent, args.Actor);
        UpdateUi(ent);
    }

    // ===================== Окно =====================

    private void OnRefresh(Entity<DiseaseDebugToolComponent> ent, ref DiseaseDebugRefreshMessage args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DiseaseDebugToolComponent> ent)
    {
        var hasTarget = TryTarget(ent, out var target);
        var entries = new List<DiseaseDebugEntry>();
        var immuneTo = new List<int>();
        var canCarry = false;

        if (hasTarget && TryComp<DiseaseCarrierComponent>(target, out var carrier))
        {
            canCarry = true;
            var diseases = carrier.Diseases.ContainedEntities;
            foreach (var uid in diseases)
            {
                if (TryComp<DiseaseComponent>(uid, out var disease))
                    entries.Add(MakeEntry(uid, disease));
            }
        }

        if (hasTarget && TryComp<ImmunityComponent>(target, out var immunity))
            immuneTo.AddRange(immunity.ImmuneTo.OrderBy(g => g));

        var state = new DiseaseDebugBuiState(
            hasTarget ? Name(target) : string.Empty,
            hasTarget,
            canCarry,
            entries,
            GetPresets(),
            immuneTo);

        _ui.SetUiState(ent.Owner, DiseaseDebugUiKey.Key, state);
    }

    private DiseaseDebugEntry MakeEntry(EntityUid uid, DiseaseComponent disease)
    {
        var effects = new List<DiseaseDebugEffectEntry>();
        var contained = disease.Effects.ContainedEntities;
        foreach (var effectUid in contained)
        {
            if (TryComp<DiseaseEffectComponent>(effectUid, out var effect))
                effects.Add(new DiseaseDebugEffectEntry(Name(effectUid), effect.Severity));
        }

        var name = Name(uid);
        if (string.IsNullOrWhiteSpace(name))
            name = Loc.GetString("disease-debug-unnamed");

        var type = _proto.TryIndex(disease.DiseaseType, out var typeProto) ? typeProto.LocalizedName : disease.DiseaseType.Id;
        return new DiseaseDebugEntry(GetNetEntity(uid), name, type, disease.Genotype, disease.InfectionProgress,
            disease.ImmunityProgress, disease.InfectionRate, disease.MutationRate, disease.Complexity, effects);
    }

    private List<DiseaseDebugPreset> GetPresets()
    {
        if (_presets.Count > 0)
            return _presets;

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.HasComp<DiseaseComponent>(_factory))
                continue;

            var name = string.IsNullOrWhiteSpace(proto.Name) ? proto.ID : proto.Name;
            _presets.Add(new DiseaseDebugPreset(proto.ID, string.IsNullOrWhiteSpace(proto.EditorSuffix) ? name : $"{name} ({proto.EditorSuffix})"));
        }

        _presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        return _presets;
    }
}
