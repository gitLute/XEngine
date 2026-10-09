using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессии волны 2 для поддиректории <c>Vector/</c>.
///
/// Общая причина почти всех проверок здесь одна: длина считалась как
/// <c>sqrt(сумма квадратов)</c>, а квадрат во <c>float</c> обнуляется ниже
/// 3.743392e-23 и переполняется выше 1.8446744e19. Из-за этого ненулевой вектор
/// терял направление с обеих сторон диапазона, и семь методов поддиректории
/// отвечали неверно на одном и том же входе.
///
/// Эталоны в <see cref="Reference"/> считаются в <c>double</c> с
/// предварительным масштабированием: double покрывает весь диапазон float
/// (1e-45 … 3.4e38) с запасом в 260 десятичных разрядов, поэтому эталон не
/// разделяет ни с кодом, ни с ним общую ошибку. Формулы написаны независимо от
/// проверяемого кода: сравнение идёт с эталонным значением, а не с повтором
/// формулы реализации.
/// </summary>
public sealed class VectorWave2Tests
{
    // ==================================================================
    // P1-7. SafeNormalize возвращает (Infinity, NaN) на ненулевых векторах.

    /// <summary>
    /// П1-7: правило README «ненулевой вектор нормализуется при любой длине»
    /// обязано выполняться с обеих сторон диапазона float, а не только в
    /// середине.
    /// </summary>
    /// <param name="magnitude">Длина исходного вектора.</param>
    [Theory]
    [InlineData(1e-45f)]
    [InlineData(1e-40f)]
    [InlineData(1e-30f)]
    [InlineData(1e-23f)]
    [InlineData(1e-22f)]
    [InlineData(1e-20f)]
    [InlineData(1f)]
    [InlineData(1e20f)]
    [InlineData(1e30f)]
    [InlineData(1e38f)]
    [InlineData(3.4e38f)]
    public void SafeNormalize_NormalizesNonZeroVectorAtAnyLength(float magnitude)
    {
        Vector3 input = new(magnitude * 0.6f, magnitude * 0.8f, 0f);

        Vector3 result = input.SafeNormalize();

        AssertFinite(result);
        VectorWave2Reference.AssertUnitLength(result, $"SafeNormalize({magnitude:E2})");

        // Ожидание берётся из тех же входов, а не из идеальных 0.6 и 0.8:
        // само умножение magnitude * 0.6f округляется, и на длинах порядка
        // 1e-40 результат отличается от 0.6 на 7.5e-6. Такая «ошибка»
        // принадлежит тесту, а не коду, и проверять её значило бы чинить
        // несуществующий дефект.
        Vector3 expected = VectorWave2Reference.Unit(input.X, input.Y, input.Z);
        MathAssert.Equal(expected, result, 1e-6f);
    }

    /// <summary>
    /// П1-7 то же для двух измерений: <c>Vector2.SafeNormalize</c> делит на ту
    /// же длину и страдал тем же.
    /// </summary>
    [Theory]
    [InlineData(1e-45f)]
    [InlineData(1e-30f)]
    [InlineData(1e-23f)]
    [InlineData(1e-22f)]
    [InlineData(1f)]
    [InlineData(1e20f)]
    [InlineData(1e38f)]
    public void SafeNormalize2_NormalizesNonZeroVectorAtAnyLength(float magnitude)
    {
        Vector2 input = new(magnitude * 0.6f, magnitude * 0.8f);

        Vector2 result = input.SafeNormalize();

        AssertFinite(result.X, result.Y);
        double length = Math.Sqrt(((double)result.X * result.X) + ((double)result.Y * result.Y));
        Assert.True(
            Math.Abs(length - 1.0) <= 1e-6,
            $"SafeNormalize({magnitude:E2}) дал длину {length:F9} вместо единицы: {result}.");

        Vector3 unit = VectorWave2Reference.Unit(input.X, input.Y, 0f);
        Vector2 expected = new Vector2(unit.X, unit.Y);
        MathAssert.Equal(expected, result, 1e-6f);
    }

    /// <summary>
    /// П1-7: результат обязан совпадать с точным значением, округлённым к
    /// float. Проверка идёт по ULP, а не по допуску в процентах: на игровом
    /// масштабе расхождение последнего разряда неотличимо от нуля, и такой
    /// допуск проглатывал бы настоящую ошибку на вырожденных длинах.
    /// </summary>
    [Fact]
    public void SafeNormalize_MatchesExactReferenceAtEveryMagnitude()
    {
        float[] magnitudes =
        [
            1e-45f, 1e-40f, 1e-35f, 1e-30f, 1e-25f, 1e-23f, 1e-22f, 1e-18f, 1e-10f,
            1f, 1e10f, 1e18f, 1e19f, 1e20f, 1e25f, 1e30f, 1e35f, 3.4e38f,
        ];

        double worst = 0;
        double worstAt = 0;
        int failing = 0;

        // Счётчик вместо утверждения в цикле — общее правило файла: xUnit не
        // прерывает цикл на первом провале и печатает сообщение на каждый.
        foreach (float magnitude in magnitudes)
        {
            // Направление не из осей: у оси один ненулевой компонент, и
            // ошибка в нём видна лучше всего.
            Vector3 input = new(magnitude * 0.2672612f, magnitude * -0.5345225f, magnitude * 0.8017837f);

            Vector3 actual = input.SafeNormalize();
            Vector3 expected = VectorWave2Reference.Unit(input.X, input.Y, input.Z);

            double deviation = VectorWave2Reference.UlpDistance(actual, expected);
            if (deviation > 2)
            {
                failing++;
            }

            if (deviation > worst)
            {
                worst = deviation;
                worstAt = magnitude;
            }
        }

        Assert.True(
            failing == 0,
            $"SafeNormalize разошёлся с точным эталоном более чем на 2 ULP в {failing} случаях из {magnitudes.Length}; худшее {worst:F0} ULP на длине {worstAt:E2}.");
    }

    /// <summary>
    /// П1-7: масштаб не должен влиять на результат. Одна и та же геометрия,
    /// посчитанная в разных единицах мира, обязана дать один и тот же ответ —
    /// это правило README «результат не зависит от масштаба мира».
    /// </summary>
    [Fact]
    public void SafeNormalize_DoesNotDependOnWorldScale()
    {
        Vector3 unit = VectorWave2Reference.Unit(0.2672612f, -0.5345225f, 0.8017837f);

        foreach (float scale in new[] { 1e-30f, 1e-20f, 1e-6f, 1f, 1e6f, 1e19f, 1e25f })
        {
            Vector3 input = unit * scale;

            Vector3 result = input.SafeNormalize();

            double deviation = VectorWave2Reference.UlpDistance(result, unit);
            Assert.True(
                deviation <= 64,
                $"Масштаб {scale:E2} дал {result} вместо {unit}: расхождение {deviation:F0} ULP.");
        }
    }

    /// <summary>
    /// П1-7: нулевой вектор обязан остаться нулём. Это единственная причина,
    /// по которой <c>SafeNormalize</c> вообще существует, и правка не должна
    /// была его сломать: масштабирование на ноль даёт <c>0/0 = NaN</c>.
    /// </summary>
    [Fact]
    public void SafeNormalize_ExactZeroStillGivesZero()
    {
        MathAssert.Equal(Vector3.Zero, Vector3.Zero.SafeNormalize());
        MathAssert.Equal(Vector2.Zero, Vector2.Zero.SafeNormalize());

        // Знаковый ноль — тот же ноль.
        MathAssert.Equal(Vector3.Zero, new Vector3(-0f, -0f, -0f).SafeNormalize());
        MathAssert.Equal(Vector2.Zero, new Vector2(-0f, -0f).SafeNormalize());
    }

    /// <summary>
    /// П1-7: нечисловой вход обязан давать нечисловой результат, а не
    /// «нулевой вектор» и не единицу. Иначе вызывающий не заметит, что
    /// координаты пришли из битого файла.
    /// </summary>
    [Theory]
    [InlineData(float.NaN, 0f, 0f)]
    [InlineData(0f, float.NaN, 0f)]
    [InlineData(0f, 0f, float.NaN)]
    [InlineData(float.PositiveInfinity, 0f, 0f)]
    public void SafeNormalize_NonFiniteStaysNonFinite(float x, float y, float z)
    {
        Vector3 result = new Vector3(x, y, z).SafeNormalize();

        Assert.True(
            !float.IsFinite(result.X) || !float.IsFinite(result.Y) || !float.IsFinite(result.Z),
            $"Нечисловой вход ({x}, {y}, {z}) дал конечный результат {result}.");
    }

    /// <summary>
    /// П1-7: граница обязана быть там, где арифметика float перестаёт работать,
    /// то есть около <c>sqrt(float.Epsilon)</c> = 3.743392e-23 и
    /// <c>sqrt(float.MaxValue)</c> = 1.8446744e19. Ниже и выше этой границы
    /// прежний код отвечал <c>(Infinity, NaN)</c> и <c>(0, 0, 0)</c>.
    /// </summary>
    [Fact]
    public void SafeNormalize_WorksJustInsideAndJustOutsideFloatSquareRange()
    {
        float[] lengths =
        [
            3.7e-23f, 1e-22f, 1e-21f,
            1.8e19f, 2e19f, 1e20f,
        ];

        foreach (float length in lengths)
        {
            Vector3 actual = new Vector3(length, 0f, 0f).SafeNormalize();
            AssertFinite(actual);
            MathAssert.Equal(Vector3.UnitX, actual, 1e-6f);
        }
    }

    // ==================================================================
    // P2-58. MoveTowards возвращает шаг длиннее maxStep.

    /// <summary>
    /// П2-58: шаг не должен превышать запрошенный ни при каком расстоянии.
    /// Дискриминирующая область — расстояния меньше <c>Scalar.Epsilon</c> = 1e-6:
    /// именно там оговорка <c>lengthSquared &lt;= Epsilon²</c> отменяла
    /// ограничение шага.
    /// </summary>
    [Fact]
    public void MoveTowards_NeverExceedsRequestedStep()
    {
        VectorWave2Rng random = new(0x51D0C7A5E1F30001UL);
        int violations = 0;
        int examined = 0;
        double worstRatio = 0;
        float worstDistance = 0;
        float worstStep = 0;

        // Внутри цикла только счётчики: xUnit не прерывает цикл на первом
        // провале, а на прежнем коде провалов 249 790 из 500 000, и каждый
        // печатал бы отформатированное сообщение. Это и есть тот зависший
        // раннер. Одно утверждение — после цикла.
        for (int i = 0; i < 500000; i++)
        {
            float distance = (float)Math.Pow(10, random.Range(-8, -4));
            float step = distance / (float)Math.Pow(10, random.Range(0, 3));
            float angle = random.Range(0f, 6.2831853f);
            Vector2 target = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

            Vector2 result = Vector2.Zero.MoveTowards(target, step);

            examined++;
            double length = VectorWave2Reference.Length(result);
            if (length > step * 1.0001)
            {
                violations++;
                if (length / step > worstRatio)
                {
                    worstRatio = length / step;
                    worstDistance = distance;
                    worstStep = step;
                }
            }
        }

        Assert.True(
            violations == 0,
            $"MoveTowards превысил шаг в {violations} случаях из {examined}; худший в {worstRatio:F1} раз на расстоянии {worstDistance:E3} шагом {worstStep:E3}.");
    }

    /// <summary>
    /// П2-58 то же в трёх измерениях: оговорка была в обоих файлах.
    /// </summary>
    [Fact]
    public void MoveTowards3_NeverExceedsRequestedStep()
    {
        VectorWave2Rng random = new(0x51D0C7A5E1F30002UL);
        int violations = 0;
        int examined = 0;
        double worstRatio = 0;

        // Счётчик вместо утверждения в цикле: см. предыдущий тест.
        for (int i = 0; i < 500000; i++)
        {
            float distance = (float)Math.Pow(10, random.Range(-8, -4));
            float step = distance / (float)Math.Pow(10, random.Range(0, 3));
            float azimuth = random.Range(0f, 6.2831853f);
            float pitch = random.Range(-1.5f, 1.5f);
            Vector3 direction = new Vector3(
                MathF.Cos(pitch) * MathF.Cos(azimuth),
                MathF.Sin(pitch),
                MathF.Cos(pitch) * MathF.Sin(azimuth));
            Vector3 target = direction * distance;

            Vector3 result = Vector3.Zero.MoveTowards(target, step);

            examined++;
            double length = VectorWave2Reference.Length(result);
            if (length > step * 1.0001)
            {
                violations++;
                worstRatio = Math.Max(worstRatio, length / step);
            }
        }

        Assert.True(
            violations == 0,
            $"MoveTowards в трёх измерениях превысил шаг в {violations} случаях из {examined}; худшее отношение {worstRatio:F1}.");
    }

    /// <summary>
    /// П2-58: конкретная граница, на которой видно отмену ограничения. Шаг ровно
    /// в десять раз короче расстояния, а расстояние меньше <c>Scalar.Epsilon</c>.
    /// Дискриминирующий вход: тот же шаг на расстоянии 1.1e-6 ограничение
    /// соблюдает, то есть проверка не проходит по нечувствительности.
    /// </summary>
    [Fact]
    public void MoveTowards_BoundaryIsExactlyScalarEpsilon()
    {
        Vector2 inside = Vector2.Zero.MoveTowards(new Vector2(1e-6f, 0f), 1e-7f);
        Vector2 outside = Vector2.Zero.MoveTowards(new Vector2(1.1e-6f, 0f), 1.1e-7f);

        Assert.True(
            VectorWave2Reference.Length(inside) <= 1e-7 * 1.0001,
            $"На расстоянии 1e-6 шаг вышел {VectorWave2Reference.Length(inside):E3} вместо {1e-7:E3}.");
        Assert.True(
            VectorWave2Reference.Length(outside) <= 1.1e-7 * 1.0001,
            $"На расстоянии 1.1e-6 шаг вышел {VectorWave2Reference.Length(outside):E3}.");
    }

    /// <summary>
    /// П2-58: результат не должен зависеть от того, в каких единицах выбран мир.
    /// Одна и та же относительная геометрия обязана давать один и тот же шаг.
    /// </summary>
    [Fact]
    public void MoveTowards_DoesNotDependOnWorldScale()
    {
        foreach (float scale in new[] { 1f, 1e-3f, 1e-5f, 1e-7f, 1e-9f, 1e-11f })
        {
            Vector2 from = new(2f * scale, 0f);
            Vector2 to = new(10f * scale, 0f);
            float step = 2f * scale;

            Vector2 result = from.MoveTowards(to, step);

            double ratio = VectorWave2Reference.Length(result) / (double)step;
            Assert.True(
                Math.Abs(ratio - 1.0) < 1e-4,
                $"Масштаб {scale:E2}: шаг вышел в {ratio:F4} от запрошенного, то есть зависит от единиц мира.");
        }
    }

    // ==================================================================
    // P2-59. Vector3.ToAngle теряет азимут при малой горизонтали.

    /// <summary>
    /// П2-59: азимут не должен зависеть от того, возведён ли в квадрат
    /// горизонтальный компонент. Двумерный сосед на тех же данных отвечает
    /// верно, поэтому расхождение двух методов на одной идее — сам дефект.
    /// </summary>
    [Theory]
    [InlineData(1e-23f)]
    [InlineData(1e-30f)]
    [InlineData(1e-40f)]
    [InlineData(1.4e-45f)]
    public void ToAngle_KeepsAzimuthOfAlmostVerticalDirection(float horizontal)
    {
        Vector3 direction = new(horizontal, 1f, horizontal);
        Vector2 flattened = new(horizontal, horizontal);

        Angle actual = direction.ToAngle();
        Angle reference = flattened.ToAngle();

        MathAssert.Equal(reference, actual, 1e-3);
    }

    /// <summary>
    /// П2-59: у вертикального направления азимута нет, и оба метода обязаны
    /// отвечать на это одинаково — нулём. Правка не должна была сломать этот
    /// случай, введя проверку «любая ненулевая компонента» вместо «обе ненулевые».
    /// </summary>
    [Theory]
    [InlineData(0f, 5f)]
    [InlineData(0f, -5f)]
    [InlineData(0f, 0f)]
    public void ToAngle_VerticalDirectionGivesZero(float x, float y)
    {
        Assert.Equal(Angle.Zero, new Vector3(x, y, 0f).ToAngle());
    }

    // ==================================================================
    // P2-60. SignedAngleAround отвергает огромную ненулевую ось.

    /// <summary>
    /// П2-60: ось длиной больше 1.8446744e19 не перестаёт быть ненулевой от
    /// того, что её квадрат переполнился. Исключение «ось нулевая» на таком
    /// входе ложно по существу, а не по формулировке.
    /// </summary>
    [Theory]
    [InlineData(1e20f)]
    [InlineData(1e30f)]
    [InlineData(3.4e38f)]
    public void SignedAngleAround_AcceptsHugeNonZeroAxis(float axisLength)
    {
        Angle angle = Vector3Extensions.SignedAngleAround(
            Vector3.UnitX,
            Vector3.UnitY,
            new Vector3(0f, 0f, axisLength));

        Assert.True(
            Math.Abs(angle.Degrees - 90.0) < 0.01,
            $"Ось длиной {axisLength:E2} дала {angle.Degrees:F6}° вместо 90°.");
    }

    /// <summary>
    /// П2-60 то же для двух измерений не существует, но ось должна работать и
    /// при малой длине: ось короче <c>Scalar.Epsilon</c> тоже не вырождена.
    /// </summary>
    [Theory]
    [InlineData(1e-30f)]
    [InlineData(1e-22f)]
    [InlineData(1e-20f)]
    public void SignedAngleAround_AcceptsTinyNonZeroAxis(float axisLength)
    {
        Angle angle = Vector3Extensions.SignedAngleAround(
            Vector3.UnitX,
            Vector3.UnitY,
            new Vector3(0f, 0f, axisLength));

        Assert.True(
            Math.Abs(angle.Degrees - 90.0) < 0.01,
            $"Ось длиной {axisLength:E2} дала {angle.Degrees:F6}° вместо 90°.");
    }

    /// <summary>
    /// П2-60: ось по-прежнему обязана отвергаться, если она нулевая в самом
    /// деле. Правка различает «нулевую» и «огромную» по признаку переполнения,
    /// и этот признак не должен был начать отвергать настоящий ноль.
    /// </summary>
    [Fact]
    public void SignedAngleAround_StillRejectsExactZeroAxis()
    {
        Assert.Throws<ArgumentException>(
            () => Vector3Extensions.SignedAngleAround(Vector3.UnitX, Vector3.UnitY, Vector3.Zero));
        Assert.Throws<ArgumentException>(
            () => Vector3Extensions.SignedAngleAround(Vector3.UnitX, Vector3.UnitY, new Vector3(0f, -0f, 0f)));
    }

    // ==================================================================
    // Следствия P1-7: остальные семь симптомов одной причины.

    /// <summary>
    /// Следствие P1-7: <c>ProjectOntoPlane</c> нормализует нормаль внутри, и
    /// нормаль короче <c>Scalar.Epsilon</c> задаёт плоскость столь же хорошо,
    /// как единичная. Прежний код выпускал <c>NaN</c> в результат.
    /// </summary>
    [Theory]
    [InlineData(1e-30f)]
    [InlineData(1e-22f)]
    [InlineData(1e20f)]
    public void ProjectOntoPlane_AcceptsAnyNonZeroNormal(float normalLength)
    {
        Vector3 result = new Vector3(1f, 1f, 1f).ProjectOntoPlane(new Vector3(normalLength, 0f, 0f));

        AssertFinite(result);
        MathAssert.Equal(new Vector3(0f, 1f, 1f), result, 1e-6f);
    }

    /// <summary>
    /// Следствие P1-7: <c>RejectFromPlane</c> — обратная операция, и её
    /// нормаль обязана вести себя так же.
    /// </summary>
    [Theory]
    [InlineData(1e-30f)]
    [InlineData(1e-22f)]
    [InlineData(1e20f)]
    public void RejectFromPlane_AcceptsAnyNonZeroNormal(float normalLength)
    {
        Vector3 result = new Vector3(1f, 1f, 1f).RejectFromPlane(new Vector3(normalLength, 0f, 0f));

        AssertFinite(result);
        MathAssert.Equal(new Vector3(1f, 0f, 0f), result, 1e-6f);
    }

    /// <summary>
    /// Следствие P1-7: <c>SignedDistanceToLine</c> нормализует нормаль внутри.
    /// Прежний код на нормали длиной 1e20 отвергал её как нулевую.
    /// </summary>
    [Theory]
    [InlineData(1e-20f)]
    [InlineData(1e20f)]
    public void SignedDistanceToLine_AcceptsAnyNonZeroNormal(float normalLength)
    {
        float distance = new Vector2(3f, 4f).SignedDistanceToLine(Vector2.Zero, new Vector2(normalLength, 0f));

        Assert.True(
            Math.Abs(distance - 3f) < 1e-4,
            $"Нормаль длиной {normalLength:E2} дала расстояние {distance:F6} вместо 3.");
    }

    /// <summary>
    /// Следствие П1-7: <c>ClampLength</c> сравнивает квадрат длины с квадратом
    /// предела, и на крошечных векторах обе величины обнуляются, то есть
    /// ограничение перестаёт действовать вопреки подписи метода.
    /// </summary>
    [Theory]
    [InlineData(1e-23f, 5e-24f)]
    [InlineData(1e-30f, 1e-31f)]
    [InlineData(1e20f, 1f)]
    public void ClampLength_LimitsVectorOfAnyMagnitude(float magnitude, float limit)
    {
        Vector2 result = new Vector2(magnitude, 0f).ClampLength(limit);

        double length = VectorWave2Reference.Length(result);
        Assert.True(
            length <= limit * 1.0001,
            $"ClampLength({magnitude:E2}, {limit:E2}) дал длину {length:E3}, то есть превысил предел.");
        MathAssert.Equal(limit, (float)length, (float)(limit * 1e-3));
    }

    /// <summary>
    /// Следствие П1-7 то же в трёх измерениях.
    /// </summary>
    [Theory]
    [InlineData(1e-23f, 5e-24f)]
    [InlineData(1e-30f, 1e-31f)]
    [InlineData(1e20f, 1f)]
    public void ClampLength3_LimitsVectorOfAnyMagnitude(float magnitude, float limit)
    {
        Vector3 result = new Vector3(magnitude, 0f, 0f).ClampLength(limit);

        double length = VectorWave2Reference.Length(result);
        Assert.True(
            length <= limit * 1.0001,
            $"ClampLength({magnitude:E2}, {limit:E2}) дал длину {length:E3}, то есть превысил предел.");
        MathAssert.Equal(limit, (float)length, (float)(limit * 1e-3));
    }

    /// <summary>
    /// Следствие П1-7: <c>Perpendicular</c> нормализует вектор внутри, и на
    /// крошечном ненулевом векторе прежний код выпускал <c>NaN</c>.
    /// </summary>
    [Theory]
    [InlineData(1e-30f)]
    [InlineData(1e-22f)]
    [InlineData(1e20f)]
    public void Perpendicular_AcceptsVectorOfAnyMagnitude(float magnitude)
    {
        Vector3 vector = new(magnitude * 3f, magnitude * 4f, 0f);

        Vector3 result = vector.Perpendicular(new Vector3(1f, 1f, 0f));

        AssertFinite(result);
        double dot = ((double)VectorWave2Reference.Unit(vector.X, vector.Y, vector.Z).X * result.X)
            + ((double)VectorWave2Reference.Unit(vector.X, vector.Y, vector.Z).Y * result.Y);
        Assert.True(
            Math.Abs(dot) < 1e-5,
            $"Perpendicular на векторе длиной {magnitude:E2} дал |dot| = {Math.Abs(dot):E3}.");
        MathAssert.Equal(1f, (float)VectorWave2Reference.Length(result), 1e-5f);
    }

    // ==================================================================
    // П3-1. Project и ProjectOntoDirection отвергают ненулевое направление
    //       короче Scalar.Epsilon.

    /// <summary>
    /// П3-1: направление проекции — безразмерная величина, и сравнивать его с
    /// <c>Scalar.Epsilon</c> значит отвергать настоящие направления. Исключение
    /// по подписи метода означает «направление нулевое».
    /// </summary>
    [Theory]
    [InlineData(1e-7f)]
    [InlineData(1e-9f)]
    [InlineData(1e-20f)]
    [InlineData(1e-30f)]
    public void Project_AcceptsAnyNonZeroDirection(float length)
    {
        Vector2 result = new Vector2(3f, 4f).Project(new Vector2(length, 0f));

        MathAssert.Equal(new Vector2(3f, 0f), result, 1e-6f);
    }

    /// <summary>
    /// П3-1 то же в трёх измерениях.
    /// </summary>
    [Theory]
    [InlineData(1e-7f)]
    [InlineData(1e-9f)]
    [InlineData(1e-20f)]
    [InlineData(1e-30f)]
    public void ProjectOntoDirection_AcceptsAnyNonZeroDirection(float length)
    {
        Vector3 result = new Vector3(3f, 4f, 5f).ProjectOntoDirection(new Vector3(length, 0f, 0f));

        MathAssert.Equal(new Vector3(3f, 0f, 0f), result, 1e-6f);
    }

    /// <summary>
    /// П3-1: проекция обязана быть одинаковой при любой длине направления,
    /// потому что длина направления не меняет его модуль, а формула
    /// <c>d·(v·d)/(d·d)</c> от длины не зависит. Дискриминирующий вход:
    /// направление под углом, где ошибка направления видна в ответе.
    /// </summary>
    [Fact]
    public void Project_DoesNotDependOnDirectionLength()
    {
        Vector2 direction = new(3f, 4f);
        Vector2 expected = new Vector2(5f, 6f).Project(direction);

        foreach (float scale in new[] { 1f, 1e-6f, 1e-20f, 1e6f, 1e20f, 1e30f })
        {
            Vector2 actual = new Vector2(5f, 6f).Project(direction * scale);

            MathAssert.Equal(expected, actual, 1e-5f);
        }
    }

    /// <summary>
    /// П3-1: настоящий ноль по-прежнему отвергается. Правка меняет порог с
    /// <c>Scalar.Epsilon²</c> на точный ноль, и этот случай не должен был
    /// перестать отвергаться.
    /// </summary>
    [Fact]
    public void Project_StillRejectsExactZeroDirection()
    {
        Assert.Throws<ArgumentException>(() => new Vector2(1f, 1f).Project(Vector2.Zero));
        Assert.Throws<ArgumentException>(() => new Vector3(1f, 1f, 1f).ProjectOntoDirection(Vector3.Zero));
        Assert.Throws<ArgumentException>(() => new Vector2(1f, 1f).Project(new Vector2(0f, -0f)));
    }

    // ==================================================================
    // П3-4. MoveTowards при NaN и совпадающих точках даёт ноль вопреки доктрине.

    /// <summary>
    /// П3-4: доктрина обещает, что <c>NaN</c> в шаге даёт <c>NaN</c>. При
    /// совпадающих точках прежняя оговорка возвращала весь <c>delta</c>, то
    /// есть ноль, и ошибка вызывающего терялась. Соседние
    /// <c>Angle.MoveTowards</c> и <c>Interpolation.MoveTowards</c> на тех же
    /// входах дают <c>NaN</c>.
    /// </summary>
    [Fact]
    public void MoveTowards_NonFiniteStepGivesNaNEvenForCoincidentPoints()
    {
        Vector2 flat = Vector2.Zero.MoveTowards(Vector2.Zero, float.NaN);
        Vector3 solid = Vector3.Zero.MoveTowards(Vector3.Zero, float.NaN);

        Assert.True(
            float.IsNaN(flat.X) && float.IsNaN(flat.Y),
            $"Vector2 при совпадающих точках и NaN-шаге дал {flat}, а доктрина обещает NaN.");
        Assert.True(
            float.IsNaN(solid.X) && float.IsNaN(solid.Y) && float.IsNaN(solid.Z),
            $"Vector3 при совпадающих точках и NaN-шаге дал {solid}, а доктрина обещает NaN.");
    }

    /// <summary>
    /// П3-4: то же для бесконечного шага. Он означает «без ограничения», то
    /// есть полный сдвиг, и на совпадающих точках это ноль, а не <c>NaN</c>.
    /// Это отличает два нечисловых шага друг от друга: <c>NaN</c> — ошибка
    /// вызывающего, <c>+∞</c> — законное значение.
    /// </summary>
    [Fact]
    public void MoveTowards_InfiniteStepGivesWholeDelta()
    {
        MathAssert.Equal(Vector2.Zero, Vector2.Zero.MoveTowards(Vector2.Zero, float.PositiveInfinity));
        MathAssert.Equal(new Vector2(7f, 0f), Vector2.Zero.MoveTowards(new Vector2(7f, 0f), float.PositiveInfinity));
        MathAssert.Equal(new Vector3(7f, 0f, 0f), Vector3.Zero.MoveTowards(new Vector3(7f, 0f, 0f), float.PositiveInfinity));
    }

    // ==================================================================
    // П3-3. Доктрина Perpendicular неверна на нижней границе.

    /// <summary>
    /// П3-3: доктрина обещает, что запасная ось берётся за пределами
    /// примерно от 1e-19 до 1e19. Измерение первой волны показало, что на самом
    /// деле граница проходит по 1e-21, то есть доктрина врёт о собственной
    /// границе. Этот тест фиксирует измеренную границу: если правка сдвинет
    /// её, тест упадёт и владелец узнает о расхождении с доктриной.
    /// </summary>
    [Fact]
    public void Perpendicular_FallbackAxisBoundaryMatchesDocumentedRange()
    {
        Vector3 vector = new(1f, 2f, 3f);
        Vector3 unit = VectorWave2Reference.Unit(vector.X, vector.Y, vector.Z);

        // Подсказка параллельна вектору: направление перпендикуляра не определено,
        // и метод обязан взять запасную ось.
        foreach (float magnitude in new[] { 1f, 1e-19f, 5e-20f, 1e-20f, 1e21f, 1e30f })
        {
            Vector3 result = vector.Perpendicular(vector * magnitude);

            double dot = ((double)unit.X * result.X) + ((double)unit.Y * result.Y) + ((double)unit.Z * result.Z);
            Assert.True(
                Math.Abs(dot) < 1e-5,
                $"Параллельная подсказка длиной {magnitude:E2} дала |dot| = {Math.Abs(dot):E3}, то есть метод её не распознал.");
            AssertFinite(result);
        }
    }

    /// <summary>
    /// П3-3: обратная сторона. Метод не должен «чиниться» постоянным возвратом
    /// запасной оси: когда подсказка образует разрешимый угол с вектором,
    /// она обязана учитываться, иначе базис не поворачивается вместе с
    /// движением. Это ровно тот случай, где ошибка в границе выдаёт себя.
    /// </summary>
    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void Perpendicular_FollowsResolvableHintAtAnyHintLength(float angle)
    {
        Vector3 vector = new(0.2672612f, -0.5345225f, 0.8017837f);
        Vector3 unit = VectorWave2Reference.Unit(vector.X, vector.Y, vector.Z);
        Vector3 axis = VectorWave2Reference.Unit(
            unit.Y * Vector3.UnitZ.Z - unit.Z * Vector3.UnitZ.Y,
            unit.Z * Vector3.UnitZ.X - unit.X * Vector3.UnitZ.Z,
            unit.X * Vector3.UnitZ.Y - unit.Y * Vector3.UnitZ.X);
        (float sin, float cos) = MathF.SinCos(angle);
        Vector3 hint = ((unit * cos) + (axis * sin));

        // Верхние длины обязаны работать так же, как единичная: прежний код
        // сравнивал |candidate|² с Epsilon²·|hint|², и при |hint| больше
        // 1.8446744e19 правая часть переполнялась, то есть запасная ось бралась
        // всегда и подсказка не учитывалась вовсе.
        foreach (float magnitude in new[] { 1e-25f, 1e-6f, 1f, 1e6f, 1e25f })
        {
            Vector3 result = vector.Perpendicular(hint * magnitude);
            float followed = Vector3.Dot(VectorWave2Reference.Unit(hint.X * magnitude, hint.Y * magnitude, hint.Z * magnitude), result);

            Assert.True(
                followed >= sin - 1e-4,
                $"Подсказка под углом {angle} рад длиной {magnitude:E2} не учтена: dot = {followed:F6} при ожидаемом {sin:F6}.");
        }
    }

    /// <summary>
    /// П3-3, обратная сторона: ниже порога подсказка обязана не учитываться, то
    /// есть берётся запасная ось. Проверяется сравнением с той же осью, что и
    /// в методе, потому что сравнение по углу не различает случаи: при почти
    /// параллельной подсказке запасная ось иногда совпадает с правильным
    /// ответом, и мера этого не видит.
    /// </summary>
    [Theory]
    [InlineData(1e-9f)]
    [InlineData(5e-7f)]
    [InlineData(9e-7f)]
    public void Perpendicular_IgnoresHintBelowDocumentedThreshold(float angle)
    {
        Vector3 vector = new(0.2672612f, -0.5345225f, 0.8017837f);
        Vector3 unit = VectorWave2Reference.Unit(vector.X, vector.Y, vector.Z);
        Vector3 axis = VectorWave2Reference.Unit(
            unit.Y * Vector3.UnitZ.Z - unit.Z * Vector3.UnitZ.Y,
            unit.Z * Vector3.UnitZ.X - unit.X * Vector3.UnitZ.Z,
            unit.X * Vector3.UnitZ.Y - unit.Y * Vector3.UnitZ.X);
        (float sin, float cos) = MathF.SinCos(angle);
        Vector3 hint = ((unit * cos) + (axis * sin));
        Vector3 fallback = VectorWave2Reference.FallbackAxis(unit);

        foreach (float magnitude in new[] { 1e-25f, 1f, 1e25f })
        {
            Vector3 result = vector.Perpendicular(hint * magnitude);

            Assert.True(
                Chord(result, fallback) < 1e-3f,
                $"Подсказка под углом {angle:E1} рад (sin = {sin:E1} меньше Scalar.Epsilon) длиной {magnitude:E2} была учтена вместо запасной оси.");
        }
    }

    /// <summary>
    /// П3-3, прямая сторона: граница обязана совпасть с той, что написана в
    /// доктрине, то есть <c>sin угла = Scalar.Epsilon</c>. Чуть выше порога
    /// подсказка учитывается, чуть ниже — нет. Это и есть проверка того, что
    /// доктрина перестала врать о собственной границе.
    /// </summary>
    [Theory]
    [InlineData(1.05e-6f, true)]
    [InlineData(9e-7f, false)]
    public void Perpendicular_ThresholdIsExactlyScalarEpsilon(float angle, bool expectFollowed)
    {
        Vector3 vector = new(0.2672612f, -0.5345225f, 0.8017837f);
        Vector3 unit = VectorWave2Reference.Unit(vector.X, vector.Y, vector.Z);
        Vector3 axis = VectorWave2Reference.Unit(
            unit.Y * Vector3.UnitZ.Z - unit.Z * Vector3.UnitZ.Y,
            unit.Z * Vector3.UnitZ.X - unit.X * Vector3.UnitZ.Z,
            unit.X * Vector3.UnitZ.Y - unit.Y * Vector3.UnitZ.X);
        (float sin, float cos) = MathF.SinCos(angle);
        Vector3 hint = ((unit * cos) + (axis * sin));
        Vector3 fallback = VectorWave2Reference.FallbackAxis(unit);

        Vector3 result = vector.Perpendicular(hint);
        bool followed = Chord(result, fallback) >= 1e-3f;

        Assert.True(
            followed == expectFollowed,
            $"Угол {angle:E1} рад: подсказка {(followed ? "учтена" : "проигнорирована")}, ожидалось наоборот.");
    }

    // ==================================================================
    // П3-5. RotateAround не документирует требование единичного кватерниона.

    /// <summary>
    /// П3-5: неединичный кватернион масштабирует результат на свой модуль в
    /// квадрате. Это не дефект <c>Vector3Extensions</c> (такой же контракт у
    /// <c>QuaternionExtensions.Rotate</c> в соседней поддиректории), но метод
    /// в моей поддиректории и обязан называть это в доктрине. Тест фиксирует
    /// поведение, чтобы «починка» соседней поддиректории не изменила его
    /// молча и не сломала вызывающих здесь.
    /// </summary>
    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(2f, 4f)]
    [InlineData(0.5f, 0.25f)]
    public void RotateAround_ScalesByQuaternionLengthSquared(float scale, float expected)
    {
        Quaternion rotation = new(scale, 0f, 0f, 0f);

        Vector3 result = Vector3.UnitX.RotateAround(Vector3.Zero, rotation);

        // Замкнутость теста на текущем поведении: этот тест зелёный и до
        // правки, и после. Он не «падает на прежнем коде», потому что
        // доказывает контракт, а не исправляет дефект.
        MathAssert.Equal(expected, (float)VectorWave2Reference.Length(result), 1e-5f);
    }

    // ==================================================================
    // Проверка, что эталон верен: иначе всё вышеперечисленное ничего не значит.

    /// <summary>
    /// Эталон обязан быть верен сам по себе, иначе «расхождение с кодом»
    /// нельзя приписывать коду (Problems.md, раздел 5: мера врала чаще кода).
    /// Проверяется, что эталон возвращает известные значения на известных
    /// входах, включая ненормальные числа.
    /// </summary>
    [Fact]
    public void Reference_IsItselfVerified()
    {
        AssertFinite(VectorWave2Reference.Unit(float.Epsilon, 0f, 0f));
        MathAssert.Equal(Vector3.UnitX, VectorWave2Reference.Unit(float.Epsilon, 0f, 0f), 0f);
        MathAssert.Equal(Vector3.UnitX, VectorWave2Reference.Unit(3.4e38f, 0f, 0f), 0f);
        MathAssert.Equal(Vector3.UnitX, VectorWave2Reference.Unit(1e-30f, 0f, 0f), 0f);

        Vector3 diagonal = VectorWave2Reference.Unit(1f, 1f, 1f);
        double target = 1.0 / Math.Sqrt(3.0);
        Assert.True(
            Math.Abs((double)diagonal.X - target) < 1e-7 && Math.Abs((double)diagonal.Y - target) < 1e-7,
            $"Эталон на (1,1,1) дал {diagonal} вместо {target:F9}.");

        // Длина эталона обязана совпадать с независимым вычислением в double.
        double expected = Math.Sqrt(1.0 + 16.0 + 25.0);
        Assert.True(
            Math.Abs(VectorWave2Reference.Length(new Vector3(1f, 4f, 5f)) - expected) < 1e-5,
            "Эталон длины врёт.");
    }

    // ==================================================================

    /// <summary>
    /// Хорда между двумя единичными векторами: расстояние по сфере.
    /// </summary>
    private static float Chord(Vector3 a, Vector3 b)
        => (float)Math.Sqrt(
            ((double)a.X - b.X) * (a.X - b.X)
            + ((double)a.Y - b.Y) * (a.Y - b.Y)
            + ((double)a.Z - b.Z) * (a.Z - b.Z));

    private static void AssertFinite(Vector3 value)
    {
        Assert.True(
            float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z),
            $"Получен нечисловой вектор {value}.");
    }

    private static void AssertFinite(float x, float y)
    {
        Assert.True(float.IsFinite(x) && float.IsFinite(y), $"Получен нечисловой вектор ({x}, {y}).");
    }
}

/// <summary>
/// Независимые эталоны и локальный генератор для <see cref="VectorWave2Tests"/>.
///
/// Эталон считает в <c>double</c> с предварительным масштабированием на
/// наибольшую по модулю компоненту. Это не пересказ формулы из проверяемого
/// кода: масштабирование нужно потому, что в <c>double</c> диапазон
/// 1e-45 … 3.4e38 целиком помещается в нормальные числа, то есть эталон
/// считает в том же диапазоне, в каком живёт проверяемый код, но без
/// переполнения и без обнуления. Именно поэтому он способен увидеть дефект,
/// а не повторить его.
///
/// Дискриминирующая проверка самого эталона —
/// <see cref="VectorWave2Tests.Reference_IsItselfVerified"/>: без неё любое
/// «расхождение с кодом» нельзя приписать коду.
/// </summary>
internal static class VectorWave2Reference
{
    /// <summary>
    /// Единичный вектор по точному значению, округлённый к float.
    /// </summary>
    public static Vector3 Unit(float x, float y, float z)
    {
        double scale = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
        if (scale == 0.0)
        {
            return Vector3.Zero;
        }

        double sx = x / scale;
        double sy = y / scale;
        double sz = z / scale;
        double length = Math.Sqrt((sx * sx) + (sy * sy) + (sz * sz));
        return new Vector3((float)(sx / length), (float)(sy / length), (float)(sz / length));
    }

    /// <summary>
    /// Длина вектора в double. <c>Vector3.Length()</c> в float на крошечных
    /// векторах обнуляется, поэтому эталон длины считает сам.
    /// </summary>
    public static double Length(Vector2 value)
        => Math.Sqrt(((double)value.X * value.X) + ((double)value.Y * value.Y));

    public static double Length(Vector3 value)
        => Math.Sqrt(
            ((double)value.X * value.X)
            + ((double)value.Y * value.Y)
            + ((double)value.Z * value.Z));

    /// <summary>
    /// Расстояние между значениями float в ULP. Считается по упорядоченным
    /// битам, а не вычитанием, поэтому мера работает и на масштабе 1e-30, где
    /// относительное допущение в процентах ничего не значит.
    /// </summary>
    public static double UlpDistance(float expected, float actual)
    {
        if (float.IsNaN(expected) || float.IsNaN(actual))
        {
            return float.IsNaN(expected) && float.IsNaN(actual) ? 0 : double.PositiveInfinity;
        }

        if (expected == actual)
        {
            return 0;
        }

        if (float.IsInfinity(expected) || float.IsInfinity(actual))
        {
            return double.PositiveInfinity;
        }

        long a = Ordered(expected);
        long b = Ordered(actual);
        return (double)Math.Abs(a - b);
    }

    public static double UlpDistance(Vector3 expected, Vector3 actual)
        => Math.Max(
            UlpDistance(expected.X, actual.X),
            Math.Max(UlpDistance(expected.Y, actual.Y), UlpDistance(expected.Z, actual.Z)));

    /// <summary>
    /// Проверка единичной длины: результат нормализации обязан давать единицу.
    /// Длина считается в double, потому что во float на вырожденных входах она
    /// обнуляется и мера сама оказалась бы дефектной.
    /// </summary>
    public static void AssertUnitLength(Vector3 value, string context)
        => Assert.True(
            Math.Abs(Length(value) - 1.0) <= 1e-6,
            $"{context}: длина {Length(value):F9} вместо единицы, вектор {value}.");

    /// <summary>
    /// Запасная ось из того же метода: без сравнения с ней нельзя отличить
    /// «подсказка проигнорирована» от «подсказка учтена», потому что при
    /// некоторых направлениях запасная ось совпадает с правильным ответом.
    /// </summary>
    public static Vector3 FallbackAxis(Vector3 unit)
    {
        float absoluteX = MathF.Abs(unit.X);
        float absoluteY = MathF.Abs(unit.Y);
        float absoluteZ = MathF.Abs(unit.Z);
        Vector3 baseAxis = absoluteX <= absoluteY && absoluteX <= absoluteZ
            ? Vector3.UnitX
            : absoluteY <= absoluteZ ? Vector3.UnitY : Vector3.UnitZ;
        return Vector3.Normalize(Vector3.Cross(baseAxis, unit));
    }

    private static long Ordered(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        return bits < 0 ? int.MinValue - bits : bits;
    }
}

/// <summary>
/// Локальный генератор для тестов. Библиотечный лежит в другом проекте, а
/// ссылаться на чужой тестовый файл нельзя: его может править другой агент.
/// xorshift64* — тот же алгоритм, что в библиотеке, поэтому прогоны
/// воспроизводимы и не зависят от версии рантайма.
/// </summary>
internal sealed class VectorWave2Rng
{
    private ulong _state;

    public VectorWave2Rng(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    /// <summary>
    /// Число из 24 разрядов: ровно столько различает float, поэтому
    /// распределение не зависит от округления double.
    /// </summary>
    public float Unit()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        ulong raw = _state * 0x2545F4914F6CDD1DUL;
        int bits = (int)((raw >> 40) & 0xFFFFFF);
        return bits * (1f / 16777216f);
    }

    public float Range(float min, float max) => min + ((max - min) * Unit());
}
