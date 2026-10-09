using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Обнаружение пересечений для примитивов 2D.
/// Используется в редакторах, отладочной отрисовке, проверке попаданий и в широкой фазе,
/// когда обращение к физическому движку излишне дорого.
/// </summary>
public static class Collision
{
    /// <summary>
    /// Проверяет пересечение двух AABB.
    /// </summary>
    /// <param name="a">Первый AABB.</param>
    /// <param name="b">Второй AABB.</param>
    /// <returns><c>true</c>, если AABB пересекаются.</returns>
    public static bool Intersects(Aabb2 a, Aabb2 b) => a.Intersects(b);

    /// <summary>
    /// Проверяет пересечение двух повёрнутых прямоугольников по разделяющим осям (SAT).
    /// </summary>
    /// <param name="centerA">Центр первого прямоугольника.</param>
    /// <param name="sizeA">Размер первого прямоугольника.</param>
    /// <param name="rotationA">Поворот первого прямоугольника.</param>
    /// <param name="centerB">Центр второго прямоугольника.</param>
    /// <param name="sizeB">Размер второго прямоугольника.</param>
    /// <param name="rotationB">Поворот второго прямоугольника.</param>
    /// <param name="penetrationAxis">Нормаль оси наименьшего проникновения.</param>
    /// <param name="penetrationDepth">Глубина проникновения по найденной оси.</param>
    /// <returns><c>true</c>, если прямоугольники пересекаются.</returns>
    /// <remarks>
    /// Оси берутся прямо из угла поворота: у прямоугольника они равны
    /// <c>(cos, sin)</c> и <c>(−sin, cos)</c>. Строить матрицу поворота,
    /// умножать её на перенос и звать TransformDirection было бессмысленно:
    /// перенос на направление не влияет, а смещение центров считается отдельно.
    /// <para>
    /// Проверка не делит проекции на длину оси: и разделение, и проекции
    /// радиусов умножаются на неё, поэтому знак перекрытия от деления не
    /// меняется, а корень берётся один раз — для победившей оси.
    /// </para>
    /// <para>
    /// Граница строгая: касание ребром не считается пересечением, и глубина
    /// проникновения при этом равна нулю. Это не расхождение с
    /// <see cref="Aabb2"/>, у которого границы включительные, а разные вопросы:
    /// Aabb2 спрашивает, есть ли общие точки объёмов, а этот метод — есть ли
    /// что раздвигать. У касающихся форм раздвигать нечего, и возвращать
    /// <c>true</c> с нулевой глубиной значило бы сообщить о контакте там, где
    /// физике нужен импульс.
    /// </para>
    /// </remarks>
    public static bool TryGetObbPenetration(
        Vector2 centerA,
        Vector2 sizeA,
        Angle rotationA,
        Vector2 centerB,
        Vector2 sizeB,
        Angle rotationB,
        out Vector2 penetrationAxis,
        out float penetrationDepth)
    {
        // Порядок элементов кортежа — сначала синус, потом косинус, как в
        // MathF.SinCos. Деконструкция именованная, но позиционная, поэтому
        // имена переменных обязаны идти в том же порядке: если назвать первую
        // переменную cos, она получит синус, и система осей окажется
        // переставленной по координатам. Для угла 0 градусов, 45, 90 и 135
        // переставленная система совпадает с правильной, поэтому дефект не
        // проявлялся на тестах с такими углами.
        (float sinA, float cosA) = Trig.SinCos((float)rotationA.Radians);
        (float sinB, float cosB) = Trig.SinCos((float)rotationB.Radians);

        Vector2 axisA0 = new(cosA, sinA);
        Vector2 axisA1 = new(-sinA, cosA);
        Vector2 axisB0 = new(cosB, sinB);
        Vector2 axisB1 = new(-sinB, cosB);

        Vector2 halfA = sizeA * 0.5f;
        Vector2 halfB = sizeB * 0.5f;
        Vector2 delta = centerB - centerA;

        Vector2 bestAxis = Vector2.Zero;
        float bestOverlap = float.MaxValue;

        for (int index = 0; index < 4; index++)
        {
            Vector2 axis = index switch
            {
                0 => axisA0,
                1 => axisA1,
                2 => axisB0,
                _ => axisB1,
            };

            float separation = MathF.Abs(Vector2.Dot(delta, axis));
            float radiusA = MathF.Abs(Vector2.Dot(axisA0, axis)) * halfA.X
                            + MathF.Abs(Vector2.Dot(axisA1, axis)) * halfA.Y;
            float radiusB = MathF.Abs(Vector2.Dot(axisB0, axis)) * halfB.X
                            + MathF.Abs(Vector2.Dot(axisB1, axis)) * halfB.Y;

            float overlap = radiusA + radiusB - separation;
            if (overlap <= 0f)
            {
                penetrationAxis = Vector2.UnitY;
                penetrationDepth = 0f;
                return false;
            }

            if (overlap < bestOverlap)
            {
                bestOverlap = overlap;
                bestAxis = axis;
            }
        }

        // Оси получены из sin и cos, то есть единичные с точностью до
        // округления. Нормировка нужна один раз, чтобы глубина проникновения
        // была в тех же единицах, что и стороны прямоугольников.
        float bestLength = bestAxis.Length();
        if (bestLength <= 0f)
        {
            penetrationAxis = Vector2.UnitY;
            penetrationDepth = 0f;
            return false;
        }

        penetrationDepth = bestOverlap / bestLength;
        Vector2 unit = bestAxis / bestLength;
        penetrationAxis = Vector2.Dot(delta, bestAxis) < 0f ? -unit : unit;
        return true;
    }

    /// <summary>
    /// Проверяет пересечение двух капсул.
    /// Сводится к поиску минимального расстояния между осевыми линиями и сравнению
    /// с суммой радиусов.
    /// </summary>
    /// <param name="a">Первая капсула.</param>
    /// <param name="b">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы пересекаются.</returns>
    public static bool Intersects(Capsule2 a, Capsule2 b)
    {
        if (!a.Bounds.Intersects(b.Bounds))
        {
            return false;
        }

        float distance = SegmentSegmentDistance(a.Segment2, b.Segment2);
        float radii = a.Radius + b.Radius;
        return distance * distance <= radii * radii;
    }

    /// <summary>
    /// Возвращает расстояние между двумя отрезками.
    /// </summary>
    /// <param name="a">Первый отрезок.</param>
    /// <param name="b">Второй отрезок.</param>
    /// <returns>Минимальное расстояние между отрезками.</returns>
    /// <remarks>
    /// Считается стандартным методом ближайших точек двух отрезков
    /// (Real-Time Collision Detection, 5.1.9): оба параметра находятся
    /// ограничением диапазона 0..1, поэтому результат не зависит от того,
    /// пересекаются отрезки или нет, и не требует отдельной проверки на
    /// пересечение.
    /// <para>
    /// Прежняя проверка пересечения сравнивала с Scalar.Epsilon векторное
    /// произведение, то есть величину в квадратных метрах, и для отрезков
    /// короче примерно миллиметра молча пропускалась: пересекающиеся отрезки
    /// сообщали ненулевое расстояние. Здесь порог применяется только к квадратам
    /// длин и в тех же единицах, в которых они измеряются.
    /// </para>
    /// <para>
    /// Отрезок считается вырожденным в точку только при точном нулевом квадрате
    /// длины. Прежний порог <c>1e-12</c> в квадратных единицах отбрасывал любой
    /// отрезок короче микрона, независимо от масштаба мира, и на микроскопическом
    /// масштабе это была ошибка в сто процентов длины: из 20000 заведомо
    /// пересекающихся отрезков ненулевое расстояние возвращали 20000.
    /// Обоснование «у отрезка короче микрона во float нет различимой внутренности»
    /// измерено и опровергнуто: ULP на величине 1e-6 равен 1.137e-13, то есть
    /// около 8.8 миллиона различимых положений на единицу длины. Длина, равная
    /// нулю, вырождена действительно, а ненулевая — нет.
    /// </para>
    /// </remarks>
    public static float SegmentSegmentDistance(Segment2 a, Segment2 b)
    {
        // Минимизируется |r + pf·d1 − ps·d2|, поэтому условия стационарности
        // дают pf = (b·f − c·e) / (a·e − b²) и ps = (f + pf·b) / e.
        // Имена параметров здесь заданы явно: parameterFirst относится к
        // первому отрезку, parameterSecond — ко второму. Раньше эти роли были
        // перепутаны в двух ветках, и расстояние получалось завышенным.
        Vector2 p1 = a.A;
        Vector2 p2 = b.A;
        Vector2 d1 = a.Delta;
        Vector2 d2 = b.Delta;
        Vector2 r = p1 - p2;

        float lengthSquared1 = Vector2.Dot(d1, d1);
        float lengthSquared2 = Vector2.Dot(d2, d2);
        float along2 = Vector2.Dot(d2, r);

        float parameterFirst;
        float parameterSecond;
        if (lengthSquared1 <= 0f && lengthSquared2 <= 0f)
        {
            parameterFirst = 0f;
            parameterSecond = 0f;
        }
        else if (lengthSquared1 <= 0f)
        {
            parameterFirst = 0f;
            parameterSecond = Scalar.Clamp(along2 / lengthSquared2, 0f, 1f);
        }
        else
        {
            float along1 = Vector2.Dot(d1, r);
            if (lengthSquared2 <= 0f)
            {
                parameterFirst = Scalar.Clamp(-along1 / lengthSquared1, 0f, 1f);
                parameterSecond = 0f;
            }
            else
            {
                float mutual = Vector2.Dot(d1, d2);

                // Знаменатель a·e − b² равен |d1|²|d2|²sin²θ, то есть это
                // произведение квадратов длин на квадрат синуса угла. Сравнивать
                // его с абсолютным допуском нельзя: результат зависел бы от
                // масштаба мира, и отрезки короче миллиметра всегда считались бы
                // параллельными. Порог задан относительно того же произведения,
                // то есть по существу проверяет sin²θ > Epsilon и от масштаба не
                // зависит.
                float denominator = (lengthSquared1 * lengthSquared2) - (mutual * mutual);
                parameterFirst = denominator > Scalar.Epsilon * lengthSquared1 * lengthSquared2
                    ? Scalar.Clamp(((mutual * along2) - (lengthSquared2 * along1)) / denominator, 0f, 1f)
                    : 0f;

                parameterSecond = (along2 + (parameterFirst * mutual)) / lengthSquared2;
                if (parameterSecond < 0f)
                {
                    parameterSecond = 0f;
                    parameterFirst = Scalar.Clamp(-along1 / lengthSquared1, 0f, 1f);
                }
                else if (parameterSecond > 1f)
                {
                    parameterSecond = 1f;
                    parameterFirst = Scalar.Clamp((mutual - along1) / lengthSquared1, 0f, 1f);
                }
            }
        }

        Vector2 closestFirst = p1 + (d1 * parameterFirst);
        Vector2 closestSecond = p2 + (d2 * parameterSecond);
        return Vector2.Distance(closestFirst, closestSecond);
    }

    /// <summary>
    /// Проверяет, находится ли точка внутри повёрнутого прямоугольника.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <param name="center">Центр прямоугольника.</param>
    /// <param name="size">Размер прямоугольника.</param>
    /// <param name="rotation">Поворот прямоугольника.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    /// <remarks>
    /// Порядок множителей задан соглашением <c>System.Numerics</c>:
    /// произведение <c>A * B</c> применяет <b>A первым</b> (то же, что в
    /// <see cref="Matrix4x4Extensions.CreateViewProjection"/>, где <c>view *
    /// projection</c> применяет сначала вид). Отсюда сначала перенос, потом
    /// поворот: <c>Translation(-center) * Rotation(-rotation)</c>.
    /// <para>
    /// Обратный порядок вращает точку вокруг мирового начала координат и
    /// только потом сдвигает, то есть спрашивает попадание в прямоугольник,
    /// центр которого повёрнут относительно настоящего центра. При центре в
    /// начале координат обе формулы совпадают, поэтому дефект не проявлялся
    /// на тесте с центром в нуле.
    /// </para>
    /// </remarks>
    public static bool Contains(Vector2 point, Vector2 center, Vector2 size, Angle rotation)
    {
        // Поворот считается через Trig, а не Matrix3x2.CreateRotation: тот
        // внутри обращается к математической библиотеке платформы.
        Matrix3x2 inverse = Matrix3x2.CreateTranslation(-center)
            * Matrix3x2Extensions.CreateRotation(-rotation);
        Vector2 local = Vector2.Transform(point, inverse);
        return MathF.Abs(local.X) <= size.X * 0.5f && MathF.Abs(local.Y) <= size.Y * 0.5f;
    }

    /// <summary>
    /// Возвращает расстояние между двумя точками с допуском.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    /// <param name="epsilon">Допуск, при котором расстояние считается нулевым.</param>
    /// <returns>Расстояние.</returns>
    public static float Distance(Vector2 a, Vector2 b, float epsilon = Scalar.Epsilon)
    {
        float distance = Vector2.Distance(a, b);
        return distance <= epsilon ? 0f : distance;
    }}
