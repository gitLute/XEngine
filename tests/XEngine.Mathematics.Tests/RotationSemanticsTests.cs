using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Различимость операций поворота и задания направления.
/// </summary>
/// <remarks>
/// Четыре операции выглядят похоже и дают разный результат, а различие между ними
/// неочевидно: <c>Rotate</c> поворачивает вектор, <c>RotateDirection</c>
/// поворачивает направление и теряет длину, <c>WithDirection</c> не поворачивает
/// вовсе, а задаёт направление, сохраняя длину.
/// <para>
/// Именно их смешение было дефектом: два метода носили одно имя
/// <c>RotateDirection</c>, вызывающий выбирал по имени и получал не то.
/// Поэтому проверяется не каждая операция по отдельности, а то, что все четыре
/// дают <em>разные</em> значения на одном входе. Слить любые две из них — значит
/// сломать тест, а не тихо изменить поведение.
/// </para>
/// <para>
/// Вход выбран так, чтобы результаты расходились сильно. На векторе вдоль оси X
/// поворот и задание направления совпадают: поворот на угол даёт направление
/// <c>φ + a</c>, а задание даёт <c>a</c>, и при <c>φ = 0</c> это одно и то же.
/// На таком входе различие не видно, и тест на различимость прошёл бы зря.
/// </para>
/// </remarks>
public sealed class RotationSemanticsTests
{
    private static readonly Vector2 Input = new(0f, 2f);
    private static readonly Angle Turn = Angle.FromDegrees(90);

    [Fact]
    public void FourRotationOperations_GiveDifferentResultsOnSameInput()
    {
        Vector2 rotated = VectorExtensions.Rotate(Input, Turn);
        Vector2 directionOnly = Turn.RotateDirection(Input);
        Vector2 assigned = VectorExtensions.WithDirection(Input, Turn);

        // Поворот: направление 90° становится 180°, длина сохраняется.
        MathAssert.Equal(-2f, rotated.X, 1e-5f);
        MathAssert.Equal(0f, rotated.Y, 1e-5f);

        // Поворот направления: тот же поворот, но длина отброшена.
        MathAssert.Equal(-1f, directionOnly.X, 1e-5f);
        MathAssert.Equal(0f, directionOnly.Y, 1e-5f);

        // Задание направления: поворота нет вовсе, исходный вектор не сдвигается.
        MathAssert.Equal(0f, assigned.X, 1e-5f);
        MathAssert.Equal(2f, assigned.Y, 1e-5f);

        // Все четыре обязаны различаться попарно.
        Assert.True(rotated != directionOnly, "Поворот вектора и поворот направления совпали.");
        Assert.True(rotated != assigned, "Поворот вектора и задание направления совпали.");
        Assert.True(directionOnly != assigned, "Поворот направления и задание направления совпали.");

        // Angle.Rotate и его расширение — одна операция, а не две.
        MathAssert.Equal(rotated.X, Turn.Rotate(Input).X, 1e-5f);
        MathAssert.Equal(rotated.Y, Turn.Rotate(Input).Y, 1e-5f);
    }

    /// <summary>
    /// <c>WithDirection</c> зависит от исходного вектора только длиной.
    /// </summary>
    /// <remarks>
    /// Это и есть суть операции: направление вектора на неё не влияет. Вход
    /// направления варьируется, длина и целевой угол фиксированы, и результат
    /// обязан совпадать во всех случаях. Если бы метод тайно поворачивал, выход
    /// зависел бы от исходного направления и тест это поймал бы.
    /// </remarks>
    [Fact]
    public void WithDirection_UsesOnlyLength_NotOriginalDirection()
    {
        Vector2 expected = VectorExtensions.WithDirection(new Vector2(2f, 0f), Turn);

        foreach (Vector2 source in new[]
        {
            new Vector2(0f, 2f),
            new Vector2(-2f, 0f),
            new Vector2(0f, -2f),
            new Vector2(1.4142136f, 1.4142136f),
        })
        {
            Vector2 result = VectorExtensions.WithDirection(source, Turn);

            MathAssert.Equal(expected.X, result.X, 1e-5f);
            MathAssert.Equal(expected.Y, result.Y, 1e-5f);
            MathAssert.Equal(2f, result.Length(), 1e-5f);
        }

        // Длина нулевого вектора даёт нулевой результат, а не исключение и не NaN.
        Vector2 zero = VectorExtensions.WithDirection(Vector2.Zero, Turn);
        Assert.True(zero == Vector2.Zero, $"Нулевой вход дал {zero}, а не нулевой вектор.");

        // Длина результата равна длине исходного вектора при любом его направлении.
        foreach (Vector2 source in new[] { new Vector2(3f, 4f), new Vector2(-3f, -4f), new Vector2(3f, 0f) })
        {
            MathAssert.Equal(
                source.Length(),
                VectorExtensions.WithDirection(source, Turn).Length(),
                1e-5f);
        }
    }

    /// <summary>
    /// <c>Angle.RotateDirection</c> теряет длину при любом ненулевом входе.
    /// </summary>
    /// <remarks>
    /// Доктрина обещает единичный результат. Обещание выполняется для любого
    /// ненулевого вектора независимо от его длины — от микроскопического до
    /// километрового. Нулевой вход даёт нулевой результат: нормализовать нечего,
    /// и выдумывать направление было бы выдумкой. Этот случай в доктрине
    /// отдельно оговорен, потому что «всегда единичный» без оговорок здесь
    /// неверно.
    /// </remarks>
    [Fact]
    public void RotateDirection_DropsLength_ButKeepsZeroSafe()
    {
        foreach (float length in new[] { 1e-6f, 0.001f, 1f, 1000f, 1e7f })
        {
            Vector2 result = Turn.RotateDirection(new Vector2(0f, length));

            MathAssert.Equal(1f, result.Length(), 1e-5f);
        }

        Vector2 zero = Turn.RotateDirection(Vector2.Zero);
        Assert.True(zero == Vector2.Zero, $"Нулевой вход дал {zero}, а не нулевой вектор.");
        Assert.True(float.IsFinite(zero.X) && float.IsFinite(zero.Y), "Нулевой вход дал нечисловой результат.");
    }

    /// <summary>
    /// Нормализация результата масштабирования обязательна, а сырой путь назван
    /// прямо в имени.
    /// </summary>
    /// <remarks>
    /// Сравнение, хеш и упорядочивание угла идут по сырым радианам, поэтому угол в
    /// 2π не равен нулю. Умножение без нормализации оставляло инвариант дырявым в
    /// самом ходовом операторе, а <c>Angle.SinCos</c> вдобавок сужает радианы до
    /// <c>float</c>, так что на большой накопленной величине точность падает.
    /// Проверяется, что нормализуют все три обычных пути и что сырой остался
    /// доступен под именем, которое это объявляет.
    /// </remarks>
    [Fact]
    public void OnlyScaleRaw_IsAllowedToLeaveTheNormalRange()
    {
        Angle source = Angle.FromDegrees(90);

        Angle scaled = source.Scale(4f);
        Angle multiplied = source * 4f;
        Angle divided = source * 8f / 2f;
        Angle normalized = scaled.Normalized();

        Angle zero = Angle.FromDegrees(0);
        foreach (Angle result in new[] { scaled, multiplied, divided, normalized })
        {
            Assert.True(result == zero, $"{result.Degrees:F4}° не совпали с нулём.");
            Assert.True(
                result.GetHashCode() == zero.GetHashCode(),
                "Хеши различаются: ключ в словаре даст две записи на одну ориентацию.");
            Assert.True(result.CompareTo(zero) == 0, "Сортировка ставит эквивалентные углы в разные места.");
        }

        // Сырой путь честно выходит за полный оборот и доступен явно названным
        // методом, а не побочным эффектом арифметики.
        Angle raw = source.ScaleRaw(4f);
        MathAssert.Equal(360f, (float)raw.Degrees, 1e-3f);
        Assert.True(raw != zero, "Сырой результат обязан отличаться от нормализованного.");

        // Граница диапазона: минус пи приводится к плюс пи.
        MathAssert.Equal(180f, (float)Angle.FromDegrees(-180).Scale(1f).Degrees, 1e-3f);

        // Границы диапазона: 90°·(−2) даёт −180°, которое приводится к +180°,
        // потому что диапазон задан как (−π; π]. А вот 90°·(−3) даёт −270°, и
        // это +90°, то есть граница влияет и на величину за её пределами.
        MathAssert.Equal(180f, (float)Angle.FromDegrees(90).Scale(-2f).Degrees, 1e-3f);
        MathAssert.Equal(180f, (float)Angle.FromDegrees(-90).Scale(-2f).Degrees, 1e-3f);
        MathAssert.Equal(90f, (float)Angle.FromDegrees(90).Scale(-3f).Degrees, 1e-3f);
    }
}