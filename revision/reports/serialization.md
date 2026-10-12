# Serialization — сверка списка (волна 1.5)

Подсистема: `src/XEngine.Mathematics/Serialization/` (`JsonConverters.cs` 351 строка,
`JsonArrayReaderHelper.cs` 403 строки).

Состояние дерева на момент работы: `HEAD = 3057642619ebff0a34e27a51c5e4c47d54bf8b57`,
рабочее дерево чистое. Ничего не правилось: ни в `src/`, ни в `tests/`, ни в
общих файлах. `git status --porcelain` до и после работы даёт одно и то же:
`M .gitignore` (изменение, существовавшее до начала сессии, мной не тронуто) и
`?? revision/` (каталог ревизии).

Изменённые мной файлы: **нет ни одного**. Записаны только файлы отчёта и
артефакты в `revision/tmp/w1.5/serialization/`.

---

## Итог

| пункт | вердикт | одно число-подтверждение |
|---|---|---|
| **P2-45** `AngleJsonConverter`: `Write` пишет `radians`, `Read` их игнорирует | **НЕ ВОСПРОИЗВОДИТСЯ** | 0 из 200 000 углов не вернулись в себя (на коде `3f36edb^` — 200 000 из 200 000) |
| **P2-46** имена каналов регистрозависимы | **НЕ ВОСПРОИЗВОДИТСЯ** | `{"X":1,"Y":2,"Z":3}` → `(1, 2, 3)` (на `3f36edb^` — `(0, 0, 0)`) |
| **P2-47** числа вне диапазона проходят молча | **НЕ ВОСПРОИЗВОДИТСЯ** | `[1e40,2,3]` → `JsonException` (на `3f36edb^` — `<Infinity, 2, 3>`, запись назад `ArgumentException`) |
| **P2-48** наружу выходит `FormatException` | **НЕ ВОСПРОИЗВОДИТСЯ** | `"GG0000FF"` → `JsonException`, `Path=$`, `BytePositionInLine=10` (на `3f36edb^` — `System.FormatException`, `catch (JsonException)` не ловит) |
| **RED-2** `ColorWithNonFiniteChannelIsRejectedOnWrite` | **красный, и по причине, отличной от указанной в задании** | `new Rgba32(float.NaN,0,0,1).R == 0f`; `Assert.Throws() Failure: No exception was thrown` |

Все четыре пункта закрыты **одним** коммитом `3f36edb` («Serialization: граница
доверия — вложенное значение, регистр имён, диапазон, угол», 10.10.2026), который
является предком `HEAD`. Коммитов после него, трогающих `Serialization/`, нет:

```
git merge-base --is-ancestor 3f36edb HEAD   ->  да
git log --oneline 3f36edb..HEAD -- src/XEngine.Mathematics/Serialization/  ->  пусто
```

Подтверждение закрытия по `git log -S` (каждый пункт — свой поиск):

| что искал | какой коммит |
|---|---|
| `WriteNumber("degrees"` (удаление) | `3f36edb` (введение было `62c3e56`) |
| `Ascii.EqualsIgnoreCase` (появление) | `3f36edb` |
| `ReadFiniteSingle` (появление) | `3f36edb` |
| `ReadFiniteDouble` (появление) | `3f36edb` |
| `FromHexChecked` (появление) | `3f36edb` |
| `float.IsFinite(value.R)` (появление) | `3f36edb` |

Отдельно: `ef0898d` («Тест Json_ColorRejectsNonColorTokens закреплял дефект
P2-48») менял одну строку `FormatException` → `JsonException` в чужом тестовом
файле `tests/XEngine.Mathematics.Tests/RemainingBranchTests.cs`. Это уже не
дефект кода, а приведение доктрины в соответствие.

**Вывод для волны 2.3:** правки в `Serialization/` по этим четырём пунктам не
требуются. Требуется решение владельца по `RED-2` — см. раздел «Требует вмешательства
владельца».

---

## Метод

Зонд — отдельное консольное проек��о, собранное **дважды из одного и того же
исходника**:

| прогон | что собирается | куда |
|---|---|---|
| текущий | `probe/Program.cs` + `ProjectReference` на `src/XEngine.Mathematics/XEngine.Mathematics.csproj` | `art/` |
| обратный ход | тот же `Program.cs` + `ProjectReference` на копию библиотеки, извлечённую из git командой `git archive 3f36edb^ src/XEngine.Mathematics \| tar -x -C revision/tmp/w1.5/serialization/oldlib --strip-components=2` | `artold/` |

Копия `oldlib/` лежит внутри моего каталога, `src/` не тронут, обратный ход сделан
**в scratch**, а не откатом файлов на месте.

Файлы измерений (в отчёт перенесены числа, не вывод):

- `revision/tmp/w1.5/serialization/out/current.txt` — 149 строк, текущий код
- `revision/tmp/w1.5/serialization/out/old.txt` — 149 строк, код на `3f36edb^`
- `revision/tmp/w1.5/serialization/out/current_det.txt` — 149 строк, `MathBackend=Deterministic`
- `revision/tmp/w1.5/serialization/out/tests_fast.txt` — полный прогон тестов

Консоль зонда печатает одну строку; весь вывод идёт в файл. Внутри циклов только
счётчики, одна проверка после. Границы циклов — до 200 000, до целочисленного
предела не приближаются.

### Внешние источники

1. **`JsonSerializerOptions.PropertyNameCaseInsensitive`** —
   <https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions.propertynamecaseinsensitive>.
   Из документации взято дословно: «Gets or sets a value that indicates whether a
   property's name uses a case-insensitive comparison during deserialization. The
   default value is **false**». Это опорный факт для трактовки эталона в P2-46.
2. **`Utf8JsonReader.TryGetSingle` / `TryGetDouble`** — исходники `dotnet/runtime`,
   `src/libraries/System.Text.Json/src/System/Text/Json/Reader/Utf8JsonReader.TryGet.cs`,
   получены через GitHub API (`tools.github.get_file_contents`). Из них взято
   дословно тело обоих методов: они проверяют **только** `TokenType == Number` и
   `span.Length == bytesConsumed`, и **не проверяют конечность результата**.
   `Utf8Parser.TryParse` на переполнении возвращает `±∞` с успехом, поэтому
   `GetSingle`/`GetDouble` отдают `±∞`. Это ровно то поведение, которое библиотеке
   приходится перекрывать своей проверкой, и именно его подтверждает измерение
   (§P2-47).
3. **Расхождение документации и реализации, зафиксированное здесь намеренно.**
   Страница <https://learn.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader.getdouble>
   перечисляет `FormatException` для числа «less than `Double.MinValue` or greater
   than `Double.MaxValue`». Исходник в `main` эту гарантию **не держит** — в
   XML-документации самого метода оговорка стоит иначе: «On any framework that is
   not .NET Core 3.0 or higher, thrown if the JSON token value represents a number
   less than `double.MinValue` or greater than `double.MaxValue`». Измерено на
   .NET 10.0.112: `GetDouble()` на `1e400` возвращает `Infinity`, исключения нет.
   Значит опираться на страницу документации здесь нельзя, опираться надо на
   исходник и на измерение; обе сходятся.
4. **`JsonSerializer.Deserialize`** — <https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserialize>.
   Взят перечень исключений метода (`JsonException`, `ArgumentException`,
   `NotSupportedException`, `InvalidOperationException`); **никакого** утверждения
   об оборачивании исключений пользовательского конвертера в документации нет,
   поэтому поведение установлено измерением (§P2-48).

---

## По каждому пункту

### P2-45. `AngleJsonConverter`: `Write` пишет `radians` в полной точности, `Read` их игнорирует

**Вердикт: НЕ ВОСПРОИЗВОДИТСЯ. Число нарушений — ноль.**

**Что в коде сейчас.** `Write` (`src/XEngine.Mathematics/Serialization/JsonConverters.cs:192-197`)
пишет ровно одно поле:

```csharp
writer.WriteStartObject();
writer.WriteNumber("radians", value.Radians);
writer.WriteEndObject();
```

`Read` (`JsonConverters.cs:158-179`) это поле читает, причём в любом регистре, и
`degrees` читает только если он задан. Приоритет `degrees` над `radians` сохранён
(`JsonConverters.cs:188`), то есть обратная совместимость со старыми файлами не
сломана.

**Исполняемый пример — круговой проход.**

Вход: 200 000 углов, полученных детерминированным xorshift64*, радианы равномерно
в диапазоне `[-1e6; 1e6]` (нормализация отрабатывает по-настоящему). Для каждого:
`JsonSerializer.Serialize(angle, opts)` → `JsonSerializer.Deserialize<Angle>(json, opts)`,
сравнение `BitConverter.DoubleToInt64Bits(Radians)`.

| мера | текущий код | код на `3f36edb^` |
|---|---|---|
| не вернулось в себя | **0 из 200 000** | 200 000 из 200 000 |
| исключений при обмене | 0 | 0 |

Фактическая запись на текущем коде:

```
Angle.FromRadians(1.0)          -> {"radians":1}
Angle.FromDegrees(90)           -> {"radians":1.5707963267948966}
Angle.FromRadiansRaw(1e6)       -> {"radians":1000000}
Angle.FromRadiansRaw(0.5).ScaleRaw(4) -> {"radians":2}
```

На коде `3f36edb^` та же величина записывалась как
`{"radians":1,"degrees":57.29578}` — то есть `Read` видел округлённое поле и
проигнорировал полное.

**Второе утверждение пункта — переворот знака на 2π в полосе 1e-6° у −180°.**

Вход: 20 000 значений градусов, равномерно на `[-180; -180 + 1e-6]`, круговой
проход, критерий переворота — `|b.Radians − a.Radians| > π`.

| мера | текущий код | код на `3f36edb^` |
|---|---|---|
| переворотов на 2π | **0 из 20 000** | 10 000 из 20 000 |
| несовпадений битов | **0 из 20 000** | 19 999 из 20 000 |

Первый переворот на старом коде (для иллюстрации природы дефекта):

```
deg=-179.99999999995  json={"radians":-3.1415926535889205,"degrees":-180}
было=-3.1415926535889205  стало=3.141592653589793  d=6.283185307178714
```

**Третье, что проверялось попутно по этому пункту** (углы ≤ 5e-7°, которые
старый код читал нулём): на текущем коде **0 из 20 000** прочитали нулём, на
`3f36edb^` — 20 000 из 20 000.

**Эталон .NET без конвертера библиотеки.** Тот же самый набор из 200 000
`double`, но через штатный `System.Text.Json` без единого конвертера:

| схема | не вернулось |
|---|---|
| `JsonSerializer.Serialize(double)` → `Deserialize<double>` (встроенный путь .NET) | **0 из 200 000** |
| округление до шести знаков, как в прежней `degrees`, и обратно | 200 000 из 200 000 |

Первая строка — то, что делает платформа сама: запись и чтение `double` в
`System.Text.Json` взаимно точны. Вторая строка воспроизводит ровно ту схему,
которая была в `Write` до `3f36edb`, и показывает, что дефект был в выборе
схемы, а не в платформе.

**Какая правка закрыла.** `3f36edb`: `Write` перестала писать `degrees`
(`JsonConverters.cs:192-197`), чтение округлённого поля ушло из активного пути,
осталось в `Read` только как совместимость со старыми файлами
(`JsonConverters.cs:171-179, 188`). Доктрина правки записана в самом файле,
`JsonConverters.cs:110-125`.

---

### P2-46. JSON-конвертеры: имена каналов регистрозависимы

**Вердикт: НЕ ВОСПРОИЗВОДИТСЯ. Число нарушений — ноль.**

**Исполняемые примеры (текущий код).**

| вход | фактический вывод | ожидаемый по контракту | код на `3f36edb^` |
|---|---|---|---|
| `{"X":1,"Y":2,"Z":3}` как `Vector3` | `<1, 2, 3>` | `<1, 2, 3>` | `<0, 0, 0>` |
| `{"x":1,"Y":2,"z":3}` как `Vector3` | `<1, 2, 3>` | — | `<1, 0, 3>` |
| `{"X":1,"Y":2}` как `Vector2` | `<1, 2>` | — | `<0, 0>` |
| `{"X":1,"Y":2,"Z":3,"W":4}` как `Vector4` | `W = 4` | — | `W = 0` |
| `{"R":1,"G":0.5,"B":0,"A":1}` как `Rgba32` | `#FF8000FF` | — | `#000000FF` |
| `{"r":1,"g":0.5,"b":0,"a":1}` как `Rgba32` | `#FF8000FF` | — | `#FF8000FF` |
| `{"Radians":1}` угол | `1` рад | — | `0` рад |
| `{"RADIANS":1}` угол | `1` рад | — | `0` рад |
| `{"rAdIaNs":1}` угол | `1` рад | — | `0` рад |
| `{"DEGREES":90}` угол | `90°` | — | `0` рад |
| `{"\u0058":1,"\u0059":2,"\u005a":3}` как `Vector3` | `<1, 2, 3>` | — | `<0, 0, 0>` |

Проверено и утверждение реестра про `PropertyNameCaseInsensitive`: флаг **не**
участвует, потому что имена сверяет сам конвертер. На старом коде
`{"X":1,"Y":2,"Z":3}` с `PropertyNameCaseInsensitive = true` давал `(0, 0, 0)` —
то есть и с флагом дефект был виден. На текущем коде — `(1, 2, 3)` в обоих
случаях.

**Эталон .NET — важная поправка к формулировке пункта.**

Формулировка «тогда как встроенный путь .NET на том же входе читает верно» на
том входе, который приведён в пункте, **не подтверждается целиком**. Измерено:

| эталон (без конвертера библиотеки) | вход `{"X":1,"Y":2,"Z":3}` |
|---|---|
| POCO со свойствами `x`, `y`, `z`, параметры по умолчанию | **`(0, 0, 0)`** — то есть .NET тоже читает неверно |
| POCO со свойствами `x`, `y`, `z`, `PropertyNameCaseInsensitive = true` | `(1, 2, 3)` |
| POCO со свойствами `X`, `Y`, `Z`, параметры по умолчанию | `(1, 2, 3)` |
| `Dictionary<string,float>`, запрос ключа `"X"` | `1` (ключ есть, но `dict["x"]` был бы `KeyNotFound`) |
| `JsonSerializer.Serialize(new Vector3(1,2,3))` без конвертера | `{}` — для `System.Numerics.Vector3` встроенного конвертера нет, поле молча теряется |

То есть на типах с именами в нижнем регистре платформа по умолчанию ведёт себя
**так же, как считался дефектный конвертер**, и отличается только при
`PropertyNameCaseInsensitive = true` (документация: «The default value is false»).
Конвертер библиотеки на текущем коде регистронезависим **при любых настройках**,
то есть строже платформы по умолчанию. Это стоит сказать владельцу: контракт
конвертера не совпадает с контрактом рефлексионного пути .NET, и расхождение
намерено.

**Какая правка закрыла.** `3f36edb`:
`JsonArrayReaderHelper.cs:302-335` (`IndexOfChannel` — сравнение одного байта с
побитовой приведением `| 0x20`, `JsonArrayReaderHelper.cs:305-331`),
`JsonArrayReaderHelper.cs:344-392` (`IndexOfEscapedChannel` — запасной путь для
имён длиннее одного символа, включая экранированные),
`JsonArrayReaderHelper.cs:401-402` (`IsChannelName` — два литерала, нижний и
прописной), и `JsonConverters.cs:228-235` (`IsFieldName` — то же для полей угла
плюс `Ascii.EqualsIgnoreCase` на строке 234).

---

### P2-47. JSON-конвертеры: числа вне диапазона проходят молча

**Вердикт: НЕ ВОСПРОИЗВОДИТСЯ. Число нарушений — ноль.**

**Исполняемые примеры (текущий код).**

| вход | фактический вывод | код на `3f36edb^` |
|---|---|---|
| `[1e40,2,3]` как `Vector3` | `JsonException`, `Path=$`, `BytePositionInLine=5`, msg `Число «1e40» не помещается в float.` | `<Infinity, 2, 3>` |
| `[-1e40,2,3]` | `JsonException`, `Path=$`, `BytePositionInLine=6` | `<-Infinity, 2, 3>` |
| `[1e400,2,3]` | `JsonException`, `BytePositionInLine=6` | `<Infinity, 2, 3>` |
| `{"x":1e40,"y":2,"z":3}` | `JsonException`, `BytePositionInLine=9` | `<Infinity, 2, 3>` |
| `{"X":1e40,"Y":2,"Z":3}` | `JsonException`, `BytePositionInLine=9` | **`<0, 0, 0>`** (имя не опознано, значение потеряно) |
| `{"radians":1e400}` угол | `JsonException`, `BytePositionInLine=16`, msg `не помещается в double` | **`NaN`** |
| `{"degrees":1e400}` угол | `JsonException`, `BytePositionInLine=16` | `NaN` |
| `{"RADIANS":1e400}` угол | `JsonException`, `BytePositionInLine=16` | **`0`** (поле не опознано → тихий ноль) |
| `1e400` голое число | `JsonException`, `BytePositionInLine=5` | `NaN` |
| `[1e40,1,2,3]` как `Rgba32` | `JsonException`, `BytePositionInLine=5` | `#FFFFFFFF` (молча) |
| `{"r":1,"g":0.5,"b":0,"a":1e400}` | `JsonException`, `BytePositionInLine=30` | `#FF8000FF` (молча) |

**Границы диапазона, обязательные по требованиям коду.**

| вход | вывод | комментарий |
|---|---|---|
| `[3.4028235e38,2,3]` | `<3.4028235E+38, 2, 3>` | ровно `float.MaxValue` — проходит, как и должен |
| `[3.5e38,2,3]` | `JsonException`, `BytePositionInLine=7` | первое значение выше `float.MaxValue` |
| `[1e-50,2,3]` | `<0, 2, 3>` | потеря порядка, но значение как `float` **точно** ноль, обратная запись проходит — нарушения обратимости тут нет |
| `{"radians":1e40}` угол | `2.8660138718114396` рад | конечное `double`, нормализация отработала |

**Утверждение «прочитанное нельзя записать обратно».**

На текущем коде обмена не происходит вовсе: все три контрольных входа
отвергаются на чтении.

```
[1e40,2,3]              чтение отвергнуто: JsonException Path=$ BytePositionInLine=5
{"radians":1e400}       чтение отвергнуто: JsonException Path=$ BytePositionInLine=16
{"x":1e40,"y":2,"z":3}  чтение отвергнуто: JsonException Path=$ BytePositionInLine=9
```

На коде `3f36edb^` ровно те же входы дают разрыв обратимости:

```
[1e40,2,3]   прочитано <Infinity, 2, 3>  запись назад: System.ArgumentException
                «.NET number values such as positive and negative infinity cannot be
                 written as valid JSON ...»
{"radians":1e400}  прочитано NaN        запись назад: System.ArgumentException
{"x":1e40,"y":2,"z":3}  прочитано <Infinity, 2, 3>  запись назад: System.ArgumentException
```

**Эталон .NET — и здесь формулировка требует поправки в другую сторону.**

| измерение без конвертера библиотеки | фактический вывод |
|---|---|
| `JsonSerializer.Deserialize<float[]>("[1e40,2,3]")` | `Infinity, 2, 3` |
| `JsonSerializer.Deserialize<float>("1e40")` | `Infinity` |
| `JsonSerializer.Deserialize<double>("1e400")` | `Infinity` |
| `JsonSerializer.Deserialize<double[]>("[1e400,2,3]")` | `Infinity, 2, 3` |
| `Utf8JsonReader("1e40")`: `Read()` → `True`, `TokenType` → `Number`, `GetSingle()` | `Infinity` |
| `Utf8JsonReader("1e400")`: `Read()` → `True`, `TokenType` → `Number`, `GetSingle()`/`GetDouble()` | `Infinity` / `Infinity` |

То есть **платформа тоже молча отдаёт ±∞**, и делает это ровно на проверенных
входах. Причина видна в исходнике (внешний источник 2): `TryGetSingle` и
`TryGetDouble` не проверяют конечность. Значит проверка `float.IsFinite` в
библиотеке — не «строгость вкуса», а единственное, что удерживает обратимость:
значение `±∞` физически не записывается обратно, писатель его отвергает.

**Какая правка закрыла.** `3f36edb`: `JsonArrayReaderHelper.cs:270-279`
(`ReadFiniteSingle`, вызывается из `:103` для массивной формы и `:249` для
объектной), `JsonArrayReaderHelper.cs:195-199` (проверка конечности на пути «имя
свойства»), `JsonConverters.cs:248-257` (`ReadFiniteDouble`),
`JsonConverters.cs:203-208` (то же для `AngleJsonConverter.ReadAsPropertyName`).

---

### P2-48. `Rgba32JsonConverter`: наружу выходит `FormatException`, а не `JsonException`

**Вердикт: НЕ ВОСПРОИЗВОДИТСЯ. Число нарушений — ноль.**

**Исполняемые примеры (текущий код).**

| вход | фактический вывод | код на `3f36edb^` |
|---|---|---|
| `"GG0000FF"` | `JsonException`, `Path=$`, `BytePositionInLine=10`, `LineNumber=0`, `Inner=FormatException`, msg `Некорректный цвет «GG0000FF»: ожидается #RRGGBB или #RRGGBBAA.` | `System.FormatException`, пути и позиции нет |
| `""` | `JsonException`, `Path=$`, `BytePositionInLine=2` | `System.FormatException` |
| `"#FF00"` | `JsonException`, `Path=$`, `BytePositionInLine=7` | `System.FormatException` |
| `"#FF 00 00"` | `JsonException`, `Path=$`, `BytePositionInLine=11` | **`#FF000000`** — молча (это P2-50 из соседней подсистемы, закрыт коммитом `59f040d`) |
| `"#FF 00 00 FF"` | `JsonException`, `Path=$`, `BytePositionInLine=14` | `System.FormatException` |
| `"#FF8000FF"` | `#FF8000FF` | `#FF8000FF` |

**Проверка главного утверждения: ловится ли ошибка одним `catch (JsonException)`.**

| вход | `catch (JsonException)` на текущем коде | на `3f36edb^` |
|---|---|---|
| `"GG0000FF"` | поймано: `JsonException`, `Path=$`, `LineNumber=0` | «поймано, но не `JsonException`» — то есть `FormatException` ушёл мимо |
| `"#FF 00 00"` | поймано: `JsonException`, `Path=$`, `LineNumber=0` | исключения не было вовсе |

Путь `$` и позиция в байтах присутствуют — ровно то, чего требовал пункт.

**Эталон .NET: что сам `System.Text.Json` делает с исключением конвертера.**

Чтобы утверждение пункта было проверено, а не принято на веру, в зонде есть
собственный конвертер, бросающий по очереди четыре вида исключений. Результат на
.NET 10.0.112:

| что бросает конвертер в `Read` | что выходит из `JsonSerializer.Deserialize` |
|---|---|
| `FormatException` | `System.FormatException` — **не обёрнут** |
| `InvalidOperationException` | `System.InvalidOperationException` — **не обёрнут** |
| `ArgumentException` | `System.ArgumentException` — **не обёрнут** |
| `JsonException` | `JsonException`, `Path=$`, `BytePositionInLine=3`, `LineNumber=0` — обёрнут и обогащён |

Из этого следует и подтверждение решения, и одна неточность доктрины:
`System.Text.Json` **не оборачивает ничего**, кроме уже готового `JsonException`.
Значит оборачивание `FormatException` в `JsonException` руками в конвертере — не
избыточность, а единственный способ, и решение правильное. А вот формулировка в
`JsonConverters.cs:330-336` («`System.Text.Json` оборачивает в `JsonException`
только `InvalidOperationException`») на этом рантайме **неверен**: измерение выше
показывает, что `InvalidOperationException` выходит наружу как есть. Правку кода
это не требует — только текста. Вынесено в «Найдено попутно».

**Какая правка закрыла.** `3f36edb`: `JsonConverters.cs:338-348`
(`FromHexChecked`), вызывается из `JsonConverters.cs:279` (чтение значения) и
`JsonConverters.cs:317` (чтение из имени свойства — то есть оба пути).

---

### RED-2. `SerializationWave2Tests.cs:714` — фактический путь значения `NaN`

**Вердикт: тест красный на чистом дереве, и причину в задании надо уточнить.**

Фактический прогон на текущем коде, изолированными артефактами:

```
dotnet test tests/XEngine.Mathematics.Tests/XEngine.Mathematics.Tests.csproj -c Release \
             --artifacts-path revision/tmp/w1.5/serialization/art \
             --filter "FullyQualifiedName~ColorWithNonFiniteChannelIsRejectedOnWrite"

[XUnit.net] XEngine.Mathematics.Tests.SerializationWave2Tests.ColorWithNonFiniteChannelIsRejectedOnWrite [FAIL]
  Error Message:  Assert.Throws() Failure: No exception was thrown
  Expected:       typeof(System.Text.Json.JsonException)
  Stack Trace:    ...SerializationWave2Tests.cs:line 714

Failed! - Failed: 1, Passed: 0, Skipped: 0, Total: 1
```

Полный набор на текущем дереве (Release, Fast): **1668 тестов, 1666 прошло, 2
упало** — ровно та же базовая линия, что в `BRIEF.md` §2. Красные те же:
`ContractConsistencyTests.Remap_OnEmptySourceRange_ReturnsTargetMinimum` (чужой
пункт) и `SerializationWave2Tests.ColorWithNonFiniteChannelIsRejectedOnWrite`.
Ничего в `tests/` я не менял, тест не переписывал.

**Где именно `NaN` превращается в ноль — измерено по шагам.**

| шаг | измерение на текущем коде |
|---|---|
| `Interpolation.Clamp01(float.NaN)` | **`0`** |
| `Interpolation.Clamp01(+∞)` | `1` |
| `Interpolation.Clamp01(−∞)` | `0` |
| `new Rgba32(float.NaN, 0, 0, 1)` | `R=0  G=0  B=0  A=1` |
| `float.IsFinite(that.R)` | `True` |
| `that.ToString()` | `#000000FF` |
| `that.ToVector4().X` | `0` |
| `JsonSerializer.Serialize(new Rgba32(0,NaN,0,1), opts)` | `"#000000FF"`, исключения нет |

Место превращения названо точно: `src/XEngine.Mathematics/Interpolation/Interpolation.cs:526`,
функция

```csharp
public static float Clamp01(float t) => t >= 0f ? (t > 1f ? 1f : t) : 0f;
```

вызывается из конструктора `src/XEngine.Mathematics/Graphics/Rgba32.cs:29-32`.
На `NaN` оба сравнения ложны, и функция уходит в ветку `: 0f`.

**Доходит ли `NaN` до конвертера — нет.** Доказано количественно, а не рассуждением:

| источник нечислового канала | измерение |
|---|---|
| 100 000 цветов, каналы равномерно в `[-1e40; 1e40]` | цветов с нечисловым каналом **0** |
| десериализация `["NaN",0,0,1]` | `#000000FF`, `R=0` — нечисловых **0** |
| десериализация `["Infinity",0,0,1]` | `#000000FF`, `R=0` — нечисловых **0** |
| десериализация `{"r":1e400,...}` | `JsonException` (P2-47) |

То есть страховка `JsonConverters.cs:307-310`
(`if (!float.IsFinite(value.R) || ... ) throw new JsonException(...)`)
на текущем коде **недостижима**: значение типа `Rgba32` не может содержать
нечисловый канал, потому что конструктор приводит все четыре к `[0; 1]`.

**Расхождение с заданием и с доктриной теста.**

Задание и `REGISTRY.md` §4 утверждают, что `NaN` не доходит до конвертера
«потому что его глушит `Clamp01`» — то есть `Clamp01` **пропускал** `NaN` насквозь,
и после правки P2-49 страховка станет проверяемой. Это верно для кода **до**
коммита `d88ad1f`, что подтверждено обратным прогоном на старой библиотеке:

```
Interpolation.Clamp01(float.NaN) = NaN          <- код на 3f36edb^
new Rgba32(float.NaN,0,0,1): R=NaN ... IsFinite(R) = False
Interpolation.Clamp01(float.NaN) = 0            <- текущий код
new Rgba32(float.NaN,0,0,1): R=0   ... IsFinite(R) = True
```

`git log -S 't >= 0f ? (t > 1f ? 1f : t) : 0f'` даёт единственный коммит
`d88ad1f` («Clamp01: нечисловой параметр больше не проходит мимо ограничения»),
а его правка была именно такой:

```
-    public static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
+    public static float Clamp01(float t) => t >= 0f ? (t > 1f ? 1f : t) : 0f;
```

То есть правка P2-49 (или её волновая замена) **уже применена**, но привела она
не к тому результату, от которого ждал тест: `NaN` теперь приводится к нулю
**намеренно и задокументированно** — `Interpolation/Interpolation.cs:517-524`
прямо пишет: «Нечисловой параметр приводится к нулю, то есть к началу интервала»
и «`NaN` не доходит до вызывающего ни по одному пути, где различие было бы видно».
Поэтому правка соседней подсистемы закрыть RED-2 уже не может: она его не
открывала заново, а изменила условие, при котором тест красный.

Доктрина самого теста `SerializationWave2Tests.cs:696-705` описывает старое
поведение дословно: «`±∞` до конвертера не доходит, потому что `Rgba32`
отсекает каналы в `[0; 1]` в конструкторе, а `NaN` отсечку проходит — обе ложны
на сравнениях». Вторая половина абзаца сегодня неверна: обе comparISON ложны и
`NaN` отсечку **не** проходит.

---

## Мера проверена

Обратный ход сделан **в scratch**, не откатом файлов на месте: копия библиотеки
на `3f36edb^` извлечена в `revision/tmp/w1.5/serialization/oldlib/` и собрана как
отдельный проект. Исходник зонда — один и тот же файл `Program.cs`, скопированный
в оба каталога. Если бы мера не различала состояния, числа в двух колонках
совпали бы.

| пункт | мера | текущий код | код на `3f36edb^` | мера различает? |
|---|---|---|---|---|
| P2-45 | углов не вернулось в себя | **0 / 200 000** | 200 000 / 200 000 | да |
| P2-45 | переворотов на 2π в полосе 1e-6° у −180° | **0 / 20 000** | 10 000 / 20 000 | да |
| P2-45 | малых углов, прочитанных нулём | **0 / 20 000** | 20 000 / 20 000 | да |
| P2-46 | `{"X":1,"Y":2,"Z":3}` → `Vector3` | **(1, 2, 3)** | (0, 0, 0) | да |
| P2-46 | `{"RADIANS":1}` → угол | **1 рад** | 0 рад | да |
| P2-47 | `[1e40,2,3]` → `Vector3` | **JsonException** | `<Infinity, 2, 3>` | да |
| P2-47 | `{"radians":1e400}` → угол | **JsonException** | `NaN` | да |
| P2-47 | запись прочитанного обратно | **невозможна, чтения нет** | `ArgumentException` | да |
| P2-48 | `"GG0000FF"` → вид исключения | **JsonException + Path + позиция** | `FormatException` без пути | да |
| P2-48 | `catch (JsonException)` | **ловит** | не ловит | да |
| RED-2 | `Clamp01(NaN)` | **0** | `NaN` | да |

Дополнительно проверено, что мера не «меряет не то»:

- **Позиция токена подтверждена прямо.** Для `1e40`, `1e400`, `3.4028235e38`
  напечатано `Read()=True; TokenType=Number`. Значит `GetSingle()`/`GetDouble()`
  действительно стоят на числе, и `Infinity` в эталоне — не мусор разбора.
- **Эталон .NET для обратимости не наивный.** Для P2-45 эталоном служит штатный
  `double`-обмен без конвертера (0 из 200 000 несовпадений) — он показывает, что
  платформа сама по себе точна, и что 200 000 из 200 000 на старом коде вызваны
  схемой записи, а не измерением.
- **Границы диапазона проверены с обеих сторон.** `3.4028235e38` (= `float.MaxValue`)
  проходит, `3.5e38` отвергается — значит порог стоит там, где надо, а не «везде
  отвергает».
- **Обе конфигурации сборки.** `-c Release` (`Fast`) и `-c Release
  -p:MathBackend=Deterministic`. `diff` файлов измерений, за вычетом трёх строк
  заголовка, пуст: **Fast и Deterministic дали побайтово одинаковый вывод**
  (`out/current.txt` против `out/current_det.txt`). Это ожидаемо — в подсистеме
  нет трансцендентных функций, нормализация угла идёт через `Math.IEEERemainder`,
  который определён точно, — но проверка выполнена, а не предположена.

---

## Гипотезы, которые не подтвердились

Раздел обязателен и здесь не пуст: три из четырёх проверок дали результат,
противоположный записанному в документе или в задании.

1. **«Встроенный путь .NET на том же входе читает верно» (P2-46).** На типах со
   свойствами в нижнем регистре и при параметрах по умолчанию .NET читает
   `{"X":1,"Y":2,"Z":3}` как `(0, 0, 0)` — то есть **так же, как считался
   дефектный конвертер**. Подтверждается только половина утверждения: с
   `PropertyNameCaseInsensitive = true` — да, читает верно. Формулировку пункта
   стоит уточнить перед тем, как опираться на неё как на эталон.

2. **«RED-2 падает из-за P2-49 и позеленеет после правки `Clamp01`».** Не
   подтверждается: правка `d88ad1f` уже применена, а тест остался красным, потому
   что `Clamp01` теперь приводит `NaN` к нулю намеренно и задокументированно, и
   страховка на записи стала недостижимой, а не достижимой. Правка соседней
   подсистемы отсюда помощи не даёт.

3. **«`Write` пишет `radians` в полной точности, а `Read` их игнорирует» (P2-45).**
   На текущем коде `Read` их не игнорирует: `{"radians":1}` → 1 рад,
   `{"RADIANS":1}` → 1 рад, `{"rAdIaNs":1}` → 1 рад.

4. **Гипотеза «P2-47 — спор о вкусе: платформа же тоже отдаёт ±∞, значит
   конвертер вправе».** Опровергнута измерением: платформа отдаёт `±∞`
   действительно (`float[]` из `[1e40,2,3]` → `Infinity, 2, 3`), но значение с
   `±∞` **физически нельзя записать обратно** — писатель бросает
   `ArgumentException`. Отказ конвертера — не строгость вкуса, а единственное,
   что удерживает обратимость.

5. **Гипотеза «`1e-50` читается нулём, значит есть ещё одна потеря точности на
   границе диапазона».** Не подтверждается как нарушение обратимости: результат
   как `float` равен ровно нулю, обратная запись проходит, значение сохраняется.
   Порядок теряется, величина — нет. Если владелец считает потерю порядка
   дефектом, это отдельное решение контракта, а не продолжение P2-47.

6. **Гипотеза «RED-2 падает одинаково на старом и на текущем коде, значит дело
   в самом тесте».** Подтвердилась лишь частично: падает одинаково, но по разным
   причинам — на старом коде `NaN` доходил до конвертера, и страховки там ещё не
   было; на текущем — не доходит. Причина изменения поведения названа точно
   (`Interpolation.cs:526`, коммит `d88ad1f`).

---

## Что не удалось

1. **Конфигурация `Debug` не прогонялась.** Профиль проекта в `BRIEF.md` §2
   называет две обязательные конфигурации — `Release`/`Fast` и `Deterministic`;
   обе прогнаны и дали одинаковый результат. `Debug` в профиль не входит, и я его
   не гонял. Причина — не запрет, а отсутствие оснований: измерение поведения
   разбора JSON, не зависящего от оптимизации, на `Debug` ничего бы не изменило,
   но и не проверено.

2. **Поднормальные значения `float` не обследованы систематически.** Проверены
   `1e-50` и `0`; весь диапазон `float` от `1.18e-38` вниз по порядку — нет.
   Точки P2-47 начинаются на переполнении, и до них я дошёл; ниже — уже другая
   тема (точность, а не диапазон).

3. **Не измерена стоимость правок.** Волна 1.5 — сверка списка, а не оптимизация;
   числа по времени не собирались. Коммит `3f36edb` содержит свои замеры в
   сообщении, я их не перепроверял и в отчёт не переносил: цифра из документа
   переписывается только новым измерением, а нового измерения здесь не было.

4. **Не проверены границы `char`-буфера в `WriteChannelsPropertyName`.** Буфер
   `stackalloc char[128]` (`JsonArrayReaderHelper.cs:142`) при самом длинном
   возможном содержимом (`Vector4` из четырёх `float` в полной записи) занимает
   около 59 символов, то есть запас есть; **измерением это не подтверждено**, а
   прочитано глазами. Если владелец сочтёт нужным, это отдельная проверка.

5. **Не проверено поведение при потоковом чтении файла больше буфера читателя.**
   Коммит `fc27272` упоминает «страховку на потоковое чтение файла больше буфера
   читателя», но это не мой пункт списка, и я его не воспроизводил. Читать код
   соседних волн было можно, но объём задания не расширяется.

6. **Инструмент, который не применялся, назван прямо:** веб-поиск по исходникам
   `dotnet/runtime` я делал через MCP-сервер GitHub (`get_file_contents`,
   `search_code`), а не через локальную копию исходников .NET — локальной копии
   на машине нет. Поэтому все ссылки на исходники .NET в отчёте даны по
   содержимому, полученному из репозитория `dotnet/runtime` через API, и
   сопровождены измерением на .NET 10.0.112 — расхождение между ними по
   `GetDouble` зафиксировано явно (внешний источник 3).

7. **Тест RED-2 не переписывался** — как и предписано. Вердикт по нему дан
   измерением, см. следующий раздел.

---

## Требует вмешательства владельца

Один вопрос, дословно и готовый к решению. Он не про `Serialization/`: волна 2.3
по четырём пунктам P2 работы не имеет, а вот `RED-2` закрыть без решения
владельца нечем.

**Вопрос 1. Что делать с `SerializationWave2Tests.ColorWithNonFiniteChannelIsRejectedOnWrite` (`tests/XEngine.Mathematics.Tests/SerializationWave2Tests.cs:714`).**

- **Что утверждает тест.** `Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new Rgba32(float.NaN, 0f, 0f, 1f), options))`.
- **Что происходит на текущем коде.** `new Rgba32(float.NaN, 0f, 0f, 1f)` даёт
  цвет `#000000FF`, все четыре канала конечны
  (`Interpolation.Clamp01(float.NaN) == 0f`, `Interpolation/Interpolation.cs:526`),
  сериализация возвращает строку `"#000000FF"` без исключения, и тест падает с
  «No exception was thrown».
- **Почему задание без решения невыполнимо.** Тест требует, чтобы нечисловой
  канал **дошёл** до конвертера. Достичь этого на текущем коде нельзя, не сломав
  публичный контракт `Rgba32` («каналы нормализованы в диапазон 0..1»,
  `Graphics/Rgba32.cs:6`) и задокументированное поведение `Clamp01`
  (`Interpolation/Interpolation.cs:517-524`, где сказано буквально: «`NaN` не
  доходит до вызывающего ни по одному пути, где различие было бы видно»).
  Измеренная недостижимость страховки `JsonConverters.cs:307-310`: 0 цветов с
  нечисловым каналом из 100 000 случайных и 0 из трёх проб чтения из JSON.
- **Последствия для вызывающего кода, если выбрать вариант 1 или 2.** Страховка
  на записи станет либо мёртвой навсегда (вариант 1), либо перестанет быть
  наблюдаемой через `Rgba32` (вариант 2). В обоих случаях `Dictionary<Rgba32,…>`
  и любые обходные пути, создающие значение из не-`Rgba32`, останутся без
  защиты.
- **Возможные варианты и чем они платят.**
  1. **Признать тест устаревшим и удалить его** (или заменить на проверку
     достижимости страховки через другой тип). Цена: страховка на записи в
     `Rgba32JsonConverter.Write` остаётся непроверяемой. Выигрыш: доктрина
     соответствует коду, набор зелёный.
  2. **Оставить тест красным как осознанный маркер**, перенеся его в
     `Skip = …` с указанием причины в доктрине. Цена: в наборе всегда есть
     красный или пропущенный тест, что мешает сигналу о новых поломках.
  3. **Изменить контракт `Interpolation.Clamp01` так, чтобы `NaN` проходил
     насквозь** (то есть вернуть поведение до `d88ad1f`). Цена: правка
     затрагивает `Curves`, `QuaternionExtensions.Slerp`, `Segment2.ClosestPointTo`,
     `Rgba32` и RED-1 (`Remap_OnEmptySourceRange_ReturnsTargetMinimum`), то есть
     это работа агента `interpolation` волны 2.2 с полным обследованием всех
     вызывающих в обеих конфигурациях. Побочный эффект, который надо учесть
     заранее: `Rgba32.ToString()` снова начнёт печатать `#004080FF` на `NaN`,
     то есть вернётся наблюдаемая ошибка P2-49 на видимом лице.
  4. **Расширить страховку на момент создания значения**: запретить нечисловой
     канал в конструкторе `Rgba32` бросающим исключением. Цена: изменение
     публичного поведения `Rgba32` для всех вызывающих, включая
     `FromBytes`/`FromHex`/`Lerp`/`WithBrightness`, и новое исключение там, где
     контракт его не обещал. Это решение владельца кода, и оно шире моей зоны.
- **Что делать в моей подсистеме не требуется.** Ничего. Правок в
  `src/XEngine.Mathematics/Serialization/` волне 2.3 делать не нужно — все четыре
  пункта закрыты коммитом `3f36edb`, что подтверждено измерением.

Вопросов по сигнатурам, новому публичному API и формулировкам документации,
требующих решения, кроме перечисленного, нет.

---

## Найдено попутно, не в объёме

Новые дефекты волна 1.5 не ищет. Три вещи замечены и измерены по ходу; они не
являются предметом волны и передаются владельцу как запись.

1. **Ключ словаря с нечисловой компонентой записывается и не читается обратно.**
   Точное место: `src/XEngine.Mathematics/Serialization/JsonArrayReaderHelper.cs:140-158`
   (`WriteChannelsPropertyName` — конечность не проверяется) против
   `JsonArrayReaderHelper.cs:195-199` (`ReadChannelsPropertyName` — проверяется).
   Измерено на текущем коде:

   ```
   Vector3(1.5, -2.5, 0)   запись={"1.5,-2.5,0":1}  чтение=<1.5, -2.5, 0>      (ок)
   Vector3(+inf, 0, 0)     запись={"Infinity,0,0":1} чтение=JsonException Path=$.Infinity,0,0 BytePositionInLine=16
   Vector3(0, NaN, 0)      запись={"0,NaN,0":1}      чтение=JsonException Path=$.0,NaN,0   BytePositionInLine=11
   Angle(NaN)              запись={"NaN":1}          чтение=JsonException Path=$.NaN
   Angle(+inf)             запись={"Infinity":1}     чтение=JsonException Path=$.Infinity
   ```

   Это буквально тот же разрыв обратимости, который P2-47 закрыл для массивной и
   объектной форм, но на пути «имя свойства» он остался: запись проходит молча,
   чтение падает. То есть значение, прочитанное из файла сохранения, может быть
   записано в файл сохранения так, что этот файл потом не прочитается.
   На коде `3f36edb^` этот случай был неразличим — там любой
   `Dictionary<Vector3,int>` падал с `NotSupportedException`, потому что
   `WriteAsPropertyName` не был переопределён вовсе.

2. **Доктрина в `JsonConverters.cs:330-336` фактически неверна.** Текст утверждает,
   что `System.Text.Json` «оборачивает в `JsonException` только
   `InvalidOperationException`». Измерено на .NET 10.0.112: конвертер, бросающий
   `FormatException`, выходит наружу как `System.FormatException`;
   `InvalidOperationException` — как `System.InvalidOperationException`;
   `ArgumentException` — как `System.ArgumentException`. Не оборачивается ничего.
   Решение библиотеки (обернуть вручную) правильное; неверен только текст
   обоснования. Правки кода не требуется, правка доктрины — вопрос вкуса
   владельца.

3. **Доктрина `RED-2` устарела и описывает код до `d88ad1f`.** См. раздел выше;
   дублирую здесь, потому что это единственное место, где чужой тестовый файл
   содержит утверждение, прямо противоречащее текущему коду. Сам тест не тронут.

---

## Артефакты

```
revision/tmp/w1.5/serialization/probe/Program.cs      исходник зонда (один на оба прогона)
revision/tmp/w1.5/serialization/probe/Probe.csproj    -> src/XEngine.Mathematics
revision/tmp/w1.5/serialization/probeold/Program.cs   копия исходника
revision/tmp/w1.5/serialization/probeold/ProbeOld.csproj -> oldlib
revision/tmp/w1.5/serialization/oldlib/               копия библиотеки на 3f36edb^
revision/tmp/w1.5/serialization/out/current.txt       измерения, текущий код, Fast
revision/tmp/w1.5/serialization/out/current_det.txt  измерения, текущий код, Deterministic
revision/tmp/w1.5/serialization/out/old.txt           измерения, обратный ход
revision/tmp/w1.5/serialization/out/tests_fast.txt    полный прогон тестов, Release
revision/tmp/w1.5/serialization/art/                  артефакты сборки, текущий код
revision/tmp/w1.5/serialization/artdet/               артефакты сборки, Deterministic
revision/tmp/w1.5/serialization/artold/               артефакты сборки, обратный ход
```

Команды (дословно, все с изоляцией артефактов, все с `timeout`):

```bash
dotnet build  revision/tmp/w1.5/serialization/probe/Probe.csproj     -c Release --artifacts-path revision/tmp/w1.5/serialization/art
dotnet build  revision/tmp/w1.5/serialization/probe/Probe.csproj     -c Release -p:MathBackend=Deterministic --artifacts-path revision/tmp/w1.5/serialization/artdet
dotnet build  revision/tmp/w1.5/serialization/probeold/ProbeOld.csproj -c Release --artifacts-path revision/tmp/w1.5/serialization/artold
dotnet test   tests/XEngine.Mathematics.Tests/XEngine.Mathematics.Tests.csproj -c Release --artifacts-path revision/tmp/w1.5/serialization/art
```

`-p:BaseIntermediateOutputPath` не использовался (BRIEF §2: ломает исключения
SDK, даёт CS0579). Общих каталогов артефактов ни с кем не делил; процессы не
убивал (никаких команд сигнала не посылал); секреты не читал; `src/` и `tests/`
не изменялись. Изменённых мной отслеживаемых файлов — **ноль**, что видно по
`git status --porcelain`: единственная модифицированная запись `.gitignore`
существовала до начала работы.