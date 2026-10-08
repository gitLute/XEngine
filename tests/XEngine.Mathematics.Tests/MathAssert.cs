using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Сравнение значений с допуском для тестов математики (17.6).
/// Один вход для всех трёхмерных тестов, чтобы допуск и формат сообщения
/// об ошибке не расходились между файлами.
/// </summary>
public static class MathAssert
{
    /// <summary>
    /// Допуск по умолчанию: подобран так, чтобы ошибка округления float не
    /// проходила, а накопление в длинной формуле проходило.
    /// </summary>
    public const float Tolerance = 1e-5f;

    /// <summary>
    /// Слабый допуск для величин, которые по определению считаются через
    /// тригонометрию: синус и косинус дают погрешность порядка 1e-7 на единицу,
    /// и она растёт с размером проверяемого вектора.
    /// </summary>
    public const float LooseTolerance = 1e-4f;

    /// <summary>
    /// Проверяет равенство чисел с допуском.
    /// </summary>
    /// <param name="expected">Ожидаемое значение.</param>
    /// <param name="actual">Полученное значение.</param>
    /// <param name="tolerance">Допуск.</param>
    /// <remarks>
    /// NaN проваливает проверку, а не проходит её. Принимать NaN за равенство
    /// нельзя: именно получением NaN проявляются деление на ноль и 0/0, и
    /// такой допуск делал бы тесты зелёными на самых дорогих дефектах.
    /// </remarks>
    public static void Equal(float expected, float actual, float tolerance = Tolerance)
        => Assert.True(
            !float.IsNaN(actual) && MathF.Abs(expected - actual) <= tolerance,
            $"Ожидалось {expected} с допуском {tolerance}, получено {actual}.");

    /// <summary>
    /// Проверяет равенство двумерных векторов с допуском.
    /// </summary>
    /// <param name="expected">Ожидаемый вектор.</param>
    /// <param name="actual">Полученный вектор.</param>
    /// <param name="tolerance">Допуск по каждой компоненте.</param>
    public static void Equal(Vector2 expected, Vector2 actual, float tolerance = Tolerance)
    {
        Equal(expected.X, actual.X, tolerance);
        Equal(expected.Y, actual.Y, tolerance);
    }

    /// <summary>
    /// Проверяет равенство трёхмерных векторов с допуском.
    /// </summary>
    /// <param name="expected">Ожидаемый вектор.</param>
    /// <param name="actual">Полученный вектор.</param>
    /// <param name="tolerance">Допуск по каждой компоненте.</param>
    public static void Equal(Vector3 expected, Vector3 actual, float tolerance = Tolerance)
    {
        Equal(expected.X, actual.X, tolerance);
        Equal(expected.Y, actual.Y, tolerance);
        Equal(expected.Z, actual.Z, tolerance);
    }

    /// <summary>
    /// Проверяет равенство четырёхмерных векторов с допуском.
    /// </summary>
    /// <param name="expected">Ожидаемый вектор.</param>
    /// <param name="actual">Полученный вектор.</param>
    /// <param name="tolerance">Допуск по каждой компоненте.</param>
    public static void Equal(Vector4 expected, Vector4 actual, float tolerance = Tolerance)
    {
        Equal(expected.X, actual.X, tolerance);
        Equal(expected.Y, actual.Y, tolerance);
        Equal(expected.Z, actual.Z, tolerance);
        Equal(expected.W, actual.W, tolerance);
    }

    /// <summary>
    /// Проверяет равенство кватернион с допуском.
    /// </summary>
    /// <param name="expected">Ожидаемый кватернион.</param>
    /// <param name="actual">Полученный кватернион.</param>
    /// <param name="tolerance">Допуск по каждой компоненте.</param>
    public static void Equal(Quaternion expected, Quaternion actual, float tolerance = Tolerance)
    {
        Equal(expected.X, actual.X, tolerance);
        Equal(expected.Y, actual.Y, tolerance);
        Equal(expected.Z, actual.Z, tolerance);
        Equal(expected.W, actual.W, tolerance);
    }

    /// <summary>
    /// Проверяет равенство матриц с допуском по всем элементам.
    /// </summary>
    /// <param name="expected">Ожидаемая матрица.</param>
    /// <param name="actual">Полученная матрица.</param>
    /// <param name="tolerance">Допуск по каждому элементу.</param>
    public static void Equal(Matrix4x4 expected, Matrix4x4 actual, float tolerance = Tolerance)
    {
        Equal(expected.M11, actual.M11, tolerance);
        Equal(expected.M12, actual.M12, tolerance);
        Equal(expected.M13, actual.M13, tolerance);
        Equal(expected.M14, actual.M14, tolerance);
        Equal(expected.M21, actual.M21, tolerance);
        Equal(expected.M22, actual.M22, tolerance);
        Equal(expected.M23, actual.M23, tolerance);
        Equal(expected.M24, actual.M24, tolerance);
        Equal(expected.M31, actual.M31, tolerance);
        Equal(expected.M32, actual.M32, tolerance);
        Equal(expected.M33, actual.M33, tolerance);
        Equal(expected.M34, actual.M34, tolerance);
        Equal(expected.M41, actual.M41, tolerance);
        Equal(expected.M42, actual.M42, tolerance);
        Equal(expected.M43, actual.M43, tolerance);
        Equal(expected.M44, actual.M44, tolerance);
    }

    /// <summary>
    /// Проверяет равенство углов с допуском.
    /// </summary>
    /// <param name="expected">Ожидаемый угол.</param>
    /// <param name="actual">Полученный угол.</param>
    /// <param name="tolerance">Допуск в градусах.</param>
    public static void Equal(Angle expected, Angle actual, double toleranceDegrees = 1e-4)
        => Assert.True(
            Math.Abs(expected.Radians - actual.Radians) <= Scalar.ToRadians(toleranceDegrees),
            $"Ожидалось {expected.Degrees:F6}°, получено {actual.Degrees:F6}°.");

    /// <summary>
    /// Проверяет равенство чисел двойной точности с допуском. Нужна для
    /// сравнения величин, которые в библиотеке хранятся как double: радиан
    /// угла, например.
    /// </summary>
    /// <param name="expected">Ожидаемое значение.</param>
    /// <param name="actual">Полученное значение.</param>
    /// <param name="tolerance">Допуск.</param>
    public static void NearlyEqual(double expected, double actual, double tolerance)
        => Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"Ожидалось {expected} с допуском {tolerance}, получено {actual}.");
}