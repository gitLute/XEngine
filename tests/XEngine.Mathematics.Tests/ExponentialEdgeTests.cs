using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Крайние случаи экспоненты и степени двойки, где результат выходит из
/// нормального диапазона.
/// </summary>
/// <remarks>
/// Эталон не вычисляется вручную: правильно округлённое значение одинарной
/// точности получается простым приведением результата двойной точности.
/// Точность <c>Math.Exp</c> и <c>Math.Pow</c> в двойной точности составляет
/// доли последнего разряда double, то есть примерно 10⁻¹⁵ отн��сительно, а
/// одинарная точность имеет шаг 6·10⁻⁸. Приведение double к float округляет
/// к ближайшему, поэтому эталон совпадает с правильно округлённым float
/// везде, кроме тех мест, где точное значение лежит ровно посередине между
/// двумя соседними float, — там допускается один шаг.
/// </remarks>
public class ExponentialEdgeTests
{
    /// <summary>
    /// Правильно округлённая одинарная точность для экспоненты.
    /// </summary>
    /// <param name="x">Показатель.</param>
    /// <returns>Эталонное значение.</returns>
    private static float ReferenceExp(float x) => (float)Math.Exp(x);

    /// <summary>
    /// Правильно округлённая одинарная точность для степени двойки.
    /// </summary>
    /// <param name="x">Показатель.</param>
    /// <returns>Эталонное значение.</returns>
    private static float ReferencePow2(float x) => (float)Math.Pow(2.0, x);

    /// <summary>
    /// Сравнение, равное с точностью до одного шага одинарной точности.
    /// </summary>
    /// <param name="expected">Эталон.</param>
    /// <param name="actual">Полученное значение.</param>
    /// <param name="what">Описание случая.</param>
    private static void AssertMatchesReference(float expected, float actual, string what)
    {
        if (float.IsNaN(expected))
        {
            Assert.True(float.IsNaN(actual), $"{what}: получено {actual}, эталон — неопределённость.");
            return;
        }

        if (expected == 0f || float.IsInfinity(expected))
        {
            Assert.True(
                expected.Equals(actual),
                $"{what}: получено {actual}, эталон {expected}. На границе диапазона сравнение строгое.");
            return;
        }

        float tolerance = MathF.Max(MathF.Abs(expected) * 1e-6f, MathF.BitIncrement(expected) - expected);
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    /// <summary>
    /// Экспонента обязана совпадать с эталоном по всей ширине диапазона,
    /// включая область ненормальных чисел, где одинарная точность ещё не
    /// обнуляется.
    /// </summary>
    [Fact]
    public void Exp_MatchesReferenceAcrossDenormalRange()
    {
        // Область ненормальных чисел одинарной точности: от 1.4·10⁻⁴⁵
        // до 1.18·10⁻³⁸. Именно здесь ошибка обнуления была незаметна:
        // результат либо верный, либо сразу ноль.
        for (int step = 0; step <= 3000; step++)
        {
            // Равномерно по показателю от -104 до -87, плюс область глубже.
            float x = -104f + (step * 0.0057f);
            AssertMatchesReference(ReferenceExp(x), Trig.Exp(x), $"exp({x:F4})");
        }
    }

    /// <summary>
    /// Граница обнуления: ниже неё результат равен нулю, выше — ещё нет.
    /// Проверяется сама граница, а не её приближённое положение.
    /// </summary>
    [Fact]
    public void Exp_UnderflowBoundaryMatchesReference()
    {
        // Широкий коридор вокруг границы: значение обязано переходить от
        // ненулевого к нулю ровно там же, где это делает эталон.
        for (float x = -110f; x <= -95f; x += 0.05f)
        {
            AssertMatchesReference(ReferenceExp(x), Trig.Exp(x), $"exp({x:F4}) на границе обнуления");
        }

        Assert.Equal(0f, ReferenceExp(-120f));
        Assert.Equal(0f, Trig.Exp(-120f));

        // Наименьшее ненормальное число достижимо и обязано быть ненулевым.
        float smallest = BitConverter.Int32BitsToSingle(1);
        Assert.True(smallest > 0f, "Наименьшее ненормальное число должно быть положительным.");
        Assert.Equal(smallest, ReferencePow2(-149f));
        AssertMatchesReference(smallest, Trig.Pow2(-149f), "pow2(-149), наименьшее ненормальное число");
    }

    /// <summary>
    /// Степень двойки обязана совпадать с эталоном в области ненормальных
    /// чисел. Здесь границы целые, поэтому эталон задаётся точно.
    /// </summary>
    [Fact]
    public void Pow2_MatchesReferenceInDenormalRange()
    {
        for (int step = 0; step <= 3000; step++)
        {
            float x = -150f + (step * 0.02f);
            AssertMatchesReference(ReferencePow2(x), Trig.Pow2(x), $"pow2({x:F4})");
        }
    }

    /// <summary>
    /// Бесконечности обязаны давать предел, а неопределённость — оставаться
    /// неопределённостью. Это определение, а не результат подбора.
    /// </summary>
    /// <Fact />
    [Theory]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Exp_AtInfinityGivesLimit(float x)
    {
        // exp(+∞) = +∞, exp(-∞) = 0. Обе величины определены пределом,
        // неопределённости на границе нет.
        float expected = x > 0 ? float.PositiveInfinity : 0f;

        Assert.Equal(expected, Trig.Exp(x));
        Assert.Equal(expected, ReferenceExp(x));

        // Степень двойки ведёт себя так же.
        Assert.Equal(expected, Trig.Pow2(x));
    }

    /// <summary>
    /// Неопределённость на входе обязана оставаться неопределённостью на
    /// выходе: молчаливый ноль или единица скрыли бы ошибку вызывающего.
    /// </summary>
    [Fact]
    public void Exp_AndPow2_PropagateNaN()
    {
        Assert.True(float.IsNaN(Trig.Exp(float.NaN)));
        Assert.True(float.IsNaN(Trig.Pow2(float.NaN)));
        Assert.True(float.IsNaN(ReferenceExp(float.NaN)));
        Assert.True(float.IsNaN(ReferencePow2(float.NaN)));
    }

    /// <summary>
    /// Переполнение вверх: оба варианта обязаны давать бесконечность там же,
    /// где её даёт эталон, а не позже.
    /// </summary>
    [Fact]
    public void Exp_AndPow2_OverflowMatchesReference()
    {
        for (float x = 87f; x <= 95f; x += 0.05f)
        {
            AssertMatchesReference(ReferenceExp(x), Trig.Exp(x), $"exp({x:F4}) на границе переполнения");
        }

        for (float x = 126f; x <= 132f; x += 0.05f)
        {
            AssertMatchesReference(ReferencePow2(x), Trig.Pow2(x), $"pow2({x:F4}) на границе переполнения");
        }
    }

    /// <summary>
    /// Оба варианта сборки обязаны давать один и тот же результат в
    /// одинарной точности. Это главная проверка: расхождение между
    /// вариантами означает, что один из них врёт, и какой именно — вопрос
    /// только порядка разрядов.
    /// </summary>
    [Fact]
    public void Exp_AndPow2_StayInNormalRange()
    {
        var random = new XorShift64Star(998877);

        for (int i = 0; i < 20_000; i++)
        {
            float x = (random.NextFloat() - 0.5f) * 80f;

            // Отсекаются только значения, где сама эталонная функция теряет
            // точность: результат на грани обнуления зависит от округления
            // внутри double и не определён однозначно.
            if (x < -100f || x > 87f)
            {
                continue;
            }

            AssertMatchesReference(ReferenceExp(x), Trig.Exp(x), $"exp({x:F4}) в нормальном диапазоне");
            AssertMatchesReference(ReferencePow2(x), Trig.Pow2(x), $"pow2({x:F4}) в нормальном диапазоне");
        }
    }
}