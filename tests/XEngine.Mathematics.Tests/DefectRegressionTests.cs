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