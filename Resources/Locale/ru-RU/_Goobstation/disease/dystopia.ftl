## Болезни (перенос из Goob-Station): реагенты, справочник, категории и то, чего не было в переводе Goob

entity-category-name-diseases = Болезни

reagent-name-immurin = иммурин
reagent-desc-immurin = Химическое вещество, усиливающее вашу иммунную систему, заставляя её работать быстрее и эффективнее. Усиление сохраняется некоторое время даже после полного процесса метаболизма. На вкус как лакрица.
reagent-name-spaceacilin = космоцилин
reagent-desc-spaceacilin = Широко используемое и эффективное антибактериальное лекарство. Слегка ослабляет ваш иммунитет.
reagent-name-devirate = девирейт
reagent-desc-devirate = Противовирусное лекарство. Слегка токсичное.

reagent-effect-guidebook-immunity-modifier =
    { $chance ->
        [1] Изменяет
       *[other] изменяет
    } скорость повышения иммунитета на { NATURALFIXED($gainrate, 5) }, силу на { NATURALFIXED($strength, 5) } как минимум на { NATURALFIXED($time, 3) } { $time ->
        [one] секунду
        [few] секунды
       *[other] секунд
    }
reagent-effect-guidebook-disease-progress-change =
    { $chance ->
        [1] Изменяет
       *[other] изменяет
    } прогресс заболевания с типом { $type } на { NATURALFIXED($amount, 5) }
reagent-effect-guidebook-disease-mutate = Мутирует заболевания на { NATURALFIXED($amount, 4) }

ghost-role-information-plague-mouse-name = Чумная мышь
ghost-role-information-plague-mouse-description = Голодная и энергичная мышь. Переносит болезни и распространяет их через укусы.

vaccinator-switch-mode = Сменить режим

ent-ClothingEyesHudViro = визор патобиолога
    .desc = Медицинский визор с продвинутым определением болезней: видно даже начинающееся заражение.

health-analyzer-window-disease-line = [color=red]Болезнь (генотип { $genotype }):[/color] инфекция { $infection }%, иммунитет { $immunity }%
guide-entry-virology = Болезни

## Отладочный инструмент «Болезни»
disease-debug-title = Болезни (отладка)
disease-debug-target = Цель: { $name }
disease-debug-target-no-carrier = Цель: { $name } (не болеет — заражение создаст носителя)
disease-debug-no-target = Цель не выбрана: щёлкните инструментом по существу.
disease-debug-force = даже при иммунитете
disease-debug-infect = Заразить
disease-debug-complexity = Сложность:
disease-debug-infect-random = Случайная болезнь
disease-debug-cure-all = Вылечить всё
disease-debug-clear-immunity = Сбросить иммунитет
disease-debug-spread = Чихнуть на всех рядом
disease-debug-refresh = Обновить
disease-debug-immunity = Иммунитет к генотипам: { $list }
disease-debug-immunity-none = Приобретённого иммунитета нет.
disease-debug-healthy = Болезней нет.
disease-debug-unnamed = мутировавший штамм
disease-debug-entry-header = { $name } — { $type }, генотип { $genotype }
disease-debug-entry-progress = Инфекция { $infection }%, иммунитет { $immunity }%
disease-debug-entry-params = Рост инфекции { $rate }/с, мутации { $mutation }, сложность { $complexity }
disease-debug-entry-effect = · { $name } (сила { $severity })
disease-debug-infection-up = Инфекция +10%
disease-debug-infection-down = Инфекция −10%
disease-debug-immunity-up = Иммунитет +10%
disease-debug-immunity-down = Иммунитет −10%
disease-debug-mutate = Мутировать
disease-debug-cure = Вылечить
disease-debug-infect-failed = Не заразилось: уже болеет этим генотипом или есть иммунитет.
disease-debug-spread-result = Рядом носителей: { $tried }, заражено: { $infected }.
