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
dystopia-city-console-stub-decrees = Раздел «Положения» появится в следующем обновлении: режимы Города и консульские уведомления.
dystopia-city-console-stub-laws = Раздел «Законы» появится в следующем обновлении: Свод законов Города.
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
dystopia-city-console-log-header = Журнал казны
dystopia-city-console-log-empty = Записей пока нет.
dystopia-city-console-no-reason = без объяснения причин
dystopia-city-console-access-denied = Доступ запрещён.
dystopia-city-console-not-enough-treasury = В казне недостаточно средств.
dystopia-city-console-nothing-to-seize = На счету нечего изымать.
dystopia-city-console-log-rates = { $actor }: { $job } — зарплата { $salary }, налог { $tax }%.
dystopia-city-console-log-bonus = { $actor }: премия { $name } (№{ $id }) +{ $net }, налог { $tax }. Основание: { $reason }
dystopia-city-console-log-seize = { $actor }: изъятие у { $name } (№{ $id }) { $amount }. Основание: { $reason }
