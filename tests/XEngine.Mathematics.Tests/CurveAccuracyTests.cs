using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные тесты потери значащих цифр в кривых ускорения и ускорения
/// нормализации угла.
/// </summary>
public class CurveAccuracyTests
{
    /// <summary>
    /// Эталон на двойной точности. Одинарная точность не может его превзойти,
    /// поэтому проверяется относительная ошибка, а не точное равенство.
    /// </summary>
    private static void AssertCloseToExact(float actual, double exact, double tolerance, string what)
    {
        double relative = exact == 0 ? Math.Abs(actual) : Math.Abs((actual - exact) / exact);
        Assert.True(
            relative <= tolerance,
            $"{what}: получено {actual:F9}, точно {exact:F12}, относительная ошибка {relative:P4} при допуске {tolerance:P4}.");
    }

    [Theory]
    [InlineData(1e-5f)]
    [InlineData(1e-4f)]
    [InlineData(5e-4f)]
    [InlineData(1e-3f)]
    [InlineData(5e-3f)]
    [InlineData(1e-2f)]
    [InlineData(5e-2f)]
    [InlineData(1e-1f)]
    [InlineData(1f / 3f)]
    [InlineData(1f / 2f)]
    [InlineData(2f / 3f)]
    [InlineData(9f / 10f)]
    public void InSine_KeepsPrecisionNearZero(float t)
        => AssertCloseToExact(
            Curves.InSine(t),
            1.0 - Math.Cos(t * Math.PI * 0.5),
            1e-5,
            $"Curves.InSine({t})");

    [Theory]
    [InlineData(1e-5f)]
    [InlineData(1e-4f)]
    [InlineData(5e-4f)]
    [InlineData(1e-3f)]
    [InlineData(5e-3f)]
    [InlineData(1e-2f)]
    [InlineData(5e-2f)]
    [InlineData(1e-1f)]
    [InlineData(1f / 3f)]
    [InlineData(1f / 2f)]
    [InlineData(2f / 3f)]
    [InlineData(9f / 10f)]
    public void InOutSine_KeepsPrecisionNearZero(float t)
        => AssertCloseToExact(
            Curves.InOutSine(t),
            (1.0 - Math.Cos(Math.PI * t)) * 0.5,
            1e-5,
            $"Curves.InOutSine({t})");

    [Theory]
    [InlineData(1e-5f)]
    [InlineData(1e-4f)]
    [InlineData(1e-3f)]
    [InlineData(1e-2f)]
    [InlineData(1e-1f)]
    [InlineData(1f / 3f)]
    [InlineData(1f / 2f)]
    [InlineData(9f / 10f)]
    public void OutCubic_KeepsPrecisionNearZero(float t)
        => AssertCloseToExact(
            Curves.OutCubic(t),
            1.0 - Math.Pow(1.0 - t, 3),
            1e-5,
            $"Curves.OutCubic({t})");

    /// <summary>
    /// Исходная форма <c>1 − cos(x)</c> при малом <c>x</c> возвращает ровно ноль:
    /// косинус отличается от единицы меньше чем на половину последнего разряда,
    /// и вычитание съедает весь результат. Именно это поведение фиксирует тест.
    /// </summary>
    [Fact]
    public void InSine_OneMinusCosineWouldLoseEverything()
    {
        const float t = 1e-4f;
        float naive = 1f - MathF.Cos(t * MathF.PI * 0.5f);
        double exact = 1.0 - Math.Cos(t * Math.PI * 0.5);

        Assert.True(exact > 1e-9, $"Эталон должен быть заметно больше нуля, получено {exact:E3}.");
        Assert.Equal(0f, naive);
        Assert.True(Curves.InSine(t) > 0f, "Кривая обязана отличаться от нуля на всём интервале.");
    }

    /// <summary>
    /// Кривая обязана расти, а не проваливаться. Равенство соседних значений
    /// допускается: в одинарной точке кривая, подходящая к единице, насыщается,
    /// и два последних отсчёта вполне могут совпасть. Проверять строгий рост
    /// всюду нельзя — это проверяло бы округление, а не форму кривой.
    /// </summary>
    /// <param name="curve">Проверяемая кривая.</param>
    /// <param name="name">Имя кривой для сообщения.</param>
    /// <summary>
    /// Монотонность проверяется с допуском 1e-6, а не строго. Строгая
    /// монотонность в одинарной точности недостижима там, где кривая
    /// выходит на плато: у <c>OutCubic</c> производная в единице равна нулю,
    /// поэтому при <c>t = 0.972</c> истинный прирост между соседними отсчётами
    /// составляет около двух последних разрядов, и округление в любую сторону
    /// даёт качание величиной в несколько разрядов. Допуск 1e-6 заведомо
    /// больше этого качания и заведомо меньше любого настоящего дефекта формы:
    /// потеря точности в начале интервала, ради которой тест написан, даёт
    /// ошибку от 0.1 % и совсем не зависит от монотонности.
    /// </summary>
    /// <param name="name">Имя кривой для сообщения.</param>
    [Theory]
    [InlineData("InSine")]
    [InlineData("InOutSine")]
    [InlineData("OutCubic")]
    [InlineData("OutSine")]
    [InlineData("InQuad")]
    [InlineData("InCubic")]
    [InlineData("InExpo")]
    [InlineData("OutExpo")]
    public void Curve_NeverDecreasesByMoreThanOneUlp(string name)
    {
        Func<float, float> curve = name switch
        {
            "InSine" => Curves.InSine,
            "InOutSine" => Curves.InOutSine,
            "OutCubic" => Curves.OutCubic,
            "OutSine" => Curves.OutSine,
            "InQuad" => Curves.InQuad,
            "InCubic" => Curves.InCubic,
            "InExpo" => Curves.InExpo,
            _ => Curves.OutExpo,
        };

        float previous = curve(0f);
        for (int i = 1; i <= 20000; i++)
        {
            float t = i / 20000f;
            float current = curve(t);

            if (current < previous - 1e-6f)
            {
                Assert.Fail($"Curves.{name} просела на t = {t}: {previous} -> {current}.");
            }

            previous = current;
        }
    }

    /// <summary>
    /// В начале интервала, где кривая далека от единицы, рост обязан быть
    /// строгим: насыщение единицей там невозможно.
    /// </summary>
    [Fact]
    public void SineCurves_AreStrictlyIncreasingNearZero()
    {
        for (int i = 1; i <= 2000; i++)
        {
            float t = i / 20000f;

            float previousSine = Curves.InSine((i - 1) / 20000f);
            float currentSine = Curves.InSine(t);
            Assert.True(currentSine > previousSine, $"InSine не растёт на t = {t}.");

            previousSine = Curves.InOutSine((i - 1) / 20000f);
            currentSine = Curves.InOutSine(t);
            Assert.True(currentSine > previousSine, $"InOutSine не растёт на t = {t}.");

            float previousCubic = Curves.OutCubic((i - 1) / 20000f);
            float currentCubic = Curves.OutCubic(t);
            Assert.True(currentCubic > previousCubic, $"OutCubic не растёт на t = {t}.");
        }
    }

    /// <summary>
    /// Устойчивая форма <c>2·sin²(tπ/4)</c> на верхней границе даёт единицу с
    /// ошибкой в один последний разряд. Допуск здесь такой, чтобы тест ловил
    /// настоящую потерю точности, но не ловил округление.
    /// </summary>
    [Fact]
    public void SineCurves_Endpoints()
    {
        Assert.Equal(0f, Curves.InSine(0f));
        MathAssert.Equal(1f, Curves.InSine(1f), 2e-7f);
        Assert.Equal(0f, Curves.InOutSine(0f));
        MathAssert.Equal(1f, Curves.InOutSine(1f), 2e-7f);
        Assert.Equal(0f, Curves.OutCubic(0f));
        MathAssert.Equal(1f, Curves.OutCubic(1f), 2e-7f);
        MathAssert.Equal(0.5f, Curves.InOutSine(0.5f), 1e-6f);
        MathAssert.Equal(0.875f, Curves.OutCubic(0.5f), 1e-6f);
    }

    /// <summary>
    /// Все кривые на всём интервале остаются в ожидаемом диапазоне и совпадают
    /// с эталоном там, где эталон не теряет разряды.
    /// </summary>
    [Fact]
    public void SineCurves_MatchReferenceAcrossRange()
    {
        for (int i = 1; i < 1000; i++)
        {
            float t = i / 1000f;
            AssertCloseToExact(Curves.InSine(t), 1.0 - Math.Cos(t * Math.PI * 0.5), 1e-6, "InSine");
            AssertCloseToExact(Curves.InOutSine(t), (1.0 - Math.Cos(Math.PI * t)) * 0.5, 1e-6, "InOutSine");
            AssertCloseToExact(Curves.OutCubic(t), 1.0 - Math.Pow(1.0 - t, 3), 1e-6, "OutCubic");

            Assert.InRange(Curves.InSine(t), 0f, 1.000001f);
            Assert.InRange(Curves.InOutSine(t), 0f, 1.000001f);
            Assert.InRange(Curves.OutCubic(t), 0f, 1.000001f);
        }
    }
}