## Реестр граждан (социоучёт)

dystopia-registry-status-citizen = гражданин
dystopia-registry-status-supervised = под надзором
dystopia-registry-status-wanted = в розыске
dystopia-registry-status-detained = задержан
dystopia-registry-status-disenfranchised = поражён в правах
dystopia-registry-status-dead = мёртв

dystopia-registry-author-registry = Реестр
dystopia-registry-author-admin = Администрация сервера

dystopia-registry-entry-registered = Внесён в Реестр граждан
dystopia-registry-entry-status = Статус: { $old } → { $new }
dystopia-registry-entry-notes = Особые приметы обновлены
dystopia-registry-entry-died = Смерть зарегистрирована
dystopia-registry-entry-revived = Возвращён к жизни
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
