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
dystopia-crucible-charge = Ждёт плавки: { $material } из { $max } единиц, плавится при { $temperature } °C.
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
    .desc = Кирпичный горн. Поставьте сверху малый тигель или кликните по горну большим — он встанет в горн. Заложите уголь или доски, разожгите огнём. Для железа и стали поставьте рядом меха и качайте.
ent-DystopiaBellows = меха
    .desc = Кожаные кузнечные меха. Поставьте вплотную к горну, прикрутите к полу ключом и качайте рукой — горн раскалится добела.
ent-DystopiaCrucibleSmall = малый тигель
    .desc = Графитовый горшок для плавки руды и лома. Раскалённый берут клещами: ткните клещами в тигель.
ent-DystopiaTongs = клещи
    .desc = Длинные кузнечные клещи. Ткните ими в раскалённый тигель или изложницу — зажмёте. Зажатым тиглем ткните в изложницу или литейный желоб — разольёте, в горн или на пол — поставите. Разжать — в руке.
ent-DystopiaMouldIngot = изложница для слитка
    .desc = Чугунная форма на один слиток. Прикручивается к полу ключом; водой остывает быстрее.
ent-DystopiaMouldSheet = изложница для листа
    .desc = Чугунная форма на один лист. Прикручивается к полу ключом; водой остывает быстрее.
ent-DystopiaMouldRod = изложница для прутьев
    .desc = Чугунная форма с двумя желобками: два прута за одну заливку. Прикручивается к полу ключом; водой остывает быстрее.
ent-DystopiaSlag = шлак
    .desc = Стекловидный ком шлака, снятый с расплава. Ни на что не годен — пока кто-нибудь не придумает, на что.
    .suffix = Полный
ent-DystopiaSlag1 = шлак
    .desc = Стекловидный ком шлака, снятый с расплава. Ни на что не годен — пока кто-нибудь не придумает, на что.
    .suffix = Один
stack-dystopia-slag = { $amount ->
    [1] кусок
    [few] куска
    *[other] кусков
} шлака
ent-DystopiaCrucibleLarge = большой тигель
    .desc = Тяжёлый графитовый тигель с цапфами и рычагом. Кликните им по горну — встанет в горн; клещами не поднять.
ent-DystopiaForgeLargeCrucible = горн с большим тиглем
    .desc = Кирпичный горн со вставленным большим тиглем. Топите, разжигайте, загружайте тигель и наклоняйте его — расплав пойдёт в подведённый литейный желоб.
ent-DystopiaCastingChannelStraight = литейный желоб
    .desc = Прямой желоб, выложенный глиной. Расплав бежит по связанным желобам к литейным столам у их концов.
ent-DystopiaCastingChannelCorner = угловой литейный желоб
    .desc = Желоб, поворачивающий за угол. Расплав бежит по связанным желобам к литейным столам у их концов.
ent-DystopiaCastingChannelTee = литейный желоб-тройник
    .desc = Желоб, расходящийся на три стороны. Расплав бежит по связанным желобам к литейным столам у их концов.
ent-DystopiaCastingChannelCross = литейный желоб-перекрёсток
    .desc = Желоб, расходящийся на четыре стороны. Расплав бежит по связанным желобам к литейным столам у их концов.
ent-DystopiaCastingTable = литейный стол
    .desc = Чугунный стол с песчаной постелью и литниковой чашей. Вставьте литейную форму и подведите к столу желоб — или лейте из тигля прямо в него.
ent-DystopiaCastingFormIngot1 = литейная форма: слиток ×1
    .desc = Чугунная форма для литейного стола: 10 ед. расплава, один слиток за заливку.
ent-DystopiaCastingFormIngot10 = литейная форма: слиток ×10
    .desc = Чугунная форма для литейного стола: 100 ед. расплава, десять слитков за заливку.
ent-DystopiaCastingFormSheet1 = литейная форма: лист ×1
    .desc = Чугунная форма для литейного стола: 10 ед. расплава, один лист за заливку.
ent-DystopiaCastingFormSheet10 = литейная форма: лист ×10
    .desc = Чугунная форма для литейного стола: 100 ед. расплава, десять листов за заливку.
ent-DystopiaCastingFormRod1 = литейная форма: прут ×1
    .desc = Чугунная форма для литейного стола: 10 ед. расплава, два прута за заливку.
ent-DystopiaCastingFormRod10 = литейная форма: прут ×10
    .desc = Чугунная форма для литейного стола: 100 ед. расплава, двадцать прутьев за заливку.
construction-category-dystopia-industry = Промышленность
