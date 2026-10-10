using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные проверки волны 4: длина во <c>float</c> и построение точки
/// поверхности.
/// </summary>
/// <remarks>
/// <para>
/// Общая болезнь семи пунктов: длина считалась как <c>sqrt(сумма квадратов)</c>,
/// а квадрат во float обнуляется при длине меньше 3.743392e-23 и переполняется
/// при длине больше 1.8446744e19. «Нулевой» и «огромный» вектор становились
/// неразличимы, и пороги вида <c>Epsilon²</c> решали не то свойство.
/// </para>
/// <para>
/// Доктрина файла:
/// <list type="bullet">
/// <item>внутри большого цикла только счётчик, проверка одна и после цикла —
/// xUnit не прерывает цикл на первом провале и печатает сообщение на каждый,
/// то есть на 200 000 точек это выглядит зависанием на 100 % CPU;</item>
/// <item>эталон считается независимо от проверяемого кода: в <c>double</c> для
/// порядка 1e-30 и ниже — в <c>BigInteger</c>, потому что <c>double</c> там
/// тоже перестаёт различать;</item>
/// <item>каждая проверка обязана различать состояния кода: обратный ход
/// выполнен и зафиксирован в отчёте волны.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class LengthAndSurfaceTests
{
    // ==================================================================
    // Пункт 1. Plane3: длина нормали и расхождение двух путей построения.

    /// <summary>
    /// П2-40: нормаль обязана быть единичной при любой длине исходной нормали.
    /// </summary>
    /// <remarks>
    /// Масштабы берутся по краям шкалы float и с запасом за её середину.
    /// Прежний код отдавал длину 0.909…1.091 при длине нормали 1e-22 (квадрат
    /// 1e-44 уходит в субнормальные числа и теряет разряды), а при длине
    /// 1.8446745e19 и выше квадрат переполнялся в бесконечность и нормаль
    /// получалась нулевой — молча, без исключения.
    /// </remarks>
    [Fact]
    public void Plane3_NormalStaysUnitAtEveryFloatMagnitude()
    {
        float[] magnitudes =
        [
            1e-45f, 1e-40f, 1e-35f, 1e-30f, 1e-25f, 1e-23f, 1e-22f, 3.7e-23f,
            1e-18f, 1f, 1e18f, 1.8e19f, 1.8446744e19f, 2e19f, 1e25f, 1e30f,
            1e35f, 3.4e38f,
        ];

        int thrown = 0;
        int notUnit = 0;
        int zero = 0;
        double worst = 0.0;
        float worstAt = 0f;

        foreach (float magnitude in magnitudes)
        {
            // Направление не по осям: у оси наибольшая компонента равна
            // единице и ошибка в ней видна лучше всего.
            Vector3 input = new(magnitude * 0.2672612f, magnitude * -0.5345225f, magnitude * 0.8017837f);
            if (input == Vector3.Zero)
            {
                continue;
            }

            // Все три входа проверяются на одном и том же входе: до правки
            // FromPointNormal и FromCoefficients отвергали малую нормаль и
            // отдавали нечисловую на большой, а конструктор на большой
            // отдавал нулевую.
            for (int path = 0; path < 3; path++)
            {
                Plane3 plane;
                try
                {
                    plane = path == 0
                        ? new Plane3(input, 1f)
                        : path == 1
                            ? Plane3.FromCoefficients(input, 1f)
                            : Plane3.FromPointNormal(Vector3.Zero, input);
                }
                catch (ArgumentException)
                {
                    thrown++;
                    continue;
                }

                if (plane.Normal == Vector3.Zero)
                {
                    zero++;
                    continue;
                }

                double deviation = Math.Abs(LengthAndSurfaceReference.Length(plane.Normal) - 1.0);
                if (deviation > worst)
                {
                    worst = deviation;
                    worstAt = magnitude;
                }

                if (deviation > 1e-6)
                {
                    notUnit++;
                }
            }
        }

        Assert.True(thrown == 0, $"Ненулевая нормаль отвергнута на {thrown} случаях.");
        Assert.True(zero == 0, $"Нормаль молча оказалась нулевой на {zero} случаях.");
        Assert.True(notUnit == 0, $"Нормаль не единичная на {notUnit} случаях из {magnitudes.Length * 3}; худшее отклонение {worst:E4} при длине {worstAt:E2}.");
    }

    /// <summary>
    /// П2-40, проверка против точного эталона: не «похоже на единицу», а
    /// совпадает с округлённым к float значением.
    /// </summary>
    [Fact]
    public void Plane3_NormalMatchesExactReferenceAtEveryMagnitude()
    {
        float[] magnitudes =
        [
            1e-45f, 1e-40f, 1e-35f, 1e-30f, 1e-25f, 1e-22f, 1e-18f, 1f,
            1e18f, 1.8e19f, 1.8446744e19f, 2e19f, 1e25f, 1e30f, 3.4e38f,
        ];

        int failing = 0;
        double worst = 0.0;
        float worstAt = 0f;

        foreach (float magnitude in magnitudes)
        {
            Vector3 input = new(magnitude * 0.2672612f, magnitude * -0.5345225f, magnitude * 0.8017837f);
            if (input == Vector3.Zero)
            {
                continue;
            }

            Vector3 expected = LengthAndSurfaceReference.Unit(input.X, input.Y, input.Z);
            Vector3 actual = new Plane3(input, 1f).Normal;

            double deviation = LengthAndSurfaceReference.UlpDistance(expected, actual);
            if (deviation > 2.0)
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
            $"Нормаль разошлась с точным эталоном более чем на 2 ULP в {failing} случаях; худшее {worst:F0} ULP при длине {worstAt:E2}.");
    }

    /// <summary>
    /// П2-41: конструктор и <see cref="Plane3.FromCoefficients"/> описывают одну
    /// и ту же плоскость <c>a·X + b = 0</c> и обязаны давать один и тот же
    /// результат побитово.
    /// </summary>
    /// <remarks>
    /// Расхождение в один ULP ломает <c>==</c> и разводит
    /// <see cref="object.GetHashCode"/>: одна и та же плоскость попадает в
    /// хеш-контейнер дважды и сравнивается с собой как с разной. Прежние два
    /// пути считались раздельно — здесь длина, там обратная величина длины —
    /// и расходились на 74 % из 200 000 пар.
    /// </remarks>
    [Fact]
    public void Plane3_ConstructorAndFactoryGiveBitwiseSamePlane()
    {
        const int pairs = 200000;
        (float x, float y, float z, float offset)[] cases =
        [
            (0f, 0f, 0f, 0f),
            (1f, 0f, 0f, 0f),
            (1f, 0f, 0f, 1f),
            (2f, 2f, 4f, 4f),
            (-3.7f, 0.5f, 11f, -0.25f),
            (1e-20f, 2e-20f, -3e-20f, 1e-20f),
            (1e20f, -2e20f, 3e20f, 7f),
            (1e-30f, 5e-31f, 0f, 1e-30f),
            (3.4e38f, 0f, 0f, 1f),
            (1f, 1f, 1f, 1f),
        ];

        int mismatches = 0;
        string firstMismatch = string.Empty;
        int index = 0;

        for (int i = 0; i < pairs; i++)
        {
            (float x, float y, float z, float offset) sample = cases[i % cases.Length];
            Vector3 normal = new(
                sample.x * LengthAndSurfaceReference.Wobble(i, 1),
                sample.y * LengthAndSurfaceReference.Wobble(i, 2),
                sample.z * LengthAndSurfaceReference.Wobble(i, 3));
            if (normal == Vector3.Zero)
            {
                continue;
            }

            float offset = sample.offset * LengthAndSurfaceReference.Wobble(i, 4);

            Plane3 byConstructor = new(normal, offset);
            Plane3 byFactory = Plane3.FromCoefficients(normal, offset);

            index++;
            if (byConstructor == byFactory)
            {
                continue;
            }

            mismatches++;
            if (firstMismatch.Length == 0)
            {
                firstMismatch = $"n = {normal}, b = {offset:R}: конструктор дал {byConstructor}, фабрика {byFactory}";
            }
        }

        Assert.True(index >= 100000, $"Набор слишком мал: {index} пар.");
        Assert.True(
            mismatches == 0,
            $"Конструктор и FromCoefficients разошлись на {mismatches} парах из {index}. Первый случай: {firstMismatch}");
    }

    /// <summary>
    /// Плоскость из <see cref="Plane3.FromPointNormal"/> обязана совпадать с
    /// плоскостью из конструктора на тех же коэффициентах уравнения.
    /// </summary>
    /// <remarks>
    /// Раньше <c>FromPointNormal</c> нормализовала нормаль и передавала её в
    /// конструктор, а тот нормализовал повторно. Величина <c>1/√(n·n)</c> у
    /// единичного вектора равна единице лишь приближённо, и второй проход
    /// сдвигал и нормаль, и свободный член.
    /// <para>
    /// Строка «побитово» здесь невозможна, и это не упущение. Два метода
    /// получают свободный член по-разному и обе формы верны:
    /// <c>FromPointNormal</c> считает <c>−(n̂·p)</c>, а
    /// <c>FromCoefficients(n, −n·p)</c> считает <c>(−n·p)/|n|</c>. Их разность
    /// — один ULP. Свести их к одной формуле можно только через коэффициентную
    /// форму, а она переполняется там, где прямая не переполняется: при
    /// <c>|n| = 1.7e19</c> и <c>|p| = 1e20</c> произведение <c>n·p</c> даёт
    /// бесконечность при конечном правильном ответе <c>−1e20</c>. Проверяется
    /// поэтому agreement с точностью в ULP, а сама плоскость — независимо, по
    /// невязке уравнения.
    /// </para>
    /// </remarks>
    [Fact]
    public void Plane3_PlaneFromPointNormalAgreesWithCoefficientForm()
    {
        const int samples = 200000;
        int beyondUlp = 0;
        double worstUlp = 0.0;
        double worstResidual = 0.0;
        string firstFailure = string.Empty;

        for (int i = 0; i < samples; i++)
        {
            Vector3 normal = new(
                LengthAndSurfaceReference.Wobble(i, 1) * 8f,
                LengthAndSurfaceReference.Wobble(i, 2) * 8f,
                LengthAndSurfaceReference.Wobble(i, 3) * 8f);
            if (normal.LengthSquared() <= float.Epsilon)
            {
                continue;
            }

            Vector3 point = new(
                LengthAndSurfaceReference.Wobble(i, 5) * 20f,
                LengthAndSurfaceReference.Wobble(i, 6) * 20f,
                LengthAndSurfaceReference.Wobble(i, 7) * 20f);

            Plane3 fromPoint = Plane3.FromPointNormal(point, normal);
            Plane3 fromCoefficients = Plane3.FromCoefficients(normal, -Vector3.Dot(normal, point));

            // Независимая проверка формы: точка, лежащая на плоскости FromPointNormal
            // по уравнению, обязана лежать и на ней самой.
            Vector3 onPlane = normal * (float)(-((double)Vector3.Dot(normal, point))
                / LengthAndSurfaceReference.LengthSquared(normal));
            double residual = Math.Abs(Plane3.FromPointNormal(onPlane, normal).DistanceTo(onPlane));
            if (residual > 1e-3)
            {
                beyondUlp++;
                firstFailure = $"точка {onPlane} не лежит на плоскости, остаток {residual:E3}";
            }

            if (residual > worstResidual)
            {
                worstResidual = residual;
            }

            double ulp = Math.Max(
                LengthAndSurfaceReference.UlpDistance(fromPoint.Distance, fromCoefficients.Distance),
                LengthAndSurfaceReference.UlpDistance(fromPoint.Normal, fromCoefficients.Normal));
            if (ulp > worstUlp)
            {
                worstUlp = ulp;
            }

            if (ulp <= 4.0)
            {
                continue;
            }

            beyondUlp++;
            if (firstFailure.Length == 0)
            {
                firstFailure = $"точка {point}, нормаль {normal}: расхождение {ulp:F0} ULP, {fromPoint} против {fromCoefficients}";
            }
        }

        Assert.True(
            beyondUlp == 0,
            $"FromPointNormal разошёлся с коэффициентной формой на {beyondUlp} случаях из {samples}; худшее {worstUlp:F0} ULP, худшая невязка {worstResidual:E3}. Первый: {firstFailure}");
    }

    /// <summary>
    /// Доктрина <c>a·X + b = 0</c> обязана выполняться после нормализации:
    /// точка <c>−a·b/|a|²</c> лежит на плоскости.
    /// </summary>
    /// <remarks>
    /// Эталон расстояния считается в <c>double</c> по исходным коэффициентам,
    /// то есть независимо от того, как плоскость построена. Именно эта проверка
    /// ломалась бы, если бы масштабирование наибольшей компонентой меняло саму
    /// плоскость, а не только её запись.
    /// </remarks>
    [Fact]
    public void Plane3_KeepsCoefficientDoctrineAfterScaling()
    {
        const int samples = 200000;
        int violations = 0;
        double worstResidual = 0.0;
        string firstViolation = string.Empty;

        for (int i = 0; i < samples; i++)
        {
            Vector3 normal = new(
                LengthAndSurfaceReference.Wobble(i, 1) * 6f,
                LengthAndSurfaceReference.Wobble(i, 2) * 6f,
                LengthAndSurfaceReference.Wobble(i, 3) * 6f);
            double squared = LengthAndSurfaceReference.LengthSquared(normal);
            if (squared <= float.Epsilon)
            {
                continue;
            }

            float offset = LengthAndSurfaceReference.Wobble(i, 4) * 30f;
            Plane3 plane = Plane3.FromCoefficients(normal, offset);

            double parameter = -(double)offset / squared;
            Vector3 onPlane = new(
                (float)(normal.X * parameter),
                (float)(normal.Y * parameter),
                (float)(normal.Z * parameter));

            double residual = Math.Abs(plane.DistanceTo(onPlane));
            double relative = residual / Math.Sqrt(squared);
            if (relative > worstResidual)
            {
                worstResidual = relative;
            }

            // Допуск — четыре последних разряда координаты точки: ровно тот
            // порядок, который накапливает сама float-арифметика при двух
            // умножениях и одном сложении.
            double tolerance = 4.0 * Math.Max(1.0, Math.Abs(parameter)) * 1.1920929e-7;
            if (residual > tolerance)
            {
                violations++;
                if (firstViolation.Length == 0)
                {
                    firstViolation = $"a = {normal}, b = {offset:R}, точка {onPlane}, остаток {residual:E3}";
                }
            }
        }

        Assert.True(
            violations == 0,
            $"Доктрина a·X + b = 0 нарушена {violations} раз из {samples}; худший нормированный остаток {worstResidual:E3}. Первый случай: {firstViolation}");
    }

    /// <summary>
    /// Настоящий нуль отвергается всеми тремя входами, а ненулевая нормаль любой
    /// длины — принимается.
    /// </summary>
    /// <remarks>
    /// Прежний порог <c>lengthSquared &lt;= float.Epsilon</c> отвергал
    /// нормали короче 3.743392e-23: они ненулевые, задают направление и
    /// нормализуемы. Отвергать должен только настоящий ноль — так же, как в
    /// <c>Segment2.ClosestPointTo</c> и <c>Circle2.ClosestPointOnBoundary</c>.
    /// </remarks>
    [Fact]
    public void Plane3_OnlyExactZeroNormalIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Plane3(Vector3.Zero, 1f));
        Assert.Throws<ArgumentException>(() => Plane3.FromPointNormal(Vector3.Zero, Vector3.Zero));
        Assert.Throws<ArgumentException>(() => Plane3.FromCoefficients(Vector3.Zero, 1f));

        float[] tiny = [1e-45f, 1e-30f, 3.7e-23f, 1e-19f];
        int rejected = 0;
        string firstRejected = string.Empty;

        foreach (float magnitude in tiny)
        {
            Vector3 normal = new(magnitude * 0.2672612f, magnitude * -0.5345225f, magnitude * 0.8017837f);
            try
            {
                Plane3 byConstructor = new(normal, 1f);
                Plane3 byFactory = Plane3.FromCoefficients(normal, 1f);
                if (byConstructor.Normal == Vector3.Zero)
                {
                    rejected++;
                    firstRejected = $"нулевая нормаль при |n| = {magnitude:E2}";
                    continue;
                }

                if (Math.Abs(LengthAndSurfaceReference.Length(byConstructor.Normal) - 1.0) > 1e-6)
                {
                    rejected++;
                    firstRejected = $"длина {LengthAndSurfaceReference.Length(byConstructor.Normal)} при |n| = {magnitude:E2}";
                    continue;
                }

                if (byConstructor != byFactory)
                {
                    rejected++;
                    firstRejected = $"расхождение путей при |n| = {magnitude:E2}";
                }
            }
            catch (ArgumentException)
            {
                rejected++;
                firstRejected = $"исключение при |n| = {magnitude:E2}";
            }
        }

        Assert.True(rejected == 0, $"Малая ненулевая нормаль отвергнута или обработана неверно: {firstRejected}");
    }

    /// <summary>
    /// Свободный член нормализуется той же величиной, что и нормаль, иначе
    /// плоскость съезжает по глубине.
    /// </summary>
    /// <remarks>
    /// Из <c>2x + 2y + 4 = 0</c> обязан получиться <c>0.707x + 0.707y + 2.828 = 0</c>,
    /// а не <c>0.707x + 0.707y + 4 = 0</c>. Проверяется и обычный случай, и
    /// случай за пределами надёжного диапазона, где длина считается
    /// масштабированием.
    /// </remarks>
    [Fact]
    public void Plane3_DistanceUsesSameScaleAsNormal()
    {
        (Vector3 normal, float offset, string name)[] cases =
        [
            (new Vector3(2f, 2f, 4f), 4f, "2x + 2y + 4 = 0"),
            (new Vector3(1e20f, 0f, 0f), 4f, "ось X длиной 1e20"),
            (new Vector3(0f, 3.4e38f, 0f), 1f, "ось Y длиной 3.4e38"),
            (new Vector3(1e-30f, 0f, 0f), 1e-30f, "ось X длиной 1e-30"),
        ];

        int failures = 0;
        string firstFailure = string.Empty;

        foreach ((Vector3 normal, float offset, string name) in cases)
        {
            Plane3 plane = Plane3.FromCoefficients(normal, offset);

            double squared = LengthAndSurfaceReference.LengthSquared(normal);
            double length = Math.Sqrt(squared);
            double expected = (double)offset / length;

            // Относительная ошибка: и свободный член, и точная величина могут
            // быть любого порядка, а сравнивать нужно именно их отношение.
            double relative = expected == 0.0
                ? Math.Abs(plane.Distance)
                : Math.Abs((plane.Distance - expected) / expected);

            if (relative > 1e-5)
            {
                failures++;
                if (firstFailure.Length == 0)
                {
                    firstFailure = $"{name}: свободный член {plane.Distance:R} вместо {expected:R}, ошибка {relative:P3}";
                }
            }

            // Точка a·(−b/|a|²) обязана лежать на плоскости — проверка формы, а
            // не повторение формулы выше.
            Vector3 probe = normal * (float)(-(double)offset / squared);
            double gap = Math.Abs(plane.DistanceTo(probe));
            if (gap > 1e-3 * Math.Max(1.0, Math.Abs(plane.Distance)))
            {
                failures++;
                if (firstFailure.Length == 0)
                {
                    firstFailure = $"{name}: точка {probe} не лежит на плоскости {plane}, невязка {gap:E3}";
                }
            }
        }

        Assert.True(failures == 0, firstFailure.Length == 0 ? "все случаи верны" : firstFailure);
    }

    // ==================================================================
    // Пункт 2. BoundingSphere: сравнение квадратов вместо расстояний.

    /// <summary>
    /// П2-39: <see cref="BoundingSphere.Contains"/> обязана отвечать по
    /// расстоянию, а не по квадрату радиуса.
    /// </summary>
    /// <remarks>
    /// Эталон — расстояние в <c>double</c>, то есть величина, которая на любом
    /// масштабе вычислима. Радиусы берутся по обе стороны от границ, где
    /// квадрат обнуляется (1.057e-19) и переполняется (1.8446744e19).
    /// </remarks>
    [Fact]
    public void BoundingSphere_ContainsFollowsDistanceAtEveryRadius()
    {
        float[] radii =
        [
            1e-40f, 1e-30f, 1e-19f, 1e-6f, 1f, 1e6f, 1e18f,
            1.8e19f, 1.8446743e19f, 1.8446744e19f, 2e19f, 1e25f, 3.4e38f,
        ];

        int wrong = 0;
        string firstWrong = string.Empty;

        // Точки на долях радиуса. Множитель 1 не берётся: там ответ решает
        // последний разряд округления, и проверка измеряла бы не дефект, а
        // границу представления. Всё остальное — заведомо внутри или
        // заведомо снаружи.
        float[] factors = [-0.5f, 0f, 0.25f, 0.5f, 0.75f, 0.9f, 1.1f, 1.25f, 1.5f, 2f];

        foreach (float radius in radii)
        {
            Vector3 center = new(3f, -2f, 7f);
            BoundingSphere sphere = new(center, radius);

            foreach (float factor in factors)
            {
                    Vector3 direction = LengthAndSurfaceReference.Unit(factor * 3f, factor * -4f, factor * 12f);
                Vector3 point = center + (direction * (radius * factor));

                bool got = sphere.Contains(point);
                double distance = LengthAndSurfaceReference.Length(point - center);
                bool want = distance <= (double)radius;

                if (got == want)
                {
                    continue;
                }

                wrong++;
                if (firstWrong.Length == 0)
                {
                    firstWrong = $"R = {radius:R}, множитель {factor}: {point} вернуло {got}, эталон {want} (расстояние {distance:E3})";
                }
            }
        }

        Assert.True(wrong == 0, $"Неверных ответов {wrong}. Первый: {firstWrong}");
    }

    /// <summary>
    /// П2-39: то же для <see cref="BoundingSphere.Intersects"/>, причём на
    /// случайных парах сфер, а не на построенных.
    /// </summary>
    [Fact]
    public void BoundingSphere_IntersectsFollowsDistanceAtEveryRadius()
    {
        float[] radii =
        [
            0f, 1e-30f, 1e-19f, 1e-6f, 1f, 1e6f, 1e18f,
            1.5e19f, 1.7e19f, 1.8e19f, 2e19f, 1e25f,
        ];

        int wrong = 0;
        string firstWrong = string.Empty;
        int checks = 0;

        for (int i = 0; i < 120000; i++)
        {
            float radiusA = radii[i % radii.Length];
            float radiusB = radii[(i / radii.Length) % radii.Length];

            Vector3 direction = LengthAndSurfaceReference.Unit(
                LengthAndSurfaceReference.Wobble(i, 1),
                LengthAndSurfaceReference.Wobble(i, 2),
                LengthAndSurfaceReference.Wobble(i, 3));
            if (direction == Vector3.Zero)
            {
                continue;
            }

            // Центры строятся от нуля в масштабе суммы радиусов. Если взять
            // постоянный центр вроде (7, −3, 11), то при радиусах порядка 1e-30
            // смещение на расстоянии в 1e-30 вообще не представимо: центры
            // сливаются, и проверка измеряла бы не дефект, а разрешение float.
            // Расстояние между центрами — фактическое, в double, а не
            // запрошенное: запрошенное может не совпасть с получившимся.
            float sum = radiusA + radiusB;

            // Центры строятся от нуля в масштабе суммы радиусов. Если взять
            // постоянный центр вроде (7, −3, 11), то при радиусах порядка 1e-30
            // смещение на расстоянии в 1e-30 вообще не представимо: центры
            // сливаются, и проверка измеряла бы не дефект, а разрешение float.
            // Расстояние между центрами — фактическое, в double, а не
            // запрошенное: запрошенное может не совпасть с получившимся.
            Vector3 centerA = direction * (sum == 0f ? 1f : sum * 4f);
            float separation = sum == 0f ? 1f : sum * (0.5f + (1.5f * LengthAndSurfaceReference.Wobble(i, 4)));

            Vector3 centerB = centerA + (direction * separation);

            BoundingSphere first = new(centerA, radiusA);
            BoundingSphere second = new(centerB, radiusB);

            bool got = first.Intersects(second);
            double actual = LengthAndSurfaceReference.Length(centerB - centerA);
            bool want = actual <= sum;
            checks++;

            if (got == want)
            {
                continue;
            }

            wrong++;
            if (firstWrong.Length == 0)
            {
                firstWrong = $"R₁ = {radiusA:R}, R₂ = {radiusB:R}, расстояние {actual:E3} при сумме {sum:R}: вернуло {got}, эталон {want}";
            }
        }

        Assert.True(checks > 100000, $"Набор слишком мал: {checks} пар.");
        Assert.True(wrong == 0, $"Неверных ответов {wrong} из {checks}. Первый: {firstWrong}");
    }

    /// <summary>
    /// Обычные радиусы должны вести себя точно как прежде: ветвление не
    /// должно менять ответ на игровом масштабе.
    /// </summary>
    [Fact]
    public void BoundingSphere_OrdinaryRadiiBehaveExactly()
    {
        float[] radii = [0f, 1e-6f, 0.05f, 0.25f, 1f, 1000f, 1e6f];
        int wrong = 0;
        string firstWrong = string.Empty;

        for (int i = 0; i < 100000; i++)
        {
            float radius = radii[i % radii.Length];
            Vector3 center = new(
                LengthAndSurfaceReference.Wobble(i, 1) * 100f,
                LengthAndSurfaceReference.Wobble(i, 2) * 100f,
                LengthAndSurfaceReference.Wobble(i, 3) * 100f);
            Vector3 point = center + (LengthAndSurfaceReference.Unit(
                LengthAndSurfaceReference.Wobble(i, 4),
                LengthAndSurfaceReference.Wobble(i, 5),
                LengthAndSurfaceReference.Wobble(i, 6)) * (radius * LengthAndSurfaceReference.Wobble(i, 7) * 2f));

            bool got = new BoundingSphere(center, radius).Contains(point);

            // Прежняя формула, вычисленная буквально: на этих радиусах квадрат
            // надёжен, и ответ обязан совпасть побитово.
            bool want = (point - center).LengthSquared() <= radius * radius;
            if (got == want)
            {
                continue;
            }

            wrong++;
            if (firstWrong.Length == 0)
            {
                firstWrong = $"R = {radius:R}, центр {center}, точка {point}: {got} вместо {want}";
            }
        }

        Assert.True(wrong == 0, $"На обычных радиусах ответ изменился {wrong} раз. Первый: {firstWrong}");
    }

    /// <summary>
    /// Эталон, написанный независимо от проверяемого кода.
    /// </summary>
    internal static class LengthAndSurfaceReference
    {
        /// <summary>
        /// Псевдослучайный множитель в [0.85; 1.15] по индексу и каналу.
        /// Детерминированный, чтобы прогоны повторялись побитово.
        /// </summary>
        public static float Wobble(int index, int channel)
        {
            uint hash = (uint)(index * 2654435761u) ^ ((uint)channel * 40503u);
            hash ^= hash >> 13;
            hash *= 1274126177u;
            hash ^= hash >> 16;
            return 0.85f + (0.3f * ((hash >> 8) * (1f / 16777216f)));
        }

        /// <summary>
        /// Длина вектора в double: во float на вырожденных длинах она сама
        /// обнуляется, и мера оказалась бы дефектной.
        /// </summary>
        public static double Length(Vector3 value)
            => Math.Sqrt(LengthSquared(value));

        public static double LengthSquared(Vector3 value)
            => ((double)value.X * value.X) + ((double)value.Y * value.Y) + ((double)value.Z * value.Z);

        public static double LengthSquared(Vector2 value)
            => ((double)value.X * value.X) + ((double)value.Y * value.Y);

        public static double Length(Vector2 value)
            => Math.Sqrt(LengthSquared(value));

        /// <summary>
        /// Единичный вектор по точному значению в <c>double</c>, округлённый к
        /// float. Наибольшая компонента приводится к единице заранее, иначе
        /// сумма квадратов переполнилась бы в <c>double</c> при |n| больше
        /// 1e154 — на границах float этого не бывает, но приём тот же, что и в
        /// проверяемом коде, и эталон от этого не зависит.
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
        /// Расстояние между двумя float в ULP по упорядоченным битам. Вычитание
        /// не годится: на масштабе 1e-30 относительное допущение в процентах
        /// ничего не значит.
        /// </summary>
        public static double UlpDistance(float expected, float actual)
        {
            if (float.IsNaN(expected) || float.IsNaN(actual))
            {
                return float.IsNaN(expected) && float.IsNaN(actual) ? 0.0 : double.PositiveInfinity;
            }

            if (expected == actual)
            {
                return 0.0;
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

        private static long Ordered(float value)
        {
            long bits = BitConverter.SingleToInt32Bits(value);
            return bits < 0 ? long.MinValue + bits : bits;
        }
    }
}