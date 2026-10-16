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

dystopia-quench-hiss = { CAPITALIZE(THE($item)) } шипит: вода вскипает паром.

dystopia-mould-shape = Форма: { $shape }, { $volume } ед. расплава на отливку.
dystopia-mould-shape-ingot = слиток
dystopia-mould-shape-sheet = лист
dystopia-mould-shape-rod = прут

ent-DystopiaForge = горн
    .desc = Кирпичный горн. Поставьте сверху тигель, заложите уголь или доски, разожгите огнём. Для железа и стали поставьте рядом меха и качайте.
ent-DystopiaBellows = меха
    .desc = Кожаные кузнечные меха. Поставьте вплотную к горну, прикрутите к полу ключом и качайте рукой — горн раскалится добела.
ent-DystopiaCrucibleSmall = малый тигель
    .desc = Графитовый горшок для плавки руды и лома. Раскалённый берут клещами: ткните клещами в тигель.
ent-DystopiaTongs = клещи
    .desc = Длинные кузнечные клещи. Ткните ими в раскалённый тигель или изложницу — зажмёте. Зажатым тиглем ткните в изложницу — разольёте, в горн или на пол — поставите. Разжать — в руке.
ent-DystopiaMouldIngot = изложница для слитка
    .desc = Чугунная форма на один слиток. Прикручивается к полу ключом; водой остывает быстрее.
ent-DystopiaMouldSheet = изложница для листа
    .desc = Чугунная форма на один лист. Прикручивается к полу ключом; водой остывает быстрее.
ent-DystopiaMouldRod = изложница для прутьев
    .desc = Чугунная форма с двумя желобками: два прута за одну заливку. Прикручивается к полу ключом; водой остывает быстрее.
ent-DystopiaSlag = шлак
    .desc = Стекловидный ком шлака, снятый с расплава. Ни на что не годен — пока кто-нибудь не придумает, на что.
