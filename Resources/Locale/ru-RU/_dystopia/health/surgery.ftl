## Хирургия (перенос Shitmed из Goob-Station)

entity-category-name-surgeries = Операции
entity-category-name-surgery-steps = Шаги операций

surgery-verb-text = Оперировать
surgery-verb-message = Открыть окно операции.
surgery-error-self-surgery = Оперировать себя нельзя.
surgery-error-laying = Пациент должен лежать — на столе, кровати или на полу.

## Окно
surgery-ui-window-title = Операция
surgery-ui-window-parts = < Части тела
surgery-ui-window-surgeries = < Операции
surgery-ui-window-steps = < Шаги
surgery-ui-window-require = Сначала
surgery-ui-window-done = Операция завершена.
surgery-ui-window-no-surgeries = Здесь нечего оперировать.
surgery-ui-window-requirement-first = Сначала выполните предыдущую операцию (наверху).
surgery-ui-window-steps-error-armor = Мешает { $item } — снимите.
surgery-ui-window-steps-error-missing-tool = Нужен инструмент: { $tool }.
surgery-ui-window-steps-error-table = Нужен операционный стол.
surgery-ui-window-steps-error-missing-limb = Возьмите в руку подходящую конечность или протез.
surgery-ui-window-steps-error-missing-organ = Возьмите в руку подходящий орган.

## Инструменты
surgery-tool-header = Этим можно оперировать:
surgery-tool-used = - [color={ $color }]{ $tool }[/color] (одноразово, скорость { $speed })
surgery-tool-unlimited = - [color={ $color }]{ $tool }[/color] (скорость { $speed })
surgery-tool-examinable-verb-text = Хирургия
surgery-tool-examinable-verb-message = Чем этот предмет служит в операциях.
surgery-tool-turn-on = Сначала включите инструмент.

surgery-tool-name-scalpel = скальпель
surgery-tool-name-retractor = ретрактор
surgery-tool-name-hemostat = гемостат
surgery-tool-name-bonesaw = пила
surgery-tool-name-cautery = прибор для прижигания
surgery-tool-name-drill = дрель
surgery-tool-name-bonesetter = костоправ
surgery-tool-name-bonegel = костный гель
surgery-tool-name-tweezers = гемостат
surgery-tool-name-tending = гемостат
surgery-tool-name-stitches = медицинская нить

## Сообщения шагов
surgery-popup-step-generic = { CAPITALIZE($user) } оперирует { $target }: { $step } ({ $part }).
surgery-popup-step-SurgeryStepOpenIncisionScalpel = { CAPITALIZE($user) } делает надрез ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRetractSkin = { CAPITALIZE($user) } разводит края раны ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepClampBleeders = { CAPITALIZE($user) } пережимает кровоточащие сосуды ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepCloseBloodOutputs = { CAPITALIZE($user) } сшивает повреждённые сосуды ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepClampInternalBleeders = { CAPITALIZE($user) } пережимает внутренние сосуды ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepSawBones = { CAPITALIZE($user) } пилит кость ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepPriseOpenBones = { CAPITALIZE($user) } раскрывает кости ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepCloseBones = { CAPITALIZE($user) } сводит кости ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepSealBones = { CAPITALIZE($user) } скрепляет кости гелем ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepCloseIncision = { CAPITALIZE($user) } прижигает и закрывает разрез ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepSetBones = { CAPITALIZE($user) } вправляет кость ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepMendBones = { CAPITALIZE($user) } скрепляет кость гелем ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepHealOrgans = { CAPITALIZE($user) } восстанавливает органы ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRepairBrain = { CAPITALIZE($user) } восстанавливает ткани мозга { $target }.
surgery-popup-step-SurgeryStepRemoveOrgan = { CAPITALIZE($user) } извлекает орган ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRemoveOrgan-failed = Не удалось извлечь орган.
surgery-popup-step-SurgeryStepInsertOrgan = { CAPITALIZE($user) } вставляет орган ({ $part }) в тело { $target }.
surgery-popup-step-SurgeryStepSealOrganWound = { CAPITALIZE($user) } закрепляет орган ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepSawFeature = { CAPITALIZE($user) } пилит кость ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRemoveFeature = { CAPITALIZE($user) } отделяет { $part } от тела { $target }!
surgery-popup-step-SurgeryStepInsertFeature = { CAPITALIZE($user) } прикладывает конечность к телу { $target }.
surgery-popup-step-SurgeryStepSealWounds = { CAPITALIZE($user) } пришивает конечность к телу { $target }.
surgery-popup-step-SurgeryStepCarefulIncisionScalpel = { CAPITALIZE($user) } иссекает края раны ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRepairBruteTissue = { CAPITALIZE($user) } обрабатывает раны ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRepairBurnTissue = { CAPITALIZE($user) } иссекает обожжённые ткани ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepSealTendWound = { CAPITALIZE($user) } закрывает рану ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRepairVeins = { CAPITALIZE($user) } сшивает вены ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRepairNerves = { CAPITALIZE($user) } сшивает нервы ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepRemoveSeveredSkin = { CAPITALIZE($user) } иссекает омертвевшие ткани ({ $part }) у { $target }.
surgery-popup-step-SurgeryStepSealDismembermentWound = { CAPITALIZE($user) } ушивает рваную рану ({ $part }) у { $target }.

## Конечности
surgery-amputated = { CAPITALIZE($user) } отделяет конечность от тела { $patient }!
surgery-limb-attached = { CAPITALIZE($user) } пришивает конечность к телу { $patient }.
surgery-no-leg-stand = Без ноги не встать.
surgery-severed-limb = { $part } ({ $patient })
surgery-severed-ArmLeft = отрезанная левая рука
surgery-severed-ArmRight = отрезанная правая рука
surgery-severed-HandLeft = отрезанная левая кисть
surgery-severed-HandRight = отрезанная правая кисть
surgery-severed-LegLeft = отрезанная левая нога
surgery-severed-LegRight = отрезанная правая нога
surgery-severed-FootLeft = отрезанная левая стопа
surgery-severed-FootRight = отрезанная правая стопа

surgery-part-Torso = Торс
surgery-part-Groin = Живот
surgery-part-Head = Голова
surgery-part-ArmLeft = левая рука
surgery-part-ArmRight = правая рука
surgery-part-HandLeft = левая кисть
surgery-part-HandRight = правая кисть
surgery-part-LegLeft = левая нога
surgery-part-LegRight = правая нога
surgery-part-FootLeft = левая стопа
surgery-part-FootRight = правая стопа
