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
    public static bool Intersects(Aabb a, Aabb b) => a.Intersects(b);

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
        Matrix3x2 transformA = Matrix3x2.CreateRotation((float)rotationA.Radians) * Matrix3x2.CreateTranslation(centerA);
        Matrix3x2 transformB = Matrix3x2.CreateRotation((float)rotationB.Radians) * Matrix3x2.CreateTranslation(centerB);

        Span<Vector2> axesA =
        [
            transformA.TransformDirection(Vector2.UnitX),
            transformA.TransformDirection(Vector2.UnitY),
        ];
        Span<Vector2> axesB =
        [
            transformB.TransformDirection(Vector2.UnitX),
            transformB.TransformDirection(Vector2.UnitY),
        ];

        Vector2 halfA = sizeA * 0.5f;
        Vector2 halfB = sizeB * 0.5f;
        Vector2 delta = centerB - centerA;

        penetrationAxis = Vector2.UnitY;
        penetrationDepth = float.MaxValue;

        Span<Vector2> axes = [axesA[0], axesA[1], axesB[0], axesB[1]];
        Span<Vector2> halfExtents = [halfA, halfA, halfB, halfB];

        for (int i = 0; i < axes.Length; i++)
        {
            Vector2 axis = axes[i].SafeNormalize();
            if (axis == Vector2.Zero)
            {
                continue;
            }

            float separation = MathF.Abs(Vector2.Dot(delta, axis));
            float radiusA = MathF.Abs(Vector2.Dot(axesA[0], axis)) * halfExtents[0].X
                            + MathF.Abs(Vector2.Dot(axesA[1], axis)) * halfExtents[1].Y;
            float radiusB = MathF.Abs(Vector2.Dot(axesB[0], axis)) * halfExtents[2].X
                            + MathF.Abs(Vector2.Dot(axesB[1], axis)) * halfExtents[3].Y;

            float overlap = radiusA + radiusB - separation;
            if (overlap <= 0f)
            {
                penetrationAxis = Vector2.UnitY;
                penetrationDepth = 0f;
                return false;
            }

            if (overlap < penetrationDepth)
            {
                penetrationDepth = overlap;
                penetrationAxis = Vector2.Dot(delta, axis) < 0f ? -axis : axis;
            }
        }

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
    public static bool Intersects(Capsule a, Capsule b)
    {
        if (!a.Bounds.Intersects(b.Bounds))
        {
            return false;
        }

        float distance = SegmentSegmentDistance(a.Segment, b.Segment);
        float radii = a.Radius + b.Radius;
        return distance * distance <= radii * radii;
    }

    /// <summary>
    /// Возвращает расстояние между двумя отрезками.
    /// </summary>
    /// <param name="a">Первый отрезок.</param>
    /// <param name="b">Второй отрезок.</param>
    /// <returns>Минимальное расстояние между отрезками.</returns>
    public static float SegmentSegmentDistance(Segment a, Segment b)
    {
        Vector2 p = a.A;
        Vector2 q = b.A;
        Vector2 r = a.Delta;
        Vector2 s = b.Delta;

        float rLengthSquared = r.LengthSquared();
        float sLengthSquared = s.LengthSquared();
        float denominator = Vector2.Cross(r, s);

        if (MathF.Abs(denominator) > Scalar.Epsilon && rLengthSquared > Scalar.Epsilon && sLengthSquared > Scalar.Epsilon)
        {
            float t = Vector2.Cross(q - p, s) / denominator;
            float u = Vector2.Cross(q - p, r) / denominator;

            if (t is >= 0f and <= 1f && u is >= 0f and <= 1f)
            {
                return 0f;
            }
        }

        float distance = Vector2.Distance(p, q);
        distance = MathF.Min(distance, Vector2.Distance(p, b.ClosestPointTo(p)));
        distance = MathF.Min(distance, Vector2.Distance(a.ClosestPointTo(q), q));
        distance = MathF.Min(distance, Vector2.Distance(a.ClosestPointTo(b.B), b.B));
        distance = MathF.Min(distance, Vector2.Distance(a.ClosestPointTo(b.A), b.A));
        return distance;
    }

    /// <summary>
    /// Проверяет, находится ли точка внутри повёрнутого прямоугольника.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <param name="center">Центр прямоугольника.</param>
    /// <param name="size">Размер прямоугольника.</param>
    /// <param name="rotation">Поворот прямоугольника.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    public static bool Contains(Vector2 point, Vector2 center, Vector2 size, Angle rotation)
    {
        Matrix3x2 inverse = Matrix3x2.CreateRotation((float)-rotation.Radians) * Matrix3x2.CreateTranslation(-center);
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
