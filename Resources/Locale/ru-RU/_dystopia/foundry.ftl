# Dystopia: ручное литьё.

reagent-name-dystopia-molten-slag = шлак
reagent-desc-dystopia-molten-slag = Расплавленная пустая порода. Всплывает над металлом; снимите её, иначе отливка выйдет браком.

dystopia-foundry-temperature = Температура: [color=#ff9a50]{ $temperature } °C[/color].
dystopia-foundry-contents-empty = Внутри пусто.
dystopia-foundry-contents-liquid = Внутри { $volume } из { $max } ед., [color=#ff9a50]жидкое[/color]:
dystopia-foundry-contents-solid = Внутри { $volume } из { $max } ед., [color=#9aa4b0]застывшее[/color]:
dystopia-foundry-contents-entry = — [color={ $color }]{ $name }[/color]: { $amount } ед.

dystopia-hot-item-need-tongs = { CAPITALIZE(THE($item)) } раскалён — берите клещами.
dystopia-hot-item-burn = Вы обжигаете руку о { $item }!

dystopia-crucible-solidified = Металл в тигле застыл — сначала разогрейте его.
dystopia-crucible-full = Тигель полон: больше не влезет.
dystopia-crucible-loaded = Вы кладёте { $item } в тигель.
dystopia-crucible-not-empty-item = Сначала опустошите { $item }.
dystopia-crucible-inside = [bold]Внутри[/bold] ({ $pieces } из { $max } кусков, плавится при { $temperature } °C):
dystopia-crucible-inside-entry = — { $name } × { $count }
dystopia-crucible-inside-empty = [bold]Внутри:[/bold] пусто.
dystopia-crucible-molten = [bold]Расплавленное[/bold] ({ $volume } из { $max } ед.):
dystopia-crucible-solid = [bold]Застывшее[/bold] ({ $volume } из { $max } ед.) — разогрейте, чтобы разлить:
dystopia-crucible-melt-none = [bold]Расплавленное:[/bold] пока нет.
dystopia-crucible-skim-verb = Снять шлак
dystopia-crucible-skim-need-tongs = Шлак снимают клещами.
dystopia-crucible-skim-too-little = Шлака нет.
dystopia-crucible-has-slag = [color=#c08050]Сверху плавает шлак[/color] — снимите его перед разливкой (клещи в руке, «Снять шлак»), иначе отливка выйдет браком.
dystopia-crucible-skimmed = Вы снимаете шлак: { $count } { $count ->
    [one] кусок
    [few] куска
   *[other] кусков
}.

dystopia-forge-no-fuel = В горне нет топлива.
dystopia-forge-lit = Горн разгорается.
dystopia-forge-not-lit = Горн не горит.
dystopia-forge-fuel-full = Горн забит топливом.
dystopia-forge-fuel-added = Топлива хватит на { $seconds } с.
dystopia-forge-examine-lit = Горн [color=#ff8a30]горит[/color].
dystopia-forge-examine-cold = Горн холодный.
dystopia-forge-examine-fuel = Топлива на { $seconds } с.
dystopia-forge-examine-max = Жар: до { $base } °C, с мехами рядом — до { $bellows } °C.
dystopia-forge-examine-bellows = Меха раздувают пламя.

dystopia-bellows-verb = Качать меха
dystopia-bellows-not-anchored = Меха не закреплены — прикрутите их к полу ключом.
dystopia-bellows-no-forge = Рядом нет горящего горна.
dystopia-bellows-examine = Качайте рукой: раздувают горящий горн в соседней клетке.
dystopia-bellows-examine-loose = [color=#d0a040]Не прикручены к полу.[/color]

dystopia-tongs-gripped = Вы зажимаете { $item } клещами.
dystopia-tongs-released = Вы разжимаете клещи и ставите { $item }.
dystopia-tongs-release-verb = Разжать клещи
dystopia-tongs-holding = В клещах зажат { $item }.
dystopia-tongs-anchored = { CAPITALIZE(THE($item)) } прикручен к полу — сначала открутите.
dystopia-tongs-cant-place = Сюда не поставить.
dystopia-tongs-surface-full = Там уже что-то стоит.
dystopia-tongs-poured = Вы разливаете { $amount } ед. в { $target }.
dystopia-tongs-pour-empty = В тигле пусто.
dystopia-tongs-pour-full = Больше не влезет.

dystopia-tongs-too-heavy = { CAPITALIZE(THE($item)) } слишком тяжёл для клещей — его ставят прямо в горн.

# Мастерская: горн с большим тиглем, литейные желоба, литейный стол.
dystopia-tilt-pour-verb = Наклонить тигель
dystopia-tilt-pour-no-target = К горну не подведён литейный желоб, и рядом нет литейного стола.
dystopia-tilt-pour-done = Вы наклоняете тигель и разливаете { $amount } ед.
dystopia-channel-no-moulds = Желоба никуда не ведут: ни один их конец не выходит к литейному столу.
dystopia-channel-moulds-full = Формы уже полны (или в столах нет форм).
dystopia-crucible-load-verb = Засыпать в тигель
dystopia-forge-mount-occupied = В горн уже вставлен тигель.
dystopia-forge-mount-clear = Сначала снимите с горна малый тигель.
dystopia-forge-mounted = Вы ставите большой тигель в горн.
dystopia-forge-unmounted = Вы вынимаете большой тигель из горна.
dystopia-forge-unmount-verb = Снять большой тигель
dystopia-forge-mounted-examine = Клик углём или досками — в топку; руда и лом — в тигель; уголь в тигель (для стали) — ПКМ «Засыпать в тигель». Разлив — «Наклонить тигель»: в желоб, подведённый к горну.
dystopia-casting-table-slot = литейную форму
dystopia-casting-table-no-form = Формы нет — вставьте литейную форму.
dystopia-casting-table-form-busy = В форме металл — дождитесь, пока выйдет отливка.

dystopia-quench-hiss = { CAPITALIZE(THE($item)) } шипит: вода вскипает паром.

dystopia-mould-shape = Форма: { $shape }, { $volume } ед. расплава на отливку.
dystopia-mould-shape-ingot = слиток
dystopia-mould-shape-sheet = лист
dystopia-mould-shape-rod = прут

ent-DystopiaForge = горн
    .desc = Кирпичный горн с колосником. Малый тигель ставят сверху, большой встаёт внутрь. Топят углём или досками, разжигают огнём. Для железа нужны меха.
ent-DystopiaBellows = меха
    .desc = Кожаные меха на деревянной раме. Прикрутите вплотную к горну и качайте, и жар поднимется до тысячи семисот градусов.
ent-DystopiaCrucibleSmall = малый тигель
    .desc = Малый шамотный тигель на десять кусков. Ставится на горн. Горячим его берут только клещами.
ent-DystopiaTongs = клещи
    .desc = Кузнечные клещи с длинными ручками. Ими берут раскалённый тигель и изложницы. Без них — ожог.
ent-DystopiaMouldIngot = изложница для слитка
    .desc = Чугунная изложница на один слиток. Прикручивается к полу ключом.
ent-DystopiaMouldSheet = изложница для листа
    .desc = Изложница на один лист, плоская и неглубокая.
ent-DystopiaMouldRod = изложница для прутьев
    .desc = Изложница на два прутка: длинная канавка в чугуне.
ent-DystopiaSlag = шлак
    .desc = Шлак. Пористая серая корка, снятая с расплава. Звенит, если стукнуть.
    .suffix = Полный
ent-DystopiaSlag1 = шлак
    .desc = Шлак. Пористая серая корка, снятая с расплава. Звенит, если стукнуть.
    .suffix = Один
stack-dystopia-slag = { $amount ->
    [1] кусок
    [few] куска
    *[other] кусков
} шлака
ent-DystopiaCrucibleLarge = большой тигель
    .desc = Большой тигель на тридцать кусков. Сам по себе не греется, его вставляют в горн. Холодным носят двумя руками.
ent-DystopiaForgeLargeCrucible = горн с большим тиглем
    .desc = Горн с вмурованным большим тиглем. Чтобы слить металл в желоб, тигель наклоняют. Снять его можно через ПКМ.
ent-DystopiaCastingChannelStraight = литейный желоб
    .desc = Прямой литейный желоб. Расплав течёт по нему к столам и изложницам.
ent-DystopiaCastingChannelCorner = угловой литейный желоб
    .desc = Угловой литейный желоб, поворачивает поток под прямым углом.
ent-DystopiaCastingChannelTee = литейный желоб-тройник
    .desc = Литейный тройник. Делит поток на две стороны, ближняя форма наполняется первой.
ent-DystopiaCastingChannelCross = литейный желоб-перекрёсток
    .desc = Литейная крестовина. Поток расходится на три стороны.
ent-DystopiaCastingTable = литейный стол
    .desc = Литейный стол: чугунная плита с гнездом под сменную форму. Готовые отливки лежат в столе, пока их не заберут.
ent-DystopiaCastingFormIngot1 = литейная форма: слиток ×1
    .desc = Форма на один слиток. Вставляется в литейный стол.
ent-DystopiaCastingFormIngot10 = литейная форма: слиток ×10
    .desc = Форма на десять слитков за одну заливку. Вставляется в литейный стол.
ent-DystopiaCastingFormSheet1 = литейная форма: лист ×1
    .desc = Форма на один лист. Вставляется в литейный стол.
ent-DystopiaCastingFormSheet10 = литейная форма: лист ×10
    .desc = Форма на десять листов за одну заливку. Вставляется в литейный стол.
ent-DystopiaCastingFormRod1 = литейная форма: прут ×1
    .desc = Форма на два прутка. Вставляется в литейный стол.
ent-DystopiaCastingFormRod10 = литейная форма: прут ×10
    .desc = Форма на двадцать прутков за одну заливку. Вставляется в литейный стол.
construction-category-dystopia-industry = Промышленность

# Литейный стол: отливки внутри
dystopia-casting-table-nothing = Готовых отливок нет.
dystopia-casting-table-taken = Вы достаёте отливки из стола.
dystopia-casting-table-take-verb = Достать отливки
dystopia-casting-table-inside = Готовых отливок внутри: { $count } (достать — клик рукой или ПКМ).

# Электрический горн
dystopia-forge-electric-on-verb = Включить горн
dystopia-forge-electric-off-verb = Выключить горн
dystopia-forge-electric-turned-on = Вы включаете горн: ТЭНы наливаются жаром.
dystopia-forge-electric-turned-off = Вы выключаете горн.
dystopia-forge-electric-examine-working = Горн [color=#ff8a30]включён[/color] и греет.
dystopia-forge-electric-examine-unpowered = Горн включён, но [color=#d05050]нет питания[/color].
dystopia-forge-electric-examine-off = Горн выключен (ПКМ — включить).
dystopia-forge-electric-examine-max = Жар: до { $max } °C, мехов не нужно.
dystopia-forge-upgrade-already = Этот горн уже электрический.
dystopia-forge-upgraded = Вы ставите ТЭНы — горн стал электрическим.
dystopia-forge-mounted-electric-examine = Всё, что плавится, — кликом в тигель (уголь тоже). Включение — ПКМ. Разлив — «Наклонить тигель»: в желоб, подведённый к горну.

ent-DystopiaForgeElectric = электрический горн
    .desc = Горн, переделанный на ТЭНы. Ни угля, ни мехов: включил, и он греется до тысячи семисот. Берёт четыре киловатта.
ent-DystopiaForgeElectricLargeCrucible = электрический горн с большим тиглем
    .desc = Электрический горн с большим тиглем. Чтобы слить металл в желоб, тигель наклоняют.
ent-DystopiaForgeHeaterKit = комплект ТЭНов
    .desc = Комплект ТЭНов с проводкой. Кликните им по горну, чтобы перевести его на электричество.

# Дуговая сталеплавильная печь
dystopia-arc-furnace-slag-ejected = Вы выгружаете шлак из печи.
dystopia-arc-furnace-examine-on = Печь [color=#a0c8ff]включена[/color].
dystopia-arc-furnace-examine-off = Печь выключена.
dystopia-arc-furnace-examine-selected = Разливает: { $alloy }.
dystopia-arc-furnace-examine-slag = Снятого шлака: { $count }.
dystopia-arc-furnace-ui-title = Дуговая сталеплавильная печь
dystopia-arc-furnace-ui-turn-on = Включить
dystopia-arc-furnace-ui-turn-off = Выключить
dystopia-arc-furnace-ui-status-off = Печь выключена
dystopia-arc-furnace-ui-status-on = Дуга горит
dystopia-arc-furnace-ui-status-unpowered = Нет питания
dystopia-arc-furnace-ui-temperature = Температура: { $temperature } °C из { $max } °C
dystopia-arc-furnace-ui-charge = Загрузка: { $charge } из { $max } кусков. Расплав: { $volume } из { $maxVolume } ед.
dystopia-arc-furnace-ui-channel-yes = Литейный желоб подведён.
dystopia-arc-furnace-ui-channel-no = Литейный желоб не подведён — разливать некуда.
dystopia-arc-furnace-ui-melt = Расплав
dystopia-arc-furnace-ui-empty = Пусто.
dystopia-arc-furnace-ui-amount = { $amount } ед.
dystopia-arc-furnace-ui-pour = Разлив
dystopia-arc-furnace-ui-pour-hint = Печь льёт в желоб только выбранный сплав и только жидкий; остальное остаётся в печи.
dystopia-arc-furnace-ui-no-pour = Не разливать
dystopia-arc-furnace-ui-slag = Шлак
dystopia-arc-furnace-ui-slag-count = Снято кусков: { $count } из { $max }.
dystopia-arc-furnace-ui-eject-slag = Выгрузить шлак

ent-DystopiaArcFurnace = дуговая сталеплавильная печь
    .desc = Дуговая сталеплавильная печь: три графитовых электрода, гул и запах озона. Сама снимает шлак и разливает сплав, выбранный в меню. Берёт пятнадцать киловатт, так что инженеров лучше предупредить.

dystopia-forge-examine-need-bellows = [color=#e0a040]Для плавки нужно { $temperature } °C — без мехов горн столько не даст: поставьте рядом меха и качайте.[/color]
dystopia-forge-examine-too-cold = [color=#d05050]Для плавки нужно { $temperature } °C — этот горн так не раскалится.[/color]
