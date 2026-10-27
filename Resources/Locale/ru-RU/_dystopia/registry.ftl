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

dystopia-registry-tab-citizens = Граждане · { $count }
dystopia-registry-tab-wanted = Розыск · { $count }
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
dystopia-registry-no-cameras = Нет данных: городские камеры ещё не подключены к Реестру.
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

dystopia-registry-route-header = Последние отметки камер
dystopia-registry-route-empty = Маршрут появится, когда городские камеры подключат к Реестру: каждая камера будет отмечать, где и когда она видела гражданина.

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
dystopia-registry-status-sign = Подпись — по ID-карте у вас в руке или в слоте; смена статуса запишется в журнал дела.
dystopia-registry-status-apply = Применить
dystopia-registry-status-cancel = Отмена

## Записи журнала и ориентировка

dystopia-registry-entry-violation = Ст. { $number } «{ $title }»
dystopia-registry-violation-details = Класс { $class }: { $legal }.
dystopia-registry-violation-details-repeat = Класс { $class } (повторное): { $legal }.
dystopia-registry-violation-dismissed = Отстранён от должности.
dystopia-registry-entry-reward = Поощрение

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
