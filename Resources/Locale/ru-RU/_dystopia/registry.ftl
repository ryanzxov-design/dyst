## Реестр граждан (социоучёт)

dystopia-registry-status-citizen = гражданин
dystopia-registry-status-supervised = под надзором
dystopia-registry-status-wanted = в розыске
dystopia-registry-status-detained = задержан
dystopia-registry-status-disenfranchised = поражён в правах
dystopia-registry-status-dead = мёртв

dystopia-registry-author-registry = Реестр
dystopia-registry-author-admin = Администрация сервера
dystopia-registry-author-security-console = Консоль криминальных записей
dystopia-registry-author = №{ $number } { $job }

dystopia-registry-entry-registered = Внесён в Реестр граждан
dystopia-registry-entry-status = Статус: { $old } → { $new }
dystopia-registry-entry-notes = Особые приметы обновлены
dystopia-registry-entry-admin-loyalty = Изменение лояльности

dystopia-registry-card-examine = Гражданин Города [color=gold]№{ $number }[/color].

## Админ-команды

cmd-cityregistry_list-desc = Показать всех граждан Реестра.
cmd-cityregistry_list-help = Использование: { $command }
cmd-cityregistry_show-desc = Показать личное дело гражданина с журналом.
cmd-cityregistry_show-help = Использование: { $command } <номер>
cmd-cityregistry_status-desc = Сменить статус гражданина.
cmd-cityregistry_status-help = Использование: { $command } <номер> <citizen|supervised|wanted|detained|disenfranchised|dead> [основание]
cmd-cityregistry_loyalty-desc = Изменить лояльность гражданина (запись в журнал).
cmd-cityregistry_loyalty-help = Использование: { $command } <номер> <изменение, например -5> [основание]

cmd-cityregistry-no-registry = На сервере нет Реестра граждан.
cmd-cityregistry-no-citizen = Нет гражданина с таким номером.
cmd-cityregistry-list-header = Граждан в Реестре: { $count }.
cmd-cityregistry-status-unchanged = Статус не изменён (уже такой, или это Консул).
cmd-cityregistry-status-done = Гражданин №{ $number }: статус «{ $status }».
cmd-cityregistry-loyalty-outside = Этот гражданин вне учёта лояльности.
cmd-cityregistry-loyalty-done = Гражданин №{ $number }: лояльность теперь { $loyalty }.

## Консоль социоучёта

dystopia-registry-console-title = Реестр граждан · социоучёт
dystopia-registry-console-subtitle = Городская Стража · терминал Реестра
dystopia-registry-console-slogan = Порядок начинается с учёта
dystopia-registry-console-denied = Нет прав на это действие.
dystopia-registry-console-reason-required = Для розыска нужно основание.
dystopia-registry-console-not-wanted = Ориентировка печатается только на гражданина в розыске.
dystopia-registry-console-identify-failed = Не удалось опознать: нет такого гражданина или запись уже ушла.
dystopia-registry-console-camera-offline = Камера не работает.

dystopia-registry-tab-citizens = Граждане · { $count }
dystopia-registry-tab-wanted = Розыск · { $count }
dystopia-registry-tab-unidentified = Неопознанные · { $count }
dystopia-registry-search = Поиск
dystopia-registry-search-placeholder = номер или имя
dystopia-registry-filter-department = Служба
dystopia-registry-filter-status = Статус
dystopia-registry-filter-all = все
dystopia-registry-col-number = №
dystopia-registry-col-name = Гражданин
dystopia-registry-col-job = Должность
dystopia-registry-col-status = Статус
dystopia-registry-col-loyalty = Лоял.
dystopia-registry-list-footer = записей { $shown } из { $total } · в розыске { $wanted } · под надзором { $supervised }
dystopia-registry-list-empty = Никого не найдено.
dystopia-registry-head = глава Города

dystopia-registry-back = ← к списку
dystopia-registry-dossier-prompt = > реестр.дело { $number }_
dystopia-registry-photo-caption = снимок при входе · { $time }
dystopia-registry-change-status = Изменить статус
dystopia-registry-status-locked = Статус не изменяется
dystopia-registry-dismissed = ОТСТРАНЁН ОТ ДОЛЖНОСТИ
dystopia-registry-loyalty = Лояльность
dystopia-registry-loyalty-norm = норма должности — { $norm } · { $delta } по журналу
dystopia-registry-outside-loyalty-1 = ★ ВНЕ УЧЁТА ★
dystopia-registry-outside-loyalty-2 = ЛОЯЛЬНОСТИ
dystopia-registry-outside-loyalty-hint = Консул — источник закона и мера лояльности; оценивать его некому.
dystopia-registry-readonly = Только просмотр: править дела можно по ID-карте Стражи, гражданского инспектора или Консула.

dystopia-registry-subtab-summary = Сводка
dystopia-registry-subtab-journal = Журнал
dystopia-registry-subtab-journal-count = Журнал · { $count }
dystopia-registry-subtab-journal-closed = Журнал · закрыт
dystopia-registry-subtab-route = Маршрут
dystopia-registry-subtab-bank = Банк
dystopia-registry-subtab-bank-closed = Банк · закрыт

dystopia-registry-field-name = Имя
dystopia-registry-field-age = Возраст
dystopia-registry-field-species = Вид · пол
dystopia-registry-field-joined = В Городе с
dystopia-registry-field-job = Должность
dystopia-registry-field-department = Служба
dystopia-registry-field-fingerprint = Отпечатки
dystopia-registry-field-dna = ДНК
dystopia-registry-joined = { $time } (вход в Город)
dystopia-registry-classified = засекречено
dystopia-registry-gender-male = мужской
dystopia-registry-gender-female = женский
dystopia-registry-gender-epicene = —
dystopia-registry-gender-neuter = —

dystopia-registry-last-seen = Последнее известное местонахождение
dystopia-registry-last-seen-none = Камеры его пока не видели.
dystopia-registry-notes = Особые приметы
dystopia-registry-notes-empty = не указаны
dystopia-registry-notes-placeholder = шрамы, татуировки, походка, с кем замечен…
dystopia-registry-notes-edit = Изменить
dystopia-registry-notes-save = Сохранить
dystopia-registry-notes-cancel = Отмена
dystopia-registry-action-bank = Банковские операции
dystopia-registry-action-journal = + Запись в журнал
dystopia-registry-action-print = Печать ориентировки

dystopia-registry-chip-all = все · { $count }
dystopia-registry-chip-violations = нарушения · { $count }
dystopia-registry-chip-rewards = поощрения · { $count }
dystopia-registry-chip-service = служебные · { $count }
dystopia-registry-add-violation = + Нарушение
dystopia-registry-add-reward = + Поощрение
dystopia-registry-journal-empty = Записей нет.
dystopia-registry-form-violation = Новое нарушение
dystopia-registry-form-law = Статья
dystopia-registry-form-law-item = Ст. { $number }. { $title }
dystopia-registry-form-class = Класс
dystopia-registry-form-class-item = Класс { $class }
dystopia-registry-form-class-repeat = Класс { $class } · повторное
dystopia-registry-form-loyalty = Снять
dystopia-registry-form-range = очков: { $min }–{ $max }
dystopia-registry-form-dismissal = отстранение и обнуление лояльности
dystopia-registry-form-details = Основание
dystopia-registry-form-details-placeholder = что произошло (необязательно)
dystopia-registry-form-write = Записать
dystopia-registry-form-reward = Новое поощрение
dystopia-registry-form-reward-text = За что
dystopia-registry-form-reward-placeholder = перевыполнение нормы, донесение о нарушении…
dystopia-registry-form-reward-amount = Добавить
dystopia-registry-form-reward-range = очков: 1–{ $max }
dystopia-registry-form-reward-write = Поощрить

dystopia-registry-route-header = Последние отметки камер (новые сверху)
dystopia-registry-route-empty = Камеры его пока не видели. Отметка появляется, когда камера Города или маска Стражи видит лицо гражданина или его ID-карту в руке или в слоте.
dystopia-registry-route-camera = камера «{ $camera }»
dystopia-registry-route-mask = { $camera }
dystopia-registry-method-card = по ID-карте
dystopia-registry-method-face = по лицу
dystopia-registry-method-manual = опознан Стражей
dystopia-registry-watch = Смотреть

## Неопознанные

dystopia-registry-unidentified-hint = Кого камеры видели со скрытым лицом и без ID-карты. Записи хранятся ограниченное время. «Опознать» — перенести отметку в маршрут гражданина с указанным номером (и запись в его журнал).
dystopia-registry-unidentified-footer = записей { $count } · в закрытых зонах { $restricted }
dystopia-registry-unidentified-empty = Камеры никого не видели со скрытым лицом.
dystopia-registry-unidentified-restricted = ЗАКРЫТАЯ ЗОНА
dystopia-registry-identify = Опознать
dystopia-registry-identify-placeholder = № гражданина
dystopia-registry-entry-identified = Опознан по записи камеры
dystopia-registry-entry-identified-details = Камера «{ $camera }», { $place }, { $time }.

## Просмотр камеры

dystopia-registry-camera-title = Камера Реестра
dystopia-registry-camera-subtitle = Городская Стража · просмотр камеры
dystopia-registry-camera-slogan = Город видит всё
dystopia-registry-camera-connecting = Подключение: { $camera }…
dystopia-registry-camera-connected = { $camera } · в эфире

dystopia-registry-bank-account = Счёт
dystopia-registry-bank-active = активен
dystopia-registry-bank-frozen = заморожен
dystopia-registry-bank-balance = Баланс
dystopia-registry-bank-marks = марок
dystopia-registry-bank-debt = Долг Городу
dystopia-registry-bank-debt-hint = гасится из доходов
dystopia-registry-bank-tax = Налог
dystopia-registry-bank-tax-hint = ставка профессии
dystopia-registry-bank-none = У гражданина нет счёта в Банке Города.
dystopia-registry-bank-history = Операции по счёту (новые сверху)
dystopia-registry-bank-readonly = только просмотр

dystopia-registry-status-title = Статус · №{ $number }
dystopia-registry-status-subtitle = Реестр граждан · смена статуса
dystopia-registry-status-window-citizen = { $name } · { $job } · сейчас: { $status }
dystopia-registry-status-new = Новый статус
dystopia-registry-status-supreme = { $status } · Консул
dystopia-registry-status-reason = Основание (для розыска обязательно)
dystopia-registry-status-reason-placeholder = статья Свода, что произошло
dystopia-registry-status-wanted-hint = Розыск: на гражданина можно будет напечатать ориентировку.
dystopia-registry-status-outlaw-hint = Вне закона: счёт в Банке Города замораживается, все ID-карты аннулируются, гражданин отстраняется от должности, лояльность обнуляется, камеры поднимают тревогу. Снятие поражения размораживает только счёт: новые карты и восстановление в должности — в Паспортном столе.
dystopia-registry-status-sign = Подпись — по ID-карте у вас в руке или в слоте; смена статуса запишется в журнал дела.
dystopia-registry-status-apply = Применить
dystopia-registry-status-cancel = Отмена

## Записи журнала и ориентировка

dystopia-registry-entry-violation = Ст. { $number } «{ $title }»
dystopia-registry-violation-details = Класс { $class }: { $legal }.
dystopia-registry-violation-details-repeat = Класс { $class } (повторное): { $legal }.
dystopia-registry-violation-dismissed = Отстранён от должности.
dystopia-registry-entry-reward = Поощрение
dystopia-registry-outlaw-effects = Вне закона: счёт заморожен, карт аннулировано: { $cards }, отстранён от должности, лояльность обнулена.
dystopia-registry-outlaw-card-reason = Поражение в правах
dystopia-registry-outlaw-lifted = Поражение в правах снято. Отстранение от должности остаётся.

dystopia-registry-poster-stamp = Городская Стража
dystopia-registry-poster-name = ориентировка №{ $poster } (№{ $number })
dystopia-registry-poster =
    {"["}color=#96201C]{"["}head=1]РАЗЫСКИВАЕТСЯ[/head][/color]
    {"["}italic]Ориентировка №{ $poster }[/italic]
    ────────────────────
    {"["}bold]Гражданин:[/bold] №{ $number } · { $name }
    {"["}bold]Должность:[/bold] { $job }
    {"["}bold]Возраст, вид:[/bold] { $age }, { $species }
    ────────────────────
    {"["}bold]Особые приметы:[/bold] { $notes }
    {"["}bold]Основание:[/bold] { $reason }
    ────────────────────
    {"["}bold]Увидевшему — сообщить ближайшему стражнику. Недонесение карается по ст. 12 Свода законов.[/bold]
    {"["}italic]Выдано: { $time } · { $author }[/italic]

## Городские камеры (тревоги на канал Стражи)

dystopia-camera-speaker = Камеры Реестра
dystopia-camera-mask-name = { $baseName } · №{ $number }
dystopia-camera-alert-wanted = Разыскиваемый №{ $number } { $name } — { $place } (камера «{ $camera }»). Основание: { $reason }
dystopia-camera-alert-outlaw = [bold]ВНЕ ЗАКОНА![/bold] Поражённый в правах №{ $number } { $name } — { $place } (камера «{ $camera }»).
dystopia-camera-alert-restricted = Неопознанный в закрытой зоне: { $description } — { $place } (камера «{ $camera }»).

## ID-карты граждан

dystopia-card-serial = К-{ $number }-{ $index }
dystopia-card-examine = Карта [color=gold]{ $serial }[/color] · гражданин Города №{ $number }.
dystopia-card-examine-annulled = [color=#FF5A4E][bold]АННУЛИРОВАНА[/bold][/color] — недействительна, доступов нет.
dystopia-card-examine-suspended = [color=#F0954A]Доступы приостановлены: владелец отстранён от должности.[/color]
dystopia-registry-entry-card-annulled = Карта { $serial } аннулирована
dystopia-registry-entry-card-issued = Выдана карта { $serial }
dystopia-registry-entry-reinstated = Восстановлен в должности

## Карты в личном деле (консоль социоучёта)

dystopia-registry-cards = ID-карты
dystopia-registry-cards-none = Карт нет.
dystopia-registry-cards-more = …и ещё { $count } (все — в Паспортном столе)
dystopia-registry-card-live = действует
dystopia-registry-card-suspended = приостановлена
dystopia-registry-card-annulled = аннулирована
dystopia-registry-card-destroyed = уничтожена

## Паспортный стол

dystopia-passport-console-title = Паспортный стол
dystopia-passport-console-subtitle = Гражданская Инспекция · ID-карты граждан
dystopia-passport-console-slogan = Каждому — своя карта
dystopia-passport-console-denied = Нет прав на это действие.
dystopia-passport-console-issue-failed = Не удалось выдать карту: для этой должности нет бланка.
dystopia-passport-console-reason-required = Укажите причину аннулирования.
dystopia-passport-console-select = Выберите гражданина в списке слева.
dystopia-passport-console-readonly = Только просмотр: выдавать и аннулировать карты можно по ID-карте Гражданской Инспекции, капитана Стражи или Консула.
dystopia-passport-col-cards = Карт
dystopia-passport-citizen = №{ $number } · { $job }
dystopia-passport-dismissed = ОТСТРАНЁН ОТ ДОЛЖНОСТИ
dystopia-passport-dismissed-hint = Доступы живых карт приостановлены, зарплата не начисляется.
dystopia-passport-outlaw-hint = Поражён в правах: восстановить в должности можно только после снятия поражения.
dystopia-passport-reinstate = Восстановить в должности
dystopia-passport-issue = Выдать новую карту
dystopia-passport-issue-hint = Карта печатается по должности гражданина: доступы должности, имя, привязка к его счёту. Бесплатно.
dystopia-passport-cards = Карты гражданина
dystopia-passport-card-issued = выдана { $time } · { $author }
dystopia-passport-card-annulled = аннулирована { $time } · { $author } · { $reason }
dystopia-passport-annul = Аннулировать
dystopia-passport-annul-reason = причина: утеряна, украдена…
dystopia-passport-annul-confirm = Подтвердить

## Консоль Гражданской Инспекции (ID-карты: имя, должность, доступы)

dystopia-id-console-title = Гражданская Инспекция
dystopia-id-console-subtitle = Инспекция · назначение на работы и доступы
dystopia-id-console-slogan = У каждого своё место
dystopia-id-console-privileged = Карта инспектора
dystopia-id-console-target = Карта гражданина
dystopia-id-console-insert = Вставить
dystopia-id-console-eject = Извлечь
dystopia-id-console-empty = — слот пуст —
dystopia-id-console-manifest = Штатный список
dystopia-id-console-status-no-privileged = Вставьте карту инспектора: без неё консоль только показывает.
dystopia-id-console-status-unauthorized = Карта инспектора не даёт права менять карты граждан.
dystopia-id-console-status-no-target = Вставьте карту гражданина, которую нужно изменить.
dystopia-id-console-status-ready = Карта гражданина открыта для правки. Изменения записываются сразу.
dystopia-id-console-full-name = Имя
dystopia-id-console-job-title = Должность
dystopia-id-console-job = Профессия
dystopia-id-console-job-hint = Профессия задаёт значок на карте и набор доступов; должность — просто надпись. Новые карты и аннулирование — в Паспортном столе.
dystopia-id-console-save = Сохранить
dystopia-id-console-access = Доступы
dystopia-id-console-select-all = Выдать все
dystopia-id-console-deselect-all = Снять все

## Голос Стражи и тревоги камер (рация)

dystopia-camera-voice = Голос Стражи
dystopia-camera-voice-personal = [color=#7CC4FF][bold]Голос Стражи[/bold] в маске: { $text }[/color]
dystopia-camera-voice-personal-name = Голос Стражи → №{ $number }
dystopia-camera-alert-keyword = [bold]Слово-маркер «{ $word }»:[/bold] { $name } — «{ $text }» — { $place } (камера «{ $camera }»).
dystopia-surveillance-accepted-radio = Тревога №{ $id } принята: { $author }. { $text } — { $place }.

## Консоль соционадзора

dystopia-surveillance-title = Соционадзор · камеры Города
dystopia-surveillance-subtitle = Городская Стража · пост наблюдения
dystopia-surveillance-slogan = Город видит всё
dystopia-surveillance-denied = Нет прав на это действие.
dystopia-surveillance-track-failed = Нет такого гражданина (или за ним не следят).
dystopia-surveillance-keyword-failed = Маркер — одно слово от 3 до 24 букв (не больше 30 маркеров).
dystopia-surveillance-voice-failed = Сказать нельзя: камера не в эфире (лично — только в надетую маску).

dystopia-surveillance-tab-cameras = Камеры · { $count }
dystopia-surveillance-tab-alerts = Тревоги · { $count }
dystopia-surveillance-tab-track = Слежка
dystopia-surveillance-tab-track-on = Слежка · ведём
dystopia-surveillance-tab-patrols = Патрули · { $count }
dystopia-surveillance-tab-keywords = Маркеры · { $count }

dystopia-surveillance-list = Список
dystopia-surveillance-map = Карта
dystopia-surveillance-legend = ● камера Города · ▲ закрытая зона · ◆ маска Стражи · серым — нет сигнала
dystopia-surveillance-group-wall = Город
dystopia-surveillance-group-restricted = Закрытые зоны
dystopia-surveillance-group-mask = Маски Стражи
dystopia-surveillance-no-cameras = На карте нет камер Реестра.
dystopia-surveillance-down-power = нет питания
dystopia-surveillance-down-off = отключена
dystopia-surveillance-off = Выключить экран
dystopia-surveillance-screen-off = экран выключен — выберите камеру
dystopia-surveillance-screen-on = { $camera } · { $place } · в эфире
dystopia-surveillance-screen-dark = { $camera } · { $place } · нет сигнала
dystopia-surveillance-no-camera = камера не выбрана
dystopia-surveillance-connecting = подключение…
dystopia-surveillance-no-signal = нет сигнала

dystopia-surveillance-speech = Эфир · что слышно у камеры
dystopia-surveillance-speech-off = Выберите камеру, чтобы слышать, что говорят рядом с ней.
dystopia-surveillance-speech-empty = Тихо. Камера слышит речь в радиусе голоса.
dystopia-surveillance-speech-line = { $time } { $name }: { $text }
dystopia-surveillance-voice = Голос
dystopia-surveillance-voice-placeholder = «Гражданин, остановитесь!»
dystopia-surveillance-voice-loud = Вслух
dystopia-surveillance-voice-personal = Лично в маску

dystopia-surveillance-alerts-hint = Тревоги камер Реестра, новые сверху. «Принять» — вызов за вами: на канал Стражи уйдёт, кто принял, чтобы двое не ехали на один.
dystopia-surveillance-alerts-empty = Тревог нет.
dystopia-surveillance-accept = Принять
dystopia-surveillance-accepted = принял { $author } в { $time }
dystopia-surveillance-resolved = снята в { $time }
dystopia-surveillance-kind-wanted = Розыск
dystopia-surveillance-kind-outlaw = Вне закона
dystopia-surveillance-kind-restricted = Закрытая зона
dystopia-surveillance-kind-cameradown = Камера погасла
dystopia-surveillance-kind-keyword = Слово-маркер
dystopia-surveillance-alert-wanted = Разыскиваемый №{ $number } { $name } (камера «{ $camera }»). Основание: { $reason }
dystopia-surveillance-alert-outlaw = Поражённый в правах №{ $number } { $name } (камера «{ $camera }»).
dystopia-surveillance-alert-restricted = Неопознанный в закрытой зоне: { $description } (камера «{ $camera }»).
dystopia-surveillance-alert-down = Камера «{ $camera }» погасла: { $reason }.
dystopia-surveillance-alert-destroyed = Камера «{ $camera }» уничтожена.
dystopia-surveillance-alert-keyword = «{ $word }»: { $name } — «{ $text }». Слышали: { $camera }.

dystopia-surveillance-track-number = Вести гражданина №
dystopia-surveillance-track-placeholder = номер
dystopia-surveillance-track-start = Вести
dystopia-surveillance-track-stop = Перестать
dystopia-surveillance-track-hint = Пока гражданин в кадре какой-нибудь камеры, экран сам переходит на неё. Выбор камеры вручную слежку снимает.
dystopia-surveillance-track-none = Никого не ведём.
dystopia-surveillance-track-now = Сейчас: { $camera } · { $place }
dystopia-surveillance-track-lost = Сейчас его не видит ни одна камера.
dystopia-surveillance-track-path = Путь за последние минуты (новые сверху)
dystopia-surveillance-track-path-empty = За последние минуты камеры его не видели.

dystopia-surveillance-patrols-hint = Маски Стражи: надета — картинка есть, приспущена или снята — картинки нет. Место — где последний раз была картинка.
dystopia-surveillance-patrols-empty = В Страже никого.
dystopia-surveillance-mask-up = маска надета
dystopia-surveillance-mask-lowered = маска приспущена
dystopia-surveillance-mask-off = маска снята
dystopia-surveillance-dark-for = { $place } · нет картинки { $minutes } мин
dystopia-surveillance-off-shift = не в смене

dystopia-surveillance-keywords-hint = Слово у камеры, начинающееся с маркера, поднимает тревогу: «культ» — и «культа», и «культисты». Маркер — одно слово, лучше основа без окончания.
dystopia-surveillance-keywords-empty = Маркеров нет.
dystopia-surveillance-keyword-placeholder = основа слова
dystopia-surveillance-keyword-add = Добавить
dystopia-surveillance-keyword-remove = Убрать
dystopia-surveillance-keyword-line = «{ $word }…»

## Соционадзор: «Дозор», датчик Стражи, уборка тревог

dystopia-surveillance-map-beacons = Отделы
dystopia-surveillance-map-recenter = Центр
dystopia-surveillance-dismiss = ×
dystopia-surveillance-dismiss-tooltip = Убрать тревогу из ленты
dystopia-surveillance-dismiss-all = Убрать обработанные
dystopia-surveillance-kind-guardcritical = Стражник ранен
dystopia-surveillance-kind-guarddead = Стражник убит
dystopia-surveillance-kind-distress = Сигнал бедствия

dystopia-dozor-tab = Дозор · { $up }/{ $total }
dystopia-dozor-targets = Цели
dystopia-dozor-trails = Следы 30 с
dystopia-dozor-list = Стража · датчики масок
dystopia-dozor-on-map = На карте
dystopia-dozor-mask = Маска
dystopia-dozor-distress = СИГНАЛ БЕДСТВИЯ
dystopia-dozor-no-data = маска снята · нет данных
dystopia-dozor-no-data-for = маска снята · нет данных { $minutes } мин · { $place }
dystopia-dozor-no-signal = НЕТ СИГНАЛА
dystopia-dozor-map-dark = { $number } · { $minutes } мин
dystopia-dozor-health-ok = в норме
dystopia-dozor-health-hurt = ранен
dystopia-dozor-health-critical = тяжёлое состояние
dystopia-dozor-health-dead = мёртв

dystopia-guard-alert-critical = Стражник №{ $number } в тяжёлом состоянии — { $place }.
dystopia-guard-alert-critical-radio = [bold]Стражник №{ $number } в тяжёлом состоянии[/bold] — { $place }.
dystopia-guard-alert-dead = Стражник №{ $number } убит — { $place }.
dystopia-guard-alert-dead-radio = [bold]Стражник №{ $number } убит[/bold] — { $place }.
dystopia-guard-alert-distress = Стражник №{ $number } подал сигнал бедствия — { $place }.
dystopia-guard-alert-distress-radio = [bold]СИГНАЛ БЕДСТВИЯ:[/bold] стражник №{ $number } — { $place }.

## Штраф в журнале дела (штрафной терминал Стражи)

dystopia-registry-entry-fine = Штраф · ст. { $number } «{ $title }»
dystopia-registry-entry-fine-free = Штраф
dystopia-registry-fine-details = Сумма { $amount }: списано { $taken }, в долг { $debt }.

## Программа КПК «Мой статус»

dystopia-status-program-name = Мой статус
dystopia-status-no-card = В КПК нет ID-карты гражданина Города.
dystopia-status-dismissed = ОТСТРАНЁН ОТ ДОЛЖНОСТИ
dystopia-status-section-status = Статус
dystopia-status-reason = Основание: { $reason }
dystopia-status-section-loyalty = Лояльность
dystopia-status-outside = Вне системы лояльности.
dystopia-status-loyalty-ok = Норма должности — { $norm }. Город вами доволен.
dystopia-status-loyalty-below = Ниже нормы должности ({ $norm }). Город наблюдает.
dystopia-status-loyalty-zero = Лояльность исчерпана. Город наблюдает.
dystopia-status-section-card = ID-карта
dystopia-status-card-annulled = АННУЛИРОВАНА
dystopia-status-card-annulled-hint = Обратитесь в Паспортный стол.
dystopia-status-card-valid = ДЕЙСТВИТЕЛЬНА
dystopia-status-field-status = Статус
dystopia-status-field-loyalty = Лояльность
dystopia-status-field-serial = Серия
dystopia-status-field-card-state = Состояние
dystopia-status-card-unknown = Серийный номер карты не записан.
