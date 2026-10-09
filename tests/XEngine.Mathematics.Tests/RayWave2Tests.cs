using System.Numerics;
using XEngine.Mathematics;
using Xunit;
using BigInteger = System.Numerics.BigInteger;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные проверки волны 2 для поддиректории <c>Ray/</c>.
/// </summary>
/// <remarks>
/// <para>
/// Все проверки против точного эталона считаются рациональной арифметикой на
/// <see cref="BigInteger"/>, а не в <c>double</c>. Все величины происходят из
/// <c>float</c>, то есть являются двоичными рациональными, и умножение на
/// <c>2^150</c> даёт целое для любого из них, включая субнормальные. Вычитание
/// границ, деление на компоненту направления и сравнение отрезков выполняются
/// поэтому без единого округления, и эталон не может ошибиться так же, как
/// плавающая арифметика. В <c>double</c> сверять было бы слабее: при наклонах
/// порядка 1e-7 он ничего не различает.
/// </para>
/// <para>
/// Дискриминирующий вход обязателен: такой, где отмена правки видна. Проверка,
/// дающая одинаковый ответ на всех входах, не проверяет ничего.
/// </para>
/// </remarks>
public sealed class RayWave2Tests
{
    // ================= P1-1: порог параллельности в слэб-методе =================

    /// <summary>
    /// Граница, на которой 1/d действительно переполняется. Порог стоял на
    /// <see cref="Scalar.Epsilon"/>, то есть на 3.403e+32 выше этой границы.
    /// </summary>
    private const float InverseOverflowBoundary = 1f / float.MaxValue;

    /// <summary>
    /// P1-1, ложный промах: луч, отклонённый от оси X меньше чем на микрорадиан,
    /// проходит сквозь объект с запасом внутрь на всём его протяжении.
    /// </summary>
    /// <remarks>
    /// Дискриминирующий вход. Наклон <c>1e-7</c> точно попадает в полосу
    /// прежнего порога, то есть компонента Y объявлялась нулевой и луч
    /// объявлялся непересекающим объём, в котором он лежит. Запас внутрь
    /// 5.0e-5 м на единичной длине бокса, то есть ошибка не на краю.
    /// </remarks>
    [Fact]
    public void Ray2_NearlyAxisAlignedRayDoesNotMissObjectItPassesThrough()
    {
        Ray2 ray = new(new Vector2(0f, 0f), new Vector2(1f, 1e-7f));
        Aabb2 bounds = new(new Vector2(999.5f, 4.9975002e-05f), new Vector2(1000.5f, 10.00005f));

        // Точный эталон, а не «так кажется»: луч пересекает бокс.
        Assert.True(
            Exact(ray.Origin, ray.Direction, bounds.Min, bounds.Max),
            "Эталон обязан видеть пересечение: луч входит в объём по X раньше, чем выходит по Y.");

        Assert.True(
            ray.Intersects(bounds),
            "Луч с наклоном 1e-7 проходит сквозь объём с запасом 5.0e-5 м и обязан его пересекать.");

        // Запас внутрь проверяется явно, чтобы тест падал и на ослабленной правке.
        float reach = 999.5f * 1e-7f;
        Assert.True(
            reach > bounds.Min.Y,
            $"Луч на X = 999.5 приходит на Y = {reach:E3}, то есть выше нижней границы {bounds.Min.Y:E3}.");
    }

    /// <summary>
    /// P1-1, ложное срабатывание: луч, уходящий из полосы объекта раньше, чем до
    /// него доходит, обязан быть промахом, а не попаданием.
    /// </summary>
    /// <remarks>
    /// Дискринимирующий вход на второй знак ошибки. Прежний порог объявлял
    /// компоненту Y нулевой, проверял только, что начало луча по Y внутри
    /// границ, и объявлял попадание — то есть выбирал объект, которого луч не
    /// касается ни одной своей точкой.
    /// </remarks>
    [Fact]
    public void Ray2_NearlyAxisAlignedRayDoesNotHitObjectItLeavesBefore()
    {
        // Луч из начала координат по направлению (1, -1e-7). Объект расположен
        // ниже оси, и на X = 1000 луч уже на Y = -1e-4, то есть ниже его
        // верхней границы -1.0 и выше нижней -1.5 нет: луч прошёл мимо.
        Ray2 ray = new(new Vector2(0f, 0f), new Vector2(1f, -1e-7f));
        Aabb2 bounds = new(new Vector2(1000f, -1.5f), new Vector2(1100f, -1.0000001f));

        Assert.False(
            Exact(ray.Origin, ray.Direction, bounds.Min, bounds.Max),
            "Эталон обязан дать промах: луч уходит из полосы до прихода к объекту.");

        Assert.False(
            ray.Intersects(bounds),
            "Луч, ушедший из полосы объекта раньше, чем до него дошёл, не должен его выбирать.");
    }

    /// <summary>
    /// P1-1 в трёх измерениях: тот же дефект, то же исправление.
    /// </summary>
    [Fact]
    public void Ray3_NearlyAxisAlignedRayDoesNotMissObjectItPassesThrough()
    {
        Ray3 ray = new(new Vector3(0f, 0f, 0f), new Vector3(1f, 1e-7f, 1e-7f));
        Aabb3 bounds = new(
            new Vector3(999.5f, 4.9975002e-05f, 4.9975002e-05f),
            new Vector3(1000.5f, 10.00005f, 10.00005f));

        Assert.True(Exact3(ray.Origin, ray.Direction, bounds.Min, bounds.Max), "Эталон обязан видеть пересечение.");
        Assert.True(ray.Intersects(in bounds), "Ray3 с наклоном 1e-7 обязан пересекать объём, который проходит насквозь.");
    }

    /// <summary>
    /// P1-1, второй знак ошибки в трёх измерениях.
    /// </summary>
    [Fact]
    public void Ray3_NearlyAxisAlignedRayDoesNotHitObjectItLeavesBefore()
    {
        Ray3 ray = new(new Vector3(0f, 0f, 0f), new Vector3(1f, -1e-7f, -1e-7f));
        Aabb3 bounds = new(
            new Vector3(1000f, -1.5f, -1.5f),
            new Vector3(1100f, -1.0000001f, -1.0000001f));

        Assert.False(Exact3(ray.Origin, ray.Direction, bounds.Min, bounds.Max), "Эталон обязан дать промах.");
        Assert.False(ray.Intersects(in bounds), "Ray3 не должен выбирать объект, из полосы которого луч ушёл.");
    }

    /// <summary>
    /// P1-1, семейство: наклон от 1e-7 до 1e-6 и расстояние от 100 до 1800,
    /// то есть вся полоса прежнего порога на обычных дистанциях сцены.
    /// </summary>
    /// <remarks>
    /// Дискриминирующий параметрический вход вместо одного примера: правка,
    /// которая чинит одну точку, этот тест не проходит.
    /// </remarks>
    [Theory]
    [InlineData(1e-7f)]
    [InlineData(5e-7f)]
    [InlineData(1e-6f)]
    public void Ray2_SlabBandIsCoveredAtEveryDistanceAndTilt(float tilt)
    {
        int checkedPairs = 0;

        for (int step = 1; step <= 18; step++)
        {
            float distance = step * 100f;

            // Ложный промах: бокс растянут по Y вместе с наклоном, чтобы луч
            // входил в него по X, находясь на полнаклона выше нижней границы, и
            // выходил, не дойдя до верхней. Верхняя граница по Y обязана быть
            // выше пути луча на всём протяжении бокса, иначе пересечения нет.
            Vector2 origin = Vector2.Zero;
            Vector2 direction = new(1f, tilt);
            Aabb2 ahead = new(
                new Vector2(distance - 0.5f, 0.5f * tilt * distance),
                new Vector2(distance + 0.5f, 1.5f * tilt * distance));

            Assert.True(
                Exact(origin, direction, ahead.Min, ahead.Max),
                $"Эталон обязан видеть объём на расстоянии {distance} при наклоне {tilt:E1}.");
            Assert.True(
                new Ray2(origin, direction).Intersects(ahead),
                $"Ложный промах на расстоянии {distance} при наклоне {tilt:E1}: луч проходит объём с запасом {0.5f * distance * tilt:E3} м.");
            checkedPairs++;

            // Ложное срабатывание: бокс ниже оси, верхняя граница по Y ровно на
            // оси, а луч уходит вниз и оказывается ниже бокса раньше, чем
            // доходит до него по X. Прежний порог объявлял компоненту Y нулевой,
            // проверял только, что начало луча по Y внутри границ, и выбирал бокс.
            Vector2 down = new(1f, -tilt);
            float entry = distance - 0.5f;
            float exit = distance + 0.5f;
            Aabb2 below = new(
                new Vector2(entry, -0.5f * tilt * exit),
                new Vector2(exit, 0f));

            Assert.False(
                Exact(origin, down, below.Min, below.Max),
                $"Эталон обязан дать промах на расстоянии {distance} при наклоне {tilt:E1}.");
            Assert.False(
                new Ray2(origin, down).Intersects(below),
                $"Ложное срабатывание на расстоянии {distance} при наклоне {tilt:E1}: луч выбирает объект, которого не касается.");
            checkedPairs++;
        }

        Assert.Equal(36, checkedPairs);
    }

    /// <summary>
    /// П1-1, обратная защита: точный ноль не должен сломать осевой луч.
    /// </summary>
    /// <remarks>
    /// Проверка на то, что правка не превратилась в «никогда ничего не
    /// пересекается». Осевой луч идёт по нулю компоненты, и этот случай прежний
    /// порог обрабатывал правильно — он обязан остаться правильным.
    /// </remarks>
    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(-1f, 0f)]
    [InlineData(0f, -1f)]
    public void Ray2_ExactAxisRaysStillHitAndStillMiss(float dx, float dy)
    {
        Aabb2 bounds = new(new Vector2(0f, 0f), new Vector2(10f, 10f));
        Ray2 ray = new(new Vector2(-5f, 5f), new Vector2(dx, dy));

        bool expected = Exact(ray.Origin, ray.Direction, bounds.Min, bounds.Max);
        Assert.Equal(expected, ray.Intersects(bounds));
    }

    /// <summary>
    /// Ловушка <c>0 * бесконечность = NaN</c>, которую порог якобы защищал.
    /// </summary>
    /// <remarks>
    /// Дискриминирующий вход. Направление <c>(1e-40, 1)</c> проходит через
    /// конструктор без потерь, то есть <c>1/d</c> переполняется в бесконечность.
    /// Начало луча лежит ровно на нижней границе по X, то есть разность границ
    /// равна нулю. Форма с умножением на обратное получает <c>0 * бесконечность =
    /// NaN</c>, после чего защитное <c>min &lt;= max</c> становится ложным и луч
    /// объявляется непересекающим объём, в котором он лежит. Форма с делением
    /// напрямую получает <c>0 / 1e-40 = 0</c>, то есть ловушки не возникает.
    /// </remarks>
    [Fact]
    public void Ray2_SubnormalDirectionOnLowerFaceIsNotLostToInfinityTimesZero()
    {
        Assert.True(
            MathF.Abs(1e-40f) < InverseOverflowBoundary,
            "Граница переполнения 1/d обязана быть выше 1e-40, иначе вход не достижим.");

        Ray2 ray = new(new Vector2(0f, -1f), new Vector2(1e-40f, 1f));
        Assert.Equal(1e-40f, ray.Direction.X);
        Assert.True(float.IsFinite(ray.Direction.X), "Нормализация не должна ломать направление.");

        Aabb2 bounds = new(new Vector2(0f, -2f), new Vector2(1000f, 0f));

        Assert.True(Exact(ray.Origin, ray.Direction, bounds.Min, bounds.Max), "Эталон обязан видеть пересечение.");
        Assert.True(ray.Intersects(bounds), "Луч лежит на нижней грани по X, и 0 * бесконечность не должно его терять.");
    }

    /// <summary>
    /// То же в трёх измерениях.
    /// </summary>
    [Fact]
    public void Ray3_SubnormalDirectionOnLowerFaceIsNotLostToInfinityTimesZero()
    {
        Ray3 ray = new(new Vector3(0f, -1f, -1f), new Vector3(1e-40f, 0f, 1f));
        Assert.Equal(1e-40f, ray.Direction.X);

        Aabb3 bounds = new(new Vector3(0f, -2f, -1f), new Vector3(1000f, 0f, 1f));

        Assert.True(Exact3(ray.Origin, ray.Direction, bounds.Min, bounds.Max), "Эталон обязан видеть пересечение.");
        Assert.True(ray.Intersects(in bounds), "Ray3 не должен терять луч из-за 0 * бесконечность.");
    }

    /// <summary>
    /// Обратная защита от ловушки: тот же субнормальный наклон, но луч начинается
    /// вне границы по этой оси и обязан быть промахом.
    /// </summary>
    /// <remarks>
    /// Без этой проверки тест выше проходил бы и на коде, который всегда
    /// отвечает <c>true</c>.
    /// </remarks>
    [Fact]
    public void Ray2_SubnormalDirectionStartingOutsideIsStillAMiss()
    {
        Ray2 ray = new(new Vector2(0f, -1f), new Vector2(1e-40f, 1f));
        Aabb2 bounds = new(new Vector2(1f, -2f), new Vector2(1000f, 0f));

        Assert.False(Exact(ray.Origin, ray.Direction, bounds.Min, bounds.Max), "Эталон обязан дать промах.");
        Assert.False(ray.Intersects(bounds), "Субнормальный наклон не должен превращать промах в попадание.");
    }

    /// <summary>
    /// Сверка слэб-метода с точным эталоном на большом потоке случайных пар.
    /// </summary>
    /// <remarks>
    /// Это проверка регрессии: правка меняет вердикт и обязана совпадать с
    /// эталоном везде, а не только на подобранных случаях. Геометрия
    /// разумная: луч целится в случайную точку бокса, поэтому доля попаданий
    /// близка к половине, и каждая ось проверяется на обоих знаках.
    /// </remarks>
    [Fact]
    public void Ray2_SlabMatchesExactRationalReference()
    {
        DeterministicRandom random = new(0x5A9B3C7D1E2F4051UL);
        int hits = 0;
        int compared = 0;
        int nearAxis = 0;
        int mismatches = 0;
        string firstMismatch = string.Empty;

        for (int i = 0; i < 60_000; i++)
        {
            float minX = random.Range(-200f, 200f);
            float minY = random.Range(-200f, 200f);
            float width = random.Range(0.01f, 8f);
            float height = random.Range(0.01f, 8f);
            Vector2 min = new(minX, minY);
            Vector2 max = new(minX + width, minY + height);

            Vector2 target = new(
                min.X + (random.NextFloat() * width),
                min.Y + (random.NextFloat() * height));

            float angle = random.Range(0f, 360f);
            float radians = (float)Scalar.ToRadians(angle);
            float distance = random.Range(1f, 400f);
            Vector2 origin = target - new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * distance;

            bool almostAxis = random.NextFloat() < 0.16f;
            if (almostAxis)
            {
                // Почти осевой луч: сдвиг начала на величину порядка наклона.
                float tilt = (random.NextFloat() < 0.5f ? 1f : -1f) * random.Range(1e-7f, 9.5e-7f);
                origin += random.NextFloat() < 0.5f ? new Vector2(tilt * distance, 0f) : new Vector2(0f, tilt * distance);
                nearAxis++;
            }

            Ray2 ray = new(origin, target - origin);
            bool expected = Exact(ray.Origin, ray.Direction, min, max);
            bool actual = ray.Intersects(new Aabb2(min, max));

            // Внутри цикла только счётчик: xUnit не прерывает цикл на первом
            // провале, и Assert на каждой из 60 000 итераций при систематическом
            // расхождении накопил бы 60 000 записей о падении — это выглядит
            // как зависание раннера, а не как падение теста.
            if (expected != actual)
            {
                mismatches++;
                if (mismatches == 1)
                {
                    firstMismatch = $"О={ray.Origin} D={ray.Direction} Min={min} Max={max}: эталон {expected}, получено {actual}";
                }
            }

            if (expected)
            {
                hits++;
            }

            compared++;
        }

        Assert.Equal(0, mismatches);
        Assert.True(nearAxis > 0, "Поток обязан содержать почти осевые лучи, иначе проверка ничего не проверяет.");
        Assert.True(hits > compared / 10, $"Попаданий всего {hits} из {compared}: распределение вырождено.");
        Assert.True(firstMismatch.Length == 0, firstMismatch);
    }

    /// <summary>
    /// Та же сверка для <see cref="Ray3"/>, геометрия вложена в <c>z = 0</c>.
    /// </summary>
    [Fact]
    public void Ray3_SlabMatchesExactRationalReference()
    {
        DeterministicRandom random = new(0x6B4D2E8F0A1C3579UL);
        int hits = 0;
        int compared = 0;
        int mismatches = 0;
        string firstMismatch = string.Empty;

        for (int i = 0; i < 40_000; i++)
        {
            float minX = random.Range(-200f, 200f);
            float minY = random.Range(-200f, 200f);
            float width = random.Range(0.01f, 8f);
            float height = random.Range(0.01f, 8f);
            Vector3 min = new(minX, minY, -1f);
            Vector3 max = new(minX + width, minY + height, 1f);

            Vector2 target2 = new(
                min.X + (random.NextFloat() * width),
                min.Y + (random.NextFloat() * height));

            float radians = (float)Scalar.ToRadians(random.Range(0f, 360f));
            float distance = random.Range(1f, 400f);
            Vector2 origin2 = target2 - new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * distance;

            if (random.NextFloat() < 0.16f)
            {
                float tilt = (random.NextFloat() < 0.5f ? 1f : -1f) * random.Range(1e-7f, 9.5e-7f);
                origin2 += random.NextFloat() < 0.5f ? new Vector2(tilt * distance, 0f) : new Vector2(0f, tilt * distance);
            }

            Vector3 origin = new(origin2.X, origin2.Y, 0f);
            Ray3 ray = new(origin, new Vector3(target2.X - origin2.X, target2.Y - origin2.Y, 0f));
            bool expected = Exact3(ray.Origin, ray.Direction, min, max);
            var aabb = new Aabb3(min, max);
            bool actual = ray.Intersects(in aabb);

            // Счётчик, а не Assert на каждой итерации: см. пояснение в
            // Ray2_SlabMatchesExactRationalReference.
            if (expected != actual)
            {
                mismatches++;
                if (mismatches == 1)
                {
                    firstMismatch = $"O={ray.Origin} D={ray.Direction} Min={min} Max={max}: эталон {expected}, получено {actual}";
                }
            }

            if (expected)
            {
                hits++;
            }

            compared++;
        }

        Assert.Equal(0, mismatches);
        Assert.True(hits > compared / 10, $"Попаданий всего {hits} из {compared}: распределение вырождено.");
        Assert.True(firstMismatch.Length == 0, firstMismatch);
    }

    // ================= P2-64: NaN в Max/Min открывал защиту =================

    /// <summary>
    /// P2-64: два луча с одним названием обязаны отвечать на один вопрос одинаково.
    /// </summary>
    /// <remarks>
    /// Дискриминирующий вход: 64 сочетания нечисловых компонент из
    /// <c>{±0, ±1, ±бесконечность, NaN}</c>. Прежде <c>Ray2</c> отвечал
    /// <c>true</c> на всех, то есть объявлял попадание во всё, а <c>Ray3</c> на
    /// той же геометрии — <c>false</c>.
    /// </remarks>
    [Fact]
    public void Ray2_AndRay3_AgreeOnEveryNonFiniteDirection()
    {
        float[] values = [0f, -0f, 1f, -1f, float.PositiveInfinity, float.NegativeInfinity, float.NaN, 1e-7f];
        var bounds2 = new Aabb2(new Vector2(-1f, -1f), new Vector2(1f, 1f));
        var bounds3 = new Aabb3(new Vector3(-1f, -1f, -1f), new Vector3(1f, 1f, 1f));

        int mismatches = 0;
        int total = 0;

        foreach (float dx in values)
        {
            foreach (float dy in values)
            {
                total++;
                bool hit2 = new Ray2(Vector2.Zero, new Vector2(dx, dy)).Intersects(bounds2);
                bool hit3 = new Ray3(Vector3.Zero, new Vector3(dx, dy, 0f)).Intersects(in bounds3);

                Assert.True(
                    hit2 == hit3,
                    $"Направление ({dx}, {dy}) два луча с одним названием ответили по-разному: Ray2 {hit2}, Ray3 {hit3}.");
                if (hit2 != hit3)
                {
                    mismatches++;
                }
            }
        }

        Assert.Equal(0, mismatches);
        Assert.Equal(64, total);
    }

    /// <summary>
    /// P2-64: нечисловое направление не должно объявлять луч пересекающим всё.
    /// </summary>
    /// <remarks>
    /// Дискриминирующий вход: нечисловое направление плюс бокс, мимо которого
    /// луч с любым конечным направлением проходит заведомо. Прежде защитное
    /// сравнение не срабатывало на <c>NaN</c> и метод отвечал <c>true</c>.
    /// </remarks>
    [Fact]
    public void Ray2_NonFiniteDirectionIsNotTreatedAsHittingEverything()
    {
        var bounds = new Aabb2(new Vector2(10f, 10f), new Vector2(20f, 20f));
        var ray = new Ray2(new Vector2(0f, 0f), new Vector2(float.NaN, 1f));

        Assert.True(float.IsFinite(ray.Direction.X), "Направление луча обязано быть конечным.");
        Assert.False(
            ray.Intersects(bounds),
            "Луч, направленный вдоль Y из начала координат, не должен выбирать бокс вдали от себя.");
    }

    /// <summary>
    /// P2-64, та же проверка для остальных фигур двумерного луча.
    /// </summary>
    [Fact]
    public void Ray2_NonFiniteDirectionDoesNotHitDistantShapes()
    {
        var ray = new Ray2(new Vector2(0f, 0f), new Vector2(float.PositiveInfinity, 1f));

        Assert.True(float.IsFinite(ray.Direction.X));
        Assert.False(ray.Intersects(new Circle2(new Vector2(50f, 50f), 1f)));
        Assert.False(ray.Intersects(new Aabb2(new Vector2(50f, 50f), new Vector2(60f, 60f))));
        Assert.False(ray.Intersects(new Segment2(new Vector2(50f, 50f), new Vector2(60f, 60f))));
    }

    /// <summary>
    /// P2-64 в трёх измерериях: нечисловое направление не выбирает дальние фигуры.
    /// </summary>
    [Fact]
    public void Ray3_NonFiniteDirectionDoesNotHitDistantShapes()
    {
        var ray = new Ray3(Vector3.Zero, new Vector3(float.NaN, 1f, 0f));

        Assert.True(float.IsFinite(ray.Direction.X));

        // Нечисловое направление отбрасывается в UnitX, поэтому луч идёт вдоль X
        // из начала координат: все фигуры стоят в стороне от этой прямой.
        Assert.Equal(Vector3.UnitX, ray.Direction);

        var farBox = new Aabb3(new Vector3(50f, 50f, 50f), new Vector3(60f, 60f, 60f));
        Assert.False(ray.Intersects(in farBox));
        Assert.False(ray.Intersects(new BoundingSphere(new Vector3(50f, 50f, 50f), 1f)));
        Assert.False(ray.Intersects(new Capsule3(new Vector3(50f, 50f, 50f), new Vector3(60f, 50f, 50f), 1f)));

        // Плоскость позади начала луча: направление нормали к ней совпадает с
        // направлением луча, то есть пересечения нет и по направлению, и по
        // знаку расстояния.
        var behind = Plane3.FromPointNormal(new Vector3(-50f, 0f, 0f), Vector3.UnitX);
        Assert.False(ray.Intersects(in behind));
    }

    // ================= P2-65: обещание нормализации =================

    /// <summary>
    /// P2-65: доктрина «направление нормализуется автоматически» обязана
    /// выполняться, а направление обязано быть конечным.
    /// </summary>
    /// <remarks>
    /// Дискринимирующий вход: векторы величиной меньше 1e-22. <c>SafeNormalize</c>
    /// делит на длину, которая для них равна нулю из-за переполнения в обратную
    /// сторону, и возвращает <c>(бесконечность, NaN)</c>. Прежняя проверка на
    /// нуль это не ловила.
    /// </remarks>
    [Theory]
    [InlineData(1e-23f, 0f)]
    [InlineData(1e-30f, 1e-30f)]
    [InlineData(1.4e-45f, 1.4e-45f)]
    [InlineData(-1e-25f, 1e-28f)]
    public void Ray2_DirectionIsAlwaysFiniteAndUnit(float dx, float dy)
    {
        Ray2 ray = new(Vector2.Zero, new Vector2(dx, dy));

        Assert.True(float.IsFinite(ray.Direction.X), $"Направление X нечисловое: {ray.Direction}");
        Assert.True(float.IsFinite(ray.Direction.Y), $"Направление Y нечисловое: {ray.Direction}");
        Assert.Equal(1f, ray.Direction.Length(), 1e-5f);
    }

    /// <summary>
    /// P2-65 в трёх измерениях.
    /// </summary>
    [Theory]
    [InlineData(1e-23f, 0f, 0f)]
    [InlineData(1e-30f, 1e-30f, 1e-30f)]
    [InlineData(1.4e-45f, 1.4e-45f, 0f)]
    public void Ray3_DirectionIsAlwaysFiniteAndUnit(float dx, float dy, float dz)
    {
        Ray3 ray = new(Vector3.Zero, new Vector3(dx, dy, dz));

        Assert.True(float.IsFinite(ray.Direction.X), $"Направление X нечисловое: {ray.Direction}");
        Assert.True(float.IsFinite(ray.Direction.Y), $"Направление Y нечисловое: {ray.Direction}");
        Assert.True(float.IsFinite(ray.Direction.Z), $"Направление Z нечисловое: {ray.Direction}");
        Assert.Equal(1f, ray.Direction.Length(), 1e-5f);
    }

    /// <summary>
    /// P2-65, обратная защита: нулевое направление по-прежнему даёт луч вдоль X,
    /// как и обещает доктрина конструктора.
    /// </summary>
    [Fact]
    public void Ray2_ZeroDirectionStillGivesRayAlongX()
    {
        Assert.Equal(Vector2.UnitX, new Ray2(Vector2.Zero, Vector2.Zero).Direction);
        Assert.Equal(Vector3.UnitX, new Ray3(Vector3.Zero, Vector3.Zero).Direction);
    }

    /// <summary>
    /// P2-65: обычные направления нормализуются и не ломаются проверкой.
    /// </summary>
    [Theory]
    [InlineData(3f, 4f)]
    [InlineData(1f, 0f)]
    [InlineData(-2f, 7f)]
    [InlineData(1e10f, 1e-10f)]
    public void Ray2_OrdinaryDirectionsStayNormalized(float dx, float dy)
    {
        Ray2 ray = new(Vector2.Zero, new Vector2(dx, dy));
        float expected = MathF.Sqrt(dx * dx + dy * dy);

        Assert.Equal(1f, ray.Direction.Length(), 1e-5f);
        Assert.True(
            ray.Direction.X * dx + ray.Direction.Y * dy > 0f,
            $"Направление {ray.Direction} развернулось относительно ({dx}, {dy}).");
        _ = expected;
    }

    // ================= P3: имя операции и out-параметр =================

    /// <summary>
    /// P3-2: <c>out distance</c> на промахе обязан быть нулём у всех четырёх
    /// фигур, как у трёх <c>Raycast</c>.
    /// </summary>
    /// <remarks>
    /// Дискриминирующий вход: плоскость позади начала луча даёт знаковое
    /// расстояние <c>-10</c> при <c>hit = false</c>. Вызывающий в цикле по
    /// объектам читает расстояние до проверки признака — это естественный
    /// порядок — и получает правдоподобное отрицательное расстояние вместо
    /// «считать нечего». У трёх <c>Raycast</c> на промахе уже ноль, и плоскость
    /// обязана вести себя так же.
    /// </remarks>
    [Fact]
    public void Ray3_MissReportsZeroDistanceForEveryShape()
    {
        Ray3 ray = new(new Vector3(-10f, 0f, 0f), Vector3.UnitX);

        // Плоскость позади начала луча: луч направлен от неё, попадания нет.
        var plane = Plane3.FromPointNormal(new Vector3(-20f, 0f, 0f), Vector3.UnitX);
        Assert.False(ray.Intersects(in plane, out float planeDistance), "Луч, направленный от плоскости, её не касается.");
        Assert.Equal(0f, planeDistance);

        // Те же три фигуры: расстояние на промахе ноль.
        Assert.False(ray.Raycast(new Aabb3(new Vector3(50f, 50f, 50f), new Vector3(60f, 60f, 60f)), out float boxDistance));
        Assert.Equal(0f, boxDistance);

        Assert.False(ray.Raycast(new BoundingSphere(new Vector3(50f, 50f, 50f), 1f), out float sphereDistance));
        Assert.Equal(0f, sphereDistance);

        Assert.False(ray.Raycast(new Capsule3(new Vector3(50f, 50f, 50f), new Vector3(60f, 50f, 50f), 1f), out float capsuleDistance));
        Assert.Equal(0f, capsuleDistance);
    }

    /// <summary>
    /// P3-2, обратная защита: на попадании расстояние остаётся настоящим.
    /// </summary>
    [Fact]
    public void Ray3_HitStillReportsRealDistance()
    {
        var plane = Plane3.FromPointNormal(new Vector3(10f, 0f, 0f), Vector3.UnitX);
        Ray3 ray = new(new Vector3(-5f, 0f, 0f), Vector3.UnitX);

        Assert.True(ray.Intersects(in plane, out float distance));
        MathAssert.Equal(15f, distance, 1e-4f);
    }

    // ================= P3-4: лишний корень в критерии параллельности =================

    /// <summary>
    /// P3-4: замена корня на сравнение квадратов не меняет ни одного вердикта.
    /// </summary>
    /// <remarks>
    /// Обратный ход этой оптимизации обязан остаться зелёным: она не правит
    /// дефект, а только убирает корень из горячего пути. Поэтому здесь нет
    /// «ожидаемого значения» в смысле исправления: сверка идёт с независимой
    /// реализацией решения прямых в <c>double</c> на потоке отрезков от 1 мм до
    /// 1 км и на расстояниях от 1 мм до 100 км, то есть ровно там, где прежний
    /// критерий зависел от длины отрезка.
    /// </remarks>
    [Fact]
    public void Ray2_ParallelCriterionIsIndependentOfSegmentLength()
    {
        DeterministicRandom random = new(0x77C1D3E5F90A2B48UL);
        int compared = 0;
        int hits = 0;
        int mismatches = 0;
        string firstMismatch = string.Empty;

        for (int i = 0; i < 40_000; i++)
        {
            float distance = random.Range(0.001f, 100_000f);
            float lateral = random.Range(-10f, 10f);
            float length = random.Range(1e-3f, 1000f);
            float radians = (float)Scalar.ToRadians(random.Range(0f, 360f));

            var ray = new Ray2(Vector2.Zero, Vector2.UnitX);
            var segment = new Segment2(
                new Vector2(distance, lateral),
                new Vector2(distance, lateral) + new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * length);

            bool expected = ReferenceSegment(ray, segment);
            bool actual = ray.Intersects(segment);

            // Счётчик вместо Assert на каждой итерации: см. пояснение в
            // Ray2_SlabMatchesExactRationalReference.
            if (expected != actual)
            {
                mismatches++;
                if (mismatches == 1)
                {
                    firstMismatch = $"отрезок на расстоянии {distance} со смещением {lateral}, длиной {length}: эталон {expected}, получено {actual}";
                }
            }

            if (actual)
            {
                hits++;
            }

            compared++;
        }

        Assert.Equal(0, mismatches);
        Assert.True(hits > 0, "Сравнение обязано содержать попадания, иначе оно тривиально.");
        Assert.True(hits < compared, "Сравнение обязано содержать промахи, иначе оно тривиально.");
        Assert.True(firstMismatch.Length == 0, firstMismatch);
    }

    /// <summary>
    /// Независимая сверка отрезка: решение двух прямых
    /// <c>origin + t·d = a + s·delta</c> в <c>double</c> плюс отдельная проверка
    /// коллинеарного случая.
    /// </summary>
    /// <remarks>
    /// Знак векторного произведения в параметре <c>s</c> здесь легко перепутать:
    /// <c>s = cross(a - origin, d) / cross(d, delta)</c>, то есть числитель
    /// считается именно в таком порядке. Автор первой версии этой сверки взял
    /// <c>cross(d, a - origin)</c>, то есть с обратным знаком, и на 179 углах из
    /// 360 получил ложные расхождения там, где библиотека права. Мера была
    /// испорчена, а не код.
    /// </remarks>
    private static bool ReferenceSegment(Ray2 ray, Segment2 segment)
    {
        Vector2 direction = ray.Direction;
        Vector2 delta = segment.Delta;
        Vector2 offset = segment.A - ray.Origin;

        double denominator = (double)direction.X * delta.Y - (double)direction.Y * delta.X;
        if (Math.Abs(denominator) <= 1e-12)
        {
            // Прямые параллельны: пересечение есть только при совпадении прямых
            // и расположении отрезка впереди начала луча.
            double cross = (double)offset.X * direction.Y - (double)offset.Y * direction.X;
            if (Math.Abs(cross) > 1e-9)
            {
                return false;
            }

            double near = (double)offset.X * direction.X + (double)offset.Y * direction.Y;
            double far = near + ((double)direction.X * delta.X + (double)direction.Y * delta.Y);
            return near >= 0f || far >= 0f;
        }

        double rayT = ((double)offset.X * delta.Y - (double)offset.Y * delta.X) / denominator;
        double segmentT = ((double)offset.X * direction.Y - (double)offset.Y * direction.X) / denominator;
        return rayT >= 0f && segmentT >= 0f && segmentT <= 1f;
    }

    // ================= точный эталон на рациональной арифметике =================

    /// <summary>
    /// Превращает <c>float</c> в целое, равное <c>2^150 * значение</c>.
    /// </summary>
    /// <remarks>
    /// Любой <c>float</c> — двоичная рациональная величина <c>m * 2^e</c> с
    /// <c>e &gt;= -149</c>, поэтому <c>2^150 * v</c> всегда целое. Умножение на
    /// степень двойки выполняется сдвигом влево, а не вправо: сдвиг вправо был бы
    /// усечением и тихо испортил бы эталон.
    /// </remarks>
    private static BigInteger ToScaledInteger(float value)
    {
        int raw = BitConverter.SingleToInt32Bits(value);
        bool negative = raw < 0;
        int exponent = (raw >> 23) & 0xFF;
        int mantissa = raw & 0x7FFFFF;
        BigInteger significand = exponent == 0 ? mantissa : mantissa | 0x800000;

        // Нормальное число: значение = significand * 2^(exponent - 150).
        // Субнормальное: значение = mantissa * 2^-149.
        int shift = exponent == 0 ? 1 : exponent;
        BigInteger result = significand << shift;
        return negative ? -result : result;
    }

    /// <summary>
    /// Рациональное значение параметра луча. Знаменатель задаёт особые случаи:
    /// ноль со знаком «минус» — минус бесконечность, ноль со знаком «плюс» —
    /// плюс бесконечность.
    /// </summary>
    private readonly struct Rational
    {
        public Rational(BigInteger numerator, BigInteger denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        public BigInteger Numerator { get; }

        public BigInteger Denominator { get; }

        public int CompareTo(in Rational other)
        {
            bool thisInfinite = Denominator.IsZero;
            bool otherInfinite = other.Denominator.IsZero;
            if (thisInfinite && otherInfinite)
            {
                return Numerator.Sign.CompareTo(other.Numerator.Sign);
            }

            if (thisInfinite)
            {
                return Numerator.Sign < 0 ? -1 : 1;
            }

            if (otherInfinite)
            {
                return other.Numerator.Sign < 0 ? 1 : -1;
            }

            return (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
        }
    }

    /// <summary>
    /// Точное пересечение двумерного луча с прямоугольником: отрезок параметров
    /// каждой оси пересекается с полуосью <c>[0, +inf)</c> без округления.
    /// </summary>
    private static bool Exact(Vector2 origin, Vector2 direction, Vector2 min, Vector2 max)
    {
        Rational tMin = new(BigInteger.Zero, BigInteger.One);
        Rational tMax = new(BigInteger.Zero, BigInteger.Zero);
        return Clip(origin.X, direction.X, min.X, max.X, ref tMin, ref tMax)
               && Clip(origin.Y, direction.Y, min.Y, max.Y, ref tMin, ref tMax);
    }

    /// <summary>
    /// То же в трёх измерениях.
    /// </summary>
    private static bool Exact3(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max)
    {
        Rational tMin = new(BigInteger.Zero, BigInteger.One);
        Rational tMax = new(BigInteger.Zero, BigInteger.Zero);
        return Clip(origin.X, direction.X, min.X, max.X, ref tMin, ref tMax)
               && Clip(origin.Y, direction.Y, min.Y, max.Y, ref tMin, ref tMax)
               && Clip(origin.Z, direction.Z, min.Z, max.Z, ref tMin, ref tMax);
    }

    /// <summary>
    /// Ограничение отрезка параметров по одной оси.
    /// </summary>
    /// <remarks>
    /// Накопление <c>tMin</c> по всем осям обязательно: проверять только
    /// «верхняя граница неотрицательна» нельзя, потому что вход по одной оси
    /// может оказаться за выходом по другой. На этом обходе эталон однажды
    /// разошёлся с библиотекой на 18 случаях из 36.
    /// </remarks>
    private static bool Clip(float origin, float step, float lower, float upper, ref Rational tMin, ref Rational tMax)
    {
        BigInteger originScaled = ToScaledInteger(origin);
        BigInteger stepScaled = ToScaledInteger(step);
        BigInteger lowerScaled = ToScaledInteger(lower);
        BigInteger upperScaled = ToScaledInteger(upper);

        if (stepScaled.IsZero)
        {
            // Параллельный случай: ось не ограничивает параметр, попадание
            // возможно только если начало лежит между границами.
            return originScaled >= lowerScaled && originScaled <= upperScaled;
        }

        BigInteger nearNumerator = lowerScaled - originScaled;
        BigInteger farNumerator = upperScaled - originScaled;
        if (stepScaled.Sign < 0)
        {
            nearNumerator = -nearNumerator;
            farNumerator = -farNumerator;
            stepScaled = -stepScaled;
        }

        Rational near = new(nearNumerator, stepScaled);
        Rational far = new(farNumerator, stepScaled);
        Rational low = near.CompareTo(far) <= 0 ? near : far;
        Rational high = near.CompareTo(far) <= 0 ? far : near;

        if (tMin.CompareTo(low) < 0)
        {
            tMin = low;
        }

        if (tMax.CompareTo(high) > 0)
        {
            tMax = high;
        }

        return tMin.CompareTo(tMax) <= 0;
    }
}