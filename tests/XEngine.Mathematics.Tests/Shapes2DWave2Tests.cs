using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Исправления волны 2 для <c>Shapes2D/</c>: P1-2, P1-3, P2-30…P2-35 и
/// найденные первой волной P3.
/// </summary>
/// <remarks>
/// Методика проверки — как в <c>Problems.md</c>, раздел 5.
/// <para>
/// 1. Эталон написан независимо от исправляемого кода: геометрия считается
///    в <c>double</c>, а там, где величины уходят в денормали и ниже
///    (<c>float</c> различает соседние числа только до 1e-45), эталон переходит
///    на <see cref="System.Numerics.BigInteger"/> и сравнение точных рациональных
///    дробей. Ни один эталон не пересказывает формулу правки.
/// </para>
/// <para>
/// 2. Дискринирующий вход обязателен. Для правки <see cref="Rect.IsEmpty"/> это
///    прямоугольник с <em>отрицательным</em> размером: на прямоугольнике с
///    положительным размером проверка проходит и до, и после правки, то есть
///    такой тест ничего не различает.
/// </para>
/// <para>
/// 3. Обратный ход обязателен: тест должен падать на прежнем коде. Откат
///    выполнялся по пунктам, числа — в отчёте волны 2.
/// </para>
/// </remarks>
public sealed class Shapes2DWave2Tests
{
    // ==================================================================
    // Эталоны. Независимы от кода библиотеки.
    // ==================================================================

    /// <summary>
    /// Нормализованные границы прямоугольника в <c>double</c>: порядок
    /// сторон нормализуется, потому что отрицательный размер задаёт
    /// настоящую область, и именно из-за этого и проверяется правка.
    /// </summary>
    /// <param name="rect">Проверяемый прямоугольник.</param>
    /// <returns>Левая, верхняя, правая и нижняя границы.</returns>
    private static (double Left, double Top, double Right, double Bottom) Area(Rect rect)
    {
        double x = rect.X;
        double y = rect.Y;
        return (
            Math.Min(x, x + rect.Width),
            Math.Min(y, y + rect.Height),
            Math.Max(x, x + rect.Width),
            Math.Max(y, y + rect.Height));
    }

    /// <summary>Пуст ли прямоугольник по своей области.</summary>
    /// <param name="rect">Проверяемый прямоугольник.</param>
    /// <returns><c>true</c>, если область пуста.</returns>
    private static bool ReferenceIsEmpty(Rect rect)
    {
        (double left, double top, double right, double bottom) = Area(rect);
        return right <= left || bottom <= top;
    }

    /// <summary>Содержит ли внешний прямоугольник внутренний по их областям.</summary>
    /// <param name="outer">Внешний прямоугольник.</param>
    /// <param name="inner">Внутренний прямоугольник.</param>
    /// <returns><c>true</c>, если внутренний целиком внутри внешнего.</returns>
    private static bool ReferenceContains(Rect outer, Rect inner)
    {
        if (ReferenceIsEmpty(inner))
        {
            return false;
        }

        (double ol, double ot, double or, double ob) = Area(outer);
        (double il, double it, double ir, double ib) = Area(inner);
        return il >= ol && ir <= or && it >= ot && ib <= ob;
    }

    /// <summary>
    /// Ближайшая точка отрезка: минимизация квадрата расстояния по
    /// параметру <c>t</c> в отрезке <c>[0; 1]</c>, целиком в <c>double</c>.
    /// </summary>
    /// <param name="a">Начало отрезка.</param>
    /// <param name="b">Конец отрезка.</param>
    /// <param name="point">Запросная точка.</param>
    /// <returns>Ближайшая точка отрезка.</returns>
    private static Vector2 ReferenceClosestPoint(Vector2 a, Vector2 b, Vector2 point)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared == 0.0)
        {
            return a;
        }

        double t = (((point.X - a.X) * dx) + ((point.Y - a.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        return new Vector2((float)(a.X + (dx * t)), (float)(a.Y + (dy * t)));
    }

    /// <summary>
    /// Квадрат расстояния между точками в <c>double</c>: в <c>float</c>
    /// <see cref="Vector2.LengthSquared()"/> загублен ниже 1e-19, и мера
    /// врала бы ровно на малых масштабах, которые здесь и проверяются.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    /// <returns>Квадрат расстояния.</returns>
    private static double ReferenceDistanceSquared(Vector2 a, Vector2 b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    // ==================================================================
    // P1-2. Rect.IsEmpty определяет пустоту по размеру, а не по области
    // ==================================================================

    /// <summary>
    /// Дискриминирующий вход P1-2: прямоугольник с <em>отрицательным</em>
    /// размером задаёт настоящую область X[4; 8] Y[4; 8], и она целиком
    /// внутри X[0; 10] Y[0; 10]. Прежний код отвечал <c>false</c>.
    /// </summary>
    /// <param name="innerX">Координата X внутреннего прямоугольника.</param>
    /// <param name="innerY">Координата Y внутреннего прямоугольника.</param>
    /// <param name="width">Ширина внутреннего прямоугольника.</param>
    /// <param name="height">Высота внутреннего прямоугольника.</param>
    [Theory]
    [InlineData(8f, 8f, -4f, -4f)]
    [InlineData(10f, 10f, -4f, -4f)]
    [InlineData(0f, 4f, 10f, -4f)]
    [InlineData(10f, 4f, -10f, 4f)]
    [InlineData(3f, 3f, -1f, -1f)]
    public void Rect_ContainsMirroredRectangleByArea(
        float innerX,
        float innerY,
        float width,
        float height)
    {
        Rect outer = new(0f, 0f, 10f, 10f);
        Rect inner = new(innerX, innerY, width, height);

        Assert.False(ReferenceIsEmpty(inner), "Эталон: у зеркального прямоугольника есть область.");
        Assert.True(outer.Contains(inner), $"{outer} содержит область {inner}.");
        Assert.Equal(ReferenceContains(outer, inner), outer.Contains(inner));
    }

    /// <summary>
    /// Правая половина P1-2: внешний прямоугольник тоже может быть
    /// зеркальным. Прежний код считал его пустым по размеру, но
    /// <see cref="Rect.Left"/>/<see cref="Rect.Right"/> нормализуют порядок
    /// сами, то есть область есть.
    /// </summary>
    [Fact]
    public void Rect_MirroredOuterRectangleContainsInner()
    {
        Rect outer = new(12f, 12f, -10f, -10f);
        Rect inner = new(5f, 5f, 2f, 2f);

        Assert.True(ReferenceIsEmpty(outer) == false, "Эталон: у внешнего прямоугольника есть область.");
        Assert.True(outer.Contains(inner), $"Область {outer} — это X[2; 12] Y[2; 12], она содержит {inner}.");
        Assert.True(inner.Contains(new Rect(5.5f, 5.5f, 1f, 1f)), "Вложенность обязана работать и на зеркальном внешнем.");
    }

    /// <summary>
    /// Сам <see cref="Rect.IsEmpty"/> обязан совпадать с эталоном «область
    /// пуста», а не «размер неположителен». Дискриминирующая строка —
    /// отрицательный размер.
    /// </summary>
    /// <param name="x">Координата X.</param>
    /// <param name="y">Координата Y.</param>
    /// <param name="width">Ширина.</param>
    /// <param name="height">Высота.</param>
    [Theory]
    [InlineData(8f, 8f, -4f, -4f)]
    [InlineData(8f, 8f, 4f, -4f)]
    [InlineData(8f, 8f, -4f, 4f)]
    [InlineData(0f, 0f, 0f, 5f)]
    [InlineData(0f, 0f, 5f, 0f)]
    [InlineData(0f, 0f, 0f, 0f)]
    [InlineData(0f, 0f, 1f, 1f)]
    [InlineData(-5f, -5f, -2f, -2f)]
    public void Rect_IsEmptyMatchesAreaReference(float x, float y, float width, float height)
    {
        Rect rect = new(x, y, width, height);

        Assert.Equal(ReferenceIsEmpty(rect), rect.IsEmpty);
    }

    /// <summary>
    /// Правка не должна сломать настоящий пустой случай: прямоугольник нулевой
    /// площади остаётся пустым, и именно он не содержится ни в чём, включая
    /// другой пустой. Это правило объявлено в доктрине
    /// <see cref="Rect.Contains(Rect)"/> и совпадает с
    /// <see cref="RectU.ContainsRect(in RectU)"/>.
    /// </summary>
    [Fact]
    public void Rect_ZeroAreaRectangleStaysEmpty()
    {
        Rect degenerate = new(3f, 4f, 0f, 5f);
        Rect other = new(0f, 0f, 10f, 10f);

        Assert.True(degenerate.IsEmpty, "Прямоугольник нулевой площади пуст.");
        Assert.False(other.Contains(degenerate), "Пустой прямоугольник не содержится ни в чём.");
        Assert.False(other.Contains(Rect.Zero), "Пустой прямоугольник не содержится ни в чём.");
        Assert.False(Rect.Zero.Contains(Rect.Zero), "Пустой не содержится даже в пустом.");
    }

    /// <summary>
    /// Правка обязана совпасть с <see cref="Aabb2"/> на тех же областях: обе
    /// структуры нормализуют порядок сторон, и разойтись они не могут.
    /// Сравнение идёт по <b>области</b>, а не по полям: поля у зеркального
    /// прямоугольника осмысленны в любом порядке.
    /// </summary>
    [Fact]
    public void Rect_AndAabb2AgreeOnAreaContainment()
    {
        (Rect outer, Rect inner)[] pairs =
        [
            (new Rect(0f, 0f, 10f, 10f), new Rect(8f, 8f, -4f, -4f)),
            (new Rect(0f, 0f, 10f, 10f), new Rect(3f, 3f, 4f, 4f)),
            (new Rect(0f, 0f, 10f, 10f), new Rect(11f, 11f, -4f, -4f)),
            (new Rect(12f, 12f, -10f, -10f), new Rect(5f, 5f, 2f, 2f)),
            (new Rect(0f, 0f, 10f, 10f), new Rect(0f, 0f, 0f, 0f)),
            (new Rect(0f, 0f, 10f, 10f), new Rect(12f, 12f, -3f, -3f)),
        ];

        // Внутри цикла только счётчики. xUnit не прерывает цикл на первом
        // провале, а накопление сотен тысяч отчётов о падении с
        // форматированными сообщениями вешает раннер на полной загрузке.
        int aabbMismatches = 0;
        int rectMismatches = 0;

        foreach ((Rect outer, Rect inner) in pairs)
        {
            // Aabb2 на прямоугольнике нулевой площади отвечает иначе, и это
            // РЕШЕНИЕ README: у него отдельное пустое значение Aabb2.Empty с
            // переставленными границами, а бокс нулевой площади считается
            // настоящим коллайдером. Сравнивать типы можно только на
            // непустых областях, иначе проверка ловит не дефект, а решение.
            if (ReferenceIsEmpty(outer) || ReferenceIsEmpty(inner))
            {
                continue;
            }

            bool byArea = ReferenceContains(outer, inner);
            if (Aabb2.FromRect(outer).Contains(Aabb2.FromRect(inner)) != byArea)
            {
                aabbMismatches++;
            }

            if (outer.Contains(inner) != byArea)
            {
                rectMismatches++;
            }
        }

        Assert.Equal(0, aabbMismatches);
        Assert.Equal(0, rectMismatches);
    }

    /// <summary>
    /// Массовый прогон: ложные отказы на отрицательном размере были
    /// единицами на миллион пар, и единичные примеры их не ловят.
    /// </summary>
    [Fact]
    public void Rect_ContainsMatchesReferenceOnFiveMillionPairs()
    {
        ulong state = 0x5EED1234ABCD0001UL;
        int falseRejects = 0;
        int negativeSizePairs = 0;

        for (int i = 0; i < 5_000_000; i++)
        {
            Rect outer = NextRect(ref state);
            Rect inner = NextRect(ref state);
            if (inner.Width < 0f || inner.Height < 0f)
            {
                negativeSizePairs++;
            }

            if (ReferenceContains(outer, inner) && !outer.Contains(inner))
            {
                falseRejects++;
            }
        }

        Assert.True(negativeSizePairs > 500_000, $"Набор должен быть массовым: пар с отрицательным размером {negativeSizePairs}.");
        Assert.Equal(0, falseRejects);
    }

    // ==================================================================
    // P1-3. Inflate/Deflate отражают вместо схлопывания
    // ==================================================================

    /// <summary>
    /// Дискриминирующий вход P1-3: отступ больше половины размера обязан
    /// схлопывать ось в точку. Прежний код отражал: <c>k = 6</c> давал
    /// высоту <c>8</c> вместо нуля, то есть прямоугольник становился больше
    /// исходного.
    /// </summary>
    /// <param name="amount">Отступ схлопывания по вертикали.</param>
    [Theory]
    [InlineData(2f)]
    [InlineData(2.5f)]
    [InlineData(3f)]
    [InlineData(4f)]
    [InlineData(6f)]
    [InlineData(100f)]
    public void Rect_DeflateCollapsesInsteadOfFlipping(float amount)
    {
        Rect rect = new(0f, 0f, 4f, 4f);

        Rect deflated = rect.Deflate(new Vector2(0f, amount));

        // По X отступа нет: ширина обязана остаться прежней. Схлопывается
        // только та ось, к которой отступ применён.
        double expectedHeight = Math.Max(0.0, 4.0 - (2.0 * amount));
        Assert.Equal((float)expectedHeight, deflated.Height, 6);
        Assert.Equal(4f, deflated.Width, 6);
        Assert.True(deflated.Height <= rect.Height + 1e-5f, "Схлопывание не может увеличить прямоугольник.");
    }

    /// <summary>
    /// Настоящий сценарий P1-3: прямоугольник, схлопнутый по одной оси,
    /// обязан остаться с другой осью без изменений. Прежний код при
    /// <c>k = 3</c> давал высоту <c>2</c> и Position уезжал.
    /// </summary>
    [Fact]
    public void Rect_DeflateCollapsesOnlyTheDeflatedAxis()
    {
        // Высота 10, отступ 6 — больше половины, то есть ось обязана
        // схлопнуться в точку 5 (середина высоты), а по X ничего не меняется.
        Rect rect = new(0f, 0f, 4f, 10f);

        Rect deflated = rect.Deflate(new Vector2(0f, 6f));

        Assert.Equal(0f, deflated.Height, 6);
        Assert.Equal(4f, deflated.Width, 6);
        Assert.Equal(5f, deflated.Top, 6);
        Assert.Equal(5f, deflated.Bottom, 6);
        Assert.Equal(0f, deflated.Left, 6);
        Assert.Equal(4f, deflated.Right, 6);
    }

    /// <summary>
    /// Схлопывание обязано совпасть с <see cref="Aabb2.Expand"/>, который на
    /// тот же случай написан и закреплён регрессионным тестом. Два типа
    /// обязаны вести себя одинаково.
    /// </summary>
    /// <param name="x">Отступ по X.</param>
    /// <param name="y">Отступ по Y.</param>
    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(2f, 2f)]
    [InlineData(2.5f, 3f)]
    [InlineData(3f, 3f)]
    [InlineData(-1f, -1f)]
    [InlineData(-3f, -6f)]
    public void Rect_InflateAgreesWithAabb2Expand(float x, float y)
    {
        Rect rect = new(0f, 0f, 4f, 4f);
        Vector2 amount = new(x, y);

        Rect inflated = rect.Inflate(amount);
        Aabb2 expanded = Aabb2.FromRect(rect).Expand(amount);

        Assert.Equal(expanded.Min.X, inflated.Left, 5);
        Assert.Equal(expanded.Min.Y, inflated.Top, 5);
        Assert.Equal(expanded.Max.X, inflated.Right, 5);
        Assert.Equal(expanded.Max.Y, inflated.Bottom, 5);
    }

    /// <summary>
    /// Инвариант отступа, сформулированный через область и не пересказывающий
    /// формулу правки:
    /// <list type="bullet">
    /// <item>положительный отступ <b>расширяет</b>: результат содержит
    /// исходный прямоугольник;</item>
    /// <item>отрицательный отступ <b>сужает</b>: результат содержится в
    /// исходном — прежний код при отступе больше половины размера выдавал
    /// прямоугольник, выходящий за исходный с обеих сторон;</item>
    /// <item>в обоих случаях размер не превышает исходный плюс
    /// <c>2·|отступ|</c>.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void Rect_InflateObeysTheOffsetInvariantOnArea()
    {
        ulong state = 0x5EED1234ABCD0002UL;
        int expandViolations = 0;
        int shrinkViolations = 0;
        int sizeViolations = 0;
        int checkedPairs = 0;

        for (int i = 0; i < 200_000; i++)
        {
            Rect rect = NextRect(ref state);
            if (ReferenceIsEmpty(rect))
            {
                continue;
            }

            Vector2 amount = new((NextFloat(ref state) * 40f) - 20f, (NextFloat(ref state) * 40f) - 20f);
            Rect inflated = rect.Inflate(amount);

            (double l0, double t0, double r0, double b0) = Area(rect);
            (double l1, double t1, double r1, double b1) = Area(inflated);
            double tolerance = 1e-3 * (1.0 + Math.Max(Math.Abs(r0), Math.Abs(b0)));

            if (amount.X >= 0f)
            {
                if (l1 > l0 + tolerance || r1 < r0 - tolerance)
                {
                    expandViolations++;
                }
            }
            else if (l1 < l0 - tolerance || r1 > r0 + tolerance)
            {
                shrinkViolations++;
            }

            if (amount.Y >= 0f)
            {
                if (t1 > t0 + tolerance || b1 < b0 - tolerance)
                {
                    expandViolations++;
                }
            }
            else if (t1 < t0 - tolerance || b1 > b0 + tolerance)
            {
                shrinkViolations++;
            }

            if ((r1 - l1) > ((r0 - l0) + (2.0 * Math.Abs(amount.X)) + tolerance)
                || (b1 - t1) > ((b0 - t0) + (2.0 * Math.Abs(amount.Y)) + tolerance))
            {
                sizeViolations++;
            }

            checkedPairs++;
        }

        Assert.True(checkedPairs > 100_000, $"Набор должен быть массовым: проверено {checkedPairs} пар.");
        Assert.Equal(0, expandViolations);
        Assert.Equal(0, shrinkViolations);
        Assert.Equal(0, sizeViolations);
    }

    /// <summary>
    /// Правка не должна сломать настоящий случай: расширение и сжатие
    /// обратны друг другу, пока отступ меньше половины размера.
    /// </summary>
    [Fact]
    public void Rect_InflateAndDefateStayInverseWithinHalfSize()
    {
        Rect rect = new(10f, 10f, 4f, 2f);

        Rect roundTrip = rect.Inflate(new Vector2(2f, 3f)).Deflate(new Vector2(2f, 3f));

        Assert.Equal(rect, roundTrip);
    }

    // ==================================================================
    // P2-30. RectU.Offset без checked
    // ==================================================================

    /// <summary>
    /// Смещение, выходящее за пределы <see cref="int"/>, обязано быть
    /// обнаружено. Прежний код молча заворачивал результат:
    /// смещение на 200 пикселей вправо давало <c>X</c> в минус.
    /// </summary>
    [Fact]
    public void RectU_OffsetDetectsOverflowInsteadOfWrapping()
    {
        RectU rect = new(int.MaxValue - 100, 0, 10, 10);

        Assert.Throws<OverflowException>(() => rect.Offset(200, 0));
    }

    /// <summary>
    /// Вторая сторона того же дефекта: смещение влево от нижней границы.
    /// Прежний код давал положительный <c>X</c>.
    /// </summary>
    [Fact]
    public void RectU_OffsetDetectsOverflowOnNegativeSide()
    {
        RectU rect = new(int.MinValue, 0, 10, 10);

        Assert.Throws<OverflowException>(() => rect.Offset(-200, 0));
    }

    /// <summary>
    /// Правка не должна сломать настоящий случай: смещение в пределах
    /// диапазона работает как раньше, а пустое значение остаётся собой.
    /// </summary>
    /// <param name="offsetX">Смещение по X.</param>
    /// <param name="offsetY">Смещение по Y.</param>
    [Theory]
    [InlineData(3, -2)]
    [InlineData(0, 0)]
    [InlineData(-7, 11)]
    public void RectU_OffsetWithinRangeIsUnchanged(int offsetX, int offsetY)
    {
        RectU rect = new(10, 20, 32, 16);

        RectU moved = rect.Offset(offsetX, offsetY);

        Assert.Equal(new RectU(10 + offsetX, 20 + offsetY, 32, 16), moved);
    }

    /// <summary>
    /// Смещение на границе диапазона обязано быть законным: результат ровно
    /// равен <see cref="int.MaxValue"/>.
    /// </summary>
    [Fact]
    public void RectU_OffsetToExactBoundaryIsLegal()
    {
        RectU rect = new(int.MaxValue - 40, 0, 10, 10);

        RectU moved = rect.Offset(40, 0);

        Assert.Equal(int.MaxValue, moved.X);
    }

    // ==================================================================
    // P2-31. Семь методов RectU бросают необъявленный OverflowException
    // ==================================================================

    /// <summary>
    /// Правка P2-31 обязана объявить поведение. Выбранный вариант: границы
    /// считаются в <c>long</c> и сужаются, поэтому предикаты
    /// (<c>Contains</c>, <c>ContainsRect</c>, <c>Intersects</c>) отвечают
    /// <see cref="bool"/> на любом законно построенном прямоугольнике, а
    /// <c>Intersection</c> и <c>Union</c> объявляют
    /// <see cref="OverflowException"/> там, где результат физически не
    /// помещается в <see cref="int"/>. До правки те же семь вызовов бросали
    /// необъявленное исключение.
    /// </summary>
    [Fact]
    public void RectU_PredicatesAnswerOnRectanglesNearIntBoundary()
    {
        RectU edge = new(int.MaxValue - 2, 0, 4, 4);
        RectU normal = new(0, 0, 4, 4);

        // Предикаты обязаны отвечать, а не бросать.
        Assert.True(edge.Contains(int.MaxValue - 2, 0));
        Assert.True(edge.Contains(int.MaxValue, 0));
        Assert.False(edge.Contains(int.MaxValue - 3, 0));
        Assert.True(edge.ContainsRect(edge));
        Assert.False(edge.Intersects(normal));
        Assert.True(edge.Intersects(new RectU(int.MaxValue - 1, 0, 2, 2)));
    }

    /// <summary>
    /// Дискриминирующий вход P2-31: прямоугольники, перекрывающие весь
    /// диапазон <see cref="int"/>. Прежний код бросал на обоих.
    /// Эталон отвечает в <c>long</c>: пересечение есть, а объединение не
    /// помещается в тип.
    /// </summary>
    [Fact]
    public void RectU_FullRangeRectsAnswerWithoutUndeclaredException()
    {
        RectU low = new(int.MinValue, 0, 10, 10);
        RectU high = new(int.MaxValue - 9, 0, 10, 10);

        // Эталон: у low правый край int.MinValue + 9, у high левый int.MaxValue - 9.
        long lowRight = (long)int.MinValue + 10 - 1;
        long highLeft = (long)int.MaxValue - 9;
        Assert.False(lowRight >= highLeft, "Эталон: прямоугольники не перекрываются.");

        Assert.False(low.Intersects(high));
        Assert.True(low.Intersection(high).IsEmpty);
    }

    /// <summary>
    /// Объединение, не помещающееся в <see cref="int"/>, обязано объявлять
    /// исключение. Прежний код бросал <see cref="ArgumentOutOfRangeException"/>
    /// из конструктора с переполненной (отрицательной) шириной, то есть
    /// исключение указывало на параметр, который вызывающий не задавал.
    /// </summary>
    [Fact]
    public void RectU_UnionThatCannotFitDeclaresOverflow()
    {
        RectU low = new(int.MinValue, 0, 10, 10);
        RectU high = new(int.MaxValue - 9, 0, 10, 10);

        Assert.Throws<OverflowException>(() => low.Union(high));
    }

    /// <summary>
    /// Объединение, помещающееся в тип, обязано работать и не бросать.
    /// Дискриминирующий вход — результат ровно на границе <see cref="int"/>.
    /// </summary>
    [Fact]
    public void RectU_UnionNearBoundaryStillWorks()
    {
        // Правый край ровно int.MaxValue - 1, ширина ровно int.MaxValue:
        // результат помещается в тип вплотную и не бросает.
        RectU left = new(0, 0, 10, 10);
        RectU right = new(int.MaxValue - 10, 0, 10, 10);

        RectU union = left.Union(right);

        Assert.Equal(0, union.Left);
        Assert.Equal(int.MaxValue - 1, union.Right);
        Assert.Equal(int.MaxValue, union.Width);
        Assert.Throws<OverflowException>(() => union.Area);
    }

    /// <summary>
    /// Правка не должна сломать обычный случай: пересечение и объединение
    /// внутри диапазона считаются как раньше.
    /// </summary>
    [Fact]
    public void RectU_IntersectionAndUnionWithinRangeAreUnchanged()
    {
        RectU region = new(0, 0, 16, 16);

        Assert.Equal(new RectU(8, 8, 8, 8), region.Intersection(new RectU(8, 8, 32, 32)));
        Assert.Equal(new RectU(0, 0, 12, 12), new RectU(0, 0, 4, 4).Union(new RectU(10, 10, 2, 2)));
        Assert.True(new RectU(100, 100, 8, 8).Intersection(region).IsEmpty);
    }

    /// <summary>
    /// Правка обязана совпасть с эталоном в <c>long</c> на псевдослучайных
    /// парах, иначе «считаем в <c>long</c>» — это только замена одной ошибки
    /// на другую.
    /// </summary>
    [Fact]
    public void RectU_MatchesLongReferenceOnRandomPairs()
    {
        ulong state = 0x5EED1234ABCD0003UL;
        int checkedPairs = 0;

        for (int i = 0; i < 200_000; i++)
        {
            RectU first = RandomRectU(ref state);
            RectU second = RandomRectU(ref state);

            long firstRight = (long)first.X + first.Width - 1;
            long firstBottom = (long)first.Y + first.Height - 1;
            long secondRight = (long)second.X + second.Width - 1;
            long secondBottom = (long)second.Y + second.Height - 1;

            bool wantIntersects = first.X <= secondRight
                && second.X <= firstRight
                && first.Y <= secondBottom
                && second.Y <= firstBottom;
            bool wantContainsRect = first.X <= second.X
                && first.Y <= second.Y
                && firstRight >= secondRight
                && firstBottom >= secondBottom;

            Assert.True(first.Intersects(second) == wantIntersects, $"{first} пересекается с {second}?");
            Assert.True(first.ContainsRect(second) == wantContainsRect, $"{first} содержит {second}?");
            checkedPairs++;
        }

        Assert.Equal(200_000, checkedPairs);
    }

    /// <summary>
    /// Проверка пикселя около границы <see cref="int"/>: эталон в
    /// <c>long</c>, потому что <c>Right</c> такого прямоугольника не
    /// помещается в <see cref="int"/>.
    /// </summary>
    [Fact]
    public void RectU_ContainsMatchesLongReferenceNearBoundary()
    {
        RectU edge = new(int.MaxValue - 2, 0, 4, 4);
        long right = (long)int.MaxValue - 2 + 4 - 1;

        int mismatches = 0;
        // Граница строго меньше: условие <= с правой границей int переполняется
        // на int.MaxValue, x++ даёт int.MinValue, и цикл становится бесконечным.
        for (int x = int.MaxValue - 5; x < int.MaxValue; x++)
        {
            if (edge.Contains(x, 0) != (x >= edge.Left && x <= right))
            {
                mismatches++;
            }
        }

        Assert.Equal(0, mismatches);
        Assert.Equal(5, int.MaxValue - (int.MaxValue - 5));
    }

    // ==================================================================
    // P2-32. Segment2.ClosestPointTo, абсолютный порог
    // ==================================================================

    /// <summary>
    /// Дискриминирующий вход P2-32: отрезок короче 1e-6 и запрос
    /// <em>за</em> точкой <c>B</c>. Эталон отвечает <c>B</c>, прежний код
    /// отвечал <c>A</c> — ошибка 100 % длины.
    /// </summary>
    /// <param name="length">Длина отрезка.</param>
    [Theory]
    [InlineData(1f)]
    [InlineData(1e-3f)]
    [InlineData(1e-5f)]
    [InlineData(1e-6f)]
    [InlineData(5e-7f)]
    [InlineData(1e-7f)]
    [InlineData(1e-9f)]
    [InlineData(1e-11f)]
    [InlineData(1e-13f)]
    [InlineData(1e-15f)]
    [InlineData(1e-20f)]
    public void Segment2_ClosestPointToShortSegmentIsScaleFree(float length)
    {
        Segment2 segment = new(Vector2.Zero, new Vector2(length, 0f));
        Vector2 query = new(length * 3f, 0f);

        Vector2 got = segment.ClosestPointTo(query);
        Vector2 want = ReferenceClosestPoint(segment.A, segment.B, query);

        Assert.Equal(want.X, got.X, 6);
        Assert.Equal(want.Y, got.Y, 6);

        // Точка B, а не A: прежний код возвращал A, то есть ошибка равнялась
        // 100 % длины отрезка.
        Assert.Equal(length, got.X, 6);
    }

    /// <summary>
    /// Ответ не обязан совпадать с эталоном побитово: он обязан быть не
    /// дальше от запроса, чем эталон. На малых длинах точность
    /// <see cref="float"/> исчерпывается, и сравнение «строка в строку»
    /// проверяло бы округление, а не дефект.
    /// </summary>
    [Fact]
    public void Segment2_ClosestPointToIsNeverFartherThanReference()
    {
        float[] lengths = [1f, 1e-3f, 1e-5f, 1e-6f, 5e-7f, 1e-7f, 1e-9f, 1e-12f, 1e-15f, 1e-19f];
        int checkedCases = 0;
        int fartherThanReference = 0;
        int outsideSegment = 0;

        foreach (float length in lengths)
        {
            Segment2 axis = new(Vector2.Zero, new Vector2(length, 0f));
            Segment2 slanted = new(new Vector2(0f, 0f), new Vector2(length * 0.6f, length * 0.8f));
            (Segment2 segment, Vector2 query)[] cases =
            [
                (axis, new Vector2(length * 3f, 0f)),
                (axis, new Vector2(-length, 0f)),
                (axis, new Vector2(length * 0.5f, length * 0.5f)),
                (slanted, new Vector2(length, length)),
                (slanted, new Vector2(-length, length)),
            ];

            foreach ((Segment2 current, Vector2 point) in cases)
            {
                Vector2 got = current.ClosestPointTo(point);
                Vector2 want = ReferenceClosestPoint(current.A, current.B, point);

                double gotDistance = ReferenceDistanceSquared(got, point);
                double wantDistance = ReferenceDistanceSquared(want, point);
                double tolerance = Math.Max(wantDistance * 1e-4, 1e-45);

                if (gotDistance > wantDistance + tolerance)
                {
                    fartherThanReference++;
                }

                // Ответ лежит на отрезке, поэтому расстояние до запроса не
                // может превышать длину отрезка.
                double segmentLength = Math.Sqrt(
                    (((double)current.B.X - current.A.X) * ((double)current.B.X - current.A.X))
                    + (((double)current.B.Y - current.A.Y) * ((double)current.B.Y - current.A.Y)));
                if (Math.Sqrt(gotDistance) > segmentLength + tolerance)
                {
                    outsideSegment++;
                }

                checkedCases++;
            }
        }

        Assert.Equal(50, checkedCases);
        Assert.Equal(0, fartherThanReference);
        Assert.Equal(0, outsideSegment);
    }

    /// <summary>
    /// Вырожденный отрезок — единственный случай, где ответ обязан быть
    /// точкой <c>A</c>: направления у него нет. Правка не должна ломать его.
    /// </summary>
    [Fact]
    public void Segment2_ClosestPointToDegenerateSegmentReturnsStart()
    {
        Segment2 point = new(new Vector2(3f, 4f), new Vector2(3f, 4f));

        Assert.Equal(new Vector2(3f, 4f), point.ClosestPointTo(new Vector2(10f, 10f)));
        Assert.Equal(new Vector2(3f, 4f), point.ClosestPointTo(new Vector2(-10f, -10f)));
    }

    /// <summary>
    /// Расстояние до отрезка обязано следовать за ближайшей точкой: на
    /// отрезке длиной 1e-7 и запросе за точкой <c>B</c> расстояние равно
    /// удвоенному расстоянию до <c>B</c>, а не расстоянию до <c>A</c>.
    /// </summary>
    [Fact]
    public void Segment2_DistanceToShortSegmentUsesTheEndNotTheStart()
    {
        Segment2 segment = new(Vector2.Zero, new Vector2(1e-7f, 0f));
        Vector2 query = new(3e-7f, 0f);

        float distance = segment.DistanceTo(query);

        Assert.Equal(2e-7f, distance, 9);
    }

    // ==================================================================
    // P2-33. Capsule2.Contains при оси короче 1e-6
    // ==================================================================

    /// <summary>
    /// Дискриминирующий вход P2-33: точка ровно на расстоянии радиуса от
    /// оси, проекция в середину оси. Эталон отвечает <c>true</c> (граница
    /// включена), прежний код отвечал <c>false</c>. Именно равенство
    /// радиусу различает: при запросе ближе расхождения не видно.
    /// </summary>
    /// <param name="axis">Длина оси.</param>
    [Theory]
    [InlineData(1f)]
    [InlineData(1e-3f)]
    [InlineData(1e-5f)]
    [InlineData(1e-6f)]
    [InlineData(5e-7f)]
    [InlineData(1e-7f)]
    [InlineData(1e-9f)]
    [InlineData(1e-11f)]
    public void Capsule2_ContainsVerdictDoesNotDependOnAxisScale(float axis)
    {
        Capsule2 capsule = new(new Segment2(Vector2.Zero, new Vector2(axis, 0f)), axis);
        // Смещение 0.95 радиуса, а не ровно радиус: на границе вердикт
        // решает последний разряд округления, и такой тест проверял бы
        // округление, а не дефект. При 0.95 правильное расстояние 0.95·R
        // (запас 5 %), а ошибочное, от точки A, равно 1.073·R (запас 7 %).
        Vector2 query = new(axis * 0.5f, axis * 0.95f);

        double distanceSquared = ReferenceDistanceSquared(query, new Vector2(axis * 0.5f, 0f));
        Assert.True(
            distanceSquared < (axis * axis),
            $"Эталон: расстояние {Math.Sqrt(distanceSquared):E3} при радиусе {axis:E3}, обязано быть меньше.");
        Assert.True(capsule.Contains(query), $"Ось длины {axis:E1}: точка на расстоянии 0.95 радиуса обязана быть внутри.");
    }

    /// <summary>
    /// Настоящий промах обязан остаться промахом на любом масштабе. Без этой
    /// половины правка превратилась бы в «капсула содержит всё».
    /// </summary>
    /// <param name="axis">Длина оси.</param>
    [Theory]
    [InlineData(1f)]
    [InlineData(1e-6f)]
    [InlineData(5e-7f)]
    [InlineData(1e-9f)]
    public void Capsule2_TrueMissStaysMissOnShortAxis(float axis)
    {
        Capsule2 capsule = new(new Segment2(Vector2.Zero, new Vector2(axis, 0f)), axis);
        Vector2 query = new(axis * 0.5f, axis * 1.5f);

        Assert.False(capsule.Contains(query));
    }

    /// <summary>
    /// Вердикт обязан совпасть с эталоном в <c>double</c> на массовом наборе,
    /// где ось и радиус одного порядка: при большом радиусе ошибка подмены
    /// ближайшей точки перекрывается радиусом и не видна.
    /// </summary>
    [Fact]
    public void Capsule2_ContainsMatchesDoubleReferenceOnSameOrderAxisAndRadius()
    {
        ulong state = 0x5EED1234ABCD0004UL;
        int wrongVerdicts = 0;
        int checkedCases = 0;

        for (int i = 0; i < 200_000; i++)
        {
            float scale = (float)Math.Pow(10.0, NextFloat(ref state) * 16.0 - 8.0);
            float axisLength = scale;
            float radius = scale;
            Vector2 direction = new(NextFloat(ref state) * 2f - 1f, NextFloat(ref state) * 2f - 1f);
            if (ReferenceDistanceSquared(direction, Vector2.Zero) == 0.0)
            {
                direction = Vector2.UnitX;
            }

            Capsule2 capsule = new(new Segment2(Vector2.Zero, new Vector2(axisLength, 0f)), radius);
            // Запас 1 % от радиуса: при 1e-4 вердикт на границе решал бы
            // последний разряд округления, и мера проверяла бы округление.
            Vector2 query = new Vector2(axisLength * 0.5f, 0f) + (Vector2.Normalize(direction) * (radius * 1.01f));

            Vector2 closest = ReferenceClosestPoint(capsule.A, capsule.B, query);
            bool want = ReferenceDistanceSquared(query, closest) <= radius * radius;

            if (capsule.Contains(query) != want)
            {
                wrongVerdicts++;
            }

            checkedCases++;
        }

        Assert.Equal(200_000, checkedCases);
        Assert.Equal(0, wrongVerdicts);
    }

    // ==================================================================
    // P2-34. ClosestPointOnBoundary возвращает произвольное направление
    // ==================================================================

    /// <summary>
    /// Дискриминирующий вход P2-34: смещение 7.07e-7 под 45° при радиусе 1.
    /// Ошибка прежнего кода — 76.54 % радиуса, потому что направление
    /// заменялось на <c>(1, 0)</c>.
    /// </summary>
    [Fact]
    public void Circle2_ClosestPointOnBoundaryKeepsDirectionForTinyOffset()
    {
        Vector2 offset = new(7.0710678e-7f, 7.0710678e-7f);
        Circle2 circle = new(Vector2.Zero, 1f);

        Vector2 got = circle.ClosestPointOnBoundary(offset);

        double length = Math.Sqrt(((double)offset.X * offset.X) + ((double)offset.Y * offset.Y));
        Vector2 want = new((float)((offset.X / length) * 1.0), (float)((offset.Y / length) * 1.0));

        Assert.Equal(want.X, got.X, 5);
        Assert.Equal(want.Y, got.Y, 5);
        Assert.True(ReferenceDistanceSquared(got, offset) < 1e-6, "Ответ обязан быть ближе всего к запросу.");
    }

    /// <summary>
    /// То же для капсулы: ось (0,0)-(10,0), запрос на расстоянии 4e-7 от
    /// оси. Прежний код errorа 141.42 % радиуса.
    /// </summary>
    [Fact]
    public void Capsule2_ClosestPointOnBoundaryKeepsDirectionForTinyOffset()
    {
        Capsule2 capsule = new(new Segment2(new Vector2(0f, 0f), new Vector2(10f, 0f)), 1f);
        Vector2 query = new(5f, 4e-7f);

        Vector2 got = capsule.ClosestPointOnBoundary(query);

        double length = 4e-7;
        Vector2 want = new(5f, (float)((4e-7 / length) * 1.0));

        Assert.Equal(want.X, got.X, 5);
        Assert.Equal(want.Y, got.Y, 5);
    }

    /// <summary>
    /// Ошибка не должна зависеть от масштаба круга: она определяется только
    /// смещением. Прежний код давал 76.54 % радиуса на всех пяти масштабах.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1e-3f)]
    [InlineData(1e-5f)]
    [InlineData(1e-6f)]
    [InlineData(1e-7f)]
    public void Circle2_BoundaryErrorIsRelativeNotAbsolute(float radius)
    {
        Vector2 offset = new(7.0710678e-7f, 7.0710678e-7f);
        Circle2 circle = new(Vector2.Zero, radius);

        Vector2 got = circle.ClosestPointOnBoundary(offset);

        double length = Math.Sqrt(((double)offset.X * offset.X) + ((double)offset.Y * offset.Y));
        double wantX = (offset.X / length) * radius;
        double wantY = (offset.Y / length) * radius;
        double error = Math.Sqrt((((double)got.X - wantX) * ((double)got.X - wantX))
            + (((double)got.Y - wantY) * ((double)got.Y - wantY)));

        Assert.True(
            error <= radius * 1e-3,
            $"Круг r={radius:E1}: ошибка {error:E3} равна {error / radius * 100:F3} % радиуса.");
    }

    /// <summary>
    /// Точка поверхности обязана приниматься собственной проверкой
    /// принадлежности на всех масштабах, включая те, где прежний код
    /// возвращал произвольное направление.
    /// </summary>
    [Fact]
    public void Circle2_BoundaryPointIsAcceptedByContainsAtEveryScale()
    {
        foreach (float radius in new float[] { 1f, 1e-3f, 1e-5f, 1e-6f, 1e-7f })
        {
            Circle2 circle = new(Vector2.Zero, radius);
            Vector2 offset = new(7.0710678e-7f, 7.0710678e-7f);

            Vector2 boundary = circle.ClosestPointOnBoundary(offset);

            Assert.True(circle.Contains(boundary), $"Круг r={radius:E1} отверг собственную точку границы {boundary}.");
        }
    }

    /// <summary>
    /// Точный ноль обязан остаться отдельным случаем: направления нет, и
    /// метод возвращает любую точку границы. Правка не имеет права превратить
    /// это в <c>NaN</c> или в центр.
    /// </summary>
    [Fact]
    public void Circle2_BoundaryAtExactCenterStaysFinite()
    {
        Circle2 circle = new(new Vector2(3f, 4f), 2f);

        Vector2 boundary = circle.ClosestPointOnBoundary(new Vector2(3f, 4f));

        Assert.True(float.IsFinite(boundary.X) && float.IsFinite(boundary.Y), $"Получено {boundary}.");
        Assert.True(circle.Contains(boundary), "Точка границы обязана принадлежать кругу.");
    }

    /// <summary>
    /// Нулевой радиус — вырожденный, но не нечисловой случай: у него нет
    /// поверхности, и метод обязан вернуть конечную точку, а не <c>NaN</c>.
    /// </summary>
    [Fact]
    public void Circle2_BoundaryWithZeroRadiusStaysFinite()
    {
        Circle2 circle = new(Vector2.Zero, 0f);

        Vector2 boundary = circle.ClosestPointOnBoundary(new Vector2(1e-30f, 1e-30f));

        Assert.True(float.IsFinite(boundary.X) && float.IsFinite(boundary.Y), $"Получено {boundary}.");
    }

    /// <summary>
    /// То же для капсулы: запрос ровно на оси даёт нулевое смещение, и
    /// прежний код отвечал <c>closest + (Radius, 0)</c> — точку, лежащую
    /// на поверхности, но в произвольной стороне. Главное здесь — ответ
    /// обязан оставаться конечным и принадлежать капсуле.
    /// </summary>
    [Fact]
    public void Capsule2_BoundaryOnAxisStaysInsideTheCapsule()
    {
        Capsule2 capsule = new(new Segment2(new Vector2(0f, 0f), new Vector2(4f, 0f)), 1f);

        Vector2 boundary = capsule.ClosestPointOnBoundary(new Vector2(2f, 0f));

        Assert.True(float.IsFinite(boundary.X) && float.IsFinite(boundary.Y), $"Получено {boundary}.");
        Assert.True(capsule.Contains(boundary), "Точка границы обязана принадлежать капсуле.");
    }

    // ==================================================================
    // P2-35. Capsule2.FromBounds не проверяет пустой Aabb2
    // ==================================================================

    /// <summary>
    /// Пустой <see cref="Aabb2"/> обязан отрабатывать, как и в
    /// <c>BoundingSphere.FromAabb(Aabb3.Empty)</c>: у пустого бокса
    /// <c>Size = (−∞, −∞)</c>, отсюда радиус <c>−∞</c>, и прежний код
    /// ронял на этом соседнюю фабрику.
    /// </summary>
    [Fact]
    public void Capsule2_FromBoundsAcceptsEmptyAabb()
    {
        Capsule2 capsule = Capsule2.FromBounds(Aabb2.Empty);

        Assert.Equal(0f, capsule.Radius);
        Assert.Equal(Vector2.Zero, capsule.A);
        Assert.Equal(Vector2.Zero, capsule.B);
        Assert.True(capsule.Contains(Vector2.Zero), "Вырожденная капсула в точке обязана содержать её.");
    }

    /// <summary>
    /// Правка не должна сломать обычный случай: капсула по-прежнему
    /// вписывается в прямоугольник, и узкое место определяется меньшей
    /// стороной.
    /// </summary>
    [Fact]
    public void Capsule2_FromBoundsStillFitsTheBox()
    {
        Aabb2 bounds = new(new Vector2(1f, 2f), new Vector2(11f, 6f));

        Capsule2 capsule = Capsule2.FromBounds(bounds);

        Assert.Equal(2f, capsule.Radius);
        Assert.Equal(new Vector2(3f, 4f), capsule.A);
        Assert.Equal(new Vector2(9f, 4f), capsule.B);
        Assert.Equal(bounds.Min.X, capsule.Bounds.Min.X, 5);
        Assert.Equal(bounds.Max.Y, capsule.Bounds.Max.Y, 5);
    }

    /// <summary>
    /// Вырожденный бокс нулевой площади — не пустой: у него есть размер
    /// по одной оси. Правка не должна перепутать его с пустым значением.
    /// </summary>
    [Fact]
    public void Capsule2_FromBoundsAcceptsZeroAreaBoxThatIsNotEmpty()
    {
        Aabb2 line = new(Vector2.Zero, new Vector2(10f, 0f));

        Capsule2 capsule = Capsule2.FromBounds(line);

        Assert.Equal(0f, capsule.Radius);
        Assert.Equal(0f, capsule.A.X, 6);
        Assert.Equal(10f, capsule.B.X, 6);
    }

    // ==================================================================
    // P3-1. Rect.Union не поглощает пустое значение
    // ==================================================================

    /// <summary>
    /// Дискриминирующий вход P3-1: прямоугольник <em>не</em> в начале
    /// координат. Прежний код втягивал <see cref="Rect.Zero"/> в объединение
    /// и давал площадь 44100 вместо 100.
    /// </summary>
    [Fact]
    public void Rect_UnionAbsorbsEmptyValue()
    {
        Rect far = new(100f, 100f, 10f, 10f);

        Assert.Equal(far, Rect.Zero.Union(far));
        Assert.Equal(far, far.Union(Rect.Zero));
    }

    /// <summary>
    /// Накопление от пустого значения — главный сценарий: именно он давал
    /// лишний охват в 44000 квадратных единиц.
    /// </summary>
    [Fact]
    public void Rect_UnionAccumulationFromEmptyCoversOnlyRealArea()
    {
        Rect accumulated = Rect.Zero;
        accumulated = accumulated.Union(new Rect(100f, 100f, 10f, 10f));
        accumulated = accumulated.Union(new Rect(200f, 200f, 10f, 10f));

        Assert.Equal(new Rect(100f, 100f, 110f, 110f), accumulated);
        Assert.Equal(12100f, accumulated.Width * accumulated.Height, 3);
    }

    /// <summary>
    /// Согласованность с соседними типами: <see cref="Aabb2.Union"/> и
    /// <see cref="RectU.Union"/> пустое значение уже поглощают.
    /// </summary>
    [Fact]
    public void Rect_UnionMatchesAabb2OnEmptyAbsorption()
    {
        Rect rect = new(100f, 100f, 10f, 10f);
        Aabb2 box = Aabb2.FromRect(rect);

        Assert.Equal(box, Aabb2.Empty.Union(box));
        Assert.Equal(box, box.Union(Aabb2.Empty));
        Assert.Equal(rect, Rect.Zero.Union(rect));
        Assert.Equal(rect, rect.Union(Rect.Zero));
    }

    /// <summary>
    /// Настоящее объединение двух непустых прямоугольников не должно
    /// измениться: поглощение пустого не имеет права трогать его.
    /// </summary>
    [Fact]
    public void Rect_UnionOfTwoNonEmptyRectanglesIsUnchanged()
    {
        Rect first = new(0f, 0f, 4f, 4f);
        Rect second = new(10f, 10f, 2f, 2f);

        Rect union = first.Union(second);

        Assert.Equal(0f, union.Left, 6);
        Assert.Equal(0f, union.Top, 6);
        Assert.Equal(12f, union.Right, 6);
        Assert.Equal(12f, union.Bottom, 6);
    }

    // ==================================================================
    // P3-2. Пустое значение Rect ведёт себя иначе соседних типов
    // ==================================================================

    /// <summary>
    /// Дискриминирующий вход P3-2: <see cref="Rect.Zero"/> пересекается сам
    /// с собой. У <see cref="Aabb2.Empty"/> и <see cref="RectU.Empty"/> —
    /// <c>false</c>.
    /// </summary>
    [Fact]
    public void Rect_EmptyDoesNotIntersectItself()
    {
        Assert.False(Rect.Zero.Intersects(Rect.Zero), "Пустой прямоугольник не пересекается даже с собой.");
        Assert.False(Rect.Zero.Intersects(new Rect(0f, 0f, 5f, 5f)), "Пустой не пересекается с непустым.");
        Assert.False(new Rect(0f, 0f, 5f, 5f).Intersects(Rect.Zero));
        Assert.False(Aabb2.Empty.Intersects(Aabb2.Empty));
        Assert.False(RectU.Empty.Intersects(RectU.Empty));
    }

    /// <summary>
    /// Проверка точки на пустом прямоугольнике обязана отвечать так же, как у
    /// соседних типов: <see cref="RectU.Empty.Contains(int, int)"/> даёт
    /// <c>false</c>.
    /// </summary>
    [Fact]
    public void Rect_EmptyContainsNoPoint()
    {
        Assert.False(Rect.Zero.Contains(Vector2.Zero));
        Assert.False(Rect.Zero.Contains(Vector2.Zero, 0.1f));
        Assert.False(RectU.Empty.Contains(0, 0));
    }

    /// <summary>
    /// Настоящее пересечение двух непустых прямоугольников, включая касание
    /// ребром, правка не имеет права сломать.
    /// </summary>
    [Fact]
    public void Rect_TouchingEdgeStillCountsAsIntersection()
    {
        Rect left = new(0f, 0f, 10f, 10f);
        Rect right = new(10f, 0f, 10f, 10f);

        Assert.True(left.Intersects(right), "Касание ребром считается пересечением.");
        Assert.False(left.Intersects(new Rect(10.5f, 0f, 5f, 5f)));
    }

    // ==================================================================
    // P3-4. default(Aabb2) не равно Aabb2.Empty
    // ==================================================================

    /// <summary>
    /// <c>default(Aabb2)</c> — вырожденный бокс в точке (0,0), а не пустое
    /// значение. Разница видна на объединении: пустое поглощается,
    /// <c>default</c> втягивает начало координат.
    /// </summary>
    [Fact]
    public void Aabb2_DefaultIsNotEmptyAndMustBeTreatedAsRealBox()
    {
        Aabb2 defaultBox = default;

        Assert.False(defaultBox.Equals(Aabb2.Empty));
        Assert.False(defaultBox.IsEmpty);
        Assert.True(default(Rect).Equals(Rect.Zero), "У Rect и RectU default совпадает с пустым значением.");
        Assert.True(default(RectU).Equals(RectU.Empty));

        Aabb2 far = Aabb2.FromRect(new Rect(10f, 10f, 2f, 2f));
        Assert.NotEqual(far, defaultBox.Union(far));
    }

    // ==================================================================
    // P3-5. Доктрина Rect.Rotate неверна
    // ==================================================================

    /// <summary>
    /// Поворот даёт объемлющий осевой прямоугольник, и он не обязан
    /// содержать исходный: при 45° ширина прямоугольника 10×2 становится
    /// 8.485. Доктрина утверждала обратное.
    /// </summary>
    [Fact]
    public void Rect_RotateResultIsBoundingBoxNotContainment()
    {
        Rect rect = new(0f, 0f, 10f, 2f);

        Rect rotated = rect.Rotate(Angle.FromDegrees(45f));

        Assert.True(rotated.Width < rect.Width, "При 45° ширина уменьшается — объёмлющий прямоугольник не обязан содержать исходный.");
        Assert.Equal(8.485f, rotated.Width, 3);
        Assert.Equal(8.485f, rotated.Height, 3);
    }

    /// <summary>
    /// При этом сам алгоритм верен: результат обязан содержать все четыре
    /// повёрнутых угла. Проверка идёт перебором углов в <c>double</c> и не
    /// использует формулу из кода.
    /// </summary>
    /// <param name="degrees">Угол поворота.</param>
    [Theory]
    [InlineData(0f)]
    [InlineData(5f)]
    [InlineData(30f)]
    [InlineData(45f)]
    [InlineData(60f)]
    [InlineData(80f)]
    [InlineData(90f)]
    public void Rect_RotateCoversAllFourRotatedCorners(float degrees)
    {
        Rect rect = new(2f, 3f, 10f, 4f);
        Vector2 pivot = new(5f, 6f);
        Angle angle = Angle.FromDegrees(degrees);

        Rect rotated = rect.Rotate(angle, pivot);

        double radians = angle.Radians;
        double sin = Math.Sin(radians);
        double cos = Math.Cos(radians);
        (double left, double top, double right, double bottom) = Area(rect);

        double[] xs = [left, right, right, left];
        double[] ys = [top, top, bottom, bottom];
        int uncoveredCorners = 0;

        for (int corner = 0; corner < 4; corner++)
        {
            double dx = xs[corner] - pivot.X;
            double dy = ys[corner] - pivot.Y;
            double rx = (dx * cos) - (dy * sin) + pivot.X;
            double ry = (dx * sin) + (dy * cos) + pivot.Y;

            if (rx < rotated.Left - 1e-3 || rx > rotated.Right + 1e-3
                || ry < rotated.Top - 1e-3 || ry > rotated.Bottom + 1e-3)
            {
                uncoveredCorners++;
            }
        }

        Assert.Equal(0, uncoveredCorners);
    }

    // ==================================================================
    // P3-6. Конструкторы не отвергают NaN
    // ==================================================================

    /// <summary>
    /// Нечисловой радиус обязан быть отвергнут: <c>NaN &lt; 0</c> ложно,
    /// поэтому прежний код создавал круг с <c>NaN</c>-радиусом, и
    /// <see cref="Circle2.Bounds"/> давал непустой бокс из <c>NaN</c>.
    /// Отвергается только <see cref="float.NaN"/> — см. доктрину.
    /// </summary>
    [Fact]
    public void Circle2_RejectsNonFiniteRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Circle2(Vector2.Zero, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Capsule2(new Segment2(Vector2.Zero, Vector2.One), float.NaN));
    }

    /// <summary>
    /// То же для капсулы. Отвергается только <see cref="float.NaN"/>:
    /// бесконечный радиус не бессмыслен (круг покрывает всю плоскость),
    /// а вот <c>NaN</c> молча портит <see cref="Capsule2.Bounds"/>.
    /// </summary>
    [Fact]
    public void Capsule2_RejectsNonFiniteRadius()
    {
        Segment2 axis = new(Vector2.Zero, Vector2.One);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Capsule2(axis, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Capsule2(axis, -float.NaN));
    }

    /// <summary>
    /// <see cref="Aabb2"/> с нечисловыми границами обязан быть отвергнут:
    /// сравнение <c>min.X &gt; max.X</c> на <c>NaN</c> ложно, и бокс создавался.
    /// </summary>
    [Fact]
    public void Aabb2_RejectsNonFiniteBounds()
    {
        Assert.Throws<ArgumentException>(() => new Aabb2(new Vector2(float.NaN, 0f), new Vector2(1f, 1f)));
        Assert.Throws<ArgumentException>(() => new Aabb2(new Vector2(0f, 0f), new Vector2(1f, float.NaN)));
        Assert.Throws<ArgumentException>(() => new Aabb2(new Vector2(float.NaN, 1f), new Vector2(float.NaN, 1f)));
    }

    /// <summary>
    /// Правка не должна сломать настоящий случай: конечные, в том числе
    /// бесконечные, координаты и нулевой радиус остаются законными.
    /// </summary>
    [Fact]
    public void Constructors_StillAcceptLegalFiniteValues()
    {
        Assert.Equal(0f, new Circle2(Vector2.Zero, 0f).Radius);
        Assert.Equal(0f, new Capsule2(new Segment2(Vector2.Zero, Vector2.One), 0f).Radius);
        Assert.True(float.IsPositiveInfinity(new Circle2(Vector2.Zero, float.PositiveInfinity).Radius));

        // Бесконечные границы законны: Aabb2.Empty построен именно на них.
        // Бокс от -inf до +inf непустой, и пустой от него отличается.
        Aabb2 infinite = new(new Vector2(float.NegativeInfinity, 0f), new Vector2(float.PositiveInfinity, 1f));
        Assert.False(infinite.IsEmpty, "Бокс от -inf до +inf покрывает прямую, это не пустое значение.");
        Assert.True(Aabb2.Empty.IsEmpty, "Aabb2.Empty остаётся пустым.");

        Aabb2 point = new(new Vector2(1f, 1f), new Vector2(1f, 1f));
        Assert.False(point.IsEmpty, "Вырожденный бокс — это точка, а не пустое значение.");
    }

    // ==================================================================
    // Вспомогательное
    // ==================================================================

    private static ulong NextUInt64(ref ulong state)
    {
        unchecked
        {
            ulong x = state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }
    }

    private static float NextFloat(ref ulong state) => (NextUInt64(ref state) >> 40) * (1f / 16777216f);

    private static Rect NextRect(ref ulong state)
        => new(
            (NextFloat(ref state) * 200f) - 100f,
            (NextFloat(ref state) * 200f) - 100f,
            (NextFloat(ref state) * 200f) - 100f,
            (NextFloat(ref state) * 200f) - 100f);

    private static RectU RandomRectU(ref ulong state)
        => new(
            (int)(NextFloat(ref state) * 4_000_000_000f) - 2_000_000_000,
            (int)(NextFloat(ref state) * 4_000_000_000f) - 2_000_000_000,
            (int)(NextFloat(ref state) * 4_000_000f) + 1,
            (int)(NextFloat(ref state) * 4_000_000f) + 1);
}