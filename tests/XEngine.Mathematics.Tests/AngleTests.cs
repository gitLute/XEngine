using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

public sealed class AngleTests
{
    private const double Tolerance = 1e-9;

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(Math.PI, Math.PI)]
    [InlineData(-Math.PI, Math.PI)]
    [InlineData(2 * Math.PI, 0.0)]
    [InlineData(3 * Math.PI, Math.PI)]
    [InlineData(-3 * Math.PI, Math.PI)]
    [InlineData(4 * Math.PI, 0.0)]
    [InlineData(0.5 * Math.PI, 0.5 * Math.PI)]
    [InlineData(-2.5 * Math.PI, -0.5 * Math.PI)]
    public void FromRadians_NormalizesToHalfTurnRange(double radians, double expected)
    {
        Assert.Equal(expected, Angle.FromRadians(radians).Radians, Tolerance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(360.0)]
    [InlineData(-360.0)]
    [InlineData(720.0)]
    public void ZeroAndFullTurns_AreEquivalent(double degrees)
    {
        Assert.Equal(Angle.Zero.Radians, Angle.FromDegrees(degrees).Radians, Tolerance);
    }

    [Fact]
    public void FromDegrees_ConvertsToRadians()
    {
        Assert.Equal(Math.PI / 4, Angle.FromDegrees(45).Radians, Tolerance);
        Assert.Equal(45, Angle.FromRadians(Math.PI / 4).Degrees, 1e-9);
    }

    [Fact]
    public void FromTurns_ConvertsToFullTurn()
    {
        Assert.Equal(0, Angle.FromTurns(1).Radians, Tolerance);
        Assert.Equal(0.25, Angle.FromTurns(0.25).Turns, Tolerance);
        Assert.Equal(90, Angle.FromTurns(0.25).Degrees, 1e-9);
    }

    [Fact]
    public void ShortestDelta_GoesThroughNegativeSide()
    {
        Angle from = Angle.FromDegrees(170);
        Angle to = Angle.FromDegrees(-170);

        double delta = Angle.ShortestDelta(from, to);

        Assert.Equal(Scalar.ToRadians(20), delta, Tolerance);
        Assert.Equal(20, Scalar.ToDegrees(delta), Tolerance);
    }

    [Fact]
    public void Lerp_TakesShortestArc()
    {
        Angle from = Angle.FromDegrees(170);
        Angle to = Angle.FromDegrees(-170);

        Angle mid = Angle.Lerp(from, to, 0.5f);

        Assert.Equal(180, Math.Abs(mid.Degrees), 1e-9);
    }

    [Fact]
    public void Lerp_EndpointsAreExact()
    {
        Angle from = Angle.FromDegrees(10);
        Angle to = Angle.FromDegrees(80);

        Assert.Equal(from.Radians, Angle.Lerp(from, to, 0f).Radians, Tolerance);
        Assert.Equal(to.Radians, Angle.Lerp(from, to, 1f).Radians, Tolerance);
    }

    [Fact]
    public void Rotate_IsCounterClockwise()
    {
        Vector2 rotated = Angle.FromDegrees(90).Rotate(Vector2.UnitX);

        Assert.Equal(0f, rotated.X, 1e-6f);
        Assert.Equal(1f, rotated.Y, 1e-6f);
    }

    [Fact]
    public void Rotate_PreservesLength()
    {
        Vector2 source = new Vector2(3f, 4f);

        Vector2 rotated = Angle.FromDegrees(37).Rotate(source);

        Assert.Equal(5f, rotated.Length(), 1e-5f);
    }

    [Fact]
    public void Rotate_AgreesWithFromPolar()
    {
        Angle angle = Angle.FromDegrees(35);

        Vector2 rotated = angle.Rotate(Vector2.UnitX);
        Vector2 polar = VectorExtensions.FromPolar(1f, angle);

        Assert.Equal(polar.X, rotated.X, 1e-6f);
        Assert.Equal(polar.Y, rotated.Y, 1e-6f);
    }

    [Fact]
    public void Direction_MatchesTrigonometry()
    {
        Angle angle = Angle.FromDegrees(63);

        Assert.Equal(angle.Cos, angle.Direction.X, 1e-6f);
        Assert.Equal(angle.Sin, angle.Direction.Y, 1e-6f);
    }

    [Fact]
    public void FromDirection_RoundTrips()
    {
        Vector2 direction = VectorExtensions.FromPolar(2f, Angle.FromDegrees(-128));

        Angle angle = VectorExtensions.ToAngle(direction);

        Assert.Equal(-128, angle.Degrees, 1e-4);
    }

    [Fact]
    public void FromDirection_ZeroVectorGivesZeroAngle()
    {
        Assert.Equal(Angle.Zero.Radians, Angle.FromDirection(Vector2.Zero).Radians, Tolerance);
    }

    [Fact]
    public void MoveTowards_StopsAtTargetWhenStepIsBigEnough()
    {
        Angle current = Angle.FromDegrees(0);

        Angle result = Angle.MoveTowards(current, Angle.FromDegrees(10), Scalar.ToRadians(45));

        Assert.Equal(10, result.Degrees, 1e-9);
    }

    [Fact]
    public void MoveTowards_LimitsStep()
    {
        Angle current = Angle.FromDegrees(0);

        Angle result = Angle.MoveTowards(current, Angle.FromDegrees(90), Math.PI / 180);

        Assert.Equal(1, result.Degrees, 1e-9);
    }

    /// <summary>
    /// Умножение приводит результат к диапазону, а сырое — нет.
    /// </summary>
    /// <remarks>
    /// Раньше <c>Scale</c> нормализации не выполнял, и инвариант класса
    /// «углы в (−π; π]» держался на честном слове: сравнение, хеш и
    /// упорядочивание идут по сырым радианам, поэтому 360° не равны 0° и хеши
    /// у них разные. Дальше <c>Angle.SinCos</c> сужает радианы до <c>float</c>,
    /// так что на большой накопленной величине точность падает независимо от
    /// того, в <c>double</c> она хранится или нет.
    /// </remarks>
    [Fact]
    public void Scale_Normalizes_AndRawDoesNot()
    {
        MathAssert.Equal(0f, (float)Angle.FromDegrees(90).Scale(4f).Degrees, 1e-5f);

        // Тот же угол два числа спустя обязан совпадать с нулём по всем трём
        // признакам, а не только по величине.
        Angle wrapped = Angle.FromDegrees(90).Scale(4f);
        Assert.True(wrapped == Angle.FromDegrees(0), "360° должны совпадать с 0°.");
        Assert.True(
            wrapped.GetHashCode() == Angle.FromDegrees(0).GetHashCode(),
            "Хеши различаются: ключ в словаре даст две записи на одну ориентацию.");

        // Сырой путь сохранён и честно выходит за полный оборот.
        MathAssert.Equal(360f, (float)Angle.FromDegrees(90).ScaleRaw(4f).Degrees, 1e-3f);
        MathAssert.Equal(180f, (float)Angle.FromDegrees(90).Scale(2f).Degrees, 1e-4f);
        // −π приводится к +π: диапазон задан как (−π; π], а не [−π; π].
        MathAssert.Equal(180f, (float)Angle.FromDegrees(90).Scale(-2f).Degrees, 1e-4f);
        MathAssert.Equal(-90f, (float)Angle.FromDegrees(90).Scale(-1f).Degrees, 1e-4f);

        // Оператор умножения обязан вести себя так же, как Scale: он на него и
        // ссылается, иначе инвариант снова окажется дырявым.
        Assert.True(Angle.FromDegrees(90) * 4f == Angle.FromDegrees(0), "operator * нормализует через Scale.");

        // Деление уже нормализовало, и на этом основании Scale тоже должен.
        Assert.True(Angle.FromDegrees(90) / 4f == Angle.FromDegrees(22.5), "Деление не изменилось.");
    }

    [Fact]
    public void Arithmetic_NormalizesResult()
    {
        Angle result = Angle.FromDegrees(170) + Angle.FromDegrees(170);

        Assert.Equal(-20, result.Degrees, 1e-9);
    }

    [Fact]
    public void Subtraction_KeepsSignedValue()
    {
        Angle result = Angle.FromDegrees(10) - Angle.FromDegrees(170);

        Assert.Equal(-160, result.Degrees, 1e-9);
    }

    [Fact]
    public void EqualityAndHashing_WorkForEqualAngles()
    {
        Angle first = Angle.FromDegrees(45);
        Angle second = Angle.FromRadians(Math.PI / 4);

        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Comparisons_UseRadians()
    {
        Assert.True(Angle.FromDegrees(10) < Angle.FromDegrees(20));
        Assert.True(Angle.FromDegrees(30) > Angle.FromDegrees(20));
        Assert.True(Angle.FromDegrees(20) <= Angle.FromDegrees(20));
        Assert.True(Angle.FromDegrees(20) >= Angle.FromDegrees(20));
    }

    [Fact]
    public void Negation_MirrorsAngle()
    {
        Assert.Equal(-30, Angle.FromDegrees(30).Negated().Degrees, 1e-9);
    }
}
