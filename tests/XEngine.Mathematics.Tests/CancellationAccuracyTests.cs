using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные тесты потери значащих цифр в сглаживании и в оставшихся
/// кривых ускорения.
/// </summary>
/// <remarks>
/// Дефект один и тот же в шести местах: результат получается вычитанием из
/// единицы числа, отличающегося от единицы меньше чем на половину последнего
/// разряда. Для синусоидальных кривых он был найден и исправлен раньше, а
/// для <c>Damp</c>, <c>OutQuad</c>, <c>OutExpo</c> и <c>OutBack</c> остался:
/// все они дают правильный результат на глаз и теряют точность только там,
/// где результат сам по себе мал, то есть в начале интервала.
/// <para>
/// Тесты построены на сверке с эталоном на двойной точности и требуют не
/// просто правильного знака, а сохранения относительной точности: наивная
/// форма проходит проверку «меньше единицы» и проваливает проверку точности.
/// </para>
/// </remarks>
public class CancellationAccuracyTests
{
    /// <summary>
    /// Проверяет относительную ошибку против эталона на двойной точности.
    /// </summary>
    /// <param name="actual">Значение библиотеки.</param>
    /// <param name="exact">Эталон на двойной точности.</param>
    /// <param name="tolerance">Допустимая относительная ошибка.</param>
    /// <param name="what">Что проверяется, для сообщения об ошибке.</param>
    private static void AssertRelative(float actual, double exact, double tolerance, string what)
    {
        double relative = exact == 0 ? Math.Abs(actual) : Math.Abs((actual - exact) / exact);
        Assert.True(
            relative <= tolerance,
            $"{what}: получено {actual:E9}, точно {exact:E12}, относительная ошибка {relative:P3} при допуске {tolerance:P3}.");
    }

    /// <summary>
    /// Эталон разности <c>1 - e^x</c> на двойной точности. Ряд используется там,
    /// где прямая формула сама теряет значащие цифры, то есть при малом
    /// отрицательном аргументе.
    /// </summary>
    /// <param name="x">Аргумент.</param>
    /// <returns>Значение разности.</returns>
    private static double ReferenceOneMinusExp(double x)
    {
        if (x > -1e-4)
        {
            return 1.0 - Math.Exp(x);
        }

        // 1 - e^x = -(x + x²/2 + x³/6 + x⁴/24 + …)
        double sum = x + ((x * x * 0.5) + ((x * x * x / 6.0) + (x * x * x * x / 24.0)));
        return -sum;
    }

    [Theory]
    [InlineData(1e-1f)]
    [InlineData(1e-2f)]
    [InlineData(1e-3f)]
    [InlineData(1e-4f)]
    [InlineData(1e-5f)]
    [InlineData(1e-6f)]
    [InlineData(1e-7f)]
    [InlineData(1e-8f)]
    [InlineData(1e-9f)]
    public void OneMinusExp_KeepsRelativePrecisionOnSmallArguments(float x)
    {
        float actual = Trig.OneMinusExp(x);
        double exact = ReferenceOneMinusExp(x);

        Assert.False(float.IsNaN(actual), $"OneMinusExp({x:E1}) вернул NaN.");
        AssertRelative(actual, exact, 1e-5, $"Trig.OneMinusExp({x:E1})");
    }

    /// <summary>
    /// Наивная форма <c>1 - exp(x)</c> возвращает ровно ноль при
    /// <c>x = -1e-8</c>. Сглаживание обязано двигать значение и на таком
    /// медленном шаге, иначе объект перестаёт догонять цель.
    /// </summary>
    [Fact]
    public void OneMinusExp_DoesNotCollapseToZeroOnTinyNegativeArgument()
    {
        float value = Trig.OneMinusExp(-1e-8f);
        Assert.True(value > 0f, "OneMinusExp обязан вернуть положительное значение на крошечном отрицательном аргументе.");
        AssertRelative(value, 1e-8, 1e-5, "OneMinusExp(-1e-8)");
    }

    [Theory]
    [InlineData(1e-1f)]
    [InlineData(1e-2f)]
    [InlineData(1e-3f)]
    [InlineData(1e-4f)]
    [InlineData(1e-5f)]
    [InlineData(1e-6f)]
    [InlineData(1e-7f)]
    [InlineData(1e-8f)]
    [InlineData(1e-9f)]
    public void Damp_KeepsRelativePrecisionOnSlowSmoothing(float product)
    {
        // lambda = 1, deltaTime = product: произведение и есть аргумент.
        float actual = Interpolation.Damp(0f, 1f, 1f, product);
        double exact = ReferenceOneMinusExp(-product);

        AssertRelative(actual, exact, 1e-5, $"Damp при lambda*dt = {product:E1}");
    }

    [Fact]
    public void Damp_StillMovesValueOnVerySlowSmoothing()
    {
        // Медленное сглаживание: за кадр значение обязано сместиться хоть
        // немного. Прежняя формула возвращала здесь ровно ноль, и объект
        // замирал.
        foreach (float product in new[] { 1e-6f, 1e-7f, 1e-8f, 1e-9f })
        {
            float smoothed = Interpolation.Damp(0f, 100f, 1f, product);
            Assert.True(smoothed > 0f, $"Damp при lambda*dt = {product:E1} обязан сместить значение, а не вернуть ноль.");
        }
    }

    /// <summary>
    /// Независимость от частоты кадров не должна пострадать при переходе на
    /// устойчивую форму: результат за секунду обязан совпадать при любом
    /// количестве шагов.
    /// </summary>
    [Fact]
    public void Damp_RemainsIndependentOfStepCount()
    {
        const float Lambda = 0.5f;
        const float Target = 10f;

        foreach (int steps in new[] { 1, 2, 5, 17, 60, 240, 1000 })
        {
            float deltaTime = 1f / steps;
            float value = 0f;
            for (int i = 0; i < steps; i++)
            {
                value = Interpolation.Damp(value, Target, Lambda, deltaTime);
            }

            double exact = Target * (1.0 - Math.Exp(-Lambda));
            AssertRelative(value, exact, 2e-4, $"Damp за секунду при {steps} шагах");
        }
    }

    [Theory]
    [InlineData(1e-7f)]
    [InlineData(1e-6f)]
    [InlineData(1e-5f)]
    [InlineData(1e-4f)]
    [InlineData(1e-3f)]
    [InlineData(1e-2f)]
    [InlineData(1e-1f)]
    public void OutQuad_KeepsRelativePrecisionNearZero(float t)
        => AssertRelative(
            Curves.OutQuad(t),
            2.0 * t - (t * t),
            1e-5,
            $"Curves.OutQuad({t:E1})");

    [Theory]
    [InlineData(1e-7f)]
    [InlineData(1e-6f)]
    [InlineData(1e-5f)]
    [InlineData(1e-4f)]
    [InlineData(1e-3f)]
    [InlineData(1e-2f)]
    [InlineData(1e-1f)]
    public void OutExpo_KeepsRelativePrecisionNearZero(float t)
        => AssertRelative(
            Curves.OutExpo(t),
            1.0 - Math.Pow(2.0, -10.0 * t),
            1e-5,
            $"Curves.OutExpo({t:E1})");

    [Theory]
    [InlineData(1e-7f)]
    [InlineData(1e-6f)]
    [InlineData(1e-5f)]
    [InlineData(1e-4f)]
    [InlineData(1e-3f)]
    [InlineData(1e-2f)]
    [InlineData(1e-1f)]
    public void OutBack_KeepsRelativePrecisionNearZero(float t)
        => AssertRelative(
            Curves.OutBack(t),
            OutBackExact(t, 1.70158),
            1e-5,
            $"Curves.OutBack({t:E1})");

    /// <summary>
    /// Эталон <c>OutBack</c> на двойной точности.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <param name="overshoot">Коэффициент перелёта.</param>
    /// <returns>Значение кривой.</returns>
    private static double OutBackExact(double t, double overshoot)
    {
        double x = t - 1.0;
        return ((x * x) * (((overshoot + 1.0) * x) + overshoot)) + 1.0;
    }

    /// <summary>
    /// Раскрытая форма обязана совпадать с исходной на всём интервале, а не
    /// только в начале: раскрытие могло бы изменить форму кривой.
    /// </summary>
    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void OutBack_MatchesOriginalFormAwayFromZero(float t)
        => AssertRelative(Curves.OutBack(t), OutBackExact(t, 1.70158), 1e-6, $"Curves.OutBack({t})");

    /// <summary>
    /// Раскрытая форма <c>OutQuad</c> обязана совпадать с исходной на всём
    /// интервале.
    /// </summary>
    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void OutQuad_MatchesOriginalFormAwayFromZero(float t)
        => AssertRelative(Curves.OutQuad(t), 1.0 - ((1.0 - t) * (1.0 - t)), 1e-6, $"Curves.OutQuad({t})");

    /// <summary>
    /// Раскрытая форма <c>OutExpo</c> обязана совпадать с исходной на всём
    /// интервале.
    /// </summary>
    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.9f)]
    public void OutExpo_MatchesOriginalFormAwayFromZero(float t)
        => AssertRelative(Curves.OutExpo(t), 1.0 - Math.Pow(2.0, -10.0 * t), 1e-6, $"Curves.OutExpo({t})");

    /// <summary>
    /// <see cref="Trig.OneMinusExp"/> обязан совпадать с прямой формулой там,
    /// где вычитание безопасно: на больших аргументах многочлен не
    /// используется, иначе ветка была бы мёртвой.
    /// </summary>
    [Theory]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    [InlineData(-3f)]
    [InlineData(-10f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(3f)]
    public void OneMinusExp_MatchesDirectFormulaOnLargeArguments(float x)
        => AssertRelative(Trig.OneMinusExp(x), 1.0 - Math.Exp(x), 1e-6, $"Trig.OneMinusExp({x})");

    /// <summary>
    /// На стыке двух ветвей не должно быть разрыва: значения слева и справа
    /// от 0.5 обязаны отличаться не больше, чем на округление.
    /// </summary>
    [Fact]
    public void OneMinusExp_HasNoJumpAtBranchBoundary()
    {
        float below = Trig.OneMinusExp(0.4999999f);
        float above = Trig.OneMinusExp(0.5f);
        Assert.True(
            MathF.Abs(below - above) <= 1e-6f,
            $"На границе ветвей разрыв {MathF.Abs(below - above):E3}: {below:R} и {above:R}.");

        float belowNegative = Trig.OneMinusExp(-0.4999999f);
        float aboveNegative = Trig.OneMinusExp(-0.5f);
        Assert.True(
            MathF.Abs(belowNegative - aboveNegative) <= 1e-6f,
            $"На границе ветвей разрыв {MathF.Abs(belowNegative - aboveNegative):E3}.");
    }
}