# Dystopia: машины переработки руды и конвейерные устройства.

dystopia-processor-work-verb = Работать
dystopia-processor-work-crank = Крутить ворот
dystopia-processor-work-turn = Крутить жернова
dystopia-processor-work-rock = Качать лоток
dystopia-processor-not-accepted = { CAPITALIZE(THE($item)) } сюда не подходит.
dystopia-processor-full = Загрузка полна.
dystopia-processor-loaded = Вы загружаете { $item } ({ $loaded } из { $capacity }).
dystopia-processor-empty = Внутри нет сырья.
dystopia-processor-not-enough = Сырья не хватает на партию.
dystopia-processor-no-reagents = Не хватает воды.
dystopia-processor-output-full = Продукции некуда деваться — выгрузите её.
dystopia-processor-not-anchored = Машина не закреплена — прикрутите её к полу ключом.
dystopia-processor-unload-verb = Выгрузить продукцию
dystopia-processor-unloaded = Вы выгружаете продукцию.
dystopia-processor-no-output = Готовой продукции нет.
dystopia-processor-eject-verb = Вытряхнуть сырьё
dystopia-processor-ejected = Вы вытряхиваете сырьё.
dystopia-processor-tank-draw-verb = Отлить из бака ({ $amount } ед.)
dystopia-processor-tank-drawn = Вы отливаете { $amount } ед. из бака.
dystopia-processor-tank-dump-verb = Слить бак на пол
dystopia-processor-tank-dumped = Вы сливаете бак на пол.
dystopia-processor-examine-load = Сырья: { $loaded } из { $capacity }.
dystopia-processor-examine-output = Готовой продукции: { $units } из { $capacity } (выгрузка — клик рукой или ПКМ).
dystopia-processor-examine-tank = В баке { $volume } из { $max } ед.: { $contents }.
dystopia-processor-examine-tank-empty = Бак пуст (вмещает { $max } ед.).
dystopia-processor-examine-tank-entry = [color={ $color }]{ $name }[/color] { $amount }
dystopia-processor-examine-cycle = За цикл — { $batches } { $batches ->
    [one] партия
    [few] партии
   *[other] партий
} за { $seconds } с.
dystopia-processor-examine-loose = [color=#d0a040]Не прикручено к полу.[/color]
dystopia-processor-examine-unpowered = [color=#d05050]Нет питания.[/color]

ent-DystopiaHandCrusher = ручная дробилка
    .desc = Ручная щековая дробилка с маховиком. Заложите кусок руды и крутите ворот, на выходе две горсти щебня.
ent-DystopiaJawCrusher = щековая дробилка
    .desc = Щековая дробилка на электромоторе. Сама грызёт руду по куску, пока есть ток.
ent-DystopiaConeCrusher = конусная дробилка
    .desc = Конусная дробилка. Шумная и прожорливая, зато дробит по три куска за раз.
ent-DystopiaHandMill = ручные жернова
    .desc = Ручные жернова. Сыпьте щебень в глазок и крутите, и выйдет рудная мука.
ent-DystopiaBallMill = шаровая мельница
    .desc = Шаровая мельница: барабан со стальными шарами. Грохочет так, что разговаривать рядом бесполезно.
ent-DystopiaBallMillLarge = большая шаровая мельница
    .desc = Большая шаровая мельница. Мелет по три горсти за раз.
ent-DystopiaSluiceBox = вашгерд
    .desc = Вашгерд, наклонный деревянный жёлоб с порожками. Налейте воды в ящик, сыпьте муку и качайте: тяжёлое останется, песок смоет.
ent-DystopiaShakingTable = концентрационный стол
    .desc = Концентрационный стол. Трясётся под струёй воды и сам делит муку на концентрат и песок.
ent-DystopiaJig = отсадочная машина
    .desc = Отсадочная машина. Вода толкается снизу, тяжёлые зёрна оседают. Три партии за цикл, но воды пьёт много.

# Конвейерные устройства
dystopia-conveyor-filter-add = Фильтр: добавить { $item }
dystopia-conveyor-filter-remove = Фильтр: убрать { $item }
dystopia-conveyor-sort-green-add = Зелёный выход: { $item }
dystopia-conveyor-sort-green-remove = Зелёный выход: убрать { $item }
dystopia-conveyor-sort-blue-add = Синий выход: { $item }
dystopia-conveyor-sort-blue-remove = Синий выход: убрать { $item }
dystopia-conveyor-clear-verb = Очистить фильтр
dystopia-conveyor-cleared = Фильтр очищен.
dystopia-conveyor-to-whitelist = Режим: белый список
dystopia-conveyor-to-blacklist = Режим: чёрный список
dystopia-conveyor-mode-whitelist = Теперь только то, что в списке.
dystopia-conveyor-mode-blacklist = Теперь всё, кроме того, что в списке.
dystopia-conveyor-list-empty = пусто
dystopia-conveyor-examine-extract = Забирает из хранилища позади и кладёт на клетку впереди (по стрелке). Запертое и заваренное не трогает.
dystopia-conveyor-examine-insert = Забирает с клетки позади и кладёт в хранилище впереди (по стрелке). Запертое и заваренное не трогает.
dystopia-conveyor-examine-whitelist = Белый список — только: { $items }.
dystopia-conveyor-examine-blacklist = Чёрный список — всё, кроме: { $items }.
dystopia-conveyor-examine-sort = Забирает с клетки позади. Остальное — в [color=#e0c040]жёлтый[/color] выход (вперёд).
dystopia-conveyor-examine-green = [color=#50c050]Зелёный[/color] выход: { $items }.
dystopia-conveyor-examine-blue = [color=#5080e0]Синий[/color] выход: { $items }.

ent-DystopiaConveyorExtractor = конвейерный извлекатель
    .desc = Рука на шарнире. Вынимает готовое из машины или ящика позади и кладёт на конвейер. Что брать, настраивается кликом.
ent-DystopiaConveyorInserter = конвейерный вставщик
    .desc = Рука на шарнире. Снимает с конвейера и закладывает в машину или ящик впереди. Что брать, настраивается кликом.
ent-DystopiaConveyorSorter = конвейерный сортировщик
    .desc = Сортировщик на три выхода: зелёный налево, синий направо, всё остальное прямо.

# Меню конвейерных устройств
dystopia-industry-ui-sub = Промышленная Служба Города  ·  Заводское оборудование
dystopia-industry-ui-slogan = План — закон. Норма — долг.
dystopia-conveyor-ui-title = Конвейерное устройство
dystopia-conveyor-ui-extract-hint = Вынимает из хранилища позади и кладёт на клетку по стрелке.
dystopia-conveyor-ui-insert-hint = Берёт с клетки позади и кладёт в хранилище по стрелке.
dystopia-conveyor-ui-sort-hint = Берёт с клетки позади и разводит по выходам: зелёный — налево, синий — направо, жёлтый — вперёд.
dystopia-conveyor-ui-whitelist = Белый список
dystopia-conveyor-ui-blacklist = Чёрный список
dystopia-conveyor-ui-list-white = Пропускать только
dystopia-conveyor-ui-list-black = Пропускать всё, кроме
dystopia-conveyor-ui-green = Зелёный выход (налево)
dystopia-conveyor-ui-blue = Синий выход (направо)
dystopia-conveyor-ui-yellow = Жёлтый выход (вперёд)
dystopia-conveyor-ui-yellow-hint = Всё, что не попало в зелёный и синий.
dystopia-conveyor-ui-add-held = Добавить то, что в руке
dystopia-conveyor-ui-clear = Очистить
dystopia-conveyor-ui-empty = Пусто.
dystopia-conveyor-ui-remove = Убрать
dystopia-conveyor-ui-search = Поиск предметов
dystopia-conveyor-ui-search-placeholder = Название предмета…
dystopia-conveyor-ui-search-hint = Введите хотя бы две буквы названия.
dystopia-conveyor-ui-nothing = Ничего не найдено.
dystopia-conveyor-ui-add = Добавить
dystopia-conveyor-ui-to-green = В зелёный
dystopia-conveyor-ui-to-blue = В синий
dystopia-conveyor-ui-hand-empty = В руке ничего нет.
