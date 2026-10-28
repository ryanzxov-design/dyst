# Dystopia: лор Города — «станция», «экипаж», NanoTrasen и ЦК заменены на Город, граждан и Консулат.
ent-BaseHandheldStationMap = карта Города
    .desc = Отображает схему текущего Города.
ent-HandheldStationMap = { ent-BaseHandheldStationMap }
    .desc = { ent-BaseHandheldStationMap.desc }
    .suffix = Ручной
ent-HandheldStationMapEmpty = { ent-HandheldStationMap }
    .desc = { ent-HandheldStationMap.desc }
    .suffix = Ручной, Пустой
ent-HandheldStationMapUnpowered = { ent-BaseHandheldStationMap }
    .desc = { ent-BaseHandheldStationMap.desc }
    .suffix = Ручной, Всегда запитан
ent-HandheldStationMapStatic = { ent-HandheldStationMap }
    .desc = { ent-HandheldStationMap.desc }
    .suffix = Handheld, Works Off-Station
ent-HandheldStationMapNukeops = карта целевого города
    .desc = Отображает показания целевого города.
    .suffix = Ручной, Ядерный оперативник
