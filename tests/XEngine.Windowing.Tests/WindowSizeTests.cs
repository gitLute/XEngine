using Xunit;

namespace XEngine.Windowing.Tests;

/// <summary>
/// Контракт <see cref="WindowSize"/>: размер окна выражается именованной
/// структурой, а не парой целых и не типом бэкенда.
/// </summary>
public sealed class WindowSizeTests
{
    [Fact]
    public void Zero_IsTheOnlyInvalidSize()
    {
        Assert.True(WindowSize.Zero.IsValid == false);
        Assert.True(new WindowSize(1920, 1080).IsValid);
        Assert.True(new WindowSize(1, 1).IsValid);
    }

    [Fact]
    public void AspectRatio_IsWidthOverHeight()
    {
        Assert.Equal(16f / 9f, new WindowSize(1920, 1080).AspectRatio, 1e-5f);
        Assert.Equal(1f, new WindowSize(800, 800).AspectRatio, 1e-5f);
    }

    [Fact]
    public void AspectRatio_OfCollapsedWindowIsZeroRatherThanInfinity()
    {
        // Свёрнутое окно имеет нулевую высоту. Отношение сторон при этом не имеет
        // смысла, и деление дало бы бесконечность, которая утекла бы в проекцию.
        Assert.Equal(0f, new WindowSize(1920, 0).AspectRatio, 1e-6f);
    }

    [Fact]
    public void Equality_ComparesByValue()
    {
        Assert.Equal(new WindowSize(800, 600), new WindowSize(800, 600));
        Assert.NotEqual(new WindowSize(800, 600), new WindowSize(600, 800));
    }

    [Fact]
    public void ToString_ShowsSizeForDiagnostics()
    {
        Assert.Equal("800x600", new WindowSize(800, 600).ToString());
    }
}