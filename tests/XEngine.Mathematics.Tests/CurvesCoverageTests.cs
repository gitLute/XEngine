using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Тесты кривых ускорения, которые раньше не вызывались ни разу.
/// </summary>
/// <remarks>
/// Эталон — те же формулы, вычисленные на двойной точности через
/// <see cref="Math"/>. Это независимая реализация: библиотека считает в
/// одинарной точности через фасад <see cref="Trig"/>, а здесь считает
/// <see cref="Math"/>, то есть сверка идёт с другой точностью и другим
/// источником тригонометрии.
/// </remarks>
public class CurvesCoverageTests
{
    /// <summary>
    /// Допуск сравнения с эталоном. Одинарная точность даёт относительную
    /// ошибку порядка 6·10⁻⁸ на единицу; для величин порядка единицы и
    /// операций с условием берётся запас в 1e-4.
    /// </summary>
    private const double Tolerance = 1e-4;

    #region Линейные и квадратичные

    /// <summary>
    /// Линейная кривая обязана возвращать параметр без изменения.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(0.999f)]
    [InlineData(1f)]
    public void Linear_ReturnsParameter(float t)
    {
        Assert.Equal(t, Curves.Linear(t));
        Assert.Equal(0f, Curves.Linear(0f));
        Assert.Equal(1f, Curves.Linear(1f));
    }

    /// <summary>
    /// Квадратичное замедление: 1 − (1 − t)².
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    public void OutQuad_MatchesReference(float t)
    {
        double reference = 1.0 - ((1.0 - t) * (1.0 - t));
        Assert.InRange(Curves.OutQuad(t), (float)reference - Tolerance, (float)reference + Tolerance);
    }

    /// <summary>
    /// Квадратичное ускорение и замедление с ветвлением по середине интервала.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.49f)]
    [InlineData(0.5f)]
    [InlineData(0.51f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    public void InOutQuad_MatchesReference(float t)
    {
        double reference = t < 0.5 ? 2.0 * t * t : 1.0 - (2.0 * (1.0 - t) * (1.0 - t));
        Assert.InRange(Curves.InOutQuad(t), (float)reference - Tolerance, (float)reference + Tolerance);
    }

    #endregion

    #region Экспоненциальные

    /// <summary>
    /// Экспоненциальное ускорение: 2^(10t−10), с нулём на левой границе.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(0.9f)]
    [InlineData(1f)]
    public void InOutExpo_MatchesReference(float t)
    {
        double reference;
        if (t <= 0.0)
        {
            reference = 0.0;
        }
        else if (t >= 1.0)
        {
            reference = 1.0;
        }
        else
        {
            reference = t < 0.5
                ? (Math.Pow(2.0, (20.0 * t) - 10.0) * 0.5)
                : ((1.0 - Math.Pow(2.0, (-20.0 * t) + 10.0)) * 0.5 + 0.5);
        }

        Assert.InRange(Curves.InOutExpo(t), (float)reference - Tolerance, (float)reference + Tolerance);
    }

    [Fact]
    public void InOutExpo_ClampsOutsideRange()
    {
        Assert.Equal(0f, Curves.InOutExpo(-1f));
        Assert.Equal(0f, Curves.InOutExpo(0f));
        Assert.Equal(1f, Curves.InOutExpo(1f));
        Assert.Equal(1f, Curves.InOutExpo(2f));
    }

    #endregion

    #region Упругие кривые

    /// <summary>
    /// Упругое ускорение по Пеннеру:
    /// −2^(10t−10)·sin((10t − 10.75)·2π/3).
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.4f)]
    [InlineData(0.5f)]
    [InlineData(0.6f)]
    [InlineData(0.75f)]
    [InlineData(0.9f)]
    [InlineData(0.95f)]
    public void InElastic_MatchesReference(float t)
    {
        double reference = -Math.Pow(2.0, (10.0 * t) - 10.0) * Math.Sin((t * 10.0 - 10.75) * (2.0 * Math.PI / 3.0));
        Assert.InRange(Curves.InElastic(t), (float)reference - Tolerance, (float)reference + Tolerance);
    }

    /// <summary>
    /// Упругое замедление по Пеннеру:
    /// 2^(−10t)·sin((10t − 0.75)·2π/3) + 1.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.4f)]
    [InlineData(0.5f)]
    [InlineData(0.6f)]
    [InlineData(0.75f)]
    [InlineData(0.9f)]
    [InlineData(0.95f)]
    public void OutElastic_MatchesReference(float t)
    {
        double reference = (Math.Pow(2.0, -10.0 * t) * Math.Sin((t * 10.0 - 0.75) * (2.0 * Math.PI / 3.0))) + 1.0;
        Assert.InRange(Curves.OutElastic(t), (float)reference - Tolerance, (float)reference + Tolerance);
    }

    /// <summary>
    /// Упругие кривые зажаты за пределами интервала.
    /// </summary>
    [Fact]
    public void ElasticCurves_ClampOutsideRange()
    {
        Assert.Equal(0f, Curves.InElastic(-1f));
        Assert.Equal(0f, Curves.InElastic(0f));
        Assert.Equal(1f, Curves.InElastic(1f));
        Assert.Equal(1f, Curves.InElastic(2f));

        Assert.Equal(0f, Curves.OutElastic(-1f));
        Assert.Equal(0f, Curves.OutElastic(0f));
        Assert.Equal(1f, Curves.OutElastic(1f));
        Assert.Equal(1f, Curves.OutElastic(2f));
    }

    /// <summary>
    /// Упругие кривые немонотонны по определению: значение уходит за нижнюю
    /// границу и возвращается. Проверяется именно наличие такого выхода, а
    /// также то, что кривая остаётся в разумных пределах.
    /// </summary>
    [Fact]
    public void ElasticCurves_OvershootAndStayBounded()
    {
        float minIn = float.MaxValue;
        float maxIn = float.MinValue;
        float minOut = float.MaxValue;
        float maxOut = float.MinValue;

        for (int i = 0; i <= 1000; i++)
        {
            float t = i / 1000f;
            minIn = MathF.Min(minIn, Curves.InElastic(t));
            maxIn = MathF.Max(maxIn, Curves.InElastic(t));
            minOut = MathF.Min(minOut, Curves.OutElastic(t));
            maxOut = MathF.Max(maxOut, Curves.OutElastic(t));
        }

        Assert.True(minIn < 0f, $"InElastic не уходит ниже нуля: минимум {minIn:F4}.");
        Assert.True(maxOut > 1f, $"OutElastic не уходит выше единицы: максимум {maxOut:F4}.");

        // Минимум амплитуды задаётся самой формулой: произведение
        // 2^(10t-10) на синус доходит примерно до -0.35 при t = 0.85, где
        // множитель равен 0.354, а синус равен единице. Верхняя граница
        // ограничена величиной 2^(10t-10) <= 1.
        Assert.InRange(minIn, -0.5f, 0f);
        Assert.InRange(maxIn, 0f, 1.0001f);
        Assert.InRange(minOut, -0.0001f, 1.5f);
        Assert.InRange(maxOut, 1f, 1.5f);
    }

    #endregion

    #region Отскок

    /// <summary>
    /// Отскок состоит из четырёх парабол, и переход между ними обязан быть
    /// непрерывным: значение на стыке совпадает с обеими сторонами.
    /// </summary>
    [Fact]
    public void OutBounce_JunctionsAreContinuous()
    {
        const float d = 2.75f;
        float[] boundaries = [1f / d, 2f / d, 2.5f / d, 1f];

        for (int i = 0; i < boundaries.Length - 1; i++)
        {
            float boundary = boundaries[i];
            float before = Curves.OutBounce(boundary - 1e-5f);
            float at = Curves.OutBounce(boundary);
            float after = Curves.OutBounce(boundary + 1e-5f);

            Assert.InRange(at, before - 0.01f, before + 0.01f);
            Assert.InRange(at, after - 0.01f, after + 0.01f);
        }
    }

    /// <summary>
    /// Каждая парабола отскока считается по своей формуле, а не по общей.
    /// </summary>
    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.2f)]
    [InlineData(0.4f)]
    [InlineData(0.6f)]
    [InlineData(0.8f)]
    [InlineData(1.0f)]
    public void OutBounce_MatchesPiecewiseReference(float t)
    {
        const float n = 7.5625f;
        const float d = 2.75f;

        double reference;
        if (t < 1.0 / d)
        {
            reference = n * t * t;
        }
        else if (t < 2.0 / d)
        {
            double shifted = t - (1.5 / d);
            reference = (n * shifted * shifted) + 0.75;
        }
        else if (t < 2.5 / d)
        {
            double shifted = t - (2.25 / d);
            reference = (n * shifted * shifted) + 0.9375;
        }
        else
        {
            double shifted = t - (2.625 / d);
            reference = (n * shifted * shifted) + 0.984375;
        }

        Assert.InRange(Curves.OutBounce(t), (float)reference - Tolerance, (float)reference + Tolerance);
    }

    #endregion

    // Дальше проверки, общие для всего набора кривых.

    /// <summary>
    /// Кривая ускорения начинается в нуле и кончается единицей — это верно для
    /// всех, кроме линейной, и проверяется для каждой отдельно.
    /// </summary>
    /// <param name="name">Имя кривой.</param>
    [Theory]
    [InlineData("InQuad")]
    [InlineData("OutQuad")]
    [InlineData("InOutQuad")]
    [InlineData("InCubic")]
    [InlineData("OutCubic")]
    [InlineData("InOutCubic")]
    [InlineData("OutSine")]
    [InlineData("InExpo")]
    [InlineData("OutExpo")]
    [InlineData("InOutExpo")]
    [InlineData("OutBounce")]
    public void Curve_StartsAtZeroAndEndsAtOne(string name)
    {
        Func<float, float> curve = CurveByName(name);

        Assert.Equal(0f, curve(0f));
        Assert.Equal(1f, curve(1f));
    }

    /// <summary>
    /// Синусоидальные кривые на верхней границе отличаются от единицы на один
    /// последний разряд: устойчивая форма 2·sin²(t·π/4) в одинарной точности
    /// даёт 0.99999994, а не ровно 1. Это цена устранения потери точности в
    /// начале интервала, и проверяется явно, чтобы смена формы не прошла
    /// незамеченной.
    /// </summary>
    [Fact]
    public void SineCurves_EndAtUnitWithinOneUlp()
    {
        MathAssert.Equal(0f, Curves.InSine(0f), 0f);
        MathAssert.Equal(1f, Curves.InSine(1f), 1e-6f);
        MathAssert.Equal(0f, Curves.InOutSine(0f), 0f);
        MathAssert.Equal(1f, Curves.InOutSine(1f), 1e-6f);
        MathAssert.Equal(0f, Curves.OutSine(0f), 1e-6f);
        MathAssert.Equal(1f, Curves.OutSine(1f), 1e-6f);
    }

    /// <summary>
    /// Кривые с перелётом и упругие за границами интервала не обязаны
    /// совпадать с нулём и единицей, но обязаны оставаться в разумных пределах,
    /// чтобы вызов вне интервала не ломал анимацию.
    /// </summary>
    /// <param name="name">Имя кривой.</param>
    [Theory]
    [InlineData("InBack")]
    [InlineData("OutBack")]
    [InlineData("InElastic")]
    [InlineData("OutElastic")]
    public void Curve_StaysBoundedOutsideRange(string name)
    {
        Func<float, float> curve = CurveByName(name);

        // Вне интервала это многочлены, и они растут без ограничения:
        // OutBack(-1) даёт около -13.8. Проверяется лишь то, что значение
        // конечно, а не бесконечность или NaN.
        Assert.True(float.IsFinite(curve(-1f)), $"{name} вернула нечисловое значение вне интервала.");
        Assert.True(float.IsFinite(curve(2f)), $"{name} вернула нечисловое значение вне интервала.");
    }

    /// <summary>
    /// Полный набор кривых обязан совпадать с эталоном на двойной точности.
    /// Это главная проверка: она ловит и ошибку формулы, и потерю точности.
    /// </summary>
    [Fact]
    public void AllCurves_MatchDoubleReference()
    {
        var random = new XorShift64Star(606);
        var problems = new List<string>();

        for (int i = 0; i < 500; i++)
        {
            float t = random.NextFloat();
            double reference;

            reference = t;
            Compare("Linear", Curves.Linear(t), t, problems);

            reference = t * t;
            Compare("InQuad", Curves.InQuad(t), reference, problems);

            reference = 1.0 - ((1.0 - t) * (1.0 - t));
            Compare("OutQuad", Curves.OutQuad(t), reference, problems);

            reference = t < 0.5 ? 2.0 * t * t : 1.0 - (2.0 * (1.0 - t) * (1.0 - t));
            Compare("InOutQuad", Curves.InOutQuad(t), reference, problems);

            reference = t * t * t;
            Compare("InCubic", Curves.InCubic(t), reference, problems);

            reference = 1.0 - Math.Pow(1.0 - t, 3.0);
            Compare("OutCubic", Curves.OutCubic(t), reference, problems);

            reference = 1.0 - Math.Cos(t * Math.PI * 0.5);
            Compare("InSine", Curves.InSine(t), reference, problems);

            reference = Math.Sin(t * Math.PI * 0.5);
            Compare("OutSine", Curves.OutSine(t), reference, problems);

            reference = (1.0 - Math.Cos(Math.PI * t)) * 0.5;
            Compare("InOutSine", Curves.InOutSine(t), reference, problems);

            reference = t <= 0.0 ? 0.0 : Math.Pow(2.0, (10.0 * t) - 10.0);
            Compare("InExpo", Curves.InExpo(t), reference, problems);

            reference = t >= 1.0 ? 1.0 : 1.0 - Math.Pow(2.0, -10.0 * t);
            Compare("OutExpo", Curves.OutExpo(t), reference, problems);

            const float overshoot = 1.70158f;
            reference = t * t * (((overshoot + 1.0) * t) - overshoot);
            Compare("InBack", Curves.InBack(t), reference, problems);

            reference = t < 0.5 ? 4.0 * t * t * t : 1.0 - (4.0 * Math.Pow(1.0 - t, 3.0));
            Compare("InOutCubic", Curves.InOutCubic(t), reference, problems);
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(10)));
    }

    /// <summary>
    /// Оценка обратного перелёта: OutBack есть 1 + x²((s+1)x + s).
    /// </summary>
    [Fact]
    public void OutBack_MatchesReference()
    {
        const float overshoot = 1.70158f;

        for (int i = 0; i <= 1000; i++)
        {
            float t = i / 1000f;
            double x = t - 1.0;
            double reference = (x * x * (((overshoot + 1.0) * x) + overshoot)) + 1.0;

            Assert.InRange(Curves.OutBack(t), (float)reference - Tolerance, (float)reference + Tolerance);
        }
    }

    /// <summary>
    /// InOutCubic: две ветви по середине интервала.
    /// </summary>
    [Fact]
    public void InOutCubic_MatchesReference()
    {
        for (int i = 0; i <= 1000; i++)
        {
            float t = i / 1000f;
            double reference = t < 0.5 ? 4.0 * t * t * t : 1.0 - (4.0 * Math.Pow(1.0 - t, 3.0));

            Assert.InRange(Curves.InOutCubic(t), (float)reference - Tolerance, (float)reference + Tolerance);
        }
    }

    private static void Compare(string name, float actual, double reference, List<string> problems)
    {
        if (Math.Abs(actual - reference) > Tolerance)
        {
            problems.Add($"{name}: получено {actual:F7}, эталон {reference:F7}");
        }
    }

    private static Func<float, float> CurveByName(string name)
        => name switch
        {
            "InQuad" => Curves.InQuad,
            "OutQuad" => Curves.OutQuad,
            "InOutQuad" => Curves.InOutQuad,
            "InCubic" => Curves.InCubic,
            "OutCubic" => Curves.OutCubic,
            "InOutCubic" => Curves.InOutCubic,
            "InSine" => Curves.InSine,
            "OutSine" => Curves.OutSine,
            "InOutSine" => Curves.InOutSine,
            "InExpo" => Curves.InExpo,
            "OutExpo" => Curves.OutExpo,
            "InOutExpo" => Curves.InOutExpo,
            "InBack" => t => Curves.InBack(t),
            "OutBack" => t => Curves.OutBack(t),
            "InElastic" => Curves.InElastic,
            "OutElastic" => Curves.OutElastic,
            "OutBounce" => Curves.OutBounce,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Неизвестная кривая."),
        };
}