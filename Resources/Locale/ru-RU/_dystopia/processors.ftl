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
dystopia-processor-examine-load = Сырья: { $loaded } из { $capacity }.
dystopia-processor-examine-output = Готовой продукции: { $units } из { $capacity } (выгрузка — клик рукой или ПКМ).
dystopia-processor-examine-tank = Вода в баке: { $volume } из { $max } ед.
dystopia-processor-examine-cycle = За цикл — { $batches } { $batches ->
    [one] партия
    [few] партии
   *[other] партий
} за { $seconds } с.
dystopia-processor-examine-loose = [color=#d0a040]Не прикручено к полу.[/color]
dystopia-processor-examine-unpowered = [color=#d05050]Нет питания.[/color]

ent-DystopiaHandCrusher = ручная дробилка
    .desc = Чугунная щековая дробилка с маховиком. Засыпьте руду и крутите ворот: оборот — кусок, из куска две горсти.
ent-DystopiaJawCrusher = щековая дробилка
    .desc = Электрическая щековая дробилка. Засыпьте руду — сама перемелет её по куску, пока есть ток.
ent-DystopiaConeCrusher = конусная дробилка
    .desc = Промышленная конусная дробилка. Жрёт много тока, зато дробит по три куска за цикл и быстро.
ent-DystopiaHandMill = ручные жернова
    .desc = Два жёрнова с рукоятью. Засыпьте дроблёную руду в глазок и крутите — выйдет рудная мука.
ent-DystopiaBallMill = шаровая мельница
    .desc = Вращающийся барабан со стальными шарами. Сама мелет дроблёную руду в муку, пока есть ток.
ent-DystopiaBallMillLarge = большая шаровая мельница
    .desc = Промышленная шаровая мельница. Жрёт много тока, зато мелет по три горсти за цикл и быстро.
ent-DystopiaSluiceBox = вашгерд
    .desc = Наклонный деревянный жёлоб с порожками. Налейте воды в ящик, засыпьте рудную муку и качайте: тяжёлые зёрна остаются, пустая порода уходит песком.
ent-DystopiaShakingTable = концентрационный стол
    .desc = Трясущийся стол под струёй воды. Сам делит рудную муку на концентрат и песок, пока есть ток и вода.
ent-DystopiaJig = отсадочная машина
    .desc = Промышленная отсадочная машина: пульсирующая вода разделяет муку по весу. Жрёт ток и воду, зато моет по три партии за цикл.

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
    .desc = Рука на шарнире: вынимает предметы из машины, ящика или шкафа позади и кладёт на конвейер впереди. Настройка фильтра — клик по устройству.
ent-DystopiaConveyorInserter = конвейерный вставщик
    .desc = Рука на шарнире: берёт предметы с конвейера позади и кладёт в машину, ящик или шкаф впереди. Настройка фильтра — клик по устройству.
ent-DystopiaConveyorSorter = конвейерный сортировщик
    .desc = Разводит предметы с конвейера позади по трём выходам: зелёному, синему и жёлтому (всё остальное). Настройка — клик по устройству.

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
