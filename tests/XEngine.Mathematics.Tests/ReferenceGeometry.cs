using System.Numerics;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Эталонные геометрические вычисления для тестов.
/// </summary>
/// <remarks>
/// Здесь нет ничего из библиотеки: только формулы, написанные напрямую из
/// определения. Нужны, чтобы тесты не сверяли реализацию с самой собой.
/// </remarks>
public static class ReferenceGeometry
{
    /// <summary>
    /// Ближайшая к точке точка отрезка. Проекция на прямую с ограничением
    /// параметра в [0; 1], то есть ровно определение расстояния до отрезка.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <param name="start">Начало отрезка.</param>
    /// <param name="end">Конец отрезка.</param>
    /// <returns>Ближайшая точка отрезка.</returns>
    public static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        double lengthSquared = (double)Vector3.Dot(delta, delta);
        if (lengthSquared == 0.0)
        {
            return start;
        }

        double t = Vector3.Dot(point - start, delta) / lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        return start + (delta * (float)t);
    }

    /// <summary>
    /// Точка отрезка по параметру.
    /// </summary>
    /// <param name="start">Начало отрезка.</param>
    /// <param name="end">Конец отрезка.</param>
    /// <param name="t">Параметр в [0; 1].</param>
    /// <returns>Точка отрезка.</returns>
    public static Vector3 PointAt(Vector3 start, Vector3 end, double t) => start + (end - start) * (float)t;

    /// <summary>
    /// Расстояние между двумя отрезками перебором.
    /// </summary>
    /// <param name="startA">Начало первого отрезка.</param>
    /// <param name="endA">Конец первого отрезка.</param>
    /// <param name="startB">Начало второго отрезка.</param>
    /// <param name="endB">Конец второго отрезка.</param>
    /// <param name="samples">Число проб на первом отрезке.</param>
    /// <returns>Минимальное расстояние между отрезками.</returns>
    /// <remarks>
    /// Перебор по первому отрезку, а по второму на каждом шаге берётся точная
    /// ближайшая точка. Так минимум находится по одному параметру, а не по
    /// двум, и сходимость гарантирована: функция расстояния от точки до
    /// отрезка непрерывна, а лучший параметр уточняется золотым сечением.
    /// </remarks>
    public static double SegmentDistanceBrute(
        Vector3 startA,
        Vector3 endA,
        Vector3 startB,
        Vector3 endB,
        int samples = 20000)
    {
        (double best, double bestParameter) = Scan(startA, endA, startB, endB, samples);
        return Refine(startA, endA, startB, endB, best, bestParameter, samples);
    }

    private static (double Distance, double Parameter) Scan(
        Vector3 startA,
        Vector3 endA,
        Vector3 startB,
        Vector3 endB,
        int samples)
    {
        double best = double.MaxValue;
        double bestParameter = 0.0;

        for (int i = 0; i <= samples; i++)
        {
            double t = (double)i / samples;
            Vector3 point = PointAt(startA, endA, t);
            Vector3 other = ClosestPointOnSegment(point, startB, endB);
            double distance = Vector3.Distance(point, other);
            if (distance < best)
            {
                best = distance;
                bestParameter = t;
            }
        }

        return (best, bestParameter);
    }

    /// <summary>
    /// Уточнение лучшего параметра золотым сечением в окрестности лучшего шага
    /// перебора.
    /// </summary>
    /// <param name="startA">Начало первого отрезка.</param>
    /// <param name="endA">Конец первого отрезка.</param>
    /// <param name="startB">Начало второго отрезка.</param>
    /// <param name="endB">Конец второго отрезка.</param>
    /// <param name="seed">Значение, уже найденное перебором.</param>
    /// <param name="seedParameter">Параметр, соответствующий значению.</param>
    /// <param name="samples">Число проб, задающее ширину окна.</param>
    /// <returns>Уточнённое расстояние.</returns>
    private static double Refine(
        Vector3 startA,
        Vector3 endA,
        Vector3 startB,
        Vector3 endB,
        double seed,
        double seedParameter,
        int samples)
    {
        double window = 2.0 / samples;
        double left = Math.Max(0.0, seedParameter - window);
        double right = Math.Min(1.0, seedParameter + window);

        const double Golden = 0.6180339887498949;
        double x1 = right - (Golden * (right - left));
        double x2 = left + (Golden * (right - left));
        double f1 = Evaluate(startA, endA, startB, endB, x1);
        double f2 = Evaluate(startA, endA, startB, endB, x2);

        for (int i = 0; i < 200; i++)
        {
            if (right - left < 1e-15)
            {
                break;
            }

            if (f1 < f2)
            {
                right = x2;
                x2 = x1;
                f2 = f1;
                x1 = right - (Golden * (right - left));
                f1 = Evaluate(startA, endA, startB, endB, x1);
            }
            else
            {
                left = x1;
                x1 = x2;
                f1 = f2;
                x2 = left + (Golden * (right - left));
                f2 = Evaluate(startA, endA, startB, endB, x2);
            }
        }

        return Math.Min(seed, Math.Min(f1, f2));
    }

    private static double Evaluate(Vector3 startA, Vector3 endA, Vector3 startB, Vector3 endB, double t)
    {
        Vector3 point = PointAt(startA, endA, t);
        return Vector3.Distance(point, ClosestPointOnSegment(point, startB, endB));
    }

    /// <summary>
    /// Расстояние между двумя точками в двойной точности.
    /// </summary>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    /// <returns>Расстояние.</returns>
    public static double Distance(Vector3 a, Vector3 b)
        => Math.Sqrt(
            ((double)a.X - b.X) * ((double)a.X - b.X)
            + ((double)a.Y - b.Y) * ((double)a.Y - b.Y)
            + ((double)a.Z - b.Z) * ((double)a.Z - b.Z));
}