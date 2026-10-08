using System.Numerics;
using System.Text.Json;
using XEngine.Mathematics.Serialization;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные тесты на дефекты, найденные при ревзии
/// <c>src/XEngine.Mathematics</c> и описанные в <c>Problems.md</c>.
/// </summary>
/// <remarks>
/// Общая черта исходных дефектов: рядом уже был тест, но он проверял тот вход,
/// на котором ошибка не проявляется. Поэтому здесь подобраны именно
/// невырожденные входы — перпендикулярный луч, неравномерный масштаб, массив
/// из трёх чисел, переполняющийся диапазон, нулевой кадр. Тест, который
/// повторяет старый, был бы бесполезен.
/// </remarks>
public class DefectRegressionTests
{
    /// <summary>
    /// P1-1: луч, перпендикулярный оси капсулы, обязан попадать в её тело.
    /// Старый код делил на dot(Direction, axis), который здесь ноль, получал
    /// NaN, а NaN не проходит ни одно сравнение, и метод возвращал false.
    /// </summary>
    [Fact]
    public void Ray3_PerpendicularRayHitsVerticalCapsuleBody()
    {
        Capsule3 capsule = new(new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f), 0.5f);
        Ray3 ray = new(new Vector3(-5f, 0f, 0f), Vector3.UnitX);

        Assert.True(ray.Intersects(capsule), "Горизонтальный луч обязан пересекать вертикальную капсулу.");
        Assert.True(ray.Raycast(capsule, out float distance));
        MathAssert.Equal(4.5f, distance, 1e-4f);
    }

    /// <summary>
    /// P1-1 (б): коэффициенты квадратного уравнения. На наклонном луче старый
    /// код выдавал оба корня отрицательными, то есть объявлял промах.
    /// </summary>
    [Fact]
    public void Ray3_TiltedRayEntersCapsuleCylinder()
    {
        Capsule3 capsule = new(new Vector3(0f, -2f, 0f), new Vector3(0f, 2f, 0f), 0.5f);
        Ray3 ray = new(new Vector3(-5f, -1f, 0f), new Vector3(1f, 0.3f, 0f));

        Assert.True(ray.Intersects(capsule));
        Assert.True(ray.Raycast(capsule, out float distance));
        MathAssert.Equal(4.6981f, distance, 1e-3f);
    }

    /// <summary>
    /// P1-1: луч не должен попадать в капсулу, мимо которой он проходит, и не
    /// должен попадать в цилиндр за пределами осевой линии.
    /// </summary>
    [Fact]
    public void Ray3_MissAndAlongAxisCasesStayCorrect()
    {
        Capsule3 capsule = new(new Vector3(0f, -2f, 0f), new Vector3(0f, 2f, 0f), 0.5f);

        Assert.False(new Ray3(new Vector3(-5f, 5f, 0f), Vector3.UnitX).Intersects(capsule), "Промах должен остаться промахом.");

        // Луч вдоль оси входит через торец, а не через боковую поверхность.
        Assert.True(new Ray3(new Vector3(0f, -5f, 0f), Vector3.UnitY).Raycast(capsule, out float along));
        MathAssert.Equal(2.5f, along, 1e-4f);
    }

    /// <summary>
    /// P2: луч, начинающийся внутри сферы, пересекает её, а расстояние до входа
    /// равно нулю. Старый код выходил по projection &lt; 0 до всякой проверки.
    /// </summary>
    [Fact]
    public void Ray3_StartingInsideSphereHitsWithZeroDistance()
    {
        BoundingSphere sphere = new(Vector3.Zero, 2f);
        Ray3 ray = new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1f, 0.3f, 0f));

        Assert.True(ray.Intersects(sphere));
        Assert.True(ray.Raycast(sphere, out float distance));
        MathAssert.Equal(0f, distance, 1e-5f);
    }

    /// <summary>
    /// То же для капсулы: старая проверка ломала её через эндкапсулы.
    /// </summary>
    [Fact]
    public void Ray3_StartingInsideCapsuleHitsWithZeroDistance()
    {
        Capsule3 capsule = new(new Vector3(0f, -2f, 0f), new Vector3(0f, 2f, 0f), 1f);
        Ray3 ray = new(new Vector3(0f, 0f, 0.5f), Vector3.UnitZ);

        Assert.True(ray.Intersects(capsule));
        Assert.True(ray.Raycast(capsule, out float distance));
        MathAssert.Equal(0f, distance, 1e-5f);
    }

    /// <summary>
    /// Сфера строго за началом луча не пересекается им, даже если лежит на той
    /// же прямой: проверка на «начало внутри» не должна путать эти два случая.
    /// </summary>
    [Fact]
    public void Ray3_SphereBehindOriginIsNotHit()
    {
        BoundingSphere sphere = new(new Vector3(0f, 0f, 10f), 2f);

        Assert.False(new Ray3(Vector3.Zero, -Vector3.UnitZ).Intersects(sphere));
    }

    /// <summary>
    /// P3: слэб-метод не должен сообщать попадание по пустому параллелепипеду:
    /// у него границы переставлены, интервал сортируется в пересечение.
    /// </summary>
    [Fact]
    public void Ray3_EmptyAabbIsNeverHit()
    {
        Ray3 ray = new(new Vector3(-10f, 0f, 0f), Vector3.UnitX);

        Assert.False(ray.Intersects(Aabb3.Empty));
        Assert.False(ray.Raycast(Aabb3.Empty, out float distance));
        MathAssert.Equal(0f, distance, 1e-5f);
    }

    /// <summary>
    /// P1-2: минимум расстояния между отрезками может достигаться на паре
    /// «конец A против отрезка B». Старый код её не проверял и повторял
    /// предыдущую строку, поэтому расстояние получалось завышенным.
    /// </summary>
    [Fact]
    public void Collision_SegmentSegmentDistanceCoversAllFourEndpointPairs()
    {
        Segment2 a = new(new Vector2(-1f, 3f), new Vector2(-2f, -3f));
        Segment2 b = new(new Vector2(1f, -4f), new Vector2(-3f, -4f));

        MathAssert.Equal(1f, Collision.SegmentSegmentDistance(a, b), 1e-4f);
        MathAssert.Equal(1f, Collision.SegmentSegmentDistance(b, a), 1e-4f);
    }

    /// <summary>
    /// P1-2: следствие — пересекающиеся капсулы сообщались как разделённые.
    /// </summary>
    [Fact]
    public void Collision_IntersectingCapsulesAreReportedAsIntersecting()
    {
        Capsule2 a = new(new Segment2(new Vector2(-1f, 3f), new Vector2(-2f, -3f)), 0.5f);
        Capsule2 b = new(new Segment2(new Vector2(1f, -4f), new Vector2(-3f, -4f)), 0.5f);

        Assert.True(Collision.Intersects(a, b));
    }

    /// <summary>
    /// Проверка всех пар на случайных отрезках: результат обязан совпадать с
    /// минимумом, найденным перебором точек обоих отрезков.
    /// </summary>
    [Fact]
    public void Collision_SegmentSegmentDistanceMatchesBruteForceMinimum()
    {
        XorShift64Star random = new(20251008UL);
        const int Samples = 512;

        for (int step = 0; step < 200; step++)
        {
            Vector2 a0 = new(random.NextFloat() * 8f - 4f, random.NextFloat() * 8f - 4f);
            Vector2 a1 = new(random.NextFloat() * 8f - 4f, random.NextFloat() * 8f - 4f);
            Vector2 b0 = new(random.NextFloat() * 8f - 4f, random.NextFloat() * 8f - 4f);
            Vector2 b1 = new(random.NextFloat() * 8f - 4f, random.NextFloat() * 8f - 4f);
            Segment2 a = new(a0, a1);
            Segment2 b = new(b0, b1);

            float reference = BruteForceDistance(a, b, Samples);
            MathAssert.Equal(reference, Collision.SegmentSegmentDistance(a, b), 2e-2f);
        }
    }

    /// <summary>
    /// P1-3: неравномерный масштаб. Старый код делил строку на масштабы по
    /// столбцам и возвращал неединичный кватернион: |q| = 1.0425 вместо 1.
    /// </summary>
    [Fact]
    public void Matrix4x4_GetRotationSurvivesNonUniformScale()
    {
        Quaternion expected = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f);
        Matrix4x4 transform = Matrix4x4Extensions.CreateTRS(Vector3.Zero, expected, new Vector3(2f, 3f, 4f));

        Quaternion actual = transform.GetRotation();

        MathAssert.Equal(1f, actual.Length(), 1e-4f);
        MathAssert.Equal(1f, MathF.Abs(Quaternion.Dot(expected, actual)), 1e-4f);
    }

    /// <summary>
    /// P1-3: тот же случай на повороте вокруг другой оси и с другим масштабом.
    /// </summary>
    [Fact]
    public void Matrix4x4_GetRotationSurvivesNonUniformScaleOnEveryAxis()
    {
        (Vector3 Axis, float Degrees, Vector3 Scale)[] cases =
        [
            (Vector3.UnitZ, 45f, new Vector3(2f, 2f, 2f)),
            (Vector3.UnitY, 37f, new Vector3(1f, 5f, 2f)),
            (Vector3.UnitX, -120f, new Vector3(3f, 1f, 1f)),
        ];

        foreach ((Vector3 axis, float degrees, Vector3 scale) in cases)
        {
            Quaternion expected = Quaternion.CreateFromAxisAngle(axis, MathF.PI * degrees / 180f);
            Matrix4x4 transform = Matrix4x4Extensions.CreateTRS(Vector3.Zero, expected, scale);

            Quaternion actual = transform.GetRotation();

            MathAssert.Equal(1f, actual.Length(), 1e-4f);

            // Кватернионы q и -q задают одну ориентацию, а CreateFromRotationMatrix
            // возвращает знак на свой выбор: сравнивать надо |dot| ≈ 1, а не
            // компоненты покомпонентно.
            MathAssert.Equal(1f, MathF.Abs(Quaternion.Dot(expected, actual)), 1e-4f);
        }
    }

    /// <summary>
    /// P1-4: нулевой поворот обязан оставить прямоугольник в покое. Старый код
    /// возвращал Rect(10, 10, 14, 12) вместо Rect(10, 10, 4, 2).
    /// </summary>
    [Fact]
    public void Rect_RotateByZeroDegreesIsIdentity()
    {
        Rect source = new(10f, 10f, 4f, 2f);

        Assert.Equal(source, source.Rotate(Angle.Zero));
    }

    /// <summary>
    /// P1-4: поворот на 90° меняет местами стороны и сохраняет площадь.
    /// </summary>
    [Fact]
    public void Rect_RotateByQuarterTurnSwapsSides()
    {
        Rect source = new(10f, 10f, 4f, 2f);

        Rect rotated = source.Rotate(Angle.FromDegrees(90f));

        MathAssert.Equal(2f, rotated.Width, 1e-4f);
        MathAssert.Equal(4f, rotated.Height, 1e-4f);
        MathAssert.Equal(source.Width * source.Height, rotated.Width * rotated.Height, 1e-4f);
    }

    /// <summary>
    /// P1-4: пивот учитывается — точка пивота принадлежит прямоугольнику и
    /// остаётся на месте, поэтому обязана лежать внутри результата.
    /// </summary>
    [Fact]
    public void Rect_RotateHonoursPivot()
    {
        Rect source = new(0f, 0f, 2f, 2f);
        Vector2 pivot = new(0f, 0f);

        Rect rotated = source.Rotate(Angle.FromDegrees(30f), pivot);

        Assert.True(
            rotated.Contains(pivot),
            $"Пивот {pivot} вне результата {rotated}: поворот должен быть вокруг него.");

        Rect other = new(10f, 10f, 4f, 2f);
        Vector2 corner = other.Position;
        Rect turned = other.Rotate(Angle.FromDegrees(90f), corner);
        Assert.True(turned.Contains(corner), $"Угол {corner} вне результата {turned}.");
        MathAssert.Equal(2f, turned.Width, 1e-4f);
        MathAssert.Equal(4f, turned.Height, 1e-4f);
    }

    /// <summary>
    /// P1-4: результат обязан содержать повёрнутый прямоугольник целиком.
    /// </summary>
    [Fact]
    public void Rect_RotateContainsAllFourRotatedCorners()
    {
        Rect source = new(-2f, -1f, 4f, 2f);
        Rect rotated = source.Rotate(Angle.FromDegrees(37f), Vector2.Zero);

        foreach (Vector2 corner in new[] { source.Position, source.Position + new Vector2(source.Width, 0f), source.Position + new Vector2(0f, source.Height), source.Position + source.Size })
        {
            Vector2 turned = Angle.FromDegrees(37f).Rotate(corner);
            Assert.True(
                turned.X >= rotated.Left - 1e-3f && turned.X <= rotated.Right + 1e-3f
                && turned.Y >= rotated.Top - 1e-3f && turned.Y <= rotated.Bottom + 1e-3f,
                $"Повёрнутый угол {turned} вышел за пределы {rotated}.");
        }
    }

    /// <summary>
    /// P1-5: InBack обязана проваливаться ниже нуля — в этом весь «перелёт
    /// назад». Старый код был дословной копией OutBack и давал 0.706.
    /// </summary>
    [Fact]
    public void Curves_InBackDipsBelowZero()
    {
        Assert.True(Curves.InBack(0.2f) < 0f, "Кривая ускорения с перелётом обязана проваливаться ниже нуля.");
        MathAssert.Equal(0f, Curves.InBack(0f), 1e-5f);
        MathAssert.Equal(1f, Curves.InBack(1f), 1e-5f);
    }

    /// <summary>
    /// P1-5: InBack и OutBack обязаны быть разными кривыми: первая проваливается
    /// ниже нуля, вторая перелетает единицу вверх. На концах диапазона обе дают
    /// 0 и 1, поэтому различие видно только внутри.
    /// </summary>
    [Fact]
    public void Curves_InBackDiffersFromOutBack()
    {
        MathAssert.Equal(-0.04645f, Curves.InBack(0.2f), 1e-3f);
        MathAssert.Equal(0.70582f, Curves.OutBack(0.2f), 1e-3f);

        // Перелёт вверх у OutBack происходит в середине диапазона, а не сразу:
        // максимум кривой обязан превышать единицу.
        float maximum = 0f;
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            maximum = MathF.Max(maximum, Curves.OutBack(t));
        }

        Assert.True(maximum > 1f, $"Максимум OutBack равен {maximum}: перелёта вверх нет.");

        // Кривые симметричны относительно anti-diagonal: OutBack(t) = 1 - InBack(1 - t).
        for (float t = 0.1f; t < 0.95f; t += 0.1f)
        {
            MathAssert.Equal(
                1f - Curves.InBack(1f - t),
                Curves.OutBack(t),
                1e-4f);
        }
    }

    /// <summary>
    /// P1-6: массив из трёх каналов задаёт непрозрачный цвет. Старый код опирался
    /// на values.Length, то есть всегда на 4, и давал альфу 0 — невидимый цвет.
    /// </summary>
    [Fact]
    public void Rgba32_ThreeChannelArrayKeepsOpaqueAlpha()
    {
        Rgba32 result = JsonSerializer.Deserialize<Rgba32>("[1,0.5,0.2]", new JsonSerializerOptions
        {
            Converters = { new Rgba32JsonConverter() },
        });

        Assert.Equal(1f, result.R, 1e-6f);
        Assert.Equal(0.5f, result.G, 1e-6f);
        Assert.Equal(0.2f, result.B, 1e-6f);
        Assert.Equal(1f, result.A, 1e-6f);
    }

    /// <summary>
    /// P1-6: массив из четырёх чисел по-прежнему задаёт альфу явно.
    /// </summary>
    [Fact]
    public void Rgba32_FourChannelArrayStillReadsExplicitAlpha()
    {
        Rgba32 result = JsonSerializer.Deserialize<Rgba32>("[1,0.5,0.2,0.25]", new JsonSerializerOptions
        {
            Converters = { new Rgba32JsonConverter() },
        });

        Assert.Equal(0.25f, result.A, 1e-6f);
    }

    /// <summary>
    /// P3: нечисловой элемент не должен сдвигать значения. Старый код двигал
    /// индекс только на токене Number, и [1, null, 3] читался как (1, 3, 0).
    /// </summary>
    [Fact]
    public void JsonConverters_NonNumericElementDoesNotShiftValues()
    {
        Vector3 result = JsonSerializer.Deserialize<Vector3>("[1,null,3]", new JsonSerializerOptions
        {
            Converters = { new Vector3JsonConverter() },
        });

        MathAssert.Equal(1f, result.X, 1e-6f);
        MathAssert.Equal(3f, result.Y, 1e-6f);
        MathAssert.Equal(0f, result.Z, 1e-6f);
    }

    /// <summary>
    /// P2: AngleBetween нормализует оба операнда. Старый брал to сырым, и
    /// AngleBetween(identity, 0.5 * поворот на 90°) давал 138.6°.
    /// </summary>
    [Fact]
    public void Quaternion_AngleBetweenIgnoresQuaternionLength()
    {
        Quaternion turned = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        Quaternion scaled = new(turned.X * 0.5f, turned.Y * 0.5f, turned.Z * 0.5f, turned.W * 0.5f);

        MathAssert.Equal(Angle.FromDegrees(90f), QuaternionExtensions.AngleBetween(Quaternion.Identity, turned), 1e-2);
        MathAssert.Equal(Angle.FromDegrees(90f), QuaternionExtensions.AngleBetween(Quaternion.Identity, scaled), 1e-2);
    }

    /// <summary>
    /// P2: нулевой кадр не должен писать NaN в скорость. Старый код считал
    /// velocity = 0/0, и NaN оказывался в ref-параметре навсегда.
    /// </summary>
    [Fact]
    public void Interpolation_SmoothDampKeepsVelocityFiniteOnZeroDeltaTime()
    {
        float velocity = 0f;

        float result = Interpolation.SmoothDamp(5f, 5f, 0.3f, 10f, 0f, ref velocity);

        MathAssert.Equal(5f, result, 1e-5f);
        Assert.False(float.IsNaN(velocity), $"Скорость стала NaN: {velocity}.");
    }

    /// <summary>
    /// P2: та же проверка с ненулевой скоростью — она тоже должна уцелеть.
    /// </summary>
    [Fact]
    public void Interpolation_SmoothDampRecoversAfterZeroDeltaTime()
    {
        float velocity = 3f;

        Interpolation.SmoothDamp(5f, 5f, 0.3f, 10f, 0f, ref velocity);
        float result = Interpolation.SmoothDamp(5f, 8f, 0.3f, 10f, 0.016f, ref velocity);

        Assert.False(float.IsNaN(result), $"Результат стал NaN: {result}.");
        Assert.True(result > 5f && result < 8f, "Значение обязано двигаться к цели.");
    }

    /// <summary>
    /// P2: AABB нулевой площади — это коллайдер-линия, а не пустое значение.
    /// При сравнении &lt;= он исчезал из Union и выпадал из широкой фазы.
    /// </summary>
    [Fact]
    public void Aabb2_ZeroAreaBoxSurvivesUnion()
    {
        Aabb2 line = new(new Vector2(0f, 0f), new Vector2(0f, 10f));

        Assert.False(line.IsEmpty, "Вырожденный по X AABB — это линия, а не пустое значение.");
        Assert.Equal(line, line.Union(Aabb2.Empty));
        Assert.Equal(line, Aabb2.Empty.Union(line));
    }

    /// <summary>
    /// P2: пустой AABB остаётся пустым и по-прежнему поглощается объединением.
    /// </summary>
    [Fact]
    public void Aabb2_EmptyStaysEmptyAfterIsEmptyChange()
    {
        Aabb2 bounds = new(new Vector2(1f, 1f), new Vector2(3f, 4f));

        Assert.True(Aabb2.Empty.IsEmpty);
        Assert.False(Aabb2.Empty.Contains(Vector2.Zero));
        Assert.False(Aabb2.Empty.Intersects(bounds));
        Assert.False(bounds.Intersects(Aabb2.Empty));
        Assert.Equal(bounds, Aabb2.Empty.Union(bounds));
        Assert.Equal(bounds, bounds.Union(Aabb2.Empty));
    }

    /// <summary>
    /// P2: отрицательный размер допустим, и границы обязаны его учитывать.
    /// Старый код отдавал Left = X, поэтому прямоугольник, выходящий за
    /// границу, считался содержащимся.
    /// </summary>
    [Fact]
    public void Rect_NegativeSizeIsNormalizedInBounds()
    {
        Rect inner = new(12f, 2f, -4f, 4f);

        MathAssert.Equal(8f, inner.Left, 1e-5f);
        MathAssert.Equal(12f, inner.Right, 1e-5f);
        Assert.False(new Rect(0f, 0f, 10f, 10f).Contains(inner), "Прямоугольник за границей не содержится.");
    }

    /// <summary>
    /// P2: операции с прямоугольником обязаны работать с нормализованными
    /// границами при отрицательном размере.
    /// </summary>
    [Fact]
    public void Rect_NegativeSizeWorksWithIntersectionAndUnion()
    {
        Rect flipped = new(12f, 2f, -4f, 4f);
        Rect outer = new(0f, 0f, 10f, 10f);

        Assert.True(outer.Intersects(flipped));
        Rect intersection = outer.Intersection(flipped);
        MathAssert.Equal(8f, intersection.Left, 1e-5f);
        MathAssert.Equal(2f, intersection.Top, 1e-5f);

        Rect union = outer.Union(flipped);
        MathAssert.Equal(12f, union.Right, 1e-5f);
    }

    /// <summary>
    /// P3: границы пересечения включительные, как у Aabb2 и RectU.
    /// </summary>
    [Fact]
    public void Rect_TouchingEdgeCountsAsIntersection()
    {
        Rect left = new(0f, 0f, 10f, 10f);
        Rect right = new(10f, 0f, 10f, 10f);

        Assert.True(left.Intersects(right), "Касание ребром должно считаться пересечением.");
        Assert.False(left.Intersects(new Rect(10.5f, 0f, 5f, 5f)));
    }

    /// <summary>
    /// P2: размах диапазона считается в long. При диапазоне шире int.MaxValue
    /// старая разность int переполнялась, и возвращалось произвольное значение,
    /// в том числе отрицательное.
    /// </summary>
    [Fact]
    public void Random_NextIntHandlesRangeWiderThanIntMaxValue()
    {
        XorShift64Star random = new(20251008UL);

        for (int step = 0; step < 500; step++)
        {
            int value = random.NextInt(int.MinValue, int.MaxValue);

            Assert.True(
                value >= int.MinValue && value < int.MaxValue,
                $"Получено {value}, что вне диапазона [int.MinValue; int.MaxValue).");
        }
    }

    /// <summary>
    /// P3: коллинеарный отрезок впереди начала луча пересекается с ним. Старый
    /// код отбрасывал его как параллельный.
    /// </summary>
    [Fact]
    public void Ray2_CollinearSegmentInFrontIsIntersected()
    {
        Ray2 ray = new(Vector2.Zero, Vector2.UnitX);

        Assert.True(ray.Intersects(new Segment2(new Vector2(5f, 0f), new Vector2(9f, 0f))));
    }

    /// <summary>
    /// P3: коллинеарный отрезок за началом луча не пересекается.
    /// </summary>
    [Fact]
    public void Ray2_CollinearSegmentBehindIsNotIntersected()
    {
        Ray2 ray = new(Vector2.Zero, Vector2.UnitX);

        Assert.False(ray.Intersects(new Segment2(new Vector2(-9f, 0f), new Vector2(-5f, 0f))));
        Assert.False(ray.Intersects(new Segment2(new Vector2(5f, 1f), new Vector2(9f, 1f))), "Параллельный сдвинутый отрезок не пересекается.");
    }

    /// <summary>
    /// P3: вырожденный отрезок-точка на луче пересекается с ним.
    /// </summary>
    [Fact]
    public void Ray2_DegenerateSegmentOnRayIsIntersected()
    {
        Ray2 ray = new(Vector2.Zero, Vector2.UnitX);

        Assert.True(ray.Intersects(new Segment2(new Vector2(3f, 0f), new Vector2(3f, 0f))));
        Assert.False(ray.Intersects(new Segment2(new Vector2(-3f, 0f), new Vector2(-3f, 0f))));
    }

    /// <summary>
    /// P2-1: вырожденный отрезок вне прямой луча обязан быть промахом, и чем
    /// дальше точка от начала луча, тем грубее ошибка была.
    /// <para>
    /// Старый код считал расстояние вычитанием <c>|offset|² − along²</c>. Обе
    /// величины порядка квадрата расстояния до точки, поэтому разность теряла все
    /// значащие цифры и давала ровно ноль всякий раз, когда точка лежала на
    /// прямой с точностью до накопленной ошибки. Фактический порог рос линейно с
    /// расстоянием, примерно как <c>along · 2⁻¹²·⁵</c>: измерено 2.4e-4 метра на
    /// одном метре и 3.2 метра на десяти километрах.
    /// </para>
    /// <para>
    /// Смещения подобраны так, чтобы вход был однозначным с обеих сторон:
    /// боковое смещение не меньше чем в десять раз выше нового порога и не больше
    /// чем вдвое ниже старого. Тогда промах не зависит от того, на каком разряде
    /// остановится округление, и тест не может стать полосой неопределённости.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(1f, 5e-5f)]
    [InlineData(10f, 5e-4f)]
    [InlineData(60f, 2e-4f)]
    [InlineData(100f, 1e-3f)]
    [InlineData(1000f, 1e-3f)]
    [InlineData(10000f, 1e-2f)]
    public void Ray2_DegenerateSegmentOffTheLineIsMissed(float distance, float lateral)
    {
        Ray2 ray = new(Vector2.Zero, Vector2.UnitX);
        Vector2 point = new(distance, lateral);

        Assert.False(
            ray.Intersects(new Segment2(point, point)),
            $"Точка в {lateral:E1} м от прямой луча на расстоянии {distance:E1} м не должна быть попаданием.");
    }

    /// <summary>
    /// P2-1 (обратная сторона): отмена вычитания не должна была превратить
    /// попадания в промахи. Точка строго на прямой луча обязана остаться
    /// попаданием на любом расстоянии, включая километры, где координаты
    /// округлены с шагом больше микрона.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(60f)]
    [InlineData(1000f)]
    [InlineData(10000f)]
    public void Ray2_DegenerateSegmentOnTheLineStaysHitAtAnyDistance(float distance)
    {
        Ray2 ray = new(Vector2.Zero, Vector2.UnitX);
        Vector2 point = new(distance, 0f);

        Assert.True(
            ray.Intersects(new Segment2(point, point)),
            $"Точка на прямой луча на расстоянии {distance:E1} м обязана быть попаданием.");
    }

    /// <summary>
    /// P2-1: обе ветви обязаны отвечать на один вопрос одинаково.
    /// <para>
    /// Ветвь вырожденного отрезка и ветвь невырожденного спрашивают одно и то
    /// же — лежит ли отрезок на прямой лча в пределах допуска — но прежние
    /// критерии отвечали по-разному, и вердикт зависел от того, вырожден ли
    /// отрезок. Отрезок-точка и отрезок на той же прямой обязаны давать один
    /// вердикт при любой длине.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(0f, true)]
    [InlineData(1e-7f, true)]
    [InlineData(1e-6f, true)]
    [InlineData(1.1e-6f, false)]
    [InlineData(1e-4f, false)]
    [InlineData(1e-2f, false)]
    [InlineData(1f, false)]
    public void Ray2_DegenerateSegmentAgreesWithLongSegmentOnTheSameLine(float lateral, bool expected)
    {
        Ray2 ray = new(Vector2.Zero, Vector2.UnitX);
        Vector2 point = new(5f, lateral);

        Assert.True(
            expected == ray.Intersects(new Segment2(point, point)),
            $"Отрезок-точка на боковом смещении {lateral:E1} м ответил неверно.");
        foreach (float length in new[] { 1e-4f, 1e-2f, 1f })
        {
            Assert.True(
                expected == ray.Intersects(new Segment2(point, point + new Vector2(length, 0f))),
                $"Отрезок длиной {length:E1} на боковом смещении {lateral:E1} м ответил иначе, чем отрезок-точка.");
        }
    }

    /// <summary>
    /// P2-1: точка позади начала луча не пересекается с ним даже при
    /// километровом удалении, где отмена вычитания давала ложное попадание.
    /// </summary>
    [Theory]
    [InlineData(60f, 1e-3f)]
    [InlineData(1000f, 1f)]
    [InlineData(10000f, 1f)]
    public void Ray2_DegenerateSegmentBehindOriginIsMissedAtAnyDistance(float distance, float behind)
    {
        Ray2 ray = new(Vector2.Zero, Vector2.UnitX);
        Vector2 point = new(-behind, 0f);

        Assert.False(
            ray.Intersects(new Segment2(point, point)),
            $"Точка на {behind:E1} м позади начала луча, на расстоянии {distance:E1} м, не должна быть попаданием.");
    }

    /// <summary>
    /// P2-2: подсказка из задачи, то есть полученная масштабированием вектора,
    /// а не введённая константой.
    /// <para>
    /// Прежний код сравнивал промежуточный результат с точным нулём. Этот
    /// результат получается вычитанием, поэтому в точности нулём он обращался
    /// только когда скалярное произведение нормали с собой равнялось ровно
    /// единице, то есть примерно у половины направлений; у остальных он был
    /// порядка 1e-7, проверка проходила насквозь, и <c>Normalize</c> усиливала
    /// ошибку округления в миллион раз. Подсказка, введённая константой,
    /// всегда непараллельна вектору и всегда проходила; подсказка из настоящей
    /// задачи параллельна по построению.
    /// </para>
    /// <para>
    /// Проверяется именно <c>|dot|</c>, а не ненулевая длина: прежний код
    /// возвращал единичный вектор, просто направленный почти вдоль исходного.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(1f, 2f, 3f, 1f)]
    [InlineData(1f, 2f, 3f, 2f)]
    [InlineData(1f, 2f, 3f, -1f)]
    [InlineData(1f, 2f, 3f, 1e-3f)]
    [InlineData(0.2672612f, -0.5345225f, 0.8017837f, 1f)]
    [InlineData(0.2672612f, -0.5345225f, 0.8017837f, 3f)]
    [InlineData(0.2672612f, -0.5345225f, 0.8017837f, -2f)]
    [InlineData(-0.6f, 0.8f, 0f, 1f)]
    [InlineData(1f, 1f, 1f, 1f)]
    [InlineData(1f, 1f, 1f, 7f)]
    public void Perpendicular_HintFromScaledVectorStaysOrthogonal(float x, float y, float z, float scale)
    {
        Vector3 vector = new(x, y, z);
        Vector3 unit = Vector3.Normalize(vector);

        Vector3 result = vector.Perpendicular(vector * scale);

        Assert.True(
            MathF.Abs(Vector3.Dot(unit, result)) < 1e-5f,
            $"Подсказка vector*{scale} дала результат с |dot| = {MathF.Abs(Vector3.Dot(unit, result)):E3}.");
        MathAssert.Equal(1f, result.Length());
    }

    /// <summary>
    /// P2-2: подсказка под малым углом к вектору. Прежний код нормализовал
    /// почти вырожденную разность и возвращал направление, почти совпадающее с
    /// исходным: измерено <c>|dot| = 0.99</c> при угле 1e-7 рада и <c>1.0</c> при
    /// угле нулевом, когда на выходе возвращался сам исходный вектор.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(1e-9f)]
    [InlineData(1e-7f)]
    [InlineData(1e-6f)]
    [InlineData(1e-5f)]
    [InlineData(1e-4f)]
    [InlineData(1e-3f)]
    public void Perpendicular_StaysOrthogonalForNearlyParallelHint(float angle)
    {
        Vector3 vector = new(0.2672612f, -0.5345225f, 0.8017837f);
        Vector3 unit = Vector3.Normalize(vector);
        Vector3 axis = Vector3.Normalize(Vector3.Cross(unit, Vector3.UnitZ));
        (float sin, float cos) = MathF.SinCos(angle);

        Vector3 result = vector.Perpendicular((unit * cos) + (axis * sin));

        Assert.True(
            MathF.Abs(Vector3.Dot(unit, result)) < 1e-5f,
            $"Подсказка под углом {angle:E1} рада дала |dot| = {MathF.Abs(Vector3.Dot(unit, result)):E3}.");
        MathAssert.Equal(1f, result.Length());
    }

    /// <summary>
    /// P2-2: базис не должен «дышать», когда подсказка слегка меняется.
    /// <para>
    /// Для движка это важнее точности в отдельной точке: из перпендикуляра
    /// строится базис, и если направление скачет вместе с округлением подсказки,
    /// базис скачет вместе с ним, то есть даёт видимое дрожание. Прежний код на
    /// параллельной подсказке давал разброс направления до 2 радиан, то есть
    /// результат зависел от шума округления полностью.
    /// </para>
    /// </summary>
    [Fact]
    public void Perpendicular_DirectionDoesNotFollowHintRounding()
    {
        DeterministicRandom random = new(0x3C6EF372FE94F82BUL);
        double worstSpread = 0.0;

        for (int i = 0; i < 20000; i++)
        {
            Vector3 vector = random.NextUnitVector();
            Vector3 reference = vector.Perpendicular(vector);

            for (int step = 1; step <= 8; step++)
            {
                float jitter = 1f + (step % 2 == 0 ? 1f : -1f) * step * 1e-7f;
                Vector3 result = vector.Perpendicular(vector * jitter);
                worstSpread = Math.Max(worstSpread, ChordDistance(result, reference));
            }
        }

        Assert.True(
            worstSpread < 1e-6,
            $"Параллельная подсказка с округлением дала разброс направления {worstSpread:E3} рад.");
    }

    /// <summary>
    /// P2-2: обратная сторона. Метод не должен «чиниться» постоянным
    /// возвратом базовой оси: когда подсказка образует разрешимый угол с
    /// вектором, она обязана учитываться, иначе базис не поворачивается вместе с
    /// движением и спрайты «прилипают» к осям.
    /// <para>
    /// Идеальный ответ на подсказку под углом <c>a</c> — это её проекция на
    /// плоскость, то есть <c>dot(результат, подсказка) = sin(a)</c>.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void Perpendicular_FollowsResolvableHint(float angle)
    {
        Vector3 vector = new(0.2672612f, -0.5345225f, 0.8017837f);
        Vector3 unit = Vector3.Normalize(vector);
        Vector3 axis = Vector3.Normalize(Vector3.Cross(unit, Vector3.UnitZ));
        (float sin, float cos) = MathF.SinCos(angle);
        Vector3 hint = (unit * cos) + (axis * sin);

        Vector3 result = vector.Perpendicular(hint);
        float followed = Vector3.Dot(Vector3.Normalize(hint), result);

        Assert.True(
            followed >= MathF.Sin(angle) - 1e-4f,
            $"Подсказка под углом {angle} рада не была учтена: dot = {followed:F6} при ожидаемом {MathF.Sin(angle):F6}.");
    }

    /// <summary>
    /// P2-2: подсказка произвольной длины не должна ни ломать перпендикулярность,
    /// ни ронять результат в нечисловое значение. Проверяются и заведомо малые,
    /// и заведомо большие длины, а также <c>NaN</c> и бесконечность: результат
    /// обязан остаться единичным конечным перпендикуляром.
    /// </summary>
    [Theory]
    [InlineData(1e-7f)]
    [InlineData(1e-20f)]
    [InlineData(1f)]
    [InlineData(1e20f)]
    [InlineData(1e30f)]
    public void Perpendicular_ExtremeHintLengthStillGivesUnitPerpendicular(float magnitude)
    {
        Vector3 vector = new(1f, 2f, 3f);
        Vector3 unit = Vector3.Normalize(vector);

        Vector3 result = vector.Perpendicular(vector * magnitude);

        Assert.True(
            float.IsFinite(result.X) && float.IsFinite(result.Y) && float.IsFinite(result.Z),
            $"Подсказка длиной {magnitude:E1} дала не конечный результат {result}.");
        MathAssert.Equal(1f, result.Length());
        Assert.True(
            MathF.Abs(Vector3.Dot(unit, result)) < 1e-5f,
            $"Подсказка длиной {magnitude:E1} дала |dot| = {MathF.Abs(Vector3.Dot(unit, result)):E3}.");
    }

    /// <summary>
    /// P2-2: нечисловая подсказка не должна выпускать <c>NaN</c> в базис.
    /// Прежний код нормализовал результат вычитания, а вычитание с нечисловым
    /// слагаем даёт нечисловой результат, и он выходил из метода как есть.
    /// </summary>
    [Theory]
    [InlineData(float.NaN, 0f, 0f)]
    [InlineData(0f, float.NaN, 0f)]
    [InlineData(0f, 0f, float.NaN)]
    [InlineData(float.PositiveInfinity, 0f, 0f)]
    [InlineData(0f, float.NegativeInfinity, 0f)]
    public void Perpendicular_NonFiniteHintDoesNotEscape(float x, float y, float z)
    {
        Vector3 vector = new(1f, 2f, 3f);
        Vector3 unit = Vector3.Normalize(vector);

        Vector3 result = vector.Perpendicular(new Vector3(x, y, z));

        Assert.True(
            float.IsFinite(result.X) && float.IsFinite(result.Y) && float.IsFinite(result.Z),
            $"Нечисловая подсказка ({x}, {y}, {z}) дала результат {result}.");
        MathAssert.Equal(1f, result.Length());
        Assert.True(MathF.Abs(Vector3.Dot(unit, result)) < 1e-5f, "Результат не перпендикулярен.");
    }

    // ==================================================================
    // P3-1. Три MoveTowards, и только один проверяет знак шага.
    //
    // Причина одна: maxStep * maxStep положителен и при отрицательном
    // maxStep, поэтому проверка «не перескакиваем» проходит, а деление идёт
    // с отрицательным множителем.

    /// <summary>
    /// P3-1: неположительный шаг не должен двигать значение от цели.
    /// </summary>
    /// <param name="step">Шаг.</param>
    /// <remarks>
    /// Дискриминирующий вход: при положительном шаге те же числа дают движение
    /// к цели, то есть проверка не проходит по нечувствительности.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(-5f)]
    [InlineData(-1e-7f)]
    [InlineData(-1000f)]
    public void Interpolation_MoveTowardsWithNonPositiveStepDoesNotMove(float step)
    {
        float result = Interpolation.MoveTowards(0f, 10f, step);

        Assert.Equal(0f, result);
        Assert.True(BitConverter.SingleToInt32Bits(result) != NegativeZeroBits, "Возвращён знаковый минус.");
    }

    /// <summary>
    /// P3-1: страховка от переусердствования — положительный шаг двигает к цели.
    /// </summary>
    /// <param name="step">Шаг.</param>
    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(5f, 5f)]
    [InlineData(10f, 10f)]
    [InlineData(1000f, 10f)]
    public void Interpolation_MoveTowardsWithPositiveStepStillMovesTowardTarget(float step, float expected)
    {
        float result = Interpolation.MoveTowards(0f, 10f, step);

        Assert.Equal(expected, result);
        Assert.True(StepRespected(0f, result, step), "Шаг превышен.");
    }

    /// <summary>
    /// P3-1: векторный вариант не должен разворачивать смещение.
    /// </summary>
    /// <param name="step">Шаг.</param>
    /// <remarks>
    /// Возвращается смещение, а не позиция, поэтому «не двигаться» — это нулевой
    /// вектор. Прежнее поведение давало <c>(−5, −0)</c>, то есть движение от цели
    /// вместе с развёрнутым знаком нуля по неиспользуемой оси.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(-5f)]
    [InlineData(-1e-7f)]
    public void Vector2_MoveTowardsWithNonPositiveStepGivesZero(float step)
    {
        Vector2 result = Vector2.Zero.MoveTowards(new Vector2(10f, 0f), step);

        Assert.Equal(Vector2.Zero, result);
        Assert.Equal(0, BitConverter.SingleToInt32Bits(result.X));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(result.Y));
    }

    /// <summary>
    /// P3-1: то же для трёх измерений.
    /// </summary>
    /// <param name="step">Шаг.</param>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(-5f)]
    [InlineData(-1e-7f)]
    public void Vector3_MoveTowardsWithNonPositiveStepGivesZero(float step)
    {
        Vector3 result = Vector3.Zero.MoveTowards(new Vector3(10f, 0f, 0f), step);

        Assert.Equal(Vector3.Zero, result);
        Assert.Equal(0, BitConverter.SingleToInt32Bits(result.Y));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(result.Z));
    }

    /// <summary>
    /// P3-1: страховка — векторный шаг не превышается и не разворачивается.
    /// </summary>
    /// <param name="step">Шаг.</param>
    [Theory]
    [InlineData(1f)]
    [InlineData(5f)]
    [InlineData(1e6f)]
    public void Vector3_MoveTowardsWithPositiveStepStillMovesTowardTarget(float step)
    {
        Vector3 result = Vector3.Zero.MoveTowards(new Vector3(10f, 0f, 0f), step);

        Assert.True(result.X > 0f, "Смещение направлено от цели.");
        Assert.True(result.Length() <= step + 1e-3f, $"Длина смещения {result.Length()} превышает шаг {step}.");
    }

    /// <summary>
    /// P3-1: неположительный шаг не проходит и через <c>NaN</c>-защиту — то есть
    /// методы ведут себя одинаково на всех неположительных шагах.
    /// </summary>
    /// <remarks>
    /// <c>NaN</c> проверку <c>maxStep &lt;= 0</c> не проходит, и это осознанно:
    /// нечисловой шаг — ошибка вызывающего, которая должна быть видна, а не
    /// заменяться молчаливым нулём. Проверка фиксирует именно эту договорённость,
    /// чтобы её не «починили» вместе с чем-то ещё.
    /// </remarks>
    [Fact]
    public void MoveTowards_NonFiniteStepIsNotSilentlyTurnedIntoZero()
    {
        Assert.True(float.IsNaN(Interpolation.MoveTowards(0f, 10f, float.NaN)));
        Assert.True(float.IsNaN(Vector2.Zero.MoveTowards(new Vector2(10f, 0f), float.NaN).X));
        Assert.True(float.IsNaN(Vector3.Zero.MoveTowards(new Vector3(10f, 0f, 0f), float.NaN).X));
    }

    // ==================================================================
    // P3-2. Aabb2 не обрабатывает пустое значение, Aabb3 обрабатывает.

    /// <summary>
    /// P3-2: пустой <c>Aabb2</c> обязан вести себя как пустой <c>Aabb3</c>.
    /// </summary>
    /// <remarks>
    /// Проверка идёт по обоим типам сразу, а не по ожидаемым значениям. Так она
    /// не может разойтись с <c>Aabb3</c> снова: если правка одного из них
    /// изменит смысл пустого значения, упадёт этот тест, а не новый.
    /// <para>
    /// Дискриминирующий вход: на непустом параллелепипеде все шесть величин
    /// одинаковы у обоих типов, то есть проверка проходит и на прежнем коде.
    /// Именно пустое значение отличает их.
    /// </para>
    /// </remarks>
    [Fact]
    public void Aabb2_EmptyBehavesLikeEmptyAabb3()
    {
        Aabb2 empty2 = Aabb2.Empty;
        Aabb3 empty3 = Aabb3.Empty;
        Vector2 point2 = new(1f, 2f);
        Vector3 point3 = new(1f, 2f, 3f);

        Assert.True(empty2.IsEmpty, "Aabb2.Empty обязан быть пустым.");
        Assert.True(empty3.IsEmpty, "Aabb3.Empty обязан быть пустым.");

        // Центр: у пустого значения ноль, а не NaN.
        Assert.Equal(Vector2.Zero, empty2.Center);

        // Ближайшая точка и расстояние: исходная точка и ноль, а не -∞ и +∞.
        Assert.Equal(point2, empty2.ClosestPoint(point2));
        Assert.Equal(0f, empty2.DistanceTo(point2));

        // Расширение не оживает.
        Assert.True(empty2.Expand(Vector2.One).IsEmpty, "Expand оживил пустой Aabb2.");

        // Объединение поглощает пустое значение и возвращает второй операнд.
        Aabb2 ordinary = new(point2, point2 + new Vector2(1f, 1f));
        Assert.Equal(ordinary, empty2.Union(ordinary));

        // Принадлежность точки и параллелепипеда — одинаково у обоих типов.
        Assert.Equal(empty3.Contains(point3), empty2.Contains(point2));
        Assert.Equal(empty3.Contains(Aabb3.Empty), empty2.Contains(Aabb2.Empty));

        // Все шесть величин совпадают с поведением Aabb3 на своём входе: у каждого
        // типа своя размерность, поэтому сравниваются не точки, а вердикты.
        Assert.Equal(empty3.Center, new Vector3(empty2.Center.X, empty2.Center.Y, 0f));
        Assert.Equal(empty3.DistanceTo(point3), empty2.DistanceTo(point2));
        Assert.Equal(empty3.Contains(point3), empty2.Contains(point2));
        Assert.Equal(empty3.Expand(Vector3.One).IsEmpty, empty2.Expand(Vector2.One).IsEmpty);
        Assert.Equal(empty3.Union(new Aabb3(point3, point3 + Vector3.One)).IsEmpty, ordinary.IsEmpty);
    }

    /// <summary>
    /// P3-2: страховка — на непустом параллелепипеде типы не расходятся.
    /// </summary>
    [Fact]
    public void Aabb2_NonEmptyBehavesLikeNonEmptyAabb3()
    {
        Vector2 point2 = new(1f, 2f);
        Vector3 point3 = new(1f, 2f, 3f);
        Aabb2 box2 = new(point2, point2 + new Vector2(2f, 3f));
        Aabb3 box3 = new(point3, point3 + new Vector3(2f, 3f, 0f));

        Assert.Equal(box3.IsEmpty, box2.IsEmpty);
        Assert.Equal(box3.Center.X == 0f, box2.Center.X == 0f && box2.Center.Y == 0f);
        Assert.Equal(box3.ClosestPoint(point3).X == 0f, box2.ClosestPoint(new Vector2(-5f, 9f)).X == 0f);
        Assert.Equal(0f, box2.DistanceTo(point2));
    }

    /// <summary>
    /// P3-2: центр пустого <c>Aabb2</c> не должен быть <c>NaN</c>.
    /// </summary>
    /// <remarks>
    /// Отдельно от проверки на равенство с <c>Aabb3</c>, потому что <c>NaN</c>
    /// не равен ничему, включая себя, и проверка «оба типа дают NaN» прошла бы.
    /// </remarks>
    [Fact]
    public void Aabb2_EmptyCenterIsNotNaN()
    {
        Vector2 center = Aabb2.Empty.Center;

        Assert.True(float.IsFinite(center.X) && float.IsFinite(center.Y), $"Центр пустого Aabb2 равен {center}.");
    }

    /// <summary>
    /// P3-2: расстояние до пустого <c>Aabb2</c> не должно быть бесконечным.
    /// </summary>
    /// <remarks>
    /// <c>±∞</c> ломает сортировку и сравнение: значение, которое больше всего,
    /// оказывается «дальним» от любой точки, включая точку самого бокса.
    /// </remarks>
    [Fact]
    public void Aabb2_EmptyDistanceIsFinite()
    {
        float distance = Aabb2.Empty.DistanceTo(new Vector2(1f, 2f));

        Assert.True(float.IsFinite(distance), $"Расстояние до пустого Aabb2 равно {distance}.");
    }

    /// <summary>
    /// P3-2: два аналогичных типа обязаны отвечать на переставленные границы одинаково.
    /// </summary>
    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(-1f, -2f)]
    [InlineData(3f, -3f)]
    public void Aabb2_ConstructorRejectsSwappedBoundsLikeAabb3(float min, float max)
    {
        Assert.Throws<ArgumentException>(() => new Aabb2(new Vector2(min, min), new Vector2(max, max)));
        Assert.Throws<ArgumentException>(() => new Aabb3(new Vector3(min, min, 0f), new Vector3(max, max, 0f)));
    }

    /// <summary>
    /// P3-2: страховка — упорядоченные границы по-прежнему принимаются.
    /// </summary>
    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(-1f, 1f)]
    [InlineData(0f, 0f)]
    public void Aabb2_ConstructorAcceptsOrderedBounds(float min, float max)
    {
        Aabb2 box = new(new Vector2(min, min), new Vector2(max, max));

        Assert.Equal(new Vector2(min, min), box.Min);
        Assert.Equal(new Vector2(max, max), box.Max);
    }

    /// <summary>
    /// P3-2: пустой параллелепипед не содержится ни в чём, включая себя.
    /// </summary>
    /// <remarks>
    /// Найдено обратным ходом правки: переставленные границы пустого значения
    /// удовлетворяли сравнениям <c>+inf &gt;= Min</c> и <c>−inf &lt;= Max</c> в
    /// любом контейнере, то есть пустой бокс считался содержащимся в пустом.
    /// <c>Aabb3</c> от такого защищён, <c>Aabb2</c> — нет.
    /// </remarks>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Aabb2_EmptyIsContainedInNothing(bool firstIsEmpty, bool secondIsEmpty)
    {
        Aabb2 first = firstIsEmpty ? Aabb2.Empty : new Aabb2(new Vector2(0f), new Vector2(1f));
        Aabb2 second = secondIsEmpty ? Aabb2.Empty : new Aabb2(new Vector2(0f), new Vector2(1f));

        Assert.False(first.Contains(second), $"Пустой Aabb2 содержится в пустом: {first} ⊇ {second}.");
    }

    /// <summary>
    /// P3-2: страховка от регрессии — вложенный параллелепипед содержится.
    /// </summary>
    [Fact]
    public void Aabb2_ContainsStillWorksForNestedBoxes()
    {
        Aabb2 outer = new(new Vector2(0f), new Vector2(10f, 10f));
        Aabb2 inner = new(new Vector2(1f, 2f), new Vector2(3f, 4f));

        Assert.True(outer.Contains(inner));
        Assert.False(inner.Contains(outer));
    }

    /// <summary>
    /// P3-2: <c>Expand</c> не должен перевернуться после запрета перестановки в конструкторе.
    /// </summary>
    /// <param name="amount">Отступ по каждой оси.</param>
    /// <remarks>
    /// Прежний код полагался на то, что конструктор переставляет границы: при
    /// отступе больше половины размера наивные min/max дали бы бокс, который
    /// больше исходного. Теперь перестановки нет, поэтому схлопывание должно
    /// идти в центр — иначе конструктор бросил бы исключение на законном вызове.
    /// </remarks>
    [Theory]
    [InlineData(3f, 1f)]
    [InlineData(-1f, -2f)]
    [InlineData(10f, 10f)]
    [InlineData(-10f, -10f)]
    [InlineData(2f, 2f)]
    public void Aabb2_ExpandCollapsesInsteadOfFlipping(float x, float y)
    {
        Aabb2 box = new(new Vector2(0f), new Vector2(4f, 4f));
        Aabb2 expanded = box.Expand(new Vector2(x, y));

        Assert.False(expanded.IsEmpty, $"Expand({x}, {y}) перевернул параллелепипед: {expanded}.");
        Assert.True(expanded.Min.X <= expanded.Max.X && expanded.Min.Y <= expanded.Max.Y);
    }

    /// <summary>
    /// P3-2: <c>FromRect</c> обязан принимать прямоугольник с отрицательным размером.
    /// </summary>
    /// <param name="size">Размер, который может быть отрицательным.</param>
    /// <remarks>
    /// <c>Rect</c> отрицательный размер допускает и сам его нормализует: <c>Left</c>,
    /// <c>Top</c>, <c>Right</c> и <c>Bottom</c> упорядочивают координаты сами. Значит
    /// и <c>FromRect</c> обязан упорядочить углы, иначе после запрета перестановки
    /// в конструкторе фабрика начала бы бросать на законных данных.
    /// </remarks>
    [Theory]
    [InlineData(4f, 2f, 1f, 2f, 5f, 4f)]
    [InlineData(-4f, 2f, -3f, 2f, 1f, 4f)]
    [InlineData(4f, -2f, 1f, 0f, 5f, 2f)]
    [InlineData(-4f, -2f, -3f, 0f, 1f, 2f)]
    public void Aabb2_FromRectAcceptsEitherSignOfSize(
        float width,
        float height,
        float expectedMinX,
        float expectedMinY,
        float expectedMaxX,
        float expectedMaxY)
    {
        Rect rect = new(new Vector2(1f, 2f), new Vector2(width, height));
        Aabb2 box = Aabb2.FromRect(rect);

        Assert.Equal(new Vector2(expectedMinX, expectedMinY), box.Min);
        Assert.Equal(new Vector2(expectedMaxX, expectedMaxY), box.Max);
        Assert.False(box.IsEmpty);
    }

    /// <summary>
    /// P3-2: <c>FromCenterAndHalfSize</c> с отрицательной половиной — ошибка вызывающего.
    /// </summary>
    /// <remarks>
    /// Проверка обязательна: после запрета перестановки в конструкторе такой вызов
    /// бросает, и это осознанное изменение поведения. <c>Aabb3</c> отвечает так же.
    /// </remarks>
    [Fact]
    public void Aabb2_FromCenterAndHalfSizeRejectsNegativeHalfSize()
    {
        Assert.Throws<ArgumentException>(() => Aabb2.FromCenterAndHalfSize(Vector2.Zero, new Vector2(-1f, 1f)));
        Assert.Throws<ArgumentException>(() => Aabb3.FromCenterAndHalfSize(Vector3.Zero, new Vector3(-1f, 1f, 1f)));
    }

    // ==================================================================
    // P3-3. Interpolation.Repeat теряет значение на больших величинах.

    /// <summary>
    /// P3-3: остаток на больших величинах обязан совпадать с точным.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="length">Период.</param>
    /// <param name="expected">Точный остаток, посчитанный вне float.</param>
    /// <remarks>
    /// Ожидания получены не рассуждением, а точной целочисленной арифметикой:
    /// <c>float</c> — это дробь со знаменателем в виде степени двойки, поэтому
    /// остаток от деления двух таких чисел есть точный остаток от деления целых,
    /// и его считает <c>BigInteger</c>. Ни <c>float</c>, ни <c>double</c> этот
    /// остаток не дают ровно там, где проверяется дефект.
    /// <para>
    /// Дискриминирующий вход: на прежнем коде все шесть строк возвращали ноль.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1e8f, 7f, 2f)]
    [InlineData(1e9f, 0.3f, 0.26357174f)]
    [InlineData(0.3f, 1e-9f, 4.0550896e-10f)]
    [InlineData(1e10f, 7f, 4f)]
    [InlineData(1e6f, 0.7f, 0.3170299f)]
    [InlineData(123456f, 0.000123f, 1.838943e-5f)]
    public void Repeat_MatchesExactModuloOnLargeValues(float value, float length, float expected)
    {
        Assert.Equal(expected, Interpolation.Repeat(value, length));
    }

    /// <summary>
    /// P3-3: за пределом точного диапазона остаток приблизительный, но не нулевой.
    /// </summary>
    /// <remarks>
    /// Отношение величин здесь 3.3e9, то есть за измеренной границей 1e8. Точный
    /// остаток равен 0.036428273, метод даёт 0.036428213: расхождение 6e-8, то
    /// есть около 16 последних разрядов результата. Причина не в правке, а в том,
    /// что остаток получается вычитанием двух чисел величиной 1e9, и в двойной
    /// точности это вычитание теряет разряды.
    /// <para>
    /// Дискриминирующий вход: прежний путь возвращал здесь ровно ноль, то есть
    /// полный промах, а не приближение.
    /// </para>
    /// </remarks>
    [Fact]
    public void Repeat_BeyondExactRangeIsApproximateButNotZero()
    {
        float result = Interpolation.Repeat(-1e9f, 0.3f);

        Assert.True(MathF.Abs(result - 0.036428273f) <= 1e-7f, $"Repeat(-1e9, 0.3) = {result:R}.");
        Assert.True(result > 0f, "Остаток обнулился вместо приближения.");
    }

    /// <summary>
    /// P3-3: страховка — на малых величинах результат прежний.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="length">Период.</param>
    /// <param name="expected">Ожидаемый остаток.</param>
    [Theory]
    [InlineData(1000.5f, 1f, 0.5f)]
    [InlineData(-1000.5f, 1f, 0.5f)]
    [InlineData(1e7f, 3f, 1f)]
    [InlineData(2.5f, 1f, 0.5f)]
    [InlineData(-2.5f, 1f, 0.5f)]
    [InlineData(0f, 7f, 0f)]
    public void Repeat_KeepsExactResultOnSmallValues(float value, float length, float expected)
    {
        MathAssert.Equal(expected, Interpolation.Repeat(value, length), 1e-6f);
    }

    /// <summary>
    /// P3-3: договорённость о диапазоне обязана выполняться всегда.
    /// </summary>
    /// <remarks>
    /// За пределами 2⁵³ в отношении значений остаток перестаёт быть определённым,
    /// и метод возвращает 0. Это вынужденно: точность теряется настолько, что
    /// любой представитель диапазона одинаково хорош. Но результат обязан быть
    /// конечным и лежать в периоде — иначе вызывающий получает <c>−∞</c> или
    /// число вне диапазона, то есть метод, документированный как
    /// <c>0..length</c>, даёт нечто другое.
    /// <para>
    /// Дискриминирующий вход: при длине 1e-30 прежний путь давал <c>−∞</c>,
    /// потому что <c>value / length</c> переполнялось и <c>Floor</c> давал
    /// бесконечность.
    /// </para>
    /// </remarks>
    [Fact]
    public void Repeat_ResultStaysWithinPeriodForEveryFiniteInput()
    {
        (float Value, float Length)[] cases =
        {
            (1e38f, 3f),
            (-1e38f, 3f),
            (3.4e38f, 1e-30f),
            (-3.4e38f, 1e-30f),
            (1e-45f, 1e-30f),
            (1e20f, 3f),
            (1e30f, 7f),
            (16777216f, 1f),
            (16777217f, 1f),
        };

        foreach ((float value, float length) in cases)
        {
            float result = Interpolation.Repeat(value, length);

            Assert.True(
                float.IsFinite(result) && result >= 0f && result <= length,
                $"Repeat({value:R}, {length:R}) = {result:R}, ожидалось число в диапазоне [0; {length:R}].");
        }
    }

    /// <summary>
    /// P3-3: <c>PingPong</c> наследует исправление, причём со своей стороны.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="length">Половина периода.</param>
    /// <param name="expected">Ожидаемое значение отражения.</param>
    /// <remarks>
    /// Дискриминирующий вход: прежний путь возвращал здесь ровно ноль. Причина не
    /// только в остатке — своё деление на <c>2 * length</c> тоже отбрасывало
    /// дробную часть, поэтому одной правки остатка не хватало.
    /// </remarks>
    [Theory]
    [InlineData(1e8f, 3f, 0.6666667f)]
    [InlineData(1e10f, 3f, 0.6666665f)]
    [InlineData(1e9f, 7f, 0.85714287f)]
    [InlineData(1e10f, 11f, 0.9090909f)]
    public void PingPong_InheritsTheFix(float value, float length, float expected)
    {
        Assert.Equal(expected, Interpolation.PingPong(value, length));
    }

    /// <summary>
    /// P3-3: страховка от переусердствования — на малых величинах <c>PingPong</c>
    /// не изменился.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="length">Половина периода.</param>
    /// <param name="expected">Ожидаемое значение отражения.</param>
    [Theory]
    [InlineData(0f, 1f, 0f)]
    [InlineData(1f, 1f, 1f)]
    [InlineData(2f, 1f, 0f)]
    [InlineData(0.5f, 1f, 0.5f)]
    [InlineData(1.5f, 1f, 0.5f)]
    [InlineData(-0.5f, 1f, 0.5f)]
    [InlineData(3f, 1f, 1f)]
    [InlineData(0.25f, 1f, 0.25f)]
    public void PingPong_KeepsExactResultOnSmallValues(float value, float length, float expected)
    {
        Assert.Equal(expected, Interpolation.PingPong(value, length));
    }

    /// <summary>
    /// P3-3: <c>Repeat01</c> на больших величинах обязан оставаться в диапазоне.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <remarks>
    /// Проверяется не значение, а договорённость о диапазоне, и на это есть
    /// причина. Ненулевого результата у <c>Repeat01</c> на больших величинах
    /// быть не может: при величине порядка 1e7 в <c>float</c> нет разряда меньше
    /// единицы, поэтому дробной части во входном числе нет. Ноль здесь — верный
    /// ответ, а не дефект, и проверка «больше нуля» была бы неверной.
    /// </remarks>
    [Theory]
    [InlineData(1e8f)]
    [InlineData(1e9f)]
    [InlineData(1e10f)]
    [InlineData(-1e8f)]
    public void Repeat01_StaysInRangeOnLargeValues(float value)
    {
        float result = Interpolation.Repeat01(value);

        Assert.True(result >= 0f && result <= 1f, $"Repeat01({value:R}) = {result:R}.");
    }

    /// <summary>
    /// P3-3: неположительный период по-прежнему бросает исключение.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Repeat_StillRejectsNonPositivePeriod(float length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Interpolation.Repeat(1f, length));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interpolation.PingPong(1f, length));
    }

    /// <summary>
    /// Биты числа <c>+0</c>.</summary>
// ==================================================================
    // P3-4. JsonArrayReaderHelper аллоцирует массив строк на каждый вызов.

    /// <summary>
    /// P3-4: объектная форма разбора не должна выделять память.
    /// </summary>
    /// <param name="json">Разбираемый объект.</param>
    /// <param name="expectedX">Ожидаемое X.</param>
    /// <param name="expectedY">Ожидаемое Y.</param>
    /// <param name="expectedZ">Ожидаемое Z.</param>
    /// <remarks>
    /// Проверяется не только отсутствие мусора: значение обязано остаться прежним.
    /// Замена <c>reader.GetString()</c> на <c>ValueTextEquals</c> меняет способ
    /// сравнения имени свойства, то есть затрагивает и разбор, и это легко сломать
    /// вместе с оптимизацией.
    /// <para>
    /// Дискриминирующий вход: массивная форма не аллоцировала ничего и прежде,
    /// поэтому проверка идёт по объектной форме.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("{\"x\":1.5,\"y\":2.5,\"z\":3.5}", 1.5f, 2.5f, 3.5f)]
    [InlineData("{\"z\":3.5,\"x\":1.5}", 1.5f, 0f, 3.5f)]
    [InlineData("{\"x\":1.5,\"extra\":9,\"y\":2.5}", 1.5f, 2.5f, 0f)]
    public void Json_ObjectFormAllocatesNothingAndKeepsValues(
        string json,
        float expectedX,
        float expectedY,
        float expectedZ)
    {
        JsonSerializerOptions options = CreateVectorOptions();

        Vector3 warm = JsonSerializer.Deserialize<Vector3>(json, options);
        MathAssert.Equal(new Vector3(expectedX, expectedY, expectedZ), warm);

        const int Iterations = 20_000;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Iterations; i++)
        {
            _ = JsonSerializer.Deserialize<Vector3>(json, options);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(
            allocated == 0,
            $"Объектная форма разбора выделила {allocated / (double)Iterations:F1} байт на вызов.");
    }

    /// <summary>
    /// P3-4: страховка — массивная форма по-прежнему не аллоцирует.
    /// </summary>
    [Fact]
    public void Json_ArrayFormStillAllocatesNothing()
    {
        JsonSerializerOptions options = CreateVectorOptions();

        _ = JsonSerializer.Deserialize<Vector3>("[1.5,2.5,3.5]", options);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20_000; i++)
        {
            _ = JsonSerializer.Deserialize<Vector3>("[1.5,2.5,3.5]", options);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated == 0, $"Массивная форма выделила {allocated / 20_000.0:F1} байт на вызов.");
    }

    private static JsonSerializerOptions CreateVectorOptions()
    {
        JsonSerializerOptions options = new() { IncludeFields = true };
        options.Converters.Add(new Vector3JsonConverter());
        return options;
    }

    // ==================================================================
    // P3-5. CreateOrthographic2D со значениями по умолчанию режет глубину до метра.

    /// <summary>
    /// P3-5: умолчания обязаны покрывать игровой масштаб глубины.
    /// </summary>
    /// <param name="z">Координата Z вдоль взгляда.</param>
    /// <param name="visible">Ожидается ли точка в диапазоне отсечения.</param>
    /// <remarks>
    /// Дискриминирующий вход: при прежних умолчаниях <c>−1</c> и <c>1</c> в кадр
    /// попадал только метр глубины, и все точки с <c>|z| &gt; 1</c> отсекались.
    /// Здесь проверяются и сто метров, и километр, то есть масштабы, на которых
    /// ошибка видна.
    /// </remarks>
    [Theory]
    [InlineData(-0.5f, true)]
    [InlineData(-1f, true)]
    [InlineData(-10f, true)]
    [InlineData(-100f, true)]
    [InlineData(-999f, true)]
    [InlineData(0f, true)]
    [InlineData(10f, true)]
    [InlineData(100f, true)]
    [InlineData(999f, true)]
    [InlineData(-1001f, false)]
    [InlineData(1001f, false)]
    public void CreateOrthographic2D_DefaultDepthRangeCoversTheWorldScale(float z, bool visible)
    {
        Matrix4x4 projection = MatrixExtensions.CreateOrthographic2D(100f, 50f);
        float ndcZ = Vector4.Transform(new Vector4(0f, 0f, z, 1f), projection).Z;

        Assert.True(
            visible == (ndcZ >= -1f && ndcZ <= 1f),
            $"ndc.z = {ndcZ} при z = {z}, ожидалась видимость {visible}.");
    }

    /// <summary>
    /// P3-5: страховка — умолчания не изменили видимую область по X и Y.
    /// </summary>
    [Fact]
    public void CreateOrthographic2D_DefaultKeepsOriginInCenter()
    {
        Matrix4x4 projection = MatrixExtensions.CreateOrthographic2D(100f, 50f);

        Vector4 origin = Vector4.Transform(new Vector4(0f, 0f, 0f, 1f), projection);
        Vector4 corner = Vector4.Transform(new Vector4(50f, 25f, 0f, 1f), projection);

        MathAssert.Equal(0f, origin.X, 1e-6f);
        MathAssert.Equal(0f, origin.Y, 1e-6f);
        MathAssert.Equal(1f, corner.X, 1e-6f);
        MathAssert.Equal(1f, corner.Y, 1e-6f);
    }

    /// <summary>
    /// P3-5: страховка — явный диапазон задаётся вызывающим как и раньше.
    /// </summary>
    [Fact]
    public void CreateOrthographic2D_ExplicitRangeStillWins()
    {
        Matrix4x4 projection = MatrixExtensions.CreateOrthographic2D(100f, 50f, 0.1f, 100f);

        MathAssert.Equal(-1f, Vector4.Transform(new Vector4(0f, 0f, -0.1f, 1f), projection).Z, 1e-5f);
        MathAssert.Equal(1f, Vector4.Transform(new Vector4(0f, 0f, -100f, 1f), projection).Z, 1e-5f);
        Assert.True(
            Vector4.Transform(new Vector4(0f, 0f, -101f, 1f), projection).Z > 1f,
            "Лишний диапазон не отсёкся.");
    }

    // ==================================================================
    // P3-6. Capsule3 измеряет параллельность в абсолютных единицах,
    // Collision — в относительных.

    /// <summary>
    /// P3-6: вердикт не должен зависеть от масштаба мира.
    /// </summary>
    /// <param name="length">Длина осей.</param>
    /// <param name="radius">Радиус капсул.</param>
    /// <remarks>
    /// Геометрия косая: первая ось лежит вдоль X, вторая повёрнута в плоскости XZ
    /// и сдвинута по Y, то есть прямые скрещиваются и не компланарны. Именно здесь
    /// запасная ветвь «отрезки параллельны» отвечает неверно: для скрещивающихся
    /// прямых ближайшие точки не находятся проекцией вдоль одной прямой.
    /// <para>
    /// Дискриминирующий вход. Расстояние между осями равно <c>0.2 * length</c> при
    /// сумме радиусов <c>2.02 * radius</c>, то есть капсулы перекрываются. Прежний
    /// абсолютный порог насчитывал расстояние на 11.8 % больше истинного и отвечал
    /// «промах»; относительный даёт точное расстояние и отвечает «попадание».
    /// </para>
    /// <para>
    /// Запас в 1 % от касания сделан намеренно: при радиусе ровно в половину
    /// расстояния ответ одинаков при обоих порогах, и проверка ничего бы не
    /// различала.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1e-4f, 1.01e-5f)]
    [InlineData(1e-5f, 1.01e-6f)]
    public void Capsule3_VerdictDoesNotDependOnWorldScale(float length, float radius)
    {
        Capsule3 first = new(Vector3.Zero, new Vector3(length, 0f, 0f), radius);
        Capsule3 second = new(
            new Vector3(length, 0.2f * length, 0f),
            new Vector3(0.6f * length, 0.2f * length, -0.1f * length),
            radius);

        Assert.True(
            first.Intersects(second),
            $"Капсулы длиной {length:R} не пересеклись, хотя расстояние между осями 0.2*length при сумме радиусов {2 * radius:R}.");
    }

    /// <summary>
    /// P3-6: страховка от переусердствования — настоящий промах остаётся промахом.
    /// </summary>
    /// <param name="length">Длина осей.</param>
    [Theory]
    [InlineData(1e-4f)]
    [InlineData(1e-5f)]
    public void Capsule3_TrueMissStaysMiss(float length)
    {
        Capsule3 first = new(Vector3.Zero, new Vector3(length, 0f, 0f), 1e-3f * length);
        Capsule3 second = new(
            new Vector3(length, 5f * length, 0f),
            new Vector3(0.6f * length, 5f * length, -0.1f * length),
            1e-3f * length);

        Assert.False(first.Intersects(second), "Капсулы с расстоянием между осями 5*length пересеклись.");
    }

    // ==================================================================
    // P3-7. SignedDistanceToLine возвращал не расстояние, а расстояние,
    // умноженное на длину нормали.

    /// <summary>
    /// P3-7: честный метод не зависит от длины нормали.
    /// </summary>
    /// <param name="normalX">X нормали.</param>
    /// <param name="normalY">Y нормали.</param>
    /// <param name="expected">Ожидаемое расстояние.</param>
    /// <remarks>
    /// Дискриминирующий вход: нормали длиной 3 и 5 прежний метод умножал ответ на
    /// их длину, то есть давал тройное расстояние там, где требовалось единичное.
    /// </remarks>
    [Theory]
    [InlineData(1f, 0f, 1f)]
    [InlineData(3f, 0f, 1f)]
    [InlineData(0f, 5f, 2f)]
    [InlineData(3f, 4f, 2.2f)]
    [InlineData(0.001f, 0f, 1f)]
    [InlineData(1000f, 0f, 1f)]
    public void SignedDistanceToLine_DoesNotDependOnNormalLength(
        float normalX,
        float normalY,
        float expected)
    {
        Vector2 point = new(1f, 2f);
        Vector2 normal = new(normalX, normalY);

        MathAssert.Equal(expected, point.SignedDistanceToLine(Vector2.Zero, normal), 1e-5f);
    }

    /// <summary>
    /// P3-7: знак задаётся нормалью, а её длина на него не влияет.
    /// </summary>
    /// <param name="normalX">X нормали.</param>
    /// <param name="normalY">Y нормали.</param>
    /// <remarks>
    /// Проверяется не «какая сторона положительная», а противоположность знаков у
    /// противоположных точек: какая именно сторона положительная, задаёт сама
    /// нормаль, и утверждать иное значило бы описывать конкретную нормаль вместо
    /// контракта. Длина нормали в проверку не входит, поэтому при её изменении
    /// знак обязан остаться тем же.
    /// </remarks>
    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(3f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(0f, -4f)]
    [InlineData(2f, 2f)]
    [InlineData(0.3f, -0.9f)]
    public void SignedDistanceToLine_KeepsSignIndependentOfNormalLength(float normalX, float normalY)
    {
        Vector2 rawNormal = new(normalX, normalY);
        Vector2 unitNormal = rawNormal.SafeNormalize();
        Vector2 scaledNormal = rawNormal * 7f;

        Vector2 point = new(1f, 2f);
        Vector2 opposite = -point;

        float first = point.SignedDistanceToLine(Vector2.Zero, unitNormal);
        float second = opposite.SignedDistanceToLine(Vector2.Zero, unitNormal);
        float scaled = point.SignedDistanceToLine(Vector2.Zero, scaledNormal);

        Assert.True(first != 0f, "Точка лежит на прямой.");
        Assert.True(first * second < 0f, $"Знаки не противоположны: {first} и {second}.");
        Assert.Equal(first, scaled, 1e-5f);
    }

    /// <summary>
    /// P3-7: переименованный метод честно называет то, что возвращает.
    /// </summary>
    /// <param name="normalX">X нормали.</param>
    /// <param name="normalY">Y нормали.</param>
    /// <param name="expected">Ожидаемое значение.</param>
    /// <remarks>
    /// Проверяется договорённость нового имени: значение равно скалярному
    /// произведению, то есть расстоянию, умноженному на длину нормали.
    /// </remarks>
    [Theory]
    [InlineData(1f, 0f, 1f)]
    [InlineData(3f, 0f, 3f)]
    [InlineData(0f, 5f, 10f)]
    [InlineData(3f, 4f, 11f)]
    public void SignedLineOffset_ReturnsTheScaledDistance(float normalX, float normalY, float expected)
    {
        Vector2 point = new(1f, 2f);
        Vector2 normal = new(normalX, normalY);

        MathAssert.Equal(expected, point.SignedLineOffset(Vector2.Zero, normal), 1e-5f);
    }

    /// <summary>
    /// P3-7: на единичной нормали оба метода обязаны совпадать.
    /// </summary>
    /// <param name="normalX">X нормали.</param>
    /// <param name="normalY">Y нормали.</param>
    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(0.6f, 0.8f)]
    [InlineData(-0.8f, 0.6f)]
    public void SignedLineOffset_MatchesDistanceOnUnitNormal(float normalX, float normalY)
    {
        Vector2 point = new(3f, -4f);
        Vector2 normal = new(normalX, normalY);

        MathAssert.Equal(
            point.SignedDistanceToLine(Vector2.Zero, normal),
            point.SignedLineOffset(Vector2.Zero, normal),
            1e-6f);
    }

    /// <summary>
    /// P3-7: нулевая нормаль не определена для нормализующего метода.
    /// </summary>
    [Fact]
    public void SignedDistanceToLine_RejectsZeroNormal()
    {
        Vector2 point = new(1f, 2f);

        Assert.Throws<ArgumentException>(() => point.SignedDistanceToLine(Vector2.Zero, Vector2.Zero));
        Assert.Equal(0f, point.SignedLineOffset(Vector2.Zero, Vector2.Zero));
    }

    // ==================================================================
    // P3-8. ToEuler терял точность в полосе шириной около четверти градуса
    // перед блокировкой.

    /// <summary>
    /// Полоса точности тангажа по результату измерения, в градусах.
    /// </summary>
    /// <remarks>
    /// Измерено на 2000 парах рыскания и крена при каждом значении тангажа: худшая
    /// ошибка лежит между 1.1e-5° и 2.2e-5°. Полоса взята с запасом и выражена в
    /// градусах, потому что ошибка сравнивается с углом. Прежний <c>asin</c> на этих
    /// же углах давал от 1.1e-3° до 4.4e-2°, то есть в сотни раз больше полосы.
    /// </remarks>
    private const double PitchToleranceDegrees = 1e-4;

    /// <summary>
    /// P3-8: тангаж у полюса обязан быть точным.
    /// </summary>
    /// <param name="pitchDegrees">Заданный тангаж в градусах.</param>
    /// <remarks>
    /// Проверяются именно углы, а не ориентация: ориентация и до правки
    /// восстанавливалась верно, и проверка на неё прошла бы при любом состоянии
    /// кода. Ориентацию следит отдельная проверка ниже.
    /// </remarks>
    [Theory]
    [InlineData(89f)]
    [InlineData(89.5f)]
    [InlineData(89.8f)]
    [InlineData(89.9f)]
    [InlineData(89.95f)]
    [InlineData(89.99f)]
    [InlineData(90f)]
    [InlineData(-90f)]
    [InlineData(-89.9f)]
    public void ToEuler_PitchStaysAccurateNearThePole(float pitchDegrees)
    {
        for (int i = 0; i < 60; i++)
        {
            Angle yaw = Angle.FromDegrees(i * 6f - 180f);
            Angle roll = Angle.FromDegrees(i * 11f - 330f);
            Angle pitch = Angle.FromDegrees(pitchDegrees);
            Quaternion rotation = QuaternionExtensions.FromEuler(yaw, pitch, roll);

            (Angle _, Angle actual, Angle _) = QuaternionExtensions.ToEuler(rotation);

            double error = Math.Abs(Angle.NormalizeRadians(actual.Radians - pitch.Radians)) * 180.0 / Math.PI;
            Assert.True(
                error <= PitchToleranceDegrees,
                $"Тангаж {pitchDegrees}° восстановлен с ошибкой {error:E3}° при рыскании {yaw.Degrees:F1}° и крене {roll.Degrees:F1}°.");
        }
    }

    /// <summary>
    /// P3-8: страховка — точность на обычных углах не изменилась.
    /// </summary>
    /// <param name="pitchDegrees">Заданный тангаж в градусах.</param>
    [Theory]
    [InlineData(0f)]
    [InlineData(30f)]
    [InlineData(45f)]
    [InlineData(60f)]
    [InlineData(80f)]
    [InlineData(-45f)]
    public void ToEuler_PitchStaysAccurateAwayFromThePole(float pitchDegrees)
    {
        for (int i = 0; i < 20; i++)
        {
            Angle pitch = Angle.FromDegrees(pitchDegrees);
            Quaternion rotation = QuaternionExtensions.FromEuler(
                Angle.FromDegrees(i * 17f - 170f),
                pitch,
                Angle.FromDegrees(i * 23f - 230f));

            (Angle _, Angle actual, Angle _) = QuaternionExtensions.ToEuler(rotation);

            double error = Math.Abs(Angle.NormalizeRadians(actual.Radians - pitch.Radians)) * 180.0 / Math.PI;
            Assert.True(error <= PitchToleranceDegrees, $"Тангаж {pitchDegrees}° восстановлен с ошибкой {error:E3}°.");
        }
    }

    /// <summary>
    /// P3-8: ориентация у полюса обязана восстанавливаться верно.
    /// </summary>
    /// <param name="pitchDegrees">Заданный тангаж в градусах.</param>
    /// <remarks>
    /// Ориентация мерится расстоянием между базисными векторами, а не через
    /// <c>2 * acos(dot)</c>: у <c>acos</c> производная бесконечна в единице, то
    /// есть мера имеет пол около 0.08° и сама не способна увидеть дефект.
    /// </remarks>
    [Theory]
    [InlineData(89.9f)]
    [InlineData(90f)]
    [InlineData(-90f)]
    public void ToEuler_OrientationIsPreservedAtThePole(float pitchDegrees)
    {
        double worst = 0;
        for (int i = 0; i < 60; i++)
        {
            Angle yaw = Angle.FromDegrees(i * 6f - 180f);
            Angle roll = Angle.FromDegrees(i * 11f - 330f);
            Angle pitch = Angle.FromDegrees(pitchDegrees);
            Quaternion rotation = QuaternionExtensions.FromEuler(yaw, pitch, roll);

            (Angle actualYaw, Angle actualPitch, Angle actualRoll) = QuaternionExtensions.ToEuler(rotation);
            Quaternion restored = QuaternionExtensions.FromEuler(actualYaw, actualPitch, actualRoll);

            worst = Math.Max(worst, BasisDistance(rotation, restored));
        }

        Assert.True(worst <= 1e-3, $"Ориентация разошлась на {worst:E3} при тангаже {pitchDegrees}°.");
    }

    /// <summary>
    /// P3-8: страховка от переусердствования — полоса блокировки не изменилась.
    /// </summary>
    /// <param name="pitchDegrees">Заданный тангаж в градусах.</param>
    /// <remarks>
    /// Расширять полосу не следует, и это проверено измерением: ошибка рыскания
    /// растёт как <c>1 / cos(тангаж)</c> независимо от способа извлечения, и
    /// расширение лишь переносит границу договора, не уменьшая ошибку. Здесь
    /// фиксируется контракт: при <c>|pitch| ≥ 89.9184°</c> рыскание равно нулю по
    /// договорённости, и крен при этом определяется однозначно.
    /// </remarks>
    [Theory]
    [InlineData(90f)]
    [InlineData(89.99f)]
    [InlineData(-90f)]
    public void ToEuler_StillForcesZeroYawInTheLockBand(float pitchDegrees)
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(37f),
            Angle.FromDegrees(pitchDegrees),
            Angle.FromDegrees(112f));

        (Angle yaw, Angle _, Angle _) = QuaternionExtensions.ToEuler(rotation);

        Assert.Equal(0f, yaw.Radians);
    }

    /// <summary>
    /// Расстояние между базисными векторами двух поворотов.
    /// </summary>
    /// <param name="first">Первый поворот.</param>
    /// <param name="second">Второй поворот.</param>
    /// <returns>Наибольшее расстояние между соответствующими базисными векторами.</returns>
    private static double BasisDistance(Quaternion first, Quaternion second)
    {
        Vector3[] basis = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
        double worst = 0;
        for (int i = 0; i < 3; i++)
        {
            worst = Math.Max(
                worst,
                Vector3.Distance(Vector3.Transform(basis[i], first), Vector3.Transform(basis[i], second)));
        }

        return worst;
    }

    private const int PositiveZeroBits = 0x00000000;

    /// <summary>Биты числа <c>−0</c>.</summary>
    /// <remarks>
    /// Отдельная константа обязательна: <c>−0f</c> на языке C# не является
    /// отрицательным нулём. Унарный минус на константе ноль сворачивается в
    /// <c>+0</c>, и запись в атрибуте <c>InlineData</c> молча передала бы
    /// положительный ноль, то есть половина проверок потеряла бы смысл, а
    /// анализатор xUnit увидел бы дубликаты. Поэтому входы передаются битами.
    /// </remarks>
    private const int NegativeZeroBits = unchecked((int)0x80000000);

    /// <summary>Биты числа <c>+1</c>.</summary>
    private const int OneBits = 0x3F800000;

    /// <summary>Биты числа <c>−1</c>.</summary>
    private const int MinusOneBits = unchecked((int)0xBF800000);

    /// <summary>Биты числа <c>π</c> в одинарной точности.</summary>
    private const int PiBits = 0x40490FDB;

    /// <summary>Биты числа <c>−π</c> в одинарной точности.</summary>
    private const int MinusPiBits = unchecked((int)0xC0490FDB);

    /// <summary>
    /// P2-3: знак нуля при отрицательном <c>x</c>. По IEEE 754 и C99 F.10.1.4
    /// <c>atan2(±0, x &lt; 0) = ±π</c>.
    /// <para>
    /// Прежняя проверка <c>y &gt;= 0f</c> истинна и для <c>−0</c>, из-за чего
    /// <c>−π</c> превращалось в <c>+π</c>. Ожидания заданы литералами, а не
    /// сравнением с <c>MathF.Atan2</c>: в сборке <c>Fast</c> детерминированный
    /// backend не вызывается, и такое сравнение проверяло бы платформу против
    /// самой себя, то есть проходило бы при любом состоянии кода.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(PositiveZeroBits, OneBits, PositiveZeroBits)]
    [InlineData(NegativeZeroBits, OneBits, NegativeZeroBits)]
    [InlineData(PositiveZeroBits, MinusOneBits, PiBits)]
    [InlineData(NegativeZeroBits, MinusOneBits, MinusPiBits)]
    [InlineData(PositiveZeroBits, PositiveZeroBits, PositiveZeroBits)]
    [InlineData(NegativeZeroBits, PositiveZeroBits, NegativeZeroBits)]
    [InlineData(PositiveZeroBits, NegativeZeroBits, PiBits)]
    [InlineData(NegativeZeroBits, NegativeZeroBits, MinusPiBits)]
    public void Atan2_SignedZeroMatchesStandard(int yBits, int xBits, int expectedBits)
    {
        float y = FromBits(yBits);
        float x = FromBits(xBits);
        float expected = FromBits(expectedBits);

        float actual = Trig.Atan2(y, x);

        Assert.True(
            BitConverter.SingleToInt32Bits(actual) == expectedBits,
            $"atan2({Show(y)}, {Show(x)}) вернул биты 0x{BitConverter.SingleToInt32Bits(actual):X8}, ожидалось 0x{expectedBits:X8} ({expected:R}).");
    }

    /// <summary>
    /// P2-3: обе координаты бесконечны. Отношение <c>y/x</c> не определено,
    /// и без отдельного разбора получался <c>inf/inf = NaN</c>: угол пропадал
    /// целиком в варианте <c>Deterministic</c>, тогда как <c>Fast</c> отвечал
    /// правильно, то есть варианты сборки расходились.
    /// </summary>
    [Theory]
    [InlineData(float.PositiveInfinity, float.PositiveInfinity, 0.25f)]
    [InlineData(float.NegativeInfinity, float.PositiveInfinity, -0.25f)]
    [InlineData(float.PositiveInfinity, float.NegativeInfinity, 0.75f)]
    [InlineData(float.NegativeInfinity, float.NegativeInfinity, -0.75f)]
    public void Atan2_BothInfiniteMatchesStandard(float y, float x, float fractionOfPi)
    {
        float expected = MathF.PI * fractionOfPi;
        float actual = Trig.Atan2(y, x);

        Assert.True(
            BitConverter.SingleToInt32Bits(actual) == BitConverter.SingleToInt32Bits(expected),
            $"atan2({Show(y)}, {Show(x)}) вернул {actual:R}, ожидалось {expected:R}.");
    }

    /// <summary>
    /// P2-3: нечисловые координаты обязаны давать нечисловой результат.
    /// <para>
    /// Это дефект, существовавший до правки знака нуля, найденный обратным
    /// ходом. Для <c>NaN</c> обе проверки <c>x &gt; 0</c> и <c>x &lt; 0</c> ложны
    /// одновременно, управление проваливалось в ветвь «<c>x</c> — ноль со
    /// знаком», и <c>NaN</c> читался там как знаковый ноль: <c>atan2(1, NaN)</c>
    /// отвечал <c>+π/2</c>, а <c>atan2(NaN, NaN)</c> — <c>−π</c>.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(float.NaN, 1f)]
    [InlineData(float.NaN, -1f)]
    [InlineData(1f, float.NaN)]
    [InlineData(-1f, float.NaN)]
    [InlineData(float.NaN, float.NaN)]
    [InlineData(float.NaN, 0f)]
    public void Atan2_NonFiniteArgumentGivesNaN(float y, float x)
    {
        Assert.True(
            float.IsNaN(Trig.Atan2(y, x)),
            $"atan2({Show(y)}, {Show(x)}) вернул {Trig.Atan2(y, x)}, а по IEEE 754 ответ обязан быть NaN.");
    }

    /// <summary>
    /// P2-3: следствие для публичного API. <c>Angle.FromDirection</c> намеренно
    /// не нормализует результат, потому что нормализация здесь не нужна, поэтому
    /// различие знаков нуля доходит до вызывающего: два направления вдоль одной
    /// оси обязаны давать разные углы с разными хешами, и <c>−v</c> обязан давать
    /// <c>−π</c>, а не <c>+π</c>.
    /// </summary>
    [Fact]
    public void Angle_SignedZeroAlongTheSameAxisGivesDifferentValues()
    {
        Angle positive = Angle.FromDirection(new Vector2(-1f, 0f));
        Angle negative = Angle.FromDirection(new Vector2(-1f, -0f));

        Assert.NotEqual(positive, negative);
        Assert.NotEqual(positive.GetHashCode(), negative.GetHashCode());
        MathAssert.NearlyEqual(-Math.PI, negative.Radians, 1e-6);
        MathAssert.NearlyEqual(Math.PI, positive.Radians, 1e-6);
        Assert.Equal(-180f, negative.Degrees, 4);
    }

    /// <summary>
    /// Полоса расхождения с платформой на особых значениях.
    /// <para>
    /// Побитовое совпадение стандарт требует не везде. Знаки нулей, нечисловые
    /// аргументы и случай обеих бесконечностей заданы точно, и они проверяются
    /// побитово отдельными тестами. Остальные пары обязаны попадать в
    /// <see cref="Scalar.Epsilon"/>-полосу точности: на этом наборе измерено
    /// ровно 1.0 последнего разряда, полоса взята с запасом.
    /// </para>
    /// <para>
    /// Источник одного разряда — не путь <c>atan2</c>, а <c>atan</c> на
    /// бесконечном аргументе: там угол восстанавливается сложением
    /// <c>1.57079632679f</c>, и этот литерал округляется на разряд ниже
    /// правильного <c>pi/2</c>. Дефект существовал до правки знака нуля и лежит
    /// внутри общего бюджета точности backend, поэтому здесь он зафиксирован
    /// полосой, а не исправлен молча.
    /// </para>
    /// </summary>
    private const float Atan2SpecialValuesBudget = 2f;

    /// <summary>
    /// P2-3: сверка с платформой на особых значениях возможна только в
    /// детерминированном варианте сборки.
    /// <para>
    /// В сборке <c>Fast</c> <see cref="Trig"/> зовёт <c>MathF.Atan2</c> напрямую,
    /// поэтому сравнение платформы с <see cref="Trig"/> проверяет платформу против
    /// самой себя и всегда проходит. Условное обозначение включается сборкой,
    /// то есть проверка не может молча выпасть из защиты.
    /// </para>
    /// </summary>
    [Fact]
    public void Atan2_SpecialValuesStayWithinBudgetOfPlatform()
    {
        float[] values = { 0f, -0f, 1f, -1f, float.PositiveInfinity, float.NegativeInfinity, float.Epsilon, -float.Epsilon };

#if XENGINE_DETERMINISTIC_MATH
        Assert.True(Trig.IsDeterministic, "Собран детерминированный вариант, признак обязан быть true.");

        double worst = 0.0;
        foreach (float y in values)
        {
            foreach (float x in values)
            {
                double gap = LastDigitGap(MathF.Atan2(y, x), Trig.Atan2(y, x));
                Assert.True(
                    gap <= Atan2SpecialValuesBudget,
                    $"atan2({Show(y)}, {Show(x)}) расходится с платформой на {gap:F0} последнего разряда при полосе {Atan2SpecialValuesBudget}.");
                worst = Math.Max(worst, gap);
            }
        }

        Assert.InRange(worst, 0.0, Atan2SpecialValuesBudget);
#else
        // В быстром варианте сверка бессмысленна, но она обязана быть видна в
        // выводе теста: иначе зелёный прогон читается как покрытие.
        Assert.False(Trig.IsDeterministic, "Собран быстрый вариант, признак обязан быть false.");
#endif
    }

    /// <summary>Восстанавливает число по его битовому образу.</summary>
    /// <param name="bits">Битовый образ float.</param>
    /// <returns>Число, в том числе знаковый ноль.</returns>
    /// <summary>Переводит двумерный вектор в трёхмерный, добавляя ноль по Z.</summary>
    private static Vector3 ToVector3(Vector2 value) => new(value.X, value.Y, 0f);

    /// <summary>Проверяет, что шаг не превышен по модулю разности.</summary>
    private static bool StepRespected(float from, float result, float step)
        => MathF.Abs(result - from) <= step + 1e-4f;

    private static float FromBits(int bits) => BitConverter.Int32BitsToSingle(bits);

    /// <summary>
    /// Расхождение в единицах последнего разряда большего по модулю числа.
    /// </summary>
    /// <param name="first">Первое значение.</param>
    /// <param name="second">Второе значение.</param>
    /// <returns>Число последних разрядов расхождения.</returns>
    private static double LastDigitGap(float first, float second)
    {
        if (BitConverter.SingleToInt32Bits(first) == BitConverter.SingleToInt32Bits(second))
        {
            return 0.0;
        }

        return Math.Abs((double)first - second) / Math.ScaleB(Math.Abs((double)first), -23);
    }

    /// <summary>
    /// Подпись значения для сообщения об ошибке.
    /// </summary>
    /// <param name="value">Значение.</param>
    /// <returns>Подпись, однозначно различающая знаковые нули.</returns>
    /// <remarks>
    /// Знак нуля различается битом, а сравнением с нулём не различается, поэтому
    /// он читается прямо из битов. Остальные значения печатаются с полной
    /// точностью: <c>F0</c> уводил бы в <c>−0</c> наименьший ненормальный
    /// участок, и сообщение об ошибке называло бы его нулём.
    /// </remarks>
    private static string Show(float value)
    {
        if (BitConverter.SingleToInt32Bits(value) == 0)
        {
            return "+0";
        }

        if (BitConverter.SingleToInt32Bits(value) == NegativeZeroBits)
        {
            return "−0";
        }

        return value.ToString("E3");
    }

    /// <summary>
    /// Хордовое расстояние между направлениями. Угол через <c>acos</c> здесь
    /// не годится: у единицы производная <c>acos</c> бесконечна, и мера имеет
    /// пол около 0.03 градуса, то есть не видит расхождений меньше него.
    /// </summary>
    /// <param name="first">Первое направление.</param>
    /// <param name="second">Второе направление.</param>
    /// <returns>Расстояние между направлениями.</returns>
    private static double ChordDistance(Vector3 first, Vector3 second)
    {
        double x = first.X - second.X;
        double y = first.Y - second.Y;
        double z = first.Z - second.Z;
        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    /// <summary>
    /// P3: пустой параллелепипед не содержится ни в чём. Старая проверка
    /// удовлетворялась на переставленных границах и отвечала true.
    /// </summary>
    [Fact]
    public void Aabb3_EmptyIsNotContainedInAnyBox()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, new Vector3(4f, 4f, 4f));

        Assert.False(bounds.Contains(Aabb3.Empty));
        Assert.False(Aabb3.Empty.Contains(Aabb3.Empty));
        Assert.False(Aabb3.Empty.Contains(bounds));
        Assert.True(bounds.Contains(Aabb3.FromCenterAndSize(Vector3.Zero, Vector3.One)));
    }

    /// <summary>
    /// P3: Expand с отрицательным отступом схлопывает параллелепипед в точку,
    /// как это делает Aabb2, а не бросает исключение.
    /// </summary>
    [Fact]
    public void Aabb3_ExpandWithNegativeAmountCollapsesInsteadOfThrowing()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, new Vector3(4f, 4f, 4f));

        Aabb3 collapsed = bounds.Expand(new Vector3(-10f, -10f, -10f));

        MathAssert.Equal(bounds.Center, collapsed.Center, 1e-4f);
        MathAssert.Equal(Vector3.Zero, collapsed.Size, 1e-5f);
    }

    /// <summary>
    /// P3: операции над пустым параллелепипедом не дают NaN.
    /// </summary>
    [Fact]
    public void Aabb3_EmptyOperationsDoNotProduceNaN()
    {
        Aabb3 empty = Aabb3.Empty;

        Assert.False(float.IsNaN(empty.Center.X));
        Assert.False(float.IsNaN(empty.HalfSize.X));
        MathAssert.Equal(new Vector3(1f, 2f, 3f), empty.ClosestPoint(new Vector3(1f, 2f, 3f)), 1e-5f);

        Aabb3 transformed = empty.Transform(Matrix4x4.CreateTranslation(new Vector3(5f, 0f, 0f)));
        Assert.True(transformed.IsEmpty, "Преобразование пустого параллелепипеда остаётся пустым.");
    }

    /// <summary>
    /// P3: переполнение площади бросает исключение вместо отрицательного числа.
    /// </summary>
    [Fact]
    public void RectU_AreaThrowsOnOverflowInsteadOfWrappingNegative()
    {
        RectU region = new(0, 0, 50000, 50000);

        Assert.Throws<OverflowException>(() => region.Area);
    }

    /// <summary>
    /// P3: тот же контракт для границ.
    /// </summary>
    [Fact]
    public void RectU_BoundsThrowOnOverflow()
    {
        RectU region = new(int.MaxValue - 1, 0, 100, 1);

        Assert.Throws<OverflowException>(() => region.Right);
    }

    /// <summary>
    /// P3: RectU в обычных пределах считает как раньше.
    /// </summary>
    [Fact]
    public void RectU_AreaAndBoundsAreUnchangedForNormalRegions()
    {
        RectU region = new(2, 3, 4, 5);

        Assert.Equal(20, region.Area);
        Assert.Equal(5, region.Right);
        Assert.Equal(7, region.Bottom);
    }

    /// <summary>
    /// P3: проверка положительности не должна пропускать NaN и бесконечность.
    /// </summary>
    [Fact]
    public void TexelDensity_RejectsNaNAndInfinity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TexelDensity.RepeatForSize(Vector2.One, float.NaN, Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TexelDensity.RepeatForSize(Vector2.One, float.PositiveInfinity, Vector2.One));
    }

    /// <summary>
    /// P3: набор весов 1e-6 ненулевой, и выбор варианта в нём определён.
    /// Старая проверка по Scalar.Epsilon считала его пустым.
    /// </summary>
    [Fact]
    public void Random_NextWeightedIndexAcceptsTinyNonZeroWeights()
    {
        XorShift64Star random = new(20251008UL);
        float[] weights = [1e-6f, 2e-6f];

        int first = random.NextWeightedIndex(weights);
        Assert.True(first is 0 or 1, $"Получен индекс {first} вне набора.");

        int second = random.NextWeightedIndex(weights);
        Assert.True(second is 0 or 1, $"Получен индекс {second} вне набора.");
    }

    /// <summary>
    /// P3: контракт обещает исключение только при нулевых весах.
    /// </summary>
    [Fact]
    public void Random_NextWeightedIndexStillRejectsAllZeroWeights()
    {
        XorShift64Star random = new(20251008UL);

        Assert.Throws<ArgumentException>(() => random.NextWeightedIndex([0f, 0f]));
    }

    /// <summary>
    /// P3: Remap ограничивает целевым диапазоном, RemapUnclamped продолжает
    /// линейную зависимость.
    /// </summary>
    [Fact]
    public void Interpolation_RemapClampsAndRemapUnclampedDoesNot()
    {
        MathAssert.Equal(0f, Interpolation.Remap(-5f, 0f, 10f, 0f, 100f), 1e-4f);
        MathAssert.Equal(100f, Interpolation.Remap(15f, 0f, 10f, 0f, 100f), 1e-4f);
        MathAssert.Equal(50f, Interpolation.Remap(5f, 0f, 10f, 0f, 100f), 1e-4f);

        MathAssert.Equal(-50f, Interpolation.RemapUnclamped(-5f, 0f, 10f, 0f, 100f), 1e-4f);
        MathAssert.Equal(150f, Interpolation.RemapUnclamped(15f, 0f, 10f, 0f, 100f), 1e-4f);
    }

    /// <summary>
    /// P3: диапазон глубины [-1; 1], как у всех остальных проекций движка.
    /// Ближняя и дальняя плоскости задаются расстояниями, поэтому проверяются
    /// точки на соответствующих отрицательных Z.
    /// </summary>
    [Fact]
    public void MatrixExtensions_CreateOrthographic2DUsesOpenGLDepthRange()
    {
        Matrix4x4 projection = MatrixExtensions.CreateOrthographic2D(100f, 50f, 0.1f, 100f);

        float nearDepth = Vector4.Transform(new Vector4(0f, 0f, -0.1f, 1f), projection).Z;
        float farDepth = Vector4.Transform(new Vector4(0f, 0f, -100f, 1f), projection).Z;

        MathAssert.Equal(-1f, nearDepth, 1e-3f);
        MathAssert.Equal(1f, farDepth, 1e-3f);
    }

    /// <summary>
    /// P3: нулевое капсул-тестовое расстояние не должно проходить за равенство:
    /// сам тест-харнесс обязан отвергать NaN.
    /// </summary>
    [Fact]
    public void MathAssert_NaNDoesNotPassEquality()
    {
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => MathAssert.Equal(1f, float.NaN));
    }

    private static float BruteForceDistance(Segment2 a, Segment2 b, int samples)
    {
        float best = float.MaxValue;
        for (int i = 0; i <= samples; i++)
        {
            Vector2 pointA = Vector2.Lerp(a.A, a.B, (float)i / samples);
            for (int j = 0; j <= samples; j++)
            {
                Vector2 pointB = Vector2.Lerp(b.B, b.A, (float)j / samples);
                best = MathF.Min(best, Vector2.Distance(pointA, pointB));
            }
        }

        return best;
    }
}