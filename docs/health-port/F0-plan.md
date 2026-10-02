# Система здоровья — Ф0: карта переноса

Цель: механики и возможности исходной системы здоровья **1 в 1**, поверх нашей новой системы тела
(органы — связанные сущности, урон через модели урона). Исходный код переносится с минимальными правками,
отличия тела закрываются **слоем совместимости**.

## Объём исходной системы

| Что | Сколько |
|---|---|
| Код (Content.*/_) | 288 файлов |
| Прототипы | 126 |
| Локализация | 79 |
| Спрайты | 643 |
| Исходная система тела | 33 файла, ~2900 строк в SharedBodySystem |

## Как система здоровья обращается к телу

Методы SharedBodySystem, которые вызывает система здоровья (по числу вызовов):
GetBodyChildren (16), GetBodyChildrenOfType (9), GetTargetBodyPart (7), TryGetBodyPartOrgans (6), GetPartOrgans (6),
TryGetRootPart (5), TryGetBodyOrganEntityComps (3), TryGetPartSlotContainerName, TryGetParentBodyPart, TryCreatePartSlot,
RemoveOrgan, GetPartSlotContainerId, GetBodyPartCount, DropSlotContents, DetachPart, CanInsertOrgan, AttachPart (по 2),
TryRemoveOrgan, TryCreateOrganSlot, InsertOrgan, GibPart, GetSlotFromBodyPart, CanAttachToSlot, ModifyMarkings,
GetBodyOrgans, GetBodyChildrenWithComponent, GetBodyPartChildren, ConvertTargetBodyPart (по 1).

Компоненты: BodyPartComponent (77 упоминаний), BodyComponent (49), OrganComponent (43).

## Соответствие тела

| Исходная система | Наша сборка | Фаза |
|---|---|---|
| Часть тела (BodyPartComponent, тип и сторона) | орган категории Torso/Groin/Head/Arm*/Hand*/Leg*/Foot* + наш BodyPartComponent (совместимость) | Ф1 ✅ |
| Корневая часть (грудь) | орган Torso с BodyPart partType Chest | Ф1 ✅ |
| Иерархия частей (слоты-связи) | ParentOrgan / ChildOrgan (InitialBody.relationships) | Ф1 ✅ |
| Пах (Groin) как отдельная часть | новая категория Groin, орган OrganGroin, ноги и органы живота — дети паха | Ф1 ✅ |
| Органы части (OrganSlot) | органы с InternalChildOrgan — дети части | Ф1 ✅ (чтение) |
| BodyPart.Body | синхронизируется с событиями вставки/извлечения органа | Ф1 ✅ |
| AttachPart / DetachPart | Relate + контейнер тела / DetachableOrganSystem.Detach | Ф1 ✅ |
| Слоты частей и органов (TryCreatePartSlot, CanInsertOrgan, InsertOrgan...) | связи + InitialBody.relationships | Ф8 |
| ItemInsertionSlot (полость груди) | ItemSlot на груди | Ф8 |
| OnAdd / OnRemove частей (BodyEffects) | поля перенесены, применение — с BodyEffects | Ф4 |
| Целостность части (Integrity, EnableIntegrity) | компоненты ран на органе-части | Ф3 |
| Исходный OrganComponent (целостность органа, эффекты) | отдельный компонент рядом с нашим OrganComponent | Ф7 |
| GetTargetBodyPart / ConvertTargetBodyPart | TargetBodyPart ↔ (тип, сторона) | Ф2 |
| ModifyMarkings | наши маркировки органов | Ф8 |

## Решения

1. **SharedBodySystem** — запечатанная общая система в исходном пространстве имён (Content.Shared.Body.Systems),
   с теми же сигнатурами. Исходный код подключается к ней без правок.
2. **BodyPartComponent** — в исходном пространстве имён (Content.Shared.Body.Part), вешается на базовые прототипы внешних органов.
3. **Пах** добавлен всем видам (13 файлов тел: ванильные + Corvax). Видимого слоя нет — на спрайте пах часть торса.
4. **Правки ванили** помечены комментарием `# Dystopia-Health`.
5. **Наши S1/S2** работают до Ф8 (добавлен пах и «Вскрыть брюшную полость»), в Ф8 заменяются полной хирургией.

## Фазы

Ф0 карта ✅ · Ф1 слой совместимости тела ✅ · Ф2 прицел · Ф3 раны · Ф4 травмы и кости · Ф5 кровь · Ф6 боль и сознание ·
Ф7 органы · Ф8 хирургия · Ф9 медицинский интерфейс · Ф10 контент · Ф11 наша сборка · Ф12 проверка паритета.

## Проверка Ф1

Команда сервера `healthbody <uid>` выводит тело так, как его видит система здоровья: корень, каждую часть (тип, сторона, тело, родитель)
и органы части. Для человека ожидается:
грудь → голова, левая/правая рука, пах; руки → кисти; пах → ноги; ноги → стопы;
органы: голова — мозг, глаза, язык, уши; грудь — сердце, лёгкие; пах — желудок, печень, почки, аппендикс.
