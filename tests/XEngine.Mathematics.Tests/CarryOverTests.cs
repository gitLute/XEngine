using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Три незакрытые копии правок волны 4, обязательные к закрытию в волне 5.
///
/// <para>
/// Приём один: проверка стоит на величине, которая не является той, что
/// ограничивается. В <c>BoundingSphere</c> это был квадрат радиуса, который
/// обнулялся или переполнялся на краях диапазона <see cref="float"/>; здесь тот
/// же приём в трёх других местах. Лечение тоже одно — сравнение длин в
/// масштабе наибольшей компоненты — и одна доктринальная поправка по измерению.
/// </para>
///
/// <para>
/// Контрольные примеры построены на входах, где дефект проявляется, а не
/// рядом с ним: диаметр порядка 1e19 и ось порядка 1e-7.
/// </para>
/// </summary>
public class CarryOverTests
{
    // =================================================================
    // Копия P2-39: Circle2.Contains и Circle2.Intersects
    // =================================================================

    /// <summary>
    /// Квадрат радиуса переполняется при <c>R = 1.8446744e19</c> и выше, и
    /// сравнение <c>inf ≤ x</c> отвечало <c>true</c> для любой точки. Граница
    /// обнуления проходит по <c>R ≈ 1.08e-22</c>, где <c>x ≤ 0</c> отвечало
    /// <c>true</c> для точки возле центра.
    /// </summary>
    [Theory]
    [InlineData(1.8446744e19f)]
    [InlineData(1.8e19f)]
    [InlineData(2e19f)]
    [InlineData(1e20f)]
    [InlineData(1e30f)]
    public void Circle2_ContainsSurvivesOverflowingRadiusSquared(float radius)
    {
        Circle2 circle = new(Vector2.Zero, radius);
        int wrong = 0;

        for (int i = 0; i < 2000; i++)
        {
            // Точка заведомо вне круга: расстояние заметно больше радиуса.
            Vector2 point = new(radius * 2f, 0f);
            if (circle.Contains(point))
            {
                wrong++;
            }
        }

        Assert.Equal(0, wrong);
    }

    /// <summary>
    /// Точка заведомо внутри круга обязана оставаться внутри на любом радиусе.
    /// Это проверка с другой стороны той же границы: прежний код отвечал
    /// <c>true</c> везде, и обе стороны давали один и тот же неверный ответ.
    /// </summary>
    [Theory]
    [InlineData(1e-22f)]
    [InlineData(1e-30f)]
    [InlineData(1e-38f)]
    public void Circle2_ContainsKeepsInteriorPointInside(float radius)
    {
        Circle2 circle = new(Vector2.Zero, radius);
        int wrong = 0;

        for (int i = 0; i < 2000; i++)
        {
            Vector2 point = new(radius * 0.5f, 0f);
            if (!circle.Contains(point))
            {
                wrong++;
            }
        }

        Assert.Equal(0, wrong);
    }

    /// <summary>
    /// Сумма радиусов переполняется раньше квадрата, то есть граница
    /// <c>Intersects</c> проходит ниже границы <c>Contains</c>.
    /// </summary>
    [Theory]
    [InlineData(1.8e19f)]
    [InlineData(1.8446744e19f)]
    [InlineData(2e19f)]
    public void Circle2_IntersectsSurvivesOverflowingRadiusSum(float radius)
    {
        Circle2 first = new(Vector2.Zero, radius);
        int wrong = 0;

        for (int i = 0; i < 2000; i++)
        {
            // Круг заведомо далеко: расстояние втрое больше суммы радиусов.
            Circle2 far = new(new Vector2(radius * 6f, 0f), radius);
            if (first.Intersects(far))
            {
                wrong++;
            }
        }

        Assert.Equal(0, wrong);
    }

    /// <summary>
    /// Круги, которые действительно пересекаются, не перестают пересекаться
    /// на большом масштабе.
    /// </summary>
    [Fact]
    public void Circle2_IntersectsKeepsTouchingCirclesTouching()
    {
        foreach (float radius in new[] { 1.8446744e19f, 2e19f, 1e20f })
        {
            Circle2 first = new(Vector2.Zero, radius);
            Circle2 touching = new(new Vector2(radius * 2f, 0f), radius);
            Assert.True(first.Intersects(touching), $"Радиус {radius:R}: касание потеряно");
        }
    }

    /// <summary>
    /// Контроль без дефекта: на обычных радиусах ответы прежние.
    /// </summary>
    [Fact]
    public void Circle2_OnOrdinaryRadiiIsUnchanged()
    {
        foreach (float radius in new[] { 1e-6f, 1f, 1e3f, 1e6f })
        {
            Circle2 circle = new(Vector2.Zero, radius);

            Assert.True(circle.Contains(Vector2.Zero));
            Assert.True(circle.Contains(new Vector2(radius * 0.5f, 0f)));
            Assert.False(circle.Contains(new Vector2(radius * 2f, 0f)));
            Assert.True(circle.Intersects(new Circle2(new Vector2(radius * 1.5f, 0f), radius)));
            Assert.False(circle.Intersects(new Circle2(new Vector2(radius * 3f, 0f), radius)));
        }
    }

    // =================================================================
    // Копия P2-33: Ray3.IntersectsCylinder, порог оси
    // =================================================================

    /// <summary>
    /// Вырожденной считается нулевая ось. Прежний порог
    /// <c>axisLengthSquared ≤ Epsilon²</c> — буквально тот же дефект, что закрыт
    /// в <c>Capsule3</c> точным нулём: у осевой линии нет собственного размера,
    /// с которым её можно было бы сравнить.
    /// </summary>
    /// <remarks>
    /// <b>Как найден различающий вход, в два шага.</b> Первая попытка обратного
    /// хода дала ноль, и на то было две причины, обе в геометрии.
    /// <para>
    /// Первая: при <c>radius ≫ axis</c> сфера на конце накрывает всю капсулу
    /// целиком, и обе ветви отвечают одинаково. Расхождение появляется только
    /// при <c>axis ≳ radius</c>.
    /// </para>
    /// <para>
    /// Вторая, и она важнее. <see cref="Ray3.Intersects(Capsule3, out float)"/>
    /// проверяет сферы на обоих концах <b>отдельно</b> от цилиндра, поэтому
    /// различаться может только луч, который не попадает ни в одну из них. Первый
    /// найденный луч попадал в сферу на конце <c>B</c>, и ответ «попадает»
    /// давался в обоих кодах. Нужен луч, проходящий сквозь <b>тело</b> цилиндра
    /// между концами и мимо обоих концов.
    /// </para>
    /// <para>
    /// Геометрия контрольного входа: луч идёт вдоль <c>+Z</c> на расстоянии
    /// <c>0.9·radius</c> от оси, над её серединой. До отрезка оси расстояние
    /// равно <c>0.9·radius</c> — меньше радиуса, то есть луч пересекает тело
    /// капсулы. До каждого из концов — <c>sqrt((axis/2)² + 0.81·radius²)</c>,
    /// что при <c>axis = radius</c> равно <c>1.03·radius</c> и больше радиуса,
    /// то есть луч не попадает ни в одну из сфер на концах. Измерено на прежнем
    /// коде: сфера A — «нет», сфера B — «нет», капсула — «нет»; после правки
    /// капсула — «да». Рейкаст проходил насквозь сквозь коллайдер, мимо которого
    /// луч физически проходит.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1e-7f)]
    [InlineData(3e-7f)]
    [InlineData(5e-7f)]
    [InlineData(9e-7f)]
    public void Ray3_HitsShortAxisCapsule(float axis)
    {
        // Радиус сравним с осью: при radius ≫ axis сфера на конце накрывает
        // капсулу целиком и разницы между ветвями нет вовсе.
        float radius = axis;
        Capsule3 capsule = new(Vector3.Zero, new Vector3(axis, 0f, 0f), radius);

        // Луч над серединой оси, смещённый на 0.9 радиуса, идущий вдоль +Z.
        Vector3 origin = new(axis * 0.5f, radius * 0.9f, -radius * 8f);
        Ray3 ray = new(origin, Vector3.UnitZ);

        Assert.True(ray.Intersects(capsule), $"Ось {axis:R}: луч прошёл сквозь тело капсулы.");
    }

    /// <summary>
    /// Контроль к предыдущему: луч не должен попадать в сферы на концах.
    /// Именно этим случаи различаются, и без этой проверки тест выше проходил бы
    /// и на прежнем коде.
    /// </summary>
    [Fact]
    public void Ray3_ShortAxisCaseMissesBothEndpointSpheres()
    {
        const float Axis = 9e-7f;
        const float Radius = 9e-7f;

        Vector3 origin = new(Axis * 0.5f, Radius * 0.9f, -Radius * 8f);
        Ray3 ray = new(origin, Vector3.UnitZ);

        Assert.False(ray.Intersects(new BoundingSphere(Vector3.Zero, Radius)), "Сфера на конце A");
        Assert.False(ray.Intersects(new BoundingSphere(new Vector3(Axis, 0f, 0f), Radius)), "Сфера на конце B");
    }

    /// <summary>
    /// Настоящий ноль оси отдаёт капсулу-сферу, и луч, попадающий в неё,
    /// пересекается, а промахивающийся — нет.
    /// </summary>
    [Fact]
    public void Ray3_ZeroAxisIsStillASphere()
    {
        Capsule3 sphere = new(Vector3.Zero, Vector3.Zero, 1f);
        Ray3 through = new(new Vector3(-5f, 0f, 0f), Vector3.UnitX);
        Ray3 away = new(new Vector3(-5f, 0f, 5f), Vector3.UnitX);

        Assert.True(through.Intersects(sphere));
        Assert.False(away.Intersects(sphere));
    }

    /// <summary>
    /// Контроль без дефекта: капсулы обычных размеров ведут себя как прежде,
    /// то есть луч попадает в тело и промахивается мимо.
    /// </summary>
    [Fact]
    public void Ray3_OnOrdinaryCapsulesIsUnchanged()
    {
        Capsule3 capsule = new(new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f), 0.5f);

        // Луч, идущий через ось капсулы, попадает.
        Assert.True(new Ray3(new Vector3(0f, 0f, -5f), Vector3.UnitZ).Intersects(capsule));

        // Луч, уходящий от капсулы, промахивается.
        Assert.False(new Ray3(new Vector3(0f, 0f, -5f), -Vector3.UnitZ).Intersects(capsule));

        // Луч, идущий выше капсулы на расстоянии больше радиуса, промахивается.
        Assert.False(new Ray3(new Vector3(0f, 5f, 0f), Vector3.UnitX).Intersects(capsule));
    }

    // =================================================================
    // Копия доктрины: Frustum, отношение порогов
    // =================================================================

    /// <summary>
    /// Доктрина сторожа утверждала, что длина суммы строк дальней плоскости
    /// при <c>far = 10 км</c> и <c>near = 1 м</c> равна <c>2e-7</c>, то есть
    /// «в сто раз меньше» прежнего порога <c>1e-6</c>. Оба числа неверны.
    /// </summary>
    /// <remarks>
    /// Измерено на собранной самой библиотекой камере. Величина равна
    /// <c>2·near/(far−near)</c>, и при <c>far/near = 1e4</c> это
    /// <c>2.0003e-4</c>, то есть <b>в двести раз</b> меньше <c>1e-6</c>.
    /// Величина <c>2e-7</c> получается при <c>far/near = 1e7</c>, и тогда
    /// отношение действительно равно 0.2, то есть <b>в пять раз</b> меньше.
    /// Само число было верным, пример к нему — нет; текст соединял их как одно
    /// утверждение, и потому неверными оказались оба.
    /// <para>
    /// Допуск — в один разряд единицы, а не в 1 %. Причина та же, что и у
    /// границы отказа: коэффициент <c>M33</c> лежит около единицы, и разность
    /// <c>M34 − M33</c> квантуется с шагом в <c>ulp(1) = 1.192e-7</c>.
    /// На <c>far/near = 1e7</c> истинное значение <c>2e-7</c> меньше двух
    /// разрядов единицы, и округление даёт ровно два — расхождение 19 %.
    /// Это измерено и внесено в доктрину как часть описания.
    /// </para>
    /// </remarks>
    [Fact]
    public void Frustum_FarRowSumMatchesTwoNearOverFar()
    {
        // ulp(1.0) — шаг, которым квантуется разность коэффициентов около единицы.
        const float Ulp = 1.1920929e-7f;

        foreach ((float near, float far) in new[] { (1f, 100f), (1f, 1000f), (1f, 10000f), (0.1f, 10000f) })
        {
            Matrix4x4 clip = BuildClip(near, far);

            // Дальняя плоскость выводится из разности строк clip-матрицы.
            float x = clip.M14 - clip.M13;
            float y = clip.M24 - clip.M23;
            float z = clip.M34 - clip.M33;
            float measured = MathF.Sqrt((x * x) + (y * y) + (z * z));

            float predicted = (float)(2.0 * near / (far - near));
            float tolerance = MathF.Max(predicted * 0.01f, Ulp);

            Assert.True(
                MathF.Abs(measured - predicted) <= tolerance,
                $"near = {near:R}, far = {far:R}: измерено {measured:E6}, ожидалось {predicted:E6}, допуск {tolerance:E3}");
        }
    }

    /// <summary>
    /// Проверка самого числа «в пять раз» — и одновременно границы его
    /// применимости. Арифметика <c>2e-7 / 1e-6 = 0.2</c> верна, но 2e-7 —
    /// это <b>идеальное</b> значение <c>2·near/(far−near)</c>, а не то, что
    /// получается на камере.
    /// </summary>
    /// <remarks>
    /// Измерено. Идеальное значение — <c>2·near/(far−near)</c>, измеренное
    /// отличается от него квантованием разности <c>M34 − M33</c> с шагом в один
    /// разряд единицы, <c>ulp(1) = 1.1920929e-7</c>.
    /// <para>
    /// Пример из доктрины — <c>near = 1 м</c> при <c>far = 10 км</c> — даёт
    /// отношение <b>200</b>, то есть «в двести раз», а не «в сто». Камера, на
    /// которой «в пять раз» действительно выполняется, — <c>near = 1 мм</c>
    /// при том же <c>far</c>: отношение 0.238. Именно её имел в виду текст,
    /// но в примере стоял <c>near = 1 м</c>.
    /// </para>
    /// <para>
    /// Вывод важнее чисел: обе камеры бесполезны как иллюстрация порядка, потому
    /// что измеренная величина на них — 2.0e-4 и 2.4e-7 против идеальных 2.0e-4
    /// и 2.0e-7. На второй расхождение 19 %, то есть пример «в пять раз» не
    /// воспроизводится даже на своей камере. Сравнение с абсолютной длиной
    /// коэффициентов отвергало не её, а любую камеру с
    /// <c>far/near &gt; 2e6</c>, где величина ещё велика и отношение
    /// действительно меньше единицы. Поэтому правка доктрины — не «в сто
    /// → в пять», а «пример и число разведены, и оба требовали замены на
    /// измеренные».
    /// </para>
    /// </remarks>
    [Fact]
    public void Frustum_RowSumAtRatioTenMillionIsQuantizedToTwoUnits()
    {
        const float Ulp = 1.1920929e-7f;
        const float Ideal = 2e-7f;

        // Камера, которой пример в доктрине соответствовал по числу.
        Matrix4x4 quantized = BuildClip(0.001f, 10000f);
        float quantizedZ = quantized.M34 - quantized.M33;
        float measured = MathF.Sqrt((quantizedZ * quantizedZ));

        // Идеальное значение 2.0e-7 — это меньше двух разрядов единицы, и
        // округление даёт ровно два: расхождение 19 %.
        Assert.Equal(2f * Ulp, measured, Ulp * 0.01f);
        Assert.True(
            measured > Ideal && measured < Ideal * 1.25f,
            $"Отношение к прежнему порогу равно {measured / 1e-6f:F4}, измерено {measured:E6}");

        // Камера из примера доктрины: отношение около 200, то есть «в двести
        // раз», а не «в сто».
        Matrix4x4 example = BuildClip(1f, 10000f);
        float exampleZ = example.M34 - example.M33;
        float ratio = MathF.Sqrt((exampleZ * exampleZ)) / 1e-6f;
        Assert.True(
            ratio > 150f && ratio < 260f,
            $"Отношение к прежнему порогу равно {ratio:F1}, а не около 200");
    }

    /// <summary>
    /// Проверка ошибки в примере: при <c>far = 10 км</c> и <c>near = 1 м</c>
    /// отношение к прежнему порогу равно 200, то есть в двести раз, а не
    /// «в сто раз» и не «в пять раз».
    /// </summary>
    [Fact]
    public void Frustum_RowSumIsTwoHundredTimesBelowThresholdAtDocstringExample()
    {
        Matrix4x4 clip = BuildClip(1f, 10000f);
        float z = clip.M34 - clip.M33;
        float measured = MathF.Sqrt((z * z));

        float ratio = measured / 1e-6f;
        Assert.True(
            ratio > 150f && ratio < 260f,
            $"Отношение к прежнему порогу равно {ratio:F1}, а не около 200");
    }

    /// <summary>
    /// Граница отказа сторожа измерена делением по битовому представлению:
    /// прежняя доктрина называла <c>far/near ≳ 1e8</c>, на деле при
    /// <c>near = 1</c> камера перестаёт выводиться на <c>far = 16777218</c>,
    /// то есть в шесть раза ниже.
    /// </summary>
    /// <remarks>
    /// Механизм — округление самих границ, а не «элементы <c>M33</c> и
    /// <c>M34</c> совпадают»: перспектива делит <c>far + near</c> на
    /// <c>far − near</c>, обе величины округляются раздельно, и на границе они
    /// округляются в одно число. Проверяется именно этот механизм, потому что
    /// он объясняет число и отличает правку от подгонки.
    /// </remarks>
    [Fact]
    public void Frustum_RejectionBoundaryMatchesMeasurement()
    {
        const float Boundary = 16777218f;

        // Последняя живая камера: сумма и разность ещё различны после
        // округления, коэффициент ненулевой.
        Matrix4x4 alive = BuildClip(1f, Boundary);
        Assert.True(
            (alive.M34 - alive.M33) != 0f,
            "На последней живой камере коэффициент дальней плоскости обязан быть ненулевым.");
        Assert.True(BuildsFrustum(Boundary), "Камера на границе обязана строиться.");

        // Следующий соседний float: обе величины округляются в одно число.
        float next = MathF.BitIncrement(Boundary);
        Matrix4x4 rejected = BuildClip(1f, next);
        Assert.Equal(0f, rejected.M34 - rejected.M33, 1e-7f);
        Assert.Equal(rejected.M34, rejected.M33);
        Assert.False(BuildsFrustum(next), "Камера за границей обязана отвергаться.");

        // Прежнее число из доктрины заведомо отвергается.
        Assert.False(BuildsFrustum(1e8f), "Камера с far/near = 1e8 обязана отвергаться.");
        Assert.True(BuildsFrustum(1e6f), "Камера с far/near = 1e6 обязана строиться.");
    }

    /// <summary>
    /// Камера, собранная самой библиотекой при законном отношении плоскостей,
    /// обязана давать фрустум: сравнение с абсолютной длиной коэффициентов его
    /// отвергало.
    /// </summary>
    [Fact]
    public void Frustum_AcceptsCameraWithTenThousandDepthRatio()
    {
        Frustum frustum = BuildFrustumOrThrow(1f, 10000f);

        Assert.True(
            frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, -100f), 1f)),
            "Объект перед камерой на расстоянии 100 м обязан быть виден при far/near = 1e4.");
    }

    private static bool BuildsFrustum(float farOverNear)
    {
        try
        {
            _ = Frustum.FromViewProjection(BuildClip(1f, farOverNear));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Matrix4x4 BuildClip(float near, float far)
    {
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(
            Vector3.Zero,
            new Vector3(0f, 0f, -1f),
            Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(
            Angle.FromDegrees(60f),
            16f / 9f,
            near,
            far);
        return Matrix4x4Extensions.CreateViewProjection(view, projection);
    }

    private static Frustum BuildFrustumOrThrow(float near, float far)
        => Frustum.FromViewProjection(BuildClip(near, far));
}