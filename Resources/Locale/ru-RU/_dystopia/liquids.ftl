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
