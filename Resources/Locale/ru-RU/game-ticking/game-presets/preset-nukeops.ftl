# Dystopia: лор Города — «станция», «экипаж», NanoTrasen и ЦК заменены на Город, граждан и Консулат.
nukeops-title = Ядерные оперативники
nukeops-description = Ядерные оперативники нацелились на Город. Постарайтесь не дать им взвести и взорвать ядерную бомбу, защищая ядерный диск!

nukeops-welcome =
    Вы — ядерный оперативник. Ваша задача — взорвать { $station } и убедиться, что от неё осталась лишь груда обломков. Ваше руководство, Синдикат, снабдило вас всем необходимым для выполнения этой задачи.
    Операция "{ $name }" началась! Смерть Городу!
nukeops-briefing = Ваши задачи просты. Доставить бомбу и убраться до того, как она взорвётся. Начинайте миссию.

nukeops-opsmajor = [color=crimson]Крупная победа Синдиката![/color]
nukeops-opsminor = [color=crimson]Малая победа Синдиката![/color]
nukeops-neutral = [color=yellow]Ничейный исход![/color]
nukeops-crewminor = [color=green]Малая победа граждан![/color]
nukeops-crewmajor = [color=green]Разгромная победа граждан![/color]

nukeops-cond-nukeexplodedoncorrectstation = Ядерным оперативникам удалось взорвать Город.
nukeops-cond-nukeexplodedonnukieoutpost = Аванпост ядерных оперативников был уничтожен ядерным взрывом!
nukeops-cond-nukeexplodedonincorrectlocation = Ядерная бомба взорвалась вне Города.
nukeops-cond-nukeactiveinstation = Ядерная бомба была оставлена взведённой в Городе.
nukeops-cond-nukeactiveatcentcom = Ядерная бомба была доставлена Консулату!
nukeops-cond-nukediskoncentcom = Граждане улетелии с диском ядерной аутентификации.
nukeops-cond-nukedisknotoncentcom = Граждане оставилии диск ядерной аутентификации в Городе.
nukeops-cond-nukiesabandoned = Ядерные оперативники были брошены.
nukeops-cond-allnukiesdead = Все ядерные оперативники погибли.
nukeops-cond-somenukiesalive = Несколько ядерных оперативников погибли.
nukeops-cond-allnukiesalive = Все ядерные оперативники выжили.

nukeops-disk-location-title = Конечное местоположение диска:
nukeops-disk-carried-by = { " " }у [color=White]{ $name }[/color], [color=orange]{ $job }[/color], { $location } { $user ->
    [unknown] { "" }
    *[other] ([color=gray]{ $user }[/color])
}

storage-hierarchy-list = { $items-left ->
    [0] { $existing-text } { $item },
    *[other] { $existing-text } { $item }, в
}

nukeops-list-start = Оперативниками были:
nukeops-list-name = - [color=White]{ $name }[/color]
nukeops-list-name-user = - [color=White]{ $name }[/color] ([color=gray]{ $user }[/color])
nukeops-not-enough-ready-players = Недостаточно игроков готовы к игре! { $readyPlayersCount } игроков из необходимых { $minimumPlayers } готовы. Нельзя запустить пресет Ядерные оперативники.
nukeops-no-one-ready = Нет готовых игроков! Нельзя запустить пресет Ядерные оперативники.

nukeops-role-commander = Командир
nukeops-role-agent = Медик
nukeops-role-operator = Оператор
