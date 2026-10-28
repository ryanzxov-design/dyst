# Dystopia: лор Города — «станция», «экипаж», NanoTrasen и ЦК заменены на Город, граждан и Консулат.
ent-StationAnchorBase = городской якорь
    .desc = Предотвращает смещение городов.
    .suffix = Включён
ent-StationAnchorIndestructible = { ent-StationAnchorBase }
    .desc = { ent-StationAnchorBase.desc }
    .suffix = Неразрушимый, Не требует питания
ent-StationAnchor = { ent-StationAnchorBase }
    .desc = { ent-StationAnchorBase.desc }
    .suffix = { ent-StationAnchorBase.suffix }
ent-StationAnchorOff = { ent-StationAnchor }
    .desc = { ent-StationAnchor.desc }
    .suffix = Выключен
