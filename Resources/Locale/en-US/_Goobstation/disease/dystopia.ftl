## Diseases (Goob-Station port): reagents, guidebook, categories

entity-category-name-diseases = Diseases

reagent-name-immurin = immurin
reagent-desc-immurin = A chemical that boosts your immune system, making it work faster and stronger. The boost lasts for a while after metabolizing.
reagent-name-spaceacilin = spaceacilin
reagent-desc-spaceacilin = A widely used and effective antibacterial medicine. Slightly weakens your immunity.
reagent-name-devirate = devirate
reagent-desc-devirate = An antiviral medicine. Slightly toxic.

reagent-effect-guidebook-immunity-modifier =
    { $chance ->
        [1] Modifies
       *[other] modify
    } immunity gain rate by { NATURALFIXED($gainrate, 5) } and strength by { NATURALFIXED($strength, 5) } for at least { NATURALFIXED($time, 3) } seconds
reagent-effect-guidebook-disease-progress-change =
    { $chance ->
        [1] Modifies
       *[other] modify
    } progress of { $type } diseases by { NATURALFIXED($amount, 5) }
reagent-effect-guidebook-disease-mutate = Mutates diseases by { NATURALFIXED($amount, 4) }

ghost-role-information-plague-mouse-name = Plague mouse
ghost-role-information-plague-mouse-description = A hungry and energetic mouse. Carries diseases and spreads them with bites.

vaccinator-switch-mode = Switch mode

health-analyzer-window-disease-line = [color=red]Disease (genotype { $genotype }):[/color] infection { $infection }%, immunity { $immunity }%
guide-entry-virology = Diseases

## Debug tool "Diseases"
disease-debug-title = Diseases (debug)
disease-debug-target = Target: { $name }
disease-debug-target-no-carrier = Target: { $name } (can't carry diseases — infecting adds a carrier)
disease-debug-no-target = No target: click a creature with the tool.
disease-debug-force = ignore immunity
disease-debug-infect = Infect
disease-debug-complexity = Complexity:
disease-debug-infect-random = Random disease
disease-debug-cure-all = Cure all
disease-debug-clear-immunity = Clear immunity
disease-debug-spread = Sneeze on everyone nearby
disease-debug-refresh = Refresh
disease-debug-immunity = Immune to genotypes: { $list }
disease-debug-immunity-none = No acquired immunity.
disease-debug-healthy = No diseases.
disease-debug-unnamed = mutated strain
disease-debug-entry-header = { $name } — { $type }, genotype { $genotype }
disease-debug-entry-progress = Infection { $infection }%, immunity { $immunity }%
disease-debug-entry-params = Infection rate { $rate }/s, mutation { $mutation }, complexity { $complexity }
disease-debug-entry-effect = · { $name } (severity { $severity })
disease-debug-infection-up = Infection +10%
disease-debug-infection-down = Infection −10%
disease-debug-immunity-up = Immunity +10%
disease-debug-immunity-down = Immunity −10%
disease-debug-mutate = Mutate
disease-debug-cure = Cure
disease-debug-infect-failed = Not infected: already has this genotype or is immune.
disease-debug-spread-result = Carriers nearby: { $tried }, infected: { $infected }.
