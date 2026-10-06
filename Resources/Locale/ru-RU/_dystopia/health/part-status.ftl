## Осмотр своих частей тела (клик по кукле состояния)
part-status-title = [bold]Вы осматриваете себя:[/bold]
part-status-line = { $part }: { $state }
part-status-wounds = {" "}— { $wounds }
part-status-wound = { $type } ({ $severity })

part-status-part-Head-None = Голова
part-status-part-Chest-None = Грудь
part-status-part-Groin-None = Пах
part-status-part-Arm-Left = Левая рука
part-status-part-Arm-Right = Правая рука
part-status-part-Hand-Left = Левая кисть
part-status-part-Hand-Right = Правая кисть
part-status-part-Leg-Left = Левая нога
part-status-part-Leg-Right = Правая нога
part-status-part-Foot-Left = Левая стопа
part-status-part-Foot-Right = Правая стопа

part-status-severity-Healthy = [color=#6FBF73]цела[/color]
part-status-severity-Minor = [color=#C9D86A]лёгкие повреждения[/color]
part-status-severity-Moderate = [color=#E8D27A]повреждена[/color]
part-status-severity-Severe = [color=#E0A040]сильно повреждена[/color]
part-status-severity-Critical = [color=#E07040]в критическом состоянии[/color]
part-status-severity-Mangled = [color=#C85A54]изуродована[/color]
part-status-severity-Severed = [color=#8A8F98]отсутствует[/color]

part-status-wound-severity-Healed = заживает
part-status-wound-severity-Minor = лёгкая
part-status-wound-severity-Moderate = средняя
part-status-wound-severity-Severe = тяжёлая
part-status-wound-severity-Critical = критическая
part-status-wound-severity-Loss = страшная

part-status-damage-Blunt = ушиб
part-status-damage-Piercing = колотая рана
part-status-damage-Slash = порез
part-status-damage-Heat = ожог
part-status-damage-Cold = обморожение
part-status-damage-Holy = святой ожог
part-status-damage-Shock = электроожог
part-status-damage-Cellular = клеточное повреждение
part-status-damage-Caustic = химический ожог
part-status-damage-Radiation = лучевое поражение
part-status-damage-Poison = отравление тканей

## Осмотр другого человека вблизи
health-examine-header = [bold]Части тела:[/bold]
health-examine-part = { $part }: { $state }.
health-examine-part-wounds = { $part }: { $state } — { $wounds }.
health-examine-missing = [color=#C85A54][bold]{ $part }: нет![/bold][/color]

## Анализатор здоровья: кукла частей тела
health-analyzer-parts-hint = Наведите на часть тела, чтобы увидеть её повреждения. Щелчок — закрепить.
health-analyzer-parts-missing = [color=#C85A54]Часть отсутствует.[/color]
health-analyzer-parts-state = Состояние: { $state }
health-analyzer-parts-integrity = Целостность: { $current } / { $max }
health-analyzer-parts-no-wounds = Ран нет.
health-analyzer-parts-wounds = Раны:
health-analyzer-parts-wound = — { $type }: { $severity } ({ $points })

## Окно анализатора здоровья
health-analyzer-window-return-button-text = < Назад
health-analyzer-window-body = Тело
health-analyzer-window-organs = Органы
health-analyzer-window-chemicals = Химикаты
health-analyzer-window-conditions = Состояние
health-analyzer-window-entity-damage-vital-text = Суммарный урон:
health-analyzer-parts-missing-name = { $part } (отсутствует)

condition-body-trauma-Dismemberment = • Удалена { $targetSymmetry }{ $targetType }...
condition-target-symmetry-Left = левая
condition-target-symmetry-Right = правая
condition-target-type-Head = голова
condition-target-type-Chest = грудь
condition-target-type-Groin = пах
condition-target-type-Arm = рука
condition-target-type-Hand = кисть
condition-target-type-Leg = нога
condition-target-type-Foot = стопа
condition-body-part-Severe = • { $woundable } сильно повреждена.
condition-body-part-Critical = • { $woundable } в критическом состоянии!
condition-body-part-Mangled = • { $woundable } изуродована!
condition-body-unrevivable = • У { $entity } слабое здоровье. Не выдержит разряд дефибриллятора.
condition-body-bleeding = • У { $entity } кровотечение.
condition-organ-damage-Normal = • { $organ } в основном в порядке.
condition-organ-damage-Damaged = • { $organ } повреждён.
condition-organ-damage-Destroyed = • { $organ } разрушен...
condition-organ-rotting = • { $organ } гниёт!
condition-none = • Нет обнаруженных состояний.
group-organ-status = { $organ } функционирует на { $capacity }%
group-solution-name = { $solution }
group-solution-unknown = Неизвестно
group-solution-contents = { $reagent }: { $quantity }
group-solution-name-bloodstream = Кровоток
group-solution-name-chemicals = Химикаты в крови
group-solution-name-metabolites = Метаболиты
group-solution-name-stomach = Желудок
group-solution-name-lung = Лёгкие


## Травмы
condition-body-trauma-BoneDamage-Damaged = • { $woundable }: кость повреждена.
condition-body-trauma-BoneDamage-Cracked = • { $woundable }: кость треснула.
condition-body-trauma-BoneDamage-Broken = • { $woundable }: перелом!
condition-body-trauma-OrganDamage = • { $woundable }: повреждён орган — { $organ }.
condition-body-trauma-VeinsDamage = • { $woundable }: повреждены вены.
condition-body-trauma-NerveDamage = • { $woundable }: повреждены нервы.
popup-trauma-BoneDamage-Damaged = Вы чувствуете тупую боль в кости: { $part }.
popup-trauma-BoneDamage-Cracked = Кость трещит! { $part }.
popup-trauma-BoneDamage-Broken = Хруст! Кость сломана: { $part }!
popup-trauma-OrganDamage-Damaged = Внутри что-то сильно болит: { $organ }.
popup-trauma-OrganDamage-Destroyed = Внутри что-то лопнуло: { $organ }!
trauma-arm-fumble = Рука дрогнула от боли в сломанной кости!
trauma-dismembered = { $patient }: { $part } отрывается!

## Кровотечение, повязки, жгут
bleeding-bandaged = Вы перевязываете: { $part }. Кровотечение остановлено.
bleeding-bandage-nothing = { $part }: кровотечения нет.
tourniquet-only-limbs = Жгут накладывают только на руку или ногу.
tourniquet-already = { $part }: жгут уже наложен.
tourniquet-applied = Вы затягиваете жгут: { $part }.
tourniquet-numb = { $part } немеет под жгутом...
tourniquet-remove-verb = Снять жгут: { $part }
tourniquet-removed = Вы снимаете жгут: { $part }.
condition-part-bleeding = • { $woundable }: кровотечение!
condition-part-bleeding-heavy = • { $woundable }: сильное кровотечение!
condition-body-bandaged = • { $woundable }: перевязано.
condition-body-tourniquet = • { $woundable }: наложен жгут ({ $minutes } мин).

## Шина
splint-already = { $part }: шина уже наложена.
splint-bone-fine = { $part }: кость цела, шина не нужна.
splint-applied = Вы накладываете шину: { $part }.
splint-healed = Кость срослась: { $part }. Шина больше не нужна.
condition-body-splinted = • { $woundable }: наложена шина.

## Боль
pain-level-Moderate = Боль становится сильнее.
pain-level-Severe = Боль почти невыносима!
pain-level-Shock = Боль затмевает всё!
pain-shock = Вы теряете сознание от боли!
pain-complaint-Moderate = Всё тело ноет от боли.
pain-complaint-Severe = Вам очень больно!
pain-complaint-Shock = Боль сводит с ума!
condition-pain-Mild = • Лёгкая боль ({ $pain }).
condition-pain-Moderate = • Умеренная боль ({ $pain }).
condition-pain-Severe = • Сильная боль ({ $pain })!
condition-pain-Shock = • Болевой шок ({ $pain })!
condition-pain-suppressed = • Действует обезболивание: глушит { $percent }% боли.
condition-pain-suppressed-shock = • Действует обезболивание: глушит { $percent }% боли, болевого шока не будет.

## Сознание
consciousness-dizzy = Голова кружится, в глазах темнеет...
consciousness-faint-blood = Вы теряете сознание от потери крови!
consciousness-blood-out = Слишком много крови потеряно... Вы проваливаетесь в темноту.
condition-unconscious-blood = • Без сознания из-за кровопотери!
alerts-dystopia-pain-name = [color=red]Боль[/color]
alerts-dystopia-pain-desc = Вам больно. Сильная боль замедляет, а болевой шок лишает сознания. Помогают обезболивающие, шина и лечение ран.

## Органы
organ-fatal-destroyed = { CAPITALIZE($organ) } разрушен. Всё гаснет...
organ-failing-Lungs = Тяжело дышать, не хватает воздуха...
organ-failing-Heart = Сердце бьётся неровно, в груди давит...
organ-failing-Liver = Во рту горечь, мутит...
organ-failing-Kidneys = Тянущая боль в пояснице...
organ-failing-Stomach = Живот скручивает...
organ-failing-Brain = Мысли путаются...
organ-failing-Eyes = Всё расплывается перед глазами...

## Отладка: уничтожитель органов
organ-destroyer-title = Уничтожитель органов
organ-destroyer-target = Цель: { $name }
organ-destroyer-none = Орган не выбран.
organ-destroyer-selected = Выбран: { $organ }
organ-destroyer-amount = Урон:
organ-destroyer-damage = Нанести урон
organ-destroyer-destroy = Уничтожить
organ-destroyer-restore = Восстановить
organ-destroyer-entry = { $organ } ({ $part }) — { $percent }% ({ $integrity }/{ $cap }), { $severity }

## Лечебная хирургия
surgery-tool-BoneSetter = костоправ
surgery-tool-BoneGel = костный гель
surgery-effect-nothing = { CAPITALIZE($part) }: лечить нечего.
surgery-effect-bone = { CAPITALIZE($part) }: кость срослась.
surgery-effect-organs = { CAPITALIZE($part) }: органы восстановлены.
surgery-effect-wounds = { CAPITALIZE($part) }: раны обработаны.
surgery-effect-vessels = { CAPITALIZE($part) }: сосуды сшиты, кровотечение остановлено.
surgery-effect-nerves = { CAPITALIZE($part) }: нервы сшиты.

## Наркоз
reagent-name-propofol = пропофол
reagent-desc-propofol = Быстрый внутривенный наркоз. От 3 ед. в крови — глубокий сон, выводится за пару минут. От 30 ед. угнетает дыхание.
reagent-name-ketamine = кетамин
reagent-desc-ketamine = Диссоциативный наркоз и сильное обезболивающее. Малая доза глушит боль, от 10 ед. — сон. От 30 ед. угнетает дыхание.
reagent-name-midazolam = мидазолам
reagent-desc-midazolam = Успокоительное долгого действия: сонливость и замедленность, от 15 ед. — сон. От 40 ед. угнетает дыхание.
surgery-armor-blocks = Сначала снимите { $armor } — через броню не оперируют.

## Прижигание
cauterize-verb = Прижечь рану: { $part }
cauterize-start = Вы прижигаете рану: { $part }.
cauterize-done = { CAPITALIZE($part) }: рана прижжена, кровь остановлена.
cauterize-nothing = { CAPITALIZE($part) }: кровотечения нет.

## Лекарства
reagent-name-paracetamol = парацетамол
reagent-desc-paracetamol = Слабое обезболивающее долгого действия. Безопасно в обычной дозе; от 25 ед. в крови разрушает печень.
reagent-name-tramadol = трамадол
reagent-desc-tramadol = Обезболивающее средней силы. Сонливость от 15 ед., от 30 ед. угнетает дыхание. Слабо вызывает привыкание.
reagent-name-morphine = морфин
reagent-desc-morphine = Сильное обезболивающее: сонливость, замедленность. От 25 ед. угнетает дыхание. Вызывает зависимость.
reagent-name-promedol = промедол
reagent-desc-promedol = Сильное быстрое обезболивающее (армейское). От 20 ед. угнетает дыхание. Сильно вызывает зависимость.
reagent-name-naloxone = налоксон
reagent-desc-naloxone = Антидот опиоидов: быстро выводит морфин, промедол и трамадол из крови. У зависимого вызывает ломку.
reagent-name-calcitonin = кальцитонин
reagent-desc-calcitonin = Ускоряет сращивание костей под шиной; от 5 ед. кости понемногу срастаются и без шины.
reagent-name-ademetionine = адеметионин
reagent-desc-ademetionine = Восстанавливает печень.
reagent-name-salbutamol = сальбутамол
reagent-desc-salbutamol = Восстанавливает лёгкие и облегчает дыхание.
reagent-name-digoxin = дигоксин
reagent-desc-digoxin = Восстанавливает сердце. От 15 ед. бьёт по сердцу, от 20 ед. угнетает дыхание.
reagent-name-piracetam = пирацетам
reagent-desc-piracetam = Восстанавливает мозг.
reagent-name-ipidacrine = ипидакрин
reagent-desc-ipidacrine = Постепенно восстанавливает повреждённые нервы.
bleeding-bandaged-partial = Вы перевязываете: { $part }. Одна из ран слишком глубокая — повязка лишь ослабила кровотечение. Нужна медицинская нить, гемостатическая губка или жгут.
bleeding-too-deep = { $part }: рана слишком глубокая — повязка лишь ослабила кровотечение. Нужна медицинская нить, гемостатическая губка или жгут.
local-anesthesia-applied = Вы обезболиваете: { $part }. Часть онемела.
condition-local-anesthesia = • { $woundable }: местная анестезия.

## Зависимость
addiction-group-Opioids = опиоиды
condition-addicted = • Зависимость: { $group }.
condition-withdrawal = • Ломка ({ $group }), стадия { $stage }!
addiction-withdrawal-1 = Тянет всё тело, хочется ещё дозу...
addiction-withdrawal-2 = Ломит кости, бросает то в жар, то в холод!
addiction-withdrawal-3 = Тело выворачивает от ломки!
condition-part-packed = • { $woundable }: туго забинтовано, кровь сочится сквозь повязку.
