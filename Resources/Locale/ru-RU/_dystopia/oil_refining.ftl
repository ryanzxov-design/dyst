# Dystopia: нефть, шаг 2 — перегонка. Генератор gen_oil_refining.py.

reagent-name-dystopia-gasoline = бензин
reagent-desc-dystopia-gasoline = Лёгкая желтоватая фракция, кипит уже от тридцати градусов. Резко пахнет и вспыхивает от искры; пары тяжелее воздуха и стелются по полу.
reagent-name-dystopia-kerosene = керосин
reagent-desc-dystopia-kerosene = Средняя фракция, почти прозрачная, маслянистая на ощупь. Горит ровно, без копоти, если фитиль подрезан. Им заправляют лампы и примусы.
reagent-name-dystopia-diesel = дизельное топливо
reagent-desc-dystopia-diesel = Янтарная солярка. Тяжелее керосина, от спички не вспыхивает — зато в цилиндре под сжатием горит как надо.
reagent-name-dystopia-fuel-oil = мазут
reagent-desc-dystopia-fuel-oil = Чёрный густой остаток из куба колонны. На холоде еле течёт. Топят им котлы и печи, а под вакуумом перегоняют ещё раз — на масло и гудрон.
reagent-name-dystopia-lubricating-oil = смазочное масло
reagent-desc-dystopia-lubricating-oil = Тягучее коричневое масло из-под вакуума. Им смазывают подшипники, шестерни и цилиндры. Разлитое — очень скользкое.
reagent-name-dystopia-bitumen = гудрон
reagent-desc-dystopia-bitumen = Чёрная смолистая масса со дна вакуумного куба. Горячая — тянется, остывшая — застывает коркой. Из неё варят битум для асфальта, кровли и изоляции.

ent-DystopiaDistillationColumn = ректификационная колонна
    .desc = Высокая колонна с тарелками над электронагревателем. Внизу заливают сырую нефть, а в бак продукции идут вместе бензин, керосин, дизельное топливо и мазут. Разделяют их жидкостными фильтрами на трубах.
ent-DystopiaVacuumDistiller = вакуумный куб
    .desc = Лежачий куб под вакуумным насосом. Мазут здесь кипит, не разлагаясь, и в бак продукции собираются смазочное масло и гудрон.
ent-DystopiaSolutionDistillationProduct = бак продукции
    .desc = { "" }
ent-DystopiaSolutionVacuumProduct = бак продукции
    .desc = { "" }
ent-DystopiaLiquidFilter = жидкостный фильтр
    .desc = Выбранное вещество выпускает вбок, всё остальное пропускает прямо. Вещество выбирают через ПКМ — из того, что сейчас есть во входной трубе.

dystopia-liquid-filter-category = Фильтр
dystopia-liquid-filter-none = Не отводить ничего
dystopia-liquid-filter-set = Фильтр: { $reagent }.
dystopia-liquid-filter-examine = Отводит вбок: [color=#d2ae68]{ $reagent }[/color].
dystopia-liquid-filter-examine-none = [color=#9aa4b0]Ничего не отводит — всё идёт прямо.[/color]
dystopia-liquid-filter-examine-side = [bold]Вбок:[/bold]
