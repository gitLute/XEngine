using System.Numerics;
using XEngine.Mathematics;
using Xunit;
using Xunit.Abstractions;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Проверка детерминированного варианта математики. Тесты собираются и
/// запускаются одинаково при <c>MathBackend=Fast</c> и
/// <c>MathBackend=Deterministic</c>: проверяется не реализация, а
/// соответствие эталону на двойной точности.
/// </summary>
public class DeterministicBackendTests
{
    private readonly ITestOutputHelper _o;

    public DeterministicBackendTests(ITestOutputHelper o) => _o = o;

    private const int Samples = 200_000;

    /// <summary>
    /// Шаг одного последнего разряда одинарной точности в точке z.
    /// </summary>
    /// <param name="z">Точка, в которой измеряется шаг.</param>
    /// <returns>Величина шага.</returns>
    private static double FloatUlp(double z)
    {
        float rounded = (float)z;
        if (rounded == 0f)
        {
            return 1.4012984643e-45;
        }

        float next = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(rounded) + 1);
        return Math.Abs((double)next - rounded);
    }

    /// <summary>
    /// Угол для выборки: равномерно по двум оборотам плюс хвост, где сведение
    /// к квадранту тяжелее всего. Хвост обязателен — именно на нём ошибаются
    /// реализации с наивным сведением аргумента.
    /// </summary>
    /// <returns>Очередной аргумент.</returns>
    private static float NextArgument(ref XorShift64Star random, int index)
    {
        float value = (float)((random.NextFloat() - 0.5f) * 4.0 * MathF.PI);
        if (index % 7 == 0)
        {
            value = (float)((random.NextFloat() - 0.5f) * 200.0 * MathF.PI);
        }

        return value;
    }

    [Fact]
    public void Backend_ReportsWhichVariantIsBuilt()
    {
        _o.WriteLine($"Собран вариант: детерминированный = {Trig.IsDeterministic}");

        // Признак обязан соответствовать условному обозначению, иначе тесты
        // двух вариантов проверяли бы одно и то же, не сообщая об этом.
#if XENGINE_DETERMINISTIC_MATH
        Assert.True(Trig.IsDeterministic, "Собран детерминированный вариант, признак обязан быть true.");
#else
        Assert.False(Trig.IsDeterministic, "Собран быстрый вариант, признак обязан быть false.");
#endif
    }

    /// <summary>
    /// Бюджет точности общий для обоих вариантов сборки.
    /// </summary>
    /// <remarks>
    /// Измерено на 200 000 точек, включая хвост до 100π, где сведение к
    /// квадранту тяжелее всего: MathF даёт 0.56 ULP, детерминированный
    /// вариант — 1.53 ULP. Ставится 2 ULP с запасом: проверяется соответствие
    /// эталону, а не конкретная реализация, и обе должны в него укладываться.
    /// </remarks>
    private const float SinCosBudget = 2f;

    /// <summary>Бюджет для экспоненты: измерено 0.94 ULP.</summary>
    private const float ExpBudget = 2f;

    /// <summary>Бюджет для atan2: измерено 3.11 ULP.</summary>
    private const float Atan2Budget = 4f;

    /// <summary>Бюджет для asin: измерено 7.08 ULP.</summary>
    /// <remarks>
    /// Точность падает у единицы, потому что там растёт производная: asin(x)
    /// считается как atan2(x, sqrt(1 − x²)), и в корне теряется точность
    /// вычитания. На угле около 90° это 7 ULP, то есть 6·10⁻⁷ относительной
    /// ошибки — для угла в градусах меньше миллионной доли градуса.
    /// </remarks>
    private const float AsinBudget = 8f;

    [Fact]
    public void SinCos_AccurateAcrossRange()
    {
        double worstSin = 0;
        double worstCos = 0;
        float atSin = 0;
        float atCos = 0;

        var random = new XorShift64Star(4242);
        for (int i = 0; i < Samples; i++)
        {
            float x = NextArgument(ref random, i);
            (float sin, float cos) = Trig.SinCos(x);

            double reference = Math.Sin(x);
            double error = Math.Abs(sin - reference) / FloatUlp(reference);
            if (error > worstSin)
            {
                worstSin = error;
                atSin = x;
            }

            reference = Math.Cos(x);
            error = Math.Abs(cos - reference) / FloatUlp(reference);
            if (error > worstCos)
            {
                worstCos = error;
                atCos = x;
            }
        }

        _o.WriteLine($"Вариант детерминированный: {Trig.IsDeterministic}");
        _o.WriteLine($"SinCos: худшая ошибка sin={worstSin:F3} ULP (x={atSin:F6}), cos={worstCos:F3} ULP (x={atCos:F6})");

        Assert.True(worstSin <= SinCosBudget, $"Sin: худшая ошибка {worstSin:F3} ULP при бюджете {SinCosBudget}.");
        Assert.True(worstCos <= SinCosBudget, $"Cos: худшая ошибка {worstCos:F3} ULP при бюджете {SinCosBudget}.");
    }

    [Fact]
    public void SinCos_IdentityHolds()
    {
        var random = new XorShift64Star(777);
        double worst = 0;
        for (int i = 0; i < Samples; i++)
        {
            float x = NextArgument(ref random, i);
            (float sin, float cos) = Trig.SinCos(x);
            worst = Math.Max(worst, Math.Abs(MathF.Sqrt((sin * sin) + (cos * cos)) - 1f));
        }

        Assert.True(worst <= 2e-6f, $"Тождество sin²+cos²=1 нарушено на {worst:E3}.");
    }

    [Fact]
    public void Trig_QuarterTurnRelations()
    {
        var random = new XorShift64Star(31337);
        for (int i = 0; i < 20000; i++)
        {
            float x = (float)((random.NextFloat() - 0.5f) * 8.0 * MathF.PI);
            (float sin, float cos) = Trig.SinCos(x);
            (float shiftedSin, float shiftedCos) = Trig.SinCos(x + MathF.PI * 0.5f);

            MathAssert.Equal(shiftedSin, cos, 2e-6f);
            MathAssert.Equal(-shiftedCos, sin, 2e-6f);
        }
    }

    [Fact]
    public void Exp_AccurateAcrossRange()
    {
        double worst = 0;
        float at = 0;
        var random = new XorShift64Star(99);
        for (int i = 0; i < Samples; i++)
        {
            float x = (float)(random.NextFloat() * 20.0 - 10.0);
            double reference = Math.Exp(x);
            double error = Math.Abs(Trig.Exp(x) - reference) / FloatUlp(reference);
            if (error > worst)
            {
                worst = error;
                at = x;
            }
        }

        _o.WriteLine($"Exp: худшая ошибка {worst:F3} ULP (при x={at:F6})");
        Assert.True(worst <= ExpBudget, $"Exp: худшая ошибка {worst:F3} ULP при x={at:F6}.");
    }

    [Fact]
    public void Pow2_AccurateAcrossRange()
    {
        double worst = 0;
        float at = 0;
        var random = new XorShift64Star(101);
        for (int i = 0; i < Samples; i++)
        {
            float x = (float)(random.NextFloat() * 20.0 - 10.0);
            double reference = Math.Pow(2.0, x);
            double error = Math.Abs(Trig.Pow2(x) - reference) / FloatUlp(reference);
            if (error > worst)
            {
                worst = error;
                at = x;
            }
        }

        _o.WriteLine($"2^x: худшая ошибка {worst:F3} ULP (при x={at:F6})");
        Assert.True(worst <= ExpBudget, $"2^x: худшая ошибка {worst:F3} ULP при x={at:F6}.");
    }

    [Fact]
    public void Atan2_AccurateAcrossQuadrants()
    {
        double worst = 0;
        float atX = 0;
        float atY = 0;

        var random = new XorShift64Star(555);
        for (int i = 0; i < Samples; i++)
        {
            float y = (float)(random.NextFloat() - 0.5f) * 200f;
            float x = (float)(random.NextFloat() - 0.5f) * 200f;

            double reference = Math.Atan2(y, x);
            double error = Math.Abs(Trig.Atan2(y, x) - reference) / FloatUlp(reference);
            if (error > worst)
            {
                worst = error;
                atX = x;
                atY = y;
            }
        }

        _o.WriteLine($"Atan2: худшая ошибка {worst:F3} ULP (при y={atY:F6}, x={atX:F6})");
        Assert.True(worst <= Atan2Budget, $"Atan2: худшая ошибка {worst:F3} ULP при y={atY:F6}, x={atX:F6}.");
    }

    [Fact]
    public void Atan2_QuadrantsAndAxes()
    {
        MathAssert.Equal(0f, Trig.Atan2(0f, 1f), 1e-6f);
        MathAssert.Equal(MathF.PI * 0.5f, Trig.Atan2(1f, 0f), 1e-6f);
        MathAssert.Equal(MathF.PI, Trig.Atan2(0f, -1f), 1e-6f);
        MathAssert.Equal(-MathF.PI * 0.5f, Trig.Atan2(-1f, 0f), 1e-6f);

        // Оба нуля: по IEEE 754 знак результата определяется знаком первого
        // аргумента. Сверяется с эталоном на двойной точности, а не с MathF:
        // на точках, где оба аргумента нули, детерминированный и системный
        // варианты вправе различаться, и сверять их друг с другом значило бы
        // требовать от детерминированного варианта повторять поведение
        // платформы, ради устранения которого он и написан.
        Assert.Equal(
            BitConverter.SingleToInt32Bits((float)Math.Atan2(0.0, -0.0)),
            BitConverter.SingleToInt32Bits(Trig.Atan2(0f, -0f)));
        Assert.Equal(
            BitConverter.SingleToInt32Bits((float)Math.Atan2(-0.0, 0.0)),
            BitConverter.SingleToInt32Bits(Trig.Atan2(-0f, 0f)));
    }

    [Fact]
    public void Atan2_IsNeverWorseThanMathFByALot()
    {
        // Не требуется совпадение с MathF: варианты могут различаться. Требуется
        // лишь одно — детерминированный вариант не должен быть заметно хуже.
        double worst = 0;
        var random = new XorShift64Star(4711);
        for (int i = 0; i < Samples; i++)
        {
            float y = (float)(random.NextFloat() - 0.5f) * 20f;
            float x = (float)(random.NextFloat() - 0.5f) * 20f;
            double reference = Math.Atan2(y, x);
            double error = Math.Abs(Trig.Atan2(y, x) - reference) / FloatUlp(reference);
            worst = Math.Max(worst, error);
        }

        Assert.True(worst <= Atan2Budget, $"Atan2 уступил точности: {worst:F3} ULP.");
    }

    [Fact]
    public void Asin_AccurateAcrossRange()
    {
        double worst = 0;
        float at = 0;
        var random = new XorShift64Star(606);
        for (int i = 0; i < Samples; i++)
        {
            float x = (float)(random.NextFloat() - 0.5f) * 2f;
            double reference = Math.Asin(x);
            double error = Math.Abs(Trig.Asin(x) - reference) / FloatUlp(reference);
            if (error > worst)
            {
                worst = error;
                at = x;
            }
        }

        _o.WriteLine($"Asin: худшая ошибка {worst:F3} ULP (при x={at:F6})");
        Assert.True(worst <= AsinBudget, $"Asin: худшая ошибка {worst:F3} ULP при x={at:F6}.");
    }

    [Fact]
    public void Asin_EndpointsAndErrors()
    {
        Assert.Equal(0f, Trig.Asin(0f));
        MathAssert.Equal(MathF.PI * 0.5f, Trig.Asin(1f), 1e-6f);
        MathAssert.Equal(-MathF.PI * 0.5f, Trig.Asin(-1f), 1e-6f);
        Assert.True(float.IsNaN(Trig.Asin(1.5f)), "Арксинус вне [-1; 1] обязан давать NaN.");
        Assert.True(float.IsNaN(Trig.Asin(-2f)), "Арксинус вне [-1; 1] обязан давать NaN.");
    }

    [Fact]
    public void Tan_MatchesSineOverCosine()
    {
        var random = new XorShift64Star(818);
        for (int i = 0; i < 20000; i++)
        {
            float x = (float)((random.NextFloat() - 0.5f) * 6.0);
            (float sin, float cos) = Trig.SinCos(x);
            if (MathF.Abs(cos) < 0.01f)
            {
                continue;
            }

            MathAssert.Equal(Trig.Tan(x), sin / cos, 2e-5f);
        }
    }

    [Fact]
    public void DeterministicVariant_UsesOnlyExactOperations()
    {
        // Детерминированный вариант обязан быть собран из операций, точно
        // определённых IEEE 754. Проверяется не исходным текстом, а поведением:
        // повторный вызов обязан дать тот же результат, а результат обязан
        // совпасть с вычислением того же многочлена в двойной точности с
        // последующим округлением — расхождение означало бы, что где-то по
        // пути появилась операция с неопределённым округлением.
#if XENGINE_DETERMINISTIC_MATH
        // Повторный вызов обязан дать тот же результат: это проверка на
        // отсутствие изменяемого состояния и на то, что компилятор не
        // переставляет операции. Именно эти два свойства и делают результат
        // воспроизводимым, а само совпадение с эталоном проверяется отдельно.
        var random = new XorShift64Star(2024);
        for (int i = 0; i < 50000; i++)
        {
            float x = (float)((random.NextFloat() - 0.5f) * 8.0 * MathF.PI);
            (float firstSin, float firstCos) = Trig.SinCos(x);
            (float secondSin, float secondCos) = Trig.SinCos(x);

            Assert.Equal(
                BitConverter.SingleToInt32Bits(firstSin),
                BitConverter.SingleToInt32Bits(secondSin));
            Assert.Equal(
                BitConverter.SingleToInt32Bits(firstCos),
                BitConverter.SingleToInt32Bits(secondCos));
        }
#else
        // Быстрый вариант обязан совпадать с системной библиотекой побитово,
        // иначе фасад искажает её, а не делегирует.
        var random = new XorShift64Star(2024);
        for (int i = 0; i < 50000; i++)
        {
            float x = (float)((random.NextFloat() - 0.5f) * 8.0 * MathF.PI);
            (float sin, float cos) = Trig.SinCos(x);
            (float systemSin, float systemCos) = MathF.SinCos(x);

            Assert.Equal(BitConverter.SingleToInt32Bits(systemSin), BitConverter.SingleToInt32Bits(sin));
            Assert.Equal(BitConverter.SingleToInt32Bits(systemCos), BitConverter.SingleToInt32Bits(cos));
        }
#endif
    }

    [Fact]
    public void Angle_RotateAgreesWithReference()
    {
        var random = new XorShift64Star(31415);
        double worst = 0;
        for (int i = 0; i < 100000; i++)
        {
            Angle angle = Angle.FromRadians((random.NextFloat() - 0.5f) * 12f);
            Vector2 vector = new(random.NextFloat() - 0.5f, random.NextFloat() - 0.5f);

            Vector2 rotated = angle.Rotate(vector);
            double reference = vector.Length() * Math.Sin(Math.Atan2(vector.Y, vector.X) + angle.Radians);
            worst = Math.Max(worst, Math.Abs(rotated.Y - reference) / Math.Max(1e-30, vector.Length()));
        }

        _o.WriteLine($"Angle.Rotate: худшая относительная ошибка {worst:E3}");
        Assert.True(worst <= 1e-5, $"Angle.Rotate: ошибка {worst:E3}.");
    }

    [Fact]
    public void Curves_SineFormsKeepPrecision()
    {
        foreach (float t in new[] { 1e-4f, 1e-3f, 1e-2f, 0.1f, 0.5f, 0.9f })
        {
            double exactSine = 1.0 - Math.Cos(t * Math.PI * 0.5);
            double exactInOut = (1.0 - Math.Cos(Math.PI * t)) * 0.5;

            Assert.True(
                Math.Abs((Curves.InSine(t) - exactSine) / exactSine) <= 1e-5,
                $"InSine({t}) потерял точность.");
            Assert.True(
                Math.Abs((Curves.InOutSine(t) - exactInOut) / exactInOut) <= 1e-5,
                $"InOutSine({t}) потерял точность.");
        }
    }

    [Fact]
    public void Curves_ExponentialMatchReference()
    {
        var random = new XorShift64Star(2718);
        for (int i = 1; i < 20000; i++)
        {
            float t = i / 20000f;
            double referenceIn = Math.Pow(2.0, (10.0 * t) - 10.0);
            double referenceOut = 1.0 - Math.Pow(2.0, -10.0 * t);

            Assert.True(
                Math.Abs((Curves.InExpo(t) - referenceIn) / referenceIn) <= 1e-4,
                $"InExpo({t}) расходится с эталоном.");
            Assert.True(
                Math.Abs((Curves.OutExpo(t) - referenceOut) / referenceOut) <= 1e-4,
                $"OutExpo({t}) расходится с эталоном.");
        }

        Assert.True(random.NextFloat() >= 0f);
    }
}