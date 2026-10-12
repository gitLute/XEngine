# Сессии агентов ревизии

`sessionID` каждого запущенного агента. Нужен для возобновления сессии после
исчерпания лимита запросов — задача при этом **не сокращается**.

## Волна 1.5 — сверка списка

Аналитики. Ничего не правят. Задача: по каждому пункту реестра дать
исполняемый пример и вывод — **воспроизводится на текущем коде или уже закрыт
правкой**. Причина волны — §9 брифа: `Problems.md` отстал от кода.

| агент | зона | sessionID | отчёт | итог |
|---|---|---|---|---|
| `graphics-random` | `Graphics/`, `Random/` | `ses_ed0e39013ffeAbn42ifbIK1ruR` | `revision/reports/graphics-random.md` | сдан |
| `interpolation` | `Interpolation/` | `ses_ed0e39011ffeM7gNINVh3bvOBT` | `revision/reports/interpolation.md` | сдан |
| `serialization` | `Serialization/` | `ses_ed0e30cdeffeXg2Uew8D4J2occ` | `revision/reports/serialization.md` | сдан |
| `transform` | `Transform/` | `ses_ed0e30cdcffeIoIg4ucw5M1sIJ` | `revision/reports/transform.md` | сдан |
| `internal-shapes3d` | `Internal/SurfaceRadius.cs`, `Shapes3D/` | `ses_ed0e2c22bffeqm70xZHk4nhJgW` | `revision/reports/internal-shapes3d.md` | сдан |

## Волны починки

Исправители. Запускаются только после приёмки волны 1.5 по каждому пункту.
Коммит делает владелец ревизии после приёмки, агентами запрещён.

| волна | агент | дефекты | sessionID | отчёт | итог |
|---|---|---|---|---|---|
