## Банк Города

dystopia-bank-account-opened = Банк Города открыл вам счёт №{ $id }. Стартовый баланс: { $balance } { $balance ->
        [one] марка
        [few] марки
       *[many] марок
    }. Ваша ID-карта — ключ к счёту.
dystopia-bank-salary-paid = Зарплата: +{ $net } { $net ->
        [one] марка
        [few] марки
       *[many] марок
    } (удержан налог: { $tax }). На счету: { $balance }.
dystopia-bank-salary-unpaid = Казна Города пуста. Зарплата за этот период не выплачена.
dystopia-bank-card-examine-account = Привязана к счёту Банка Города [color=gold]№{ $id }[/color].
dystopia-bank-card-examine-balance = На счету: [color=gold]{ $balance } { $balance ->
        [one] марка
        [few] марки
       *[many] марок
    }[/color].

## Админ-команды Банка Города

cmd-citybank_info-desc = Показать казну Города и все счета.
cmd-citybank_info-help = Использование: { $command }
cmd-citybank_treasury-desc = Изменить казну Города на сумму (можно отрицательную).
cmd-citybank_treasury-help = Использование: { $command } <сумма>
cmd-citybank_deposit-desc = Начислить (или списать, если сумма отрицательная) деньги на счёт жителя.
cmd-citybank_deposit-help = Использование: { $command } <номер счёта> <сумма>
cmd-citybank_payday-desc = Немедленно выплатить зарплаты.
cmd-citybank_payday-help = Использование: { $command }

cmd-citybank-no-bank = Банк Города не найден: на станции нет компонента CityBank.
cmd-citybank-bad-amount = Неверные аргументы. Суммы и номера счетов — целые числа.
cmd-citybank-no-account = Счёт №{ $id } не найден.
cmd-citybank-info-header = Казна: { $treasury }. Счетов: { $accounts }. Следующая выплата через { $next } сек.
cmd-citybank-treasury-done = Казна Города: { $treasury }.
cmd-citybank-deposit-done = Счёт №{ $id } ({ $name }): { $balance }.
cmd-citybank-payday-done = Выплачено зарплат: { $paid }, не хватило денег в казне: { $unpaid }. Казна: { $treasury }.

## Премии и изъятия (сообщения жителю)

dystopia-bank-bonus-received = Консул выписал вам премию: +{ $net } { $net ->
        [one] марка
        [few] марки
       *[many] марок
    } (удержан налог: { $tax }). Основание: { $reason }
dystopia-bank-seized = По решению Консула с вашего счёта изъято { $amount } { $amount ->
        [one] марка
        [few] марки
       *[many] марок
    }. Основание: { $reason }
dystopia-bank-log-payday = Выплата зарплат: получили { $paid }, не хватило казны: { $unpaid }. Казна: { $treasury }.

## Консоль Управления Городом

dystopia-city-console-title = Консоль Управления Городом
dystopia-city-console-tab-treasury = Казна
dystopia-city-console-tab-decrees = Положения
dystopia-city-console-tab-laws = Законы
dystopia-city-console-treasury = Казна Города: { $amount } { $amount ->
        [one] марка
        [few] марки
       *[many] марок
    }
dystopia-city-console-payday = Следующая выплата зарплат через { $minutes }:{ $seconds }
dystopia-city-console-rates-header = Ставки профессий (зарплата за период и налог с дохода)
dystopia-city-console-col-job = Профессия
dystopia-city-console-col-salary = Зарплата
dystopia-city-console-col-tax = Налог, %
dystopia-city-console-save = Сохранить
dystopia-city-console-money-header = Премии и изъятия
dystopia-city-console-amount = Сумма
dystopia-city-console-reason = Основание
dystopia-city-console-bonus = Выписать премию
dystopia-city-console-seize = Изъять
dystopia-city-console-account-line = №{ $id } — { $name } ({ $job }) — { $balance }{ $frozen }
dystopia-city-console-frozen = {" "}[заморожен]
dystopia-city-console-log-header = Журнал решений Консула
dystopia-city-console-log-empty = Записей пока нет.
dystopia-city-console-no-reason = без объяснения причин
dystopia-city-console-access-denied = Доступ запрещён.
dystopia-city-console-not-enough-treasury = В казне недостаточно средств.
dystopia-city-console-nothing-to-seize = На счету нечего изымать.
dystopia-city-console-log-rates = { $actor }: { $job } — зарплата { $salary }, налог { $tax }%.
dystopia-city-console-log-bonus = { $actor }: премия { $name } (№{ $id }) +{ $net }, налог { $tax }. Основание: { $reason }
dystopia-city-console-log-seize = { $actor }: изъятие у { $name } (№{ $id }) { $amount }. Основание: { $reason }

## Консоль: Положения

dystopia-city-console-sender = Консул Города
dystopia-city-console-current-mode = Действующее положение: { $name }
dystopia-city-console-no-modes = Положения Города недоступны на этой станции.
dystopia-city-console-modes-header = Ввести положение
dystopia-city-console-announce-header = Консульское уведомление
dystopia-city-console-announce-placeholder = Текст уведомления для всего Города
dystopia-city-console-announce-button = Опубликовать
dystopia-city-console-announce-cooldown = Следующее уведомление можно опубликовать через { $seconds } сек.
dystopia-city-console-cooldown = Слишком часто. Подождите.
dystopia-city-console-mode-announcement = { $announcement } Положение: «{ $name }». { $instructions }
dystopia-city-console-log-mode = { $actor }: введено положение «{ $name }».
dystopia-city-console-log-announce = { $actor }: уведомление — { $text }

## История счёта (программа КПК «Банк»)

dystopia-bank-history-opened = Счёт открыт, стартовый баланс { $amount }
dystopia-bank-history-salary = Зарплата +{ $net } (налог { $tax }, погашено долга { $debt })
dystopia-bank-history-bonus = Премия +{ $net } (налог { $tax }): { $reason }
dystopia-bank-history-seized = Изъято −{ $amount }: { $reason }
dystopia-bank-history-transfer-out = Перевод −{ $amount } на №{ $id } ({ $name }): { $comment }
dystopia-bank-history-transfer-in = Перевод +{ $amount } от №{ $id } ({ $name }): { $comment }
dystopia-bank-history-fine = Штраф −{ $amount } (в долг { $debt }): { $reason }

## Банковский реестр (терминал банковских операций)

dystopia-bank-ledger-opened = Открыт счёт №{ $id } ({ $name }), стартовый баланс { $amount }.
dystopia-bank-ledger-salary = Зарплата №{ $id } ({ $name }): +{ $net }, налог { $tax }, погашено долга { $debt }.
dystopia-bank-ledger-bonus = Премия Консула №{ $id } ({ $name }): +{ $net }, налог { $tax }. { $reason }
dystopia-bank-ledger-seized = Изъятие Консула №{ $id } ({ $name }): −{ $amount }. { $reason }
dystopia-bank-ledger-transfer = Перевод №{ $from } → №{ $to }: { $amount }. { $comment }
dystopia-bank-ledger-fine = Штраф №{ $id } ({ $name }): { $amount }, в долг { $debt }. { $reason }

## Переводы

dystopia-bank-transfer-received = Входящий перевод: +{ $amount } от №{ $id } ({ $name }). { $comment }
dystopia-bank-transfer-success = Переведено { $amount } на счёт №{ $id }.
dystopia-bank-transfer-frozen = Счёт заморожен. Операции запрещены.
dystopia-bank-transfer-recipient-frozen = Счёт получателя заморожен. Перевод невозможен.
dystopia-bank-transfer-no-money = Недостаточно средств.
dystopia-bank-transfer-no-recipient = Счёт получателя не найден.
dystopia-bank-transfer-same = Нельзя перевести на свой же счёт.
dystopia-bank-transfer-bad-amount = Неверная сумма.
dystopia-bank-debt-withheld = В счёт долга удержано { $amount }. Остаток долга: { $debt }.

## Программа КПК «Банк»

dystopia-bank-program-name = Банк
dystopia-bank-ui-no-card = В КПК нет ID-карты со счётом Банка Города.
dystopia-bank-ui-account = Счёт №{ $id } — { $name }
dystopia-bank-ui-balance = Баланс: { $balance } { $balance ->
        [one] марка
        [few] марки
       *[many] марок
    }
dystopia-bank-ui-frozen = {" "}[ЗАМОРОЖЕН]
dystopia-bank-ui-debt = Долг перед Городом: { $debt }
dystopia-bank-ui-transfer-header = Перевод
dystopia-bank-ui-to = Номер счёта получателя
dystopia-bank-ui-amount = Сумма
dystopia-bank-ui-comment = Комментарий
dystopia-bank-ui-send = Перевести
dystopia-bank-ui-history-header = История операций
dystopia-bank-ui-history-empty = Операций пока нет.

## Уведомления КПК

dystopia-bank-notification-header = Банк Города
dystopia-bank-fined = Выписан штраф { $amount }. В долг: { $debt }. Основание: { $reason }
dystopia-bank-frozen-notify = Ваш счёт заморожен. Операции по счёту запрещены.
dystopia-bank-unfrozen-notify = Ваш счёт разморожен.
dystopia-bank-history-frozen = Счёт заморожен
dystopia-bank-history-unfrozen = Счёт разморожен
dystopia-bank-ledger-frozen = Счёт №{ $id } ({ $name }) заморожен. Распорядился: { $actor }.
dystopia-bank-ledger-unfrozen = Счёт №{ $id } ({ $name }) разморожен. Распорядился: { $actor }.

## Терминал банковских операций

dystopia-bank-terminal-title = Терминал банковских операций
dystopia-bank-terminal-search = Поиск: номер счёта, имя или профессия
dystopia-bank-terminal-accounts-header = Реестр счетов
dystopia-bank-terminal-col-id = Счёт
dystopia-bank-terminal-col-name = Владелец
dystopia-bank-terminal-col-job = Профессия
dystopia-bank-terminal-col-balance = Баланс
dystopia-bank-terminal-col-debt = Долг
dystopia-bank-terminal-col-status = Статус
dystopia-bank-terminal-status-active = активен
dystopia-bank-terminal-status-frozen = ЗАМОРОЖЕН
dystopia-bank-terminal-history = Операции
dystopia-bank-terminal-freeze = Заморозить
dystopia-bank-terminal-unfreeze = Разморозить
dystopia-bank-terminal-ledger-header = Журнал операций (все счета)
dystopia-bank-terminal-ledger-header-account = Журнал операций: счёт №{ $id }
dystopia-bank-terminal-reset-filter = Все операции
dystopia-bank-terminal-ledger-empty = Операций нет.

## Покупки (платёжный терминал)

dystopia-bank-history-purchase = Покупка −{ $amount } у №{ $id } ({ $name }): { $description }
dystopia-bank-history-sale = Продажа +{ $net } (налог { $tax }) покупателю №{ $id }: { $description }
dystopia-bank-ledger-purchase = Покупка №{ $from } → №{ $to }: { $amount }, налог { $tax }. { $description }
dystopia-bank-purchase-notify = Оплата { $amount } ({ $name }): { $description }
dystopia-bank-sale-notify = Продажа: +{ $net } (налог { $tax }). { $description }

## Платёжный терминал

dystopia-payment-terminal-title = Платёжный терминал
dystopia-payment-terminal-linked-to = Привязан к счёту №{ $id } ({ $name })
dystopia-payment-terminal-unlinked = Терминал не привязан к счёту.
dystopia-payment-terminal-link = Привязать к моему счёту
dystopia-payment-terminal-unlink = Отвязать
dystopia-payment-terminal-amount = Сумма
dystopia-payment-terminal-description = За что (товар или услуга)
dystopia-payment-terminal-set-bill = Выставить счёт
dystopia-payment-terminal-cancel-bill = Отменить счёт
dystopia-payment-terminal-bill = К оплате: { $amount } — { $description }
dystopia-payment-terminal-no-bill-ui = Счёт к оплате не выставлен.
dystopia-payment-terminal-hint = Покупатель прикладывает к терминалу ID-карту или КПК.
dystopia-payment-terminal-tax-rate = Налог с продаж: { $rate }%
dystopia-payment-terminal-tax-preview = Налог { $rate }%: { $tax } в казну, вам — { $net }
dystopia-payment-terminal-linked = Терминал привязан к счёту №{ $id }.
dystopia-payment-terminal-not-owner = Управлять терминалом может только владелец привязанного счёта.
dystopia-payment-terminal-no-card = Нет ID-карты со счётом Банка Города.
dystopia-payment-terminal-not-linked = Сначала привяжите терминал к счёту.
dystopia-payment-terminal-no-bill = Счёт к оплате не выставлен.
dystopia-payment-terminal-bill-changed = Счёт к оплате изменился. Приложите карту снова.
dystopia-payment-terminal-paid = Оплачено: { $amount }.
dystopia-payment-terminal-merchant-frozen = Счёт продавца заморожен. Оплата невозможна.
dystopia-payment-terminal-same = Нельзя оплатить самому себе.
dystopia-payment-terminal-examine-unlinked = Терминал не привязан к счёту.
dystopia-payment-terminal-examine-linked = Привязан к счёту [color=gold]№{ $id }[/color].
dystopia-payment-terminal-examine-bill = К оплате: [color=gold]{ $amount }[/color] — { $description }
