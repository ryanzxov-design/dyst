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
