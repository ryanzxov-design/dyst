# Dystopia: нефть, шаг 4, архив 2 — многоблоки, градирни, цистерны, бочки, форсунка, лампы, карбид (gen_oil4b.py).

ent-DystopiaFrameArcFurnace = секция каркаса дуговой печи
    .desc = Стальная обечайка на шамотной подушке — часть кожуха будущей дуговой печи. Печь собирается из 3×3 секций, электродный узел — в центре.
ent-DystopiaFrameArcFurnaceCore = электродный узел дуговой печи
    .desc = Свод с электрододержателями и токоподводом. Ставится в центр каркаса 3×3.
ent-DystopiaFrameCoolingTowerSmall = секция каркаса малой градирни
    .desc = Бетонная опора с решёткой оросителя. Малая градирня собирается из 3×3 секций, вентиляторный узел — в центре.
ent-DystopiaFrameCoolingTowerSmallCore = вентиляторный узел малой градирни
    .desc = Вентилятор с мотором и разбрызгивателями. Ставится в центр каркаса 3×3.
ent-DystopiaFrameCoolingTowerLarge = секция каркаса большой градирни
    .desc = Бетонная опора с решёткой оросителя. Большая градирня собирается из 5×5 секций, вентиляторный узел — в центре.
ent-DystopiaFrameCoolingTowerLargeCore = вентиляторный узел большой градирни
    .desc = Большой вентилятор с двумя моторами. Ставится в центр каркаса 5×5.
ent-DystopiaFrameCistern = секция каркаса цистерны
    .desc = Стальное ложемент-седло и листы обечайки. Цистерна собирается из 3×5 секций — вдоль или поперёк.
ent-DystopiaCoolingTowerSmall = малая градирня
    .desc = Башня-охладитель 3×3: пар из газовой трубы оседает на оросителе и стекает водой в бассейн. Часть пара уходит облаком. С током вентилятор гонит воздух в полную силу, без тока — на тяге, втрое медленнее.
dystopia-coolingtowersmall-ports = [color=#c8e4f0]Пар[/color] — газовой трубой в патрубки по бокам (белые фланцы слева и справа). [color=#78bee1]Вода[/color] — жидкостным импортером вплотную к синему крану спереди посередине.
ent-DystopiaCoolingTowerLarge = большая градирня
    .desc = Башня-охладитель 5×5, втрое производительнее малой: пар из газовой трубы оседает на оросителе и стекает водой в бассейн. Часть пара уходит облаком. Без тока работает на тяге, втрое медленнее.
dystopia-coolingtowerlarge-ports = [color=#c8e4f0]Пар[/color] — газовой трубой в патрубки по бокам (белые фланцы слева и справа). [color=#78bee1]Вода[/color] — жидкостным импортером вплотную к синему крану спереди посередине.
ent-DystopiaCoolingTowerInlet = патрубок градирни
    .desc = Патрубок пара градирни.
ent-DystopiaCistern = цистерна
    .desc = Стальная цистерна на 10 000 единиц для любой жидкости, на бетонном основании с коллектором по периметру: наливают и сливают её жидкостными экспортером и импортером вплотную к любой стороне.
dystopia-cistern-ports = [color=#e0b830]Коллектор[/color] — по всему периметру основания: жидкостный экспортер (залить) или импортер (слить) вплотную к любой стороне.
ent-DystopiaBarrel = бочка
    .desc = Стальная бочка на 200 единиц. Наливают ведром или из крана; чтобы налить или слить трубами, прикрутите ключом вплотную к жидкостному экспортеру или импортеру.
ent-DystopiaBarrelDiesel = бочка
    .desc = { ent-DystopiaBarrel.desc }
    .suffix = { reagent-name-dystopia-diesel }
ent-DystopiaBarrelFuelOil = бочка
    .desc = { ent-DystopiaBarrel.desc }
    .suffix = { reagent-name-dystopia-fuel-oil }
ent-DystopiaBarrelKerosene = бочка
    .desc = { ent-DystopiaBarrel.desc }
    .suffix = { reagent-name-dystopia-kerosene }
ent-DystopiaBarrelGasoline = бочка
    .desc = { ent-DystopiaBarrel.desc }
    .suffix = { reagent-name-dystopia-gasoline }
ent-DystopiaBarrelLubricatingOil = бочка
    .desc = { ent-DystopiaBarrel.desc }
    .suffix = { reagent-name-dystopia-lubricating-oil }
ent-DystopiaBarrelWater = бочка
    .desc = { ent-DystopiaBarrel.desc }
    .suffix = { reagent-name-water }
ent-DystopiaBarrelWeldingFuel = бочка
    .desc = { ent-DystopiaBarrel.desc }
    .suffix = { reagent-name-welding-fuel }
ent-DystopiaFuelOilBurner = мазутная форсунка
    .desc = Форсунка под давлением, ставится вплотную к горну соплом к нему. Заливают мазут, дизель или керосин — ведром или жидкостным экспортером. Включённая сама разжигает горн, подкидывает ему топлива и дует, как меха: качать не нужно.
ent-DystopiaKeroseneLamp = керосиновая лампа
    .desc = Фонарь «летучая мышь»: фитиль и стекло. Заливается керосином, горит тёплым светом около 25 минут. Можно повесить на стену: ПКМ лампой по стене — «Повесить лампу».
ent-DystopiaWallLampKerosene = керосиновая лампа на стене
    .desc = Керосиновый фонарь на крюке. Щелчок — зажечь или погасить; ПКМ — снять.
ent-DystopiaCarbideLamp = карбидная лампа
    .desc = Шахтёрская ацетиленовая лампа: в камеру — куски карбида (до двух), в бачок — вода. Пока горит, вода капает на карбид, и ацетилен горит ярким белым пламенем. Шлам гашёной извести копится в камере — иногда сливайте.
ent-DystopiaWallLampCarbide = карбидная лампа на стене
    .desc = Карбидная лампа на крюке. Щелчок — зажечь или погасить; ПКМ — снять.
ent-DystopiaSolutionCarbideLampWater = бачок воды
    .desc = Бачок воды карбидной лампы.
reagent-name-dystopia-calcium-carbide = карбид кальция
reagent-desc-dystopia-calcium-carbide = Серый порошок. С водой бурно шипит, выделяя ацетилен — горючий газ для сварки и ламп.
reagent-physical-desc-dystopia-gritty = зернистое
reagent-name-dystopia-molten-lime = расплав извести
reagent-desc-dystopia-molten-lime = Ослепительно-белая жидкая известь. С углеродом при 1750 °C превращается в карбид кальция.
reagent-name-dystopia-molten-carbide = расплав карбида
reagent-desc-dystopia-molten-carbide = Жидкий карбид кальция. Разливают в слитки — куски карбида.
ent-DystopiaCalciumCarbide = карбид кальция
    .desc = Серые куски с чесночным запахом. Молотые и смоченные водой дают ацетилен; карбидная лампа берёт их целиком.
    .suffix = Карбид, полный
ent-DystopiaCalciumCarbide1 = карбид кальция
    .desc = Серые куски с чесночным запахом. Молотые и смоченные водой дают ацетилен; карбидная лампа берёт их целиком.
    .suffix = Карбид, один
stack-dystopia-calcium-carbide = карбид кальция
materials-dystopia-calcium-carbide = карбид кальция
dystopia-arcfurnace-ports = Собрана из секций каркаса; ломом разбирается обратно в секции.
dystopia-multiblock-size = { $w }×{ $h }
dystopia-multiblock-size-either = { $w }×{ $h } (вдоль или поперёк)
dystopia-multiblock-frame-examine = Секция каркаса: [bold]{ $machine }[/bold], собирается ровно { $size }.
dystopia-multiblock-frame-core = Узел — строго в центр.
dystopia-multiblock-frame-count = Стоит секций: { $count } из { $need }.
dystopia-multiblock-frame-shape = [color=#e0c040]Секций хватает, но они не в ровном прямоугольнике или узел не в центре.[/color]
dystopia-multiblock-frame-blocked = [color=#e0c040]Каркас собран — сойдите с лесов, и машина встанет.[/color]
dystopia-multiblock-frame-extra = [color=#e05040]Лишние секции: стоит { $count }, нужно ровно { $need }.[/color]
dystopia-cooling-tower-examine-fan = Вентилятор под током — работает в полную силу.
dystopia-cooling-tower-examine-draft = [color=#e0c040]Без тока — на естественной тяге, втрое медленнее.[/color]
dystopia-cooling-tower-examine-rate = Пар: { $steam } моль/с → вода: { $water } ед./с.
dystopia-cooling-tower-examine-water = В бассейне воды: { $amount } из { $max }.
dystopia-cooling-tower-examine-full = [color=#e05040]Бассейн полон — пар больше не принимается. Заберите воду импортером.[/color]
dystopia-burner-verb-on = Зажечь форсунку
dystopia-burner-verb-off = Погасить форсунку
dystopia-burner-turned-on = Форсунка открыта.
dystopia-burner-turned-off = Форсунка закрыта.
dystopia-burner-examine-burning = Факел ревёт в горне.
dystopia-burner-examine-idle = [color=#e0c040]Открыта, но не горит: нет топлива или горна перед соплом.[/color]
dystopia-burner-examine-off = Закрыта.
dystopia-burner-examine-no-forge = [color=#e05040]Перед соплом нет горна.[/color]
dystopia-burner-examine-fuel = Топлива: { $amount } из { $max }.
dystopia-lamp-no-fuel = Нечему гореть.
dystopia-lamp-full = Больше не влезает.
dystopia-lamp-loaded = Карбид зашипел в бачке.
dystopia-lamp-verb-mount = Повесить лампу
dystopia-lamp-verb-unmount = Снять лампу
dystopia-lamp-verb-on = Зажечь
dystopia-lamp-verb-off = Погасить
dystopia-lamp-occupied = Здесь уже висит лампа.
dystopia-lamp-mounted = Лампа повешена.
dystopia-lamp-examine-fuel = Топлива примерно на { $minutes } мин.
dystopia-lamp-no-place = Некуда повесить: подойдите к стене вплотную со свободной стороны.
dystopia-lamp-examine-load = Карбида в камере: { $amount } из { $max }.
dystopia-lamp-examine-water = Воды в бачке: { $amount } из { $max }.
dystopia-lamp-examine-empty = [color=#e05040]Топлива нет.[/color]
