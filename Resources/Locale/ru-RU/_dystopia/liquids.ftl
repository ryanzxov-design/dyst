# SPDX-License-Identifier: AGPL-3.0-or-later
# Dystopia: жидкостная система — трубы, порт, насос, экспортер, импортер.

dystopia-liquid-examine-disconnected = Ни к чему не подключено.
dystopia-liquid-examine-empty = Сеть пуста (вмещает { $max } ед.).
dystopia-liquid-examine-fill = Заполнено на { $percent }% ({ $volume } из { $max } ед.), { $temperature } °C:
dystopia-liquid-examine-entry = — [color={ $color }]{ $name }[/color]: { $amount } ед.
dystopia-liquid-examine-inlet = [bold]Вход:[/bold]
dystopia-liquid-examine-outlet = [bold]Выход:[/bold]
dystopia-liquid-examine-on = Включён.
dystopia-liquid-examine-off = [color=#9aa4b0]Выключен.[/color]
dystopia-liquid-examine-unpowered = [color=#e05050]Включён, но нет питания.[/color]

dystopia-liquid-turn-on = Включить
dystopia-liquid-turn-off = Выключить
dystopia-liquid-turned-on = Включено.
dystopia-liquid-turned-off = Выключено.

# нефтяной выход и качалка (Extractor)
dystopia-liquid-source-rich = [color=#c8a060]Нефть идёт густо — запас богатый.[/color]
dystopia-liquid-source-fair = [color=#a08a60]Нефти ещё порядочно.[/color]
dystopia-liquid-source-poor = [color=#8a7a6a]Нефть еле сочится — запас на исходе.[/color]
dystopia-liquid-source-depleted = [color=#7a7a7a]Иссяк: только засохшая корка.[/color]
dystopia-liquid-source-none = [color=#e05050]Под качалкой нет нефтяного выхода.[/color]

# меню жидкостных устройств
dystopia-liquid-ui-status-off = Выключено
dystopia-liquid-ui-status-unpowered = Нет питания
dystopia-liquid-ui-status-working = Работает
dystopia-liquid-ui-status-idle = Простаивает
dystopia-liquid-ui-hint-port = Выравнивает уровень в трубах и в цистерне на своей клетке.
dystopia-liquid-ui-hint-pump = Гонит жидкость из трубы позади в трубу впереди.
dystopia-liquid-ui-hint-exporter = Выпускает из трубы в то, что стоит впереди: цистерну, бак машины (только её сырьё) или раскалённую форму.
dystopia-liquid-ui-hint-importer = Забирает из цистерны или бака продукции машины позади в трубу.
dystopia-liquid-ui-hint-extractor = Качает нефть из выхода под собой в трубу.
dystopia-liquid-ui-hint-filter = Выбранное вещество выпускает вбок, всё остальное пропускает прямо.
dystopia-liquid-ui-rate-label = Скорость (ед./с):
dystopia-liquid-ui-rate-max = не больше { $max }
dystopia-liquid-ui-set = Установить
dystopia-liquid-ui-max = Макс.
dystopia-liquid-ui-filter-current = Сейчас отводится: { $reagent }
dystopia-liquid-ui-filter-nothing = ничего
dystopia-liquid-ui-filter-choose = Выберите вещество для отвода:
dystopia-liquid-ui-filter-search = Поиск вещества…
dystopia-liquid-ui-filter-select = Выбрать вещество
dystopia-liquid-ui-filter-present = (в трубе)
dystopia-liquid-ui-inlet = Вход
dystopia-liquid-ui-outlet = Выход
dystopia-liquid-ui-side = Вбок
dystopia-liquid-ui-net = Труба
dystopia-liquid-ui-disconnected = ни к чему не подключено
dystopia-liquid-ui-empty = пусто (вмещает { $max } ед.)
dystopia-liquid-ui-fill = { $percent }% ({ $volume } из { $max } ед.), { $temperature } °C
dystopia-liquid-ui-amount = { $amount } ед.

ent-DystopiaLiquidPipeHalf = жидкостная труба
    .desc = Обрезок жидкостной трубы, заглушённый с одной стороны.
    .suffix = Половина
ent-DystopiaLiquidPipeStraight = жидкостная труба
    .desc = Жидкостная труба. С газовыми не стыкуется, по ней идёт только жидкость.
    .suffix = Прямая
ent-DystopiaLiquidPipeBend = жидкостная труба
    .desc = Колено жидкостной трубы.
    .suffix = Угол
ent-DystopiaLiquidPipeTJunction = жидкостная труба
    .desc = Тройник жидкостной трубы.
    .suffix = Тройник
ent-DystopiaLiquidPipeFourway = жидкостная труба
    .desc = Крестовина жидкостной трубы.
    .suffix = Крестовина
ent-DystopiaLiquidPort = жидкостный порт
    .desc = Порт для цистерны. Прикрутите цистерну поверх, и уровень в ней и в трубах выровняется.
ent-DystopiaLiquidPump = жидкостный насос
    .desc = Жидкостный насос. Гонит жидкость из трубы позади в трубу впереди. Нужен ток, включается через ПКМ.
ent-DystopiaLiquidExporter = жидкостный экспортер
    .desc = Выпуск из труб в то, что стоит впереди: в цистерну, бак машины или на раскалённую форму, чтобы остудить.
ent-DystopiaLiquidImporter = жидкостный импортер
    .desc = Заборник. Тянет жидкость в трубы из цистерны или бака машины позади.
