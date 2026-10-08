using System.Numerics;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Независимая реализация проверки пересечения повёрнутых прямоугольников.
/// </summary>
/// <remarks>
/// Реализация намеренно не использует ничего из библиотеки, кроме типа
/// <see cref="Angle"/>, и считает в двойной точности, чтобы сверять с ней
/// не по той же формуле, по которой написан проверяемый код.
/// <para>
/// Формула: теорема о разделяющей оси требует проверять проекции на нормали
/// граней обеих фигур. Для каждой оси перекрытие равно сумме радиусов
/// проекций минус расстояние между центрами, и пересечение есть тогда и
/// только тогда, когда перекрытие положительно по всем четырём осям.
/// Ось наименьшего перекрытия даёт вектор минимального раздвижения.
/// </para>
/// </remarks>
public static class ReferenceObb
{
    /// <summary>
    /// Результат проверки: вердикт, глубина и ось минимального раздвижения.
    /// </summary>
    /// <param name="Overlaps">Пересекаются ли фигуры.</param>
    /// <param name="Depth">Глубина перекрытия по минимальной оси.</param>
    /// <param name="Axis">Единичная ось, направленная от первой фигуры ко второй.</param>
    public readonly record struct Result(bool Overlaps, double Depth, Vector2 Axis);

    /// <summary>
    /// Проверяет пересечение двух повёрнутых прямоугольников.
    /// </summary>
    /// <param name="centerA">Центр первой фигуры.</param>
    /// <param name="sizeA">Размер первой фигуры.</param>
    /// <param name="rotationA">Поворот первой фигуры.</param>
    /// <param name="centerB">Центр второй фигуры.</param>
    /// <param name="sizeB">Размер второй фигуры.</param>
    /// <param name="rotationB">Поворот второй фигуры.</param>
    /// <returns>Вердикт, глубина и ось.</returns>
    public static Result Overlap(
        Vector2 centerA,
        Vector2 sizeA,
        Angle rotationA,
        Vector2 centerB,
        Vector2 sizeB,
        Angle rotationB)
    {
        double radA = rotationA.Radians;
        double radB = rotationB.Radians;

        Vector2 axisA0 = new((float)Math.Cos(radA), (float)Math.Sin(radA));
        Vector2 axisA1 = new((float)-Math.Sin(radA), (float)Math.Cos(radA));
        Vector2 axisB0 = new((float)Math.Cos(radB), (float)Math.Sin(radB));
        Vector2 axisB1 = new((float)-Math.Sin(radB), (float)Math.Cos(radB));

        Vector2 halfA = sizeA * 0.5f;
        Vector2 halfB = sizeB * 0.5f;
        Vector2 delta = centerB - centerA;

        Vector2[] axes = [axisA0, axisA1, axisB0, axisB1];
        double bestOverlap = double.MaxValue;
        Vector2 bestAxis = Vector2.Zero;

        foreach (Vector2 axis in axes)
        {
            double radiusA = (Math.Abs(Dot(axisA0, axis)) * halfA.X) + (Math.Abs(Dot(axisA1, axis)) * halfA.Y);
            double radiusB = (Math.Abs(Dot(axisB0, axis)) * halfB.X) + (Math.Abs(Dot(axisB1, axis)) * halfB.Y);
            double overlap = radiusA + radiusB - Math.Abs(Dot(delta, axis));

            if (overlap <= 0.0)
            {
                return new Result(false, 0.0, Vector2.Zero);
            }

            if (overlap < bestOverlap)
            {
                bestOverlap = overlap;
                bestAxis = axis;
            }
        }

        Vector2 unit = bestAxis.SafeNormalize();
        Vector2 direction = Dot(delta, bestAxis) < 0.0 ? -unit : unit;
        return new Result(true, bestOverlap, direction);
    }

    /// <summary>
    /// Скалярное произведение на двойной точности.
    /// </summary>
    /// <param name="a">Первый вектор.</param>
    /// <param name="b">Второй вектор.</param>
    /// <returns>Произведение.</returns>
    private static double Dot(Vector2 a, Vector2 b) => ((double)a.X * b.X) + ((double)a.Y * b.Y);

    /// <summary>
    /// Углы прямоугольника, противоположные углам по диагонали.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="size">Размер.</param>
    /// <param name="rotation">Поворот.</param>
    /// <returns>Четыре вершины по порядку против часовой стрелки.</returns>
    public static Vector2[] Corners(Vector2 center, Vector2 size, Angle rotation)
    {
        double c = Math.Cos(rotation.Radians);
        double s = Math.Sin(rotation.Radians);
        Vector2[] local =
        [
            new Vector2(-0.5f, -0.5f),
            new Vector2(0.5f, -0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-0.5f, 0.5f),
        ];

        Vector2[] result = new Vector2[4];
        for (int index = 0; index < local.Length; index++)
        {
            double x = local[index].X * size.X;
            double y = local[index].Y * size.Y;
            result[index] = center + new Vector2((float)((x * c) - (y * s)), (float)((x * s) + (y * c)));
        }

        return result;
    }

    /// <summary>
    /// Принадлежит ли точка прямоугольнику по построению: переводом в локальные
    /// оси, без матриц.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <param name="center">Центр прямоугольника.</param>
    /// <param name="size">Размер прямоугольника.</param>
    /// <param name="rotation">Поворот прямоугольника.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    public static bool Contains(Vector2 point, Vector2 center, Vector2 size, Angle rotation)
    {
        double c = Math.Cos(rotation.Radians);
        double s = Math.Sin(rotation.Radians);
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;

        // Проекции на локальные оси: поворот на минус угол.
        double localX = (dx * c) + (dy * s);
        double localY = (-dx * s) + (dy * c);

        double margin = size.Length() * 1e-5;
        return Math.Abs(localX) <= (size.X * 0.5) + margin
            && Math.Abs(localY) <= (size.Y * 0.5) + margin;
    }
}