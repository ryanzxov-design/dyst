// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: отрыв конечности от тяжёлых ран (травма «отрыв»). Культя остаётся открытой — её закрывают
// операцией, а конечность можно пришить обратно.

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared._Dystopia.Health.Surgery;
using Content.Shared._Dystopia.Health.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Dystopia.Health.Surgery;

public sealed partial class DismembermentSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private DetachableOrganSystem _detachable = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier DismemberSound = new SoundCollectionSpecifier("gib");

    private static readonly HashSet<string> LimbCategories = new()
    {
        "ArmLeft", "ArmRight", "HandLeft", "HandRight", "LegLeft", "LegRight", "FootLeft", "FootRight",
    };

    /// <summary>Сколько крови теряется сразу при отрыве.</summary>
    private const float DismemberBleed = 6f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DismemberRequestEvent>(OnDismemberRequest);
    }

    private void OnDismemberRequest(ref DismemberRequestEvent ev)
    {
        if (ev.Handled || TerminatingOrDeleted(ev.Body)
            || !TryComp<OrganComponent>(ev.Part, out var organ) || organ.Category is not { } category
            || !LimbCategories.Contains(category.Id))
            return;

        ev.Handled = true;
        Dismember(ev.Body, ev.Part, category.Id);
    }

    /// <summary>Конечность отрывается вместе с кистью или стопой и падает рядом; остаётся открытая культя.</summary>
    public void Dismember(EntityUid patient, EntityUid part, string category)
    {
        EntityUid? parentPart = TryComp<ChildOrganComponent>(part, out var child) ? child.Parent : null;

        RemComp<IncisionOpenComponent>(part);
        RemComp<SkinRetractedComponent>(part);
        RemComp<BleedersClampedComponent>(part);
        RemComp<InternalBleedersClampedComponent>(part);
        RemComp<BonesSawedComponent>(part);
        RemComp<BonesOpenComponent>(part);
        RemComp<BodyPartSawedComponent>(part);
        RemComp<BoneSetComponent>(part);

        // Раны оторванной конечности не исчезают из тела: они остаются на культе (излишек уходит выше).
        // Иначе отрыв «лечил» бы человека и выводил из крита.
        var carried = _wounds.GetLimbWoundDamage(part);

        EnsureComp<SurgeryLimbLossComponent>(patient);
        if (_detachable.Detach(part) is not { } limb)
            return;

        if (parentPart is { } stump && Exists(stump))
        {
            if (!carried.Empty)
                _wounds.InduceWoundsFromDamage(stump, carried);

            // Рваная культя: открыта и кровоточит (зажимы не наложены)
            EnsureComp<IncisionOpenComponent>(stump);
            EnsureComp<SkinRetractedComponent>(stump);
        }

        _metaData.SetEntityName(limb, Loc.GetString("surgery-severed-limb",
            ("part", Loc.GetString($"surgery-severed-{category}")), ("patient", Name(patient))));

        _bloodstream.TryModifyBleedAmount(patient, DismemberBleed);
        _popup.PopupEntity(Loc.GetString("trauma-dismembered", ("patient", patient),
            ("part", Loc.GetString($"surgery-part-{category}"))), patient, PopupType.LargeCaution);
        _audio.PlayPvs(DismemberSound, patient);
    }
}
