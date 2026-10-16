# Dystopia: дробилки руды.

dystopia-crusher-not-crushable = { CAPITALIZE(THE($item)) } дробилка не берёт.
dystopia-crusher-full = Дробилка полна руды.
dystopia-crusher-loaded = Вы засыпаете { $item } в дробилку ({ $loaded } из { $capacity }).
dystopia-crusher-crank-verb = Крутить ворот
dystopia-crusher-eject-verb = Вытряхнуть руду
dystopia-crusher-ejected = Вы вытряхиваете руду из дробилки.
dystopia-crusher-not-anchored = Дробилка не закреплена — прикрутите её к полу ключом.
dystopia-crusher-empty = В дробилке нет руды.
dystopia-crusher-examine-load = Руды внутри: { $loaded } из { $capacity } кусков.
dystopia-crusher-examine-cycle = За цикл дробит { $ores } { $ores ->
    [one] кусок
    [few] куска
   *[other] кусков
} за { $seconds } с; из куска выходит две горсти.
dystopia-crusher-examine-loose = [color=#d0a040]Не прикручена к полу.[/color]
dystopia-crusher-examine-unpowered = [color=#d05050]Нет питания.[/color]

ent-DystopiaHandCrusher = ручная дробилка
    .desc = Чугунная щековая дробилка с маховиком. Засыпьте руду и крутите ворот: оборот — кусок.
ent-DystopiaJawCrusher = щековая дробилка
    .desc = Электрическая щековая дробилка. Засыпьте руду — сама перемелет её по куску, пока есть ток.
ent-DystopiaConeCrusher = конусная дробилка
    .desc = Промышленная конусная дробилка. Жрёт много тока, зато дробит по три куска за цикл и быстро.
