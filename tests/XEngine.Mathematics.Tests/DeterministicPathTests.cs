using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Проверка того, что ориентация и аффинные преобразования считаются через
/// <see cref="Trig"/>, а не через математическую библиотеку платформы.
/// </summary>
/// <remarks>
/// Детерминированный вариант сборки имеет смысл только тогда, когда ни один
/// путь вычислений не выходит за его пределы. До правки этого не было:
/// поворот вокруг оси, сферическая интерполяция, все аффинные матрицы 2D и
/// азимут в трёх измерениях считались вызовами <c>System.Numerics</c> и
/// <c>Math</c>, то есть результат зависел от операционной системы, а смена
/// варианта сборки на них не влияла вообще.
/// <para>
/// Проверять это одним процессом нельзя: два варианта сборки не живут в одном
/// процессе. Поэтому тест построен на сверке с независимой реализацией,
/// написанной через <see cref="Trig.SinCos"/> и <see cref="Trig.Atan2"/>.
/// В детерминированном варианте совпадение обязано быть побитовым, во
/// быстром — с допуском: там обе стороны зовут одну и ту же платформенную
/// функцию. Если библиотека вернётся к <c>Quaternion.CreateFromAxisAngle</c>,
/// детерминированная сборка разойдётся с эталоном, и тест упадёт именно там.
/// </para>
/// </remarks>
public class DeterministicPathTests
{
    /// <summary>
    /// Допуск сверки: во быстом варианте обе стороны считаются одной и той же
    /// платформенной функцией, но порядок операций может отличаться на
    /// последний разряд.
    /// </summary>
    private const float FastTolerance = 1e-6f;

    /// <summary>
    /// Сравнивает с эталоном, написанным через <see cref="Trig"/>: в
    /// детерминированном варианте совпадение обязано быть побитовым.
    /// </summary>
    /// <param name="expected">Эталонное значение.</param>
    /// <param name="actual">Значение библиотеки.</param>
    /// <param name="what">Что проверяется, для сообщения об ошибке.</param>
    /// <param name="absoluteError">
    /// Допуск, не зависящий от величины результата. Нужен там, где эталон и
    /// библиотека складывают одни и те же числа в разном порядке: там
    /// совпадение побитовое невозможно по построению.
    /// </param>
    private static void SameAsReference(float expected, float actual, string what, float absoluteError = 0f)
    {
        if (Trig.IsDeterministic && absoluteError == 0f)
        {
            Assert.True(
                BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual),
                $"{what}: эталон {expected:R} и результат {actual:R} обязаны совпадать побитово в детерминированном варианте.");
            return;
        }

        float allowed = absoluteError + (FastTolerance * (1f + MathF.Abs(expected)));
        Assert.True(
            MathF.Abs(expected - actual) <= allowed,
            $"{what}: эталон {expected:R}, результат {actual:R}, допуск {allowed:R}.");
    }

    /// <summary>
    /// Сравнивает со значением, посчитанным на двойной точности средствами
    /// <c>System.Math</c>. Допуск обязателен в обоих вариантах сборки: в
    /// детерминированном функции отличаются от платформенных на последний
    /// разряд по построению, и это не дефект.
    /// </summary>
    /// <param name="expected">Значение на двойной точности.</param>
    /// <param name="actual">Значение библиотеки.</param>
    /// <param name="what">Что проверяется, для сообщения об ошибке.</param>
    /// <param name="tolerance">Относительный допуск.</param>
    private static void MatchesDouble(double expected, float actual, string what, double tolerance = 1e-5)
    {
        double allowed = tolerance * Math.Max(1.0, Math.Abs(expected));
        Assert.True(
            Math.Abs(expected - actual) <= allowed,
            $"{what}: эталон {expected:R}, результат {actual:R}, допуск {allowed:R}.");
    }

    /// <summary>
    /// Независимое построение поворота вокруг единичной оси через фасад.
    /// </summary>
    /// <param name="axis">Единичная ось.</param>
    /// <param name="radians">Угол в радианах.</param>
    /// <returns>Кватернион поворота.</returns>
    private static Quaternion ReferenceFromAxisAngle(Vector3 axis, double radians)
    {
        (float sin, float cos) = Trig.SinCos((float)radians * 0.5f);
        return new Quaternion(axis.X * sin, axis.Y * sin, axis.Z * sin, cos);
    }

    [Fact]
    public void FromAxisAngle_MatchesTrigBasedReference()
    {
        DeterministicRandom random = new(0x1D2C3B4A59687788UL);

        for (int i = 0; i < 2000; i++)
        {
            Vector3 axis = random.NextUnitVector();
            Angle angle = Angle.FromDegrees(random.Range(-360f, 360f));

            Quaternion actual = QuaternionExtensions.FromAxisAngle(axis, angle);
            Quaternion expected = ReferenceFromAxisAngle(axis, angle.Radians);

            MathAssert.Equal(expected, actual, FastTolerance);
            SameAsReference(expected.W, actual.W, $"FromAxisAngle, ось {axis}, угол {angle.Degrees:F3}°");

            Assert.Equal(1f, actual.Length(), 1e-5f);
        }
    }

    [Fact]
    public void FromEuler_MatchesCompositionOfTrigBasedRotations()
    {
        DeterministicRandom random = new(0x2E3F4A5B6C7D8E9FUL);

        for (int i = 0; i < 2000; i++)
        {
            Angle yaw = Angle.FromDegrees(random.Range(-180f, 180f));
            Angle pitch = Angle.FromDegrees(random.Range(-89f, 89f));
            Angle roll = Angle.FromDegrees(random.Range(-180f, 180f));

            Quaternion expected = ReferenceFromAxisAngle(Vector3.UnitY, yaw.Radians)
                * (ReferenceFromAxisAngle(Vector3.UnitX, pitch.Radians)
                * ReferenceFromAxisAngle(Vector3.UnitZ, roll.Radians));
            Quaternion actual = QuaternionExtensions.FromEuler(yaw, pitch, roll);

            SameAsReference(expected.W, actual.W, $"FromEuler ({yaw.Degrees:F2}, {pitch.Degrees:F2}, {roll.Degrees:F2})");
            MathAssert.Equal(expected, actual, FastTolerance);
        }
    }

    [Fact]
    public void CreateTransform_MatchesCompositionOfTrigBasedMatrices()
    {
        DeterministicRandom random = new(0x3F4A5B6C7D8E9FA0UL);

        for (int i = 0; i < 2000; i++)
        {
            Vector2 position = new Vector2(random.Range(-50f, 50f), random.Range(-50f, 50f));
            Vector2 scale = new Vector2(random.Range(0.1f, 8f), random.Range(0.1f, 8f));
            Vector2 pivot = new Vector2(random.Range(-5f, 5f), random.Range(-5f, 5f));
            Angle rotation = Angle.FromDegrees(random.Range(-360f, 360f));

            // Эталон: то же произведение матриц, но поворот собран через фасад.
            (float sin, float cos) = Trig.SinCos((float)rotation.Radians);
            Matrix3x2 expected = Matrix3x2.CreateTranslation(-pivot)
                * Matrix3x2.CreateScale(scale)
                * new Matrix3x2(cos, sin, -sin, cos, 0f, 0f)
                * Matrix3x2.CreateTranslation(pivot + position);

            Matrix3x2 actual = Matrix3x2Extensions.CreateTransform(position, rotation, scale, pivot);

            SameAsReference(expected.M11, actual.M11, "CreateTransform M11");
            SameAsReference(expected.M12, actual.M12, "CreateTransform M12");
            SameAsReference(expected.M21, actual.M21, "CreateTransform M21");
            SameAsReference(expected.M22, actual.M22, "CreateTransform M22");
            // Перенос получается вычитанием, поэтому его относительная ошибка
            // определяется не результатом, а величиной уменьшаемого. Допуск
            // задаётся по сумме разрядов, из которых он сложен.
            float translationScale = MathF.Max(1f, position.Length() + pivot.Length());
            SameAsReference(expected.M31, actual.M31, "CreateTransform M31", translationScale * 1e-6f);
            SameAsReference(expected.M32, actual.M32, "CreateTransform M32", translationScale * 1e-6f);
        }
    }

    [Fact]
    public void CreateRotation_MatchesTrigBasis()
    {
        for (int degrees = -180; degrees <= 180; degrees += 3)
        {
            Angle angle = Angle.FromDegrees(degrees);
            (float sin, float cos) = Trig.SinCos((float)angle.Radians);
            Matrix3x2 actual = Matrix3x2Extensions.CreateRotation(angle);

            SameAsReference(cos, actual.M11, $"CreateRotation({degrees}°) M11");
            SameAsReference(sin, actual.M12, $"CreateRotation({degrees}°) M12");
            SameAsReference(-sin, actual.M21, $"CreateRotation({degrees}°) M21");
            SameAsReference(cos, actual.M22, $"CreateRotation({degrees}°) M22");
            Assert.Equal(0f, actual.M31);
            Assert.Equal(0f, actual.M32);
        }
    }

    [Fact]
    public void Slerp_MatchesReferenceOnShortestArc()
    {
        DeterministicRandom random = new(0x4A5B6C7D8E9FA0B1UL);

        for (int i = 0; i < 2000; i++)
        {
            Vector3 axisA = random.NextUnitVector();
            Vector3 axisB = random.NextUnitVector();
            Angle angleA = Angle.FromDegrees(random.Range(-180f, 180f));
            Angle angleB = Angle.FromDegrees(random.Range(-180f, 180f));

            Quaternion from = QuaternionExtensions.FromAxisAngle(axisA, angleA);
            Quaternion to = QuaternionExtensions.FromAxisAngle(axisB, angleB);
            float t = random.NextFloat();

            Quaternion actual = QuaternionExtensions.Slerp(from, to, t);

            Assert.Equal(1f, actual.Length(), 1e-5f);

            // Проверка по смыслу: промежуточная ориентация находится на кратчайшей
            // дуге, то есть её угол до начала равен t от полного угла между
            // операндами. Считается через те же функции, что и в Slerp, поэтому
            // сверка точна в детерминированном варианте.
            float fullAngle = (float)QuaternionExtensions.AngleBetween(from, to).Degrees;
            float partAngle = (float)QuaternionExtensions.AngleBetween(from, actual).Degrees;
            // Угол между ориентациями — производная величина: он считается
            // через atan2 от относительного поворота и накапливает округления
            // обоих концов. Здесь проверяется принадлежность дуге, поэтому
            // сравнение с относительным допуском, а не побитовое.
            const float ArcTolerance = 1e-4f;
            float arcAllowed = ArcTolerance * MathF.Max(1f, fullAngle);
            Assert.True(
                MathF.Abs((fullAngle * t) - partAngle) <= arcAllowed,
                $"Slerp, t = {t:F4}: ожидался угол {fullAngle * t:F6}°, получен {partAngle:F6}°, допуск {arcAllowed:R}°.");
        }
    }

    [Fact]
    public void ToAngle_UsesTrigAndIgnoresVerticalComponent()
    {
        DeterministicRandom random = new(0x5B6C7D8E9FA0B1C2UL);

        for (int i = 0; i < 2000; i++)
        {
            float azimuth = random.Range(-180f, 180f);
            float pitch = random.Range(-89f, 89f);
            float distance = random.Range(0.01f, 1000f);

            float azimuthRadians = (float)Scalar.ToRadians(azimuth);
            float pitchRadians = (float)Scalar.ToRadians(pitch);
            Vector3 direction = new Vector3(
                MathF.Cos(pitchRadians) * MathF.Cos(azimuthRadians),
                MathF.Sin(pitchRadians),
                MathF.Cos(pitchRadians) * MathF.Sin(azimuthRadians)) * distance;

            Angle actual = direction.ToAngle();
            double expected = Math.Atan2(direction.Z, direction.X);

            MatchesDouble(expected, (float)actual.Radians, $"ToAngle, азимут {azimuth:F2}°");

            // Вертикальная составляющая не влияет на азимут: тот определяется
            // проекцией на плоскость XZ.
            Vector3 flattened = new Vector3(direction.X, 0f, direction.Z);
            Assert.True(
                Math.Abs(Angle.ShortestDelta(actual, flattened.ToAngle())) <= 1e-4,
                $"ToAngle не зависит от Y при азимуте {azimuth:F2}°: {actual.Degrees:F6}° против {flattened.ToAngle().Degrees:F6}°.");
        }
    }

    /// <summary>
    /// Азимут обязан совпадать с <see cref="Trig.Atan2"/> побитово: иначе
    /// возвращение к <c>Math.Atan2</c> в двойной точности осталось бы незамеченным,
    /// потому что значения отличаются на последний разряд и любой допуск это
    /// скрывает.
    /// </summary>
    [Fact]
    public void ToAngle_MatchesTrigAtan2Bitwise()
    {
        DeterministicRandom random = new(0x7D8E9FA0B1C2D3E4UL);

        for (int i = 0; i < 2000; i++)
        {
            Vector3 direction = new Vector3(
                random.Range(-1f, 1f),
                random.Range(-1f, 1f),
                random.Range(-1f, 1f));
            if (direction.X == 0f && direction.Z == 0f)
            {
                continue;
            }

            SameAsReference(
                Trig.Atan2(direction.Z, direction.X),
                (float)direction.ToAngle().Radians,
                $"ToAngle({direction})");
        }
    }

    /// <summary>
    /// Интерполяция ориентаций обязана считаться через фасад. Проверяется
    /// побитово против независимой реализации, написанной на
    /// <see cref="Trig.Acos"/> и <see cref="Trig.Sin"/>.
    /// </summary>
    [Fact]
    public void Slerp_MatchesTrigBasedImplementationBitwise()
    {
        DeterministicRandom random = new(0x8E9FA0B1C2D3E4F5UL);

        for (int i = 0; i < 2000; i++)
        {
            Vector3 axisA = random.NextUnitVector();
            Vector3 axisB = random.NextUnitVector();
            Quaternion from = QuaternionExtensions.FromAxisAngle(axisA, Angle.FromDegrees(random.Range(-180f, 180f)));
            Quaternion to = QuaternionExtensions.FromAxisAngle(axisB, Angle.FromDegrees(random.Range(-180f, 180f)));
            float t = random.NextFloat();

            Quaternion expected = ReferenceSlerp(from, to, t);
            Quaternion actual = QuaternionExtensions.Slerp(from, to, t);

            SameAsReference(expected.X, actual.X, $"Slerp X, t = {t:F4}");
            SameAsReference(expected.Y, actual.Y, $"Slerp Y, t = {t:F4}");
            SameAsReference(expected.Z, actual.Z, $"Slerp Z, t = {t:F4}");
            SameAsReference(expected.W, actual.W, $"Slerp W, t = {t:F4}");
        }
    }

    /// <summary>
    /// Независимая реализация сферической интерполяции на функциях фасада.
    /// </summary>
    /// <remarks>
    /// Порядок операций совпадает с проверяемым: тест проверяет не формулу, а
    /// то, какие функции вызваны. Если вместо <see cref="Trig.Acos"/> и
    /// <see cref="Trig.Sin"/> вернутся <c>MathF.Acos</c> и <c>MathF.Sin</c>,
    /// результат разойдётся на последний разряд, и в детерминированном варианте
    /// это видно побитово.
    /// </remarks>
    /// <param name="from">Начальная ориентация.</param>
    /// <param name="to">Конечная ориентация.</param>
    /// <param name="t">Параметр.</param>
    /// <returns>Интерполированная ориентация.</returns>
    private static Quaternion ReferenceSlerp(Quaternion from, Quaternion to, float t)
    {
        // Нормализация умножением на обратный корень, а не делением на корень:
        // это разные округления, и в побитовой сверке различие видно.
        from = ScaleToUnit(from);
        to = ScaleToUnit(to);

        float dot = Quaternion.Dot(from, to);
        Quaternion target = to;
        float cosine = dot;
        if (cosine < 0f)
        {
            target = new Quaternion(-to.X, -to.Y, -to.Z, -to.W);
            cosine = -cosine;
        }

        float resultX;
        float resultY;
        float resultZ;
        float resultW;
        if (cosine > 0.9995f)
        {
            resultX = from.X + ((target.X - from.X) * t);
            resultY = from.Y + ((target.Y - from.Y) * t);
            resultZ = from.Z + ((target.Z - from.Z) * t);
            resultW = from.W + ((target.W - from.W) * t);
        }
        else
        {
            float angle = Trig.Acos(cosine);
            float sinAngle = Trig.Sin(angle);
            float firstShare = Trig.Sin((1f - t) * angle) / sinAngle;
            float secondShare = Trig.Sin(t * angle) / sinAngle;
            resultX = (from.X * firstShare) + (target.X * secondShare);
            resultY = (from.Y * firstShare) + (target.Y * secondShare);
            resultZ = (from.Z * firstShare) + (target.Z * secondShare);
            resultW = (from.W * firstShare) + (target.W * secondShare);
        }

        return ScaleToUnit(new Quaternion(resultX, resultY, resultZ, resultW));
    }

    /// <summary>
    /// Нормализация кватерниона умножением на обратный корень длины.
    /// </summary>
    /// <param name="value">Исходный кватернион.</param>
    /// <returns>Единичный кватернион.</returns>
    private static Quaternion ScaleToUnit(Quaternion value)
    {
        float lengthSquared = value.LengthSquared();
        float scale = 1f / MathF.Sqrt(lengthSquared);
        return new Quaternion(value.X * scale, value.Y * scale, value.Z * scale, value.W * scale);
    }

    [Fact]
    public void ToAngle_VerticalDirectionGivesZero()
    {
        Assert.Equal(Angle.Zero, new Vector3(0f, 5f, 0f).ToAngle());
        Assert.Equal(Angle.Zero, new Vector3(0f, -5f, 0f).ToAngle());
        Assert.Equal(Angle.Zero, Vector3.Zero.ToAngle());
    }

    [Fact]
    public void Acos_IsInverseOfCosOnWholeRange()
    {
        DeterministicRandom random = new(0x6C7D8E9FA0B1C2D3UL);

        for (int i = 0; i < 5000; i++)
        {
            float x = random.Range(-1f, 1f);
            float angle = Trig.Acos(x);
            MatchesDouble(Math.Acos(x), angle, $"Acos({x:F6})");
            MathAssert.Equal(x, Trig.Cos(angle), 1e-5f);
        }

        Assert.Equal(0f, Trig.Acos(1f));
        Assert.Equal(MathF.PI, Trig.Acos(-1f));
        MathAssert.Equal(MathF.PI * 0.5f, Trig.Acos(0f), 1e-6f);
    }

    /// <summary>
    /// Угол между осями бокса восстанавливается поворотом матрицы
    /// <see cref="Matrix3x2Extensions.CreateRotation"/>: это проверяет, что
    /// собственная матрица поворота согласована с разбором, который к ней
    /// обращается.
    /// </summary>
    [Fact]
    public void CreateRotation_RoundTripsThroughRotationReader()
    {
        for (int degrees = -180; degrees <= 180; degrees += 2)
        {
            Matrix3x2 matrix = Matrix3x2Extensions.CreateRotation(Angle.FromDegrees(degrees));
            float recovered = (float)Matrix3x2Extensions.Rotation(matrix).Degrees;

            // При 180° синус равен не нулю, а примерно -4.4e-8, поэтому atan2
            // возвращает угол чуть меньше плюс пи: 180° и -179.99998° — это
            // одна и та же ориентация. Сравнение идёт по разности, приведённой
            // к полному обороту.
            float difference = (float)Math.Abs(Angle.ShortestDelta(
                Angle.FromDegrees(degrees),
                Angle.FromDegrees(recovered)) * 180.0 / Math.PI);
            Assert.True(difference <= 1e-3f, $"CreateRotation({degrees}°) восстановлен как {recovered:F5}°, расхождение {difference:F5}°.");
        }
    }
}