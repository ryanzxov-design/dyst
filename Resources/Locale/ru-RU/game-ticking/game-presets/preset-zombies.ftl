# Dystopia: лор Города — «станция», «экипаж», NanoTrasen и ЦК заменены на Город, граждан и Консулат.
zombie-title = Зомби
zombie-description = В Городе появились ожившие мертвецы! Работайте сообща с другими гражданами, чтобы пережить эпидемию и защитить Город.

zombieteors-title = Зомбитеоры
zombieteors-description = В Городе во время катаклизмического метеоритного дождя появились зомби! Работайте вместе с гражданами и сделайте всё возможное, чтобы выжить!

zombie-not-enough-ready-players = Недостаточно игроков готовы к игре! { $readyPlayersCount } игроков из необходимых { $minimumPlayers } готовы. Нельзя запустить пресет Зомби.
zombie-no-one-ready = Нет готовых игроков! Нельзя запустить пресет Зомби.

zombie-patientzero-role-greeting = Вы — нулевой пациент. Снарядитесь и подготовьтесь к своему превращению. Ваша цель — захватить Город, заразив при этом как можно больше граждан.
zombie-healing = Вы ощущаете шевеление в своей плоти
zombie-infection-warning = Вы чувствуете, как зомби-вирус берёт верх
zombie-infection-underway = Ваша кровь начинает сгущаться

zombie-alone = Вы чувствуете себя совершенно одиноким.

zombie-shuttle-call = Мы зафиксировали, что зомби захватили Город. Аварийный шаттл был отправлен для эвакуации оставшегося персонала.

zombie-round-end-initial-count = { $initialCount ->
    [one] Единственным нулевым пациентом был:
    *[other] Нулевых пациентов было { $initialCount }, ими были:
}
zombie-round-end-user-was-initial = - [color=plum]{ $name }[/color] ([color=gray]{ $username }[/color]) был одним из нулевых пациентов.

zombie-round-end-amount-none = [color=green]Все зомби были уничтожены![/color]
zombie-round-end-amount-low = [color=green]Почти все зомби были уничтожены.[/color]
zombie-round-end-amount-medium = [color=yellow]{ $percent }% граждан были обращены в зомби.[/color]
zombie-round-end-amount-high = [color=crimson]{ $percent }% граждан были обращены в зомби.[/color]
zombie-round-end-amount-all = [color=darkred]Все граждане обратились в зомби![/color]

zombie-round-end-survivor-count = { $count ->
    [one] Единственным выжившим стал:
    *[other] Осталось всего { $count } выживших, это:
}
zombie-round-end-user-was-survivor = - [color=White]{ $name }[/color] ([color=gray]{ $username }[/color]) пережил заражение.
