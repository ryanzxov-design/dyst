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

ent-DystopiaLiquidPipeHalf = жидкостная труба
    .desc = Половина жидкостной трубы — тупик.
    .suffix = Половина
ent-DystopiaLiquidPipeStraight = жидкостная труба
    .desc = Гонит жидкости. С газовыми трубами не соединяется.
    .suffix = Прямая
ent-DystopiaLiquidPipeBend = жидкостная труба
    .desc = Гонит жидкости. С газовыми трубами не соединяется.
    .suffix = Угол
ent-DystopiaLiquidPipeTJunction = жидкостная труба
    .desc = Гонит жидкости. С газовыми трубами не соединяется.
    .suffix = Тройник
ent-DystopiaLiquidPipeFourway = жидкостная труба
    .desc = Гонит жидкости. С газовыми трубами не соединяется.
    .suffix = Крестовина
ent-DystopiaLiquidPort = жидкостный порт
    .desc = Соединяет сеть труб с цистерной, прикрученной поверх него: выравнивает заполненность цистерны и труб.
ent-DystopiaLiquidPump = жидкостный насос
    .desc = Перекачивает жидкость из трубы позади в трубу впереди. Нужно питание; включается и выключается вручную (ПКМ).
ent-DystopiaLiquidExporter = жидкостный экспортер
    .desc = Выпускает жидкость из трубы позади в объект впереди: в цистерну, в бак машины или — только охладитель и только на горячее — на литейный стол и в изложницу.
ent-DystopiaLiquidImporter = жидкостный импортер
    .desc = Забирает жидкость из цистерны или бака машины позади в трубу впереди.
