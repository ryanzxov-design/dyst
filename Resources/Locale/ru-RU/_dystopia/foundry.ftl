# Dystopia: ручное литьё.

reagent-name-dystopia-molten-slag = шлак
reagent-desc-dystopia-molten-slag = Расплавленная пустая порода. Всплывает над металлом; снимите её, иначе отливка выйдет браком.

dystopia-foundry-temperature = Температура: [color=#ff9a50]{ $temperature } °C[/color].

dystopia-hot-item-need-tongs = { CAPITALIZE(THE($item)) } раскалён — возьмите клещи в другую руку.
dystopia-hot-item-burn = Вы обжигаете руку о { $item }!

dystopia-crucible-solidified = Металл в тигле застыл — сначала разогрейте его.
dystopia-crucible-full = Тигель полон: больше не влезет.
dystopia-crucible-loaded = Вы кладёте { $item } в тигель.
dystopia-crucible-not-empty-item = Сначала опустошите { $item }.
dystopia-crucible-charge = Ждёт плавки: { $material } из { $max } единиц.
dystopia-crucible-skim-verb = Снять шлак
dystopia-crucible-skim-need-tongs = Шлак снимают клещами.
dystopia-crucible-skim-too-little = Шлака слишком мало, чтобы его снять.
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
dystopia-forge-examine-max = Жар: до { $base } °C, с мехами — до { $bellows } °C.
dystopia-forge-examine-bellows = Меха раздувают пламя.

dystopia-mould-shape = Форма: { $shape }, { $volume } ед. расплава на отливку.
dystopia-mould-shape-ingot = слиток
dystopia-mould-shape-sheet = лист
dystopia-mould-shape-rod = прут

ent-DystopiaForge = горн
    .desc = Кирпичный горн с кожаными мехами. Поставьте сверху тигель, заложите уголь или доски, разожгите огнём. Для железа и стали качайте меха.
ent-DystopiaCrucibleSmall = малый тигель
    .desc = Графитовый горшок для плавки руды и лома. Раскалённый берут только клещами.
ent-DystopiaTongs = клещи
    .desc = Длинные кузнечные клещи. Держите в другой руке, чтобы нести раскалённый тигель или изложницу.
ent-DystopiaMouldIngot = изложница для слитка
    .desc = Чугунная форма на один слиток.
ent-DystopiaMouldSheet = изложница для листа
    .desc = Чугунная форма на один лист.
ent-DystopiaMouldRod = изложница для прутьев
    .desc = Чугунная форма с двумя желобками: два прута за одну заливку.
ent-DystopiaSlag = шлак
    .desc = Стекловидный ком шлака, снятый с расплава. Ни на что не годен — пока кто-нибудь не придумает, на что.
