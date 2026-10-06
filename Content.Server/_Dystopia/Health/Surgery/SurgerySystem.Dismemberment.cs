// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: отрыв конечности от тяжёлых ран (травма «отрыв»).

using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared._Dystopia.Health.Surgery;
using Content.Shared.Body;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Dystopia.Health.Surgery;

public sealed partial class SurgerySystem
{
    [Dependency] private SharedAudioSystem _dismemberAudio = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems.WoundSystem _dismemberWounds = default!;

    private static readonly SoundSpecifier DismemberSound = new SoundCollectionSpecifier("gib");

    private void InitializeDismemberment()
    {
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

        RemComp<SurgeryIncisionOpenComponent>(part);
        RemComp<SurgeryBleedersClampedComponent>(part);
        RemComp<SurgerySkinRetractedComponent>(part);
        RemComp<SurgeryBonesSawedComponent>(part);
        RemComp<SurgeryBonesOpenComponent>(part);

        // Раны оторванной конечности не исчезают из тела: они остаются на культе (излишек уходит выше).
        // Иначе отрыв «лечил» бы человека и выводил из крита.
        var carried = _dismemberWounds.GetLimbWoundDamage(part);

        EnsureComp<SurgeryLimbLossComponent>(patient);
        if (_detachable.Detach(part) is not { } limb)
            return;

        if (parentPart is { } stumpPart && Exists(stumpPart) && !carried.Empty)
            _dismemberWounds.InduceWoundsFromDamage(stumpPart, carried);

        // Рваная культя: открыта и кровоточит (зажимы не наложены)
        if (parentPart is { } stump && Exists(stump))
        {
            EnsureComp<SurgeryIncisionOpenComponent>(stump);
            EnsureComp<SurgerySkinRetractedComponent>(stump);
        }

        _metaData.SetEntityName(limb, Loc.GetString("surgery-severed-limb",
            ("part", Loc.GetString($"surgery-severed-{category}")), ("patient", Name(patient))));

        _bloodstream.TryModifyBleedAmount(patient, AmputationBleed * 2);
        _popup.PopupEntity(Loc.GetString("trauma-dismembered", ("patient", patient),
            ("part", Loc.GetString($"surgery-part-{category}"))), patient, PopupType.LargeCaution);
        _dismemberAudio.PlayPvs(DismemberSound, patient);
    }
}
