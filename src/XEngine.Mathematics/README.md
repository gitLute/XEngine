# XEngine.Mathematics

Математическая библиотека движка XEngine. Основа — типы из `System.Numerics`
(часть BCL, без внешних зависимостей), поверх них добавлены игровые примитивы,
которых нет в BCL.

## Зачем она нужна

- `XEngine.Core` и `XEngine.Mathematics` не должны зависеть ни от Silk.NET, ни от Box2D,
  ни от OpenTK. Математика — единственный тип, который используют и ядро, и бэкенды.
- Ошибки текущего `MathUtils` (`Homogenize` клал `X` в `Z`, `Rotate` вращал
  в сторону, противоположную `FromPolar`) больше не могут повториться: у каждой
  операции одна реализация и один контракт, зафиксированный тестом.
- Типы из `System.Numerics` уже покрывают векторы и матрицы, поэтому дублировать
  их вручную нет смысла; здесь только то, чего в BCL нет.

## Состав

| Файл | Содержание |
|---|---|
| `Scalar.cs` | `Epsilon`, `Sign`, `IsNearlyZero`, `IsNearlyEqual`, `Snap`, `Clamp`, перевод градусов и радиан |
| `Angle.cs` | угол с нормализацией в `[-π; π]`, интерполяция по кратчайшей дуге, поворот вектора, `MoveTowards` |
| `VectorExtensions.cs` | операции поверх `Vector2`: `FromPolar`, `Rotate`, `Perpendicular`, `ToAngle`, `SafeNormalize`, `Project`, `MoveTowards` |
| `Matrix3x2Extensions.cs` | `CreateTransform` (позиция, угол, масштаб, опорная точка), разбор матрицы, `TryInvert` |
| `MatrixExtensions.cs` | упаковка `Matrix4x4` в column-major для OpenGL, ортографическая проекция 2D |
| `Rect.cs` | прямоугольник с операциями над множеством: пересечение, объединение, отступы, масштаб, поворот |
| `Aabb.cs` | осевой ограничивающий прямоугольник, `FromPoints`, `ClosestPoint`, `DistanceTo`, углы |
| `Circle.cs`, `Segment.cs`, `Capsule.cs` | примитивы форм с проверками принадлежности и ближайшими точками |
| `Ray.cs` | луч с пересечениями по AABB, кругу и отрезку |
| `Collision.cs` | SAT для повёрнутых прямоугольников, расстояние между отрезками, проверка принадлежности |
| `Interpolation.cs` | `Lerp`, `InverseLerp`, `MoveTowards`, `Damp`, `SmoothDamp`, `Repeat`, `PingPong`, `Remap` |
| `Curves.cs` | кривые ускорения (quad, cubic, sine, expo, back, elastic, bounce), квадратичная кривая Безье |
| `Rgba32.cs` | цвет RGBA, разбор `#RRGGBBAA`, преобразование в вектор uniform |
| `Random/IRandomSource.cs` | интерфейс источника случайности |
| `Random/XorShift64Star.cs` | детерминированный быстрый генератор |
| `Random/RandomExtensions.cs` | `NextRange`, `NextSymmetric`, `NextDirection`, `NextInsideUnitCircle`, `NextItem`, `NextWeightedIndex`, `NextAngle` |
| `Serialization/` | JSON-конвертеры для `Vector2/3/4`, `Angle`, `Rgba32` |

## Правила

1. Типов из BCL не дублируем: добавляем только то, чего в них нет.
2. Никаких зависимостей: проект ссылается только на `net10.0`.
3. Каждая операция имеет один контракт и покрыта тестом.
4. Углы в публичном API — тип `Angle`, а не «число в радианах».
5. Сглаживание через `Interpolation.Damp`, а не `Lerp(value, target, dt * k)`:
   только `Damp` не зависит от частоты кадров.
6. Порядок обхода и соглашение о знаках фиксированы: угол отсчитывается
   против часовой стрелки, ось Y направлена вверх, `Angle.Rotate` вращает
   против часовой стрелки.

## Сборка и тесты

```bash
dotnet build src/XEngine.Mathematics/XEngine.Mathematics.csproj
dotnet test tests/XEngine.Mathematics.Tests
```

Тесты обязательны для: `Angle` (нормализация, кратчайшая дуга, эквивалентность
0 и 2π), матриц (композиция, обращение, порядок преобразования точки),
`Rect`/`Aabb` (пересечение, объединение, пустые значения), `Collision`
(SAT для повёрнутых прямоугольников), `Interpolation.Damp` (сходимость при
разных `dt`), `Rgba32` (hex-разбор), детерминированность `XorShift64Star`.
