using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Проверка нормализации угла.
/// </summary>
/// <remarks>
/// Отдельно от <c>AngleTests</c> потому, что здесь важно другое: доказать, что
/// быстрый путь внутри <see cref="Angle.NormalizeRadians"/> не меняет результат
/// ни на одном разряде. Откат этого ускорения, в отличие от отката
/// исправлений в кривых, тестом поймать нельзя — результат совпадает, и это
/// именно то свойство, которое нужно зафиксировать.
/// </remarks>
public class AngleNormalizationTests
{
    /// <summary>
    /// Независимая реализация контракта: полный остаток от деления без
    /// быстрого пути. Считается прямо в тесте, чтобы эталон не зависел от
    /// кода библиотеки.
    /// </summary>
    /// <param name="radians">Исходное значение.</param>
    /// <returns>Значение в диапазоне <c>(-π; π]</c>.</returns>
    private static double Reference(double radians)
    {
        double wrapped = Math.IEEERemainder(radians, Angle.Tau);
        return wrapped <= -Math.PI ? wrapped + Angle.Tau : wrapped;
    }

    [Fact]
    public void NormalizeRadians_MatchesReferenceBitForBit()
    {
        var random = new XorShift64Star(6161);

        for (int i = 0; i < 500_000; i++)
        {
            double radians = (random.NextFloat() - 0.5f) * 40.0 * Math.PI;
            double expected = Reference(radians);
            double actual = Angle.NormalizeRadians(radians);

            Assert.True(
                BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
                $"Расхождение на {radians:R}: ожидалось {expected:R}, получено {actual:R}.");
        }
    }

    /// <summary>
    /// Точки, на которых ускорение меняет путь исполнения: кратные π и 2π, а
    /// также нули разных знаков. Именно здесь чаще всего ломается нормализация
    /// при попытке «сократить» код.
    /// </summary>
    [Theory]
    // Отрицательный ноль предпочтительнее положительного: именно на нём
    // нормализация чаще всего портит знак. Ноль со знаком плюс проверяется в
    // NormalizeRadians_MatchesReferenceBitForBit.
    [InlineData(-0.0)]
    [InlineData(Math.PI)]
    [InlineData(-Math.PI)]
    [InlineData(Math.PI / 2)]
    [InlineData(-Math.PI / 2)]
    [InlineData(Angle.Tau)]
    [InlineData(-Angle.Tau)]
    [InlineData(3.0 * Math.PI)]
    [InlineData(-3.0 * Math.PI)]
    [InlineData(Math.PI / 4)]
    [InlineData(-Math.PI / 4)]
    [InlineData(1000.0 * Math.PI)]
    [InlineData(-1000.0 * Math.PI)]
    [InlineData(1e15)]
    [InlineData(-1e15)]
    [InlineData(double.NaN)]
    public void NormalizeRadians_MatchesReferenceAtBoundaries(double radians)
    {
        double expected = Reference(radians);
        double actual = Angle.NormalizeRadians(radians);

        if (double.IsNaN(expected))
        {
            Assert.True(double.IsNaN(actual), $"NaN обязан остаться NaN, получено {actual:R}.");
            return;
        }

        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"Расхождение на {radians:R}: ожидалось {expected:R}, получено {actual:R}.");
    }

    /// <summary>
    /// Результат обязан лежать в диапазоне <c>(-π; π]</c> при любом входе,
    /// включая отрицательный ноль: -0 не равен 0 по побитовому представлению,
    /// и нормализация не должна его портить.
    /// </summary>
    [Fact]
    public void NormalizeRadians_StaysInRange()
    {
        var random = new XorShift64Star(4242);
        for (int i = 0; i < 200_000; i++)
        {
            double radians = (random.NextFloat() - 0.5f) * 1e6;
            double value = Angle.NormalizeRadians(radians);

            Assert.True(value > -Math.PI, $"Значение {value:R} вне диапазона для входа {radians:R}.");
            Assert.True(value <= Math.PI, $"Значение {value:R} вне диапазона для входа {radians:R}.");
        }

        // -0 обязан остаться -0: угол из нормализованного источника может
        // нести отрицательный ноль, и смена знака здесь была бы изменением
        // значения без видимой причины.
        double negativeZero = Angle.NormalizeRadians(-0.0);
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(negativeZero));
    }

    /// <summary>
    /// Конструктор <see cref="Angle.FromRadians(double)"/> обязан давать то же,
    /// что и нормализация: разница только в обёртке.
    /// </summary>
    [Fact]
    public void FromRadians_AgreesWithNormalize()
    {
        var random = new XorShift64Star(717);
        for (int i = 0; i < 200_000; i++)
        {
            double radians = (random.NextFloat() - 0.5f) * 60.0 * Math.PI;
            double expected = Reference(radians);
            double actual = Angle.FromRadians(radians).Radians;

            Assert.True(
                BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
                $"Расхождение на {radians:R}: ожидалось {expected:R}, получено {actual:R}.");
        }
    }
}