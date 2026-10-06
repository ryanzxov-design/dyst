// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лечебная хирургия — срастить кость, восстановить органы, обработать раны, сшить сосуды и нервы.

using System.Linq;
using Content.Shared._Dystopia.Health.Medical.Bleeding;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Traumas.Systems;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Components;
using Content.Shared._Dystopia.Health.Medical.Surgery.Wounds.Systems;
using Content.Shared._Dystopia.Health.Surgery;
using Content.Shared.Popups;

namespace Content.Server._Dystopia.Health.Surgery;

public sealed partial class SurgerySystem
{
    [Dependency] private TraumaSystem _treatTraumas = default!;
    [Dependency] private WoundBleedingSystem _treatBleeding = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Armor.ArmorCoverageSystem _treatArmor = default!;
    [Dependency] private Content.Shared._Dystopia.Health.Medical.Pain.LocalAnesthesiaSystem _treatLocal = default!;

    /// <summary>Есть ли на части что лечить этим эффектом.</summary>
    private bool NeedsTreatment(EntityUid part, SurgeryEffect need)
    {
        switch (need)
        {
            case SurgeryEffect.None:
                return true;

            case SurgeryEffect.MendBone:
                return _treatTraumas.GetBoneSeverity(part, effective: false) != BoneSeverity.Normal;

            case SurgeryEffect.RepairOrgans:
                foreach (var organ in InternalOrgans(part))
                {
                    if (TryComp<OrganIntegrityComponent>(organ, out var integrity) && integrity.Integrity < integrity.IntegrityCap)
                        return true;
                }

                return false;

            case SurgeryEffect.TendWounds:
                return _dismemberWounds.HasWounds(part);

            case SurgeryEffect.RepairVessels:
                return HasTrauma(part, TraumaSystem.VeinsDamage) || _treatBleeding.IsBleeding(part);

            case SurgeryEffect.RepairNerves:
                return HasTrauma(part, TraumaSystem.NerveDamage);
        }

        return false;
    }

    private bool HasTrauma(EntityUid part, Robust.Shared.Prototypes.ProtoId<TraumaTypePrototype> type)
    {
        return _treatTraumas.GetWoundableTraumas(part).Any(t => t.Comp.TraumaType == type);
    }

    private void RemoveTraumas(EntityUid part, Robust.Shared.Prototypes.ProtoId<TraumaTypePrototype> type)
    {
        foreach (var trauma in _treatTraumas.GetWoundableTraumas(part).ToList())
        {
            if (trauma.Comp.TraumaType == type)
                _treatTraumas.RemoveTrauma(trauma);
        }
    }

    /// <summary>Выполнить лечебный эффект шага. Возвращает ключ сообщения о результате (или null).</summary>
    private string? ApplyTreatment(EntityUid patient, EntityUid part, SurgeryStepComponent step)
    {
        switch (step.Effect)
        {
            case SurgeryEffect.MendBone:
            {
                if (!TryComp<WoundableComponent>(part, out var woundable) || _treatTraumas.GetBone(woundable) is not { } bone
                    || !TryComp<BoneComponent>(bone, out var boneComp))
                    return "surgery-effect-nothing";

                // Целая кость — травмы перелома снимаются сами (TraumaSystem.OnBoneIntegrityChanged)
                _treatTraumas.SetBoneIntegrity(bone, boneComp.IntegrityCap, boneComp);
                RemoveTraumas(part, TraumaSystem.BoneDamage);
                RemComp<BoneSplintedComponent>(part);
                return "surgery-effect-bone";
            }

            case SurgeryEffect.RepairOrgans:
            {
                var any = false;
                foreach (var organ in InternalOrgans(part).ToList())
                {
                    if (!TryComp<OrganIntegrityComponent>(organ, out var integrity) || integrity.Integrity >= integrity.IntegrityCap)
                        continue;

                    _treatTraumas.RestoreOrganIntegrity(organ, integrity);
                    any = true;
                }

                RemoveTraumas(part, TraumaSystem.OrganDamage);
                return any ? "surgery-effect-organs" : "surgery-effect-nothing";
            }

            case SurgeryEffect.TendWounds:
            {
                var healed = _dismemberWounds.TendWounds(part, step.EffectAmount);
                _treatBleeding.StopBleeding(part);
                return healed > 0 ? "surgery-effect-wounds" : "surgery-effect-nothing";
            }

            case SurgeryEffect.RepairVessels:
            {
                var any = HasTrauma(part, TraumaSystem.VeinsDamage);
                RemoveTraumas(part, TraumaSystem.VeinsDamage);
                any |= _treatBleeding.StopBleeding(part);
                return any ? "surgery-effect-vessels" : "surgery-effect-nothing";
            }

            case SurgeryEffect.RepairNerves:
            {
                if (!HasTrauma(part, TraumaSystem.NerveDamage))
                    return "surgery-effect-nothing";

                RemoveTraumas(part, TraumaSystem.NerveDamage);
                return "surgery-effect-nerves";
            }
        }

        return null;
    }
}
