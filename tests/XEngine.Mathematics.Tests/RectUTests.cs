using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Контракт <c>RectU</c>: прямоугольник в целых пикселях с включёнными
/// границами для регионов атласа (10.6).
/// </summary>
public sealed class RectUTests
{
    [Fact]
    public void RectU_KeepsSizeInPixelsWithInclusiveBounds()
    {
        RectU rect = new(10, 20, 32, 16);

        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(32, rect.Width);
        Assert.Equal(16, rect.Height);
        Assert.Equal(41, rect.Right);
        Assert.Equal(35, rect.Bottom);
        Assert.Equal(512, rect.Area);
        Assert.False(rect.IsEmpty);
    }

    [Fact]
    public void RectU_SinglePixelHasEqualBounds()
    {
        RectU rect = new(5, 5, 1, 1);

        Assert.Equal(5, rect.Left);
        Assert.Equal(5, rect.Right);
        Assert.Equal(5, rect.Top);
        Assert.Equal(5, rect.Bottom);
        Assert.Equal(1, rect.Area);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 4)]
    public void RectU_RejectsNonPositiveSize(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RectU(0, 0, width, height));
    }

    [Fact]
    public void RectU_ContainsBoundaryPixels()
    {
        RectU rect = new(10, 20, 32, 16);

        Assert.True(rect.Contains(10, 20), "Левый верхний угол входит в прямоугольник.");
        Assert.True(rect.Contains(41, 35), "Правый нижний угол входит в прямоугольник.");
        Assert.True(rect.Contains(25, 27), "Точка внутри входит в прямоугольник.");
        Assert.False(rect.Contains(9, 27), "Точка левее не входит.");
        Assert.False(rect.Contains(25, 36), "Точка ниже не входит.");
        Assert.False(rect.Contains(42, 35), "Точка правее не входит.");
    }

    [Fact]
    public void RectU_IntersectsDetectsSharedPixels()
    {
        RectU rect = new(0, 0, 10, 10);

        Assert.True(rect.Intersects(new RectU(9, 9, 10, 10)), "Один общий пиксель — пересечение.");
        Assert.True(rect.Intersects(new RectU(5, 5, 1, 1)), "Прямоугольник внутри.");
        Assert.False(rect.Intersects(new RectU(10, 0, 10, 10)), "Соседний прямоугольник не пересекается.");
    }

    [Fact]
    public void RectU_IntersectionClipsRegion()
    {
        RectU region = new(0, 0, 16, 16);

        RectU clipped = region.Intersection(new RectU(8, 8, 32, 32));

        Assert.Equal(new RectU(8, 8, 8, 8), clipped);
    }

    [Fact]
    public void RectU_IntersectionOfDisjointRectsIsEmpty()
    {
        RectU region = new(0, 0, 8, 8);

        RectU clipped = region.Intersection(new RectU(100, 100, 8, 8));

        Assert.True(clipped.IsEmpty);
        Assert.Equal(0, clipped.Width);
        Assert.Equal(0, clipped.Height);
    }

    [Fact]
    public void RectU_IntersectionIsEmptyForEmptyRect()
    {
        RectU empty = RectU.Empty;

        Assert.True(empty.IsEmpty);
        Assert.Equal(0, empty.Area);
    }

    [Fact]
    public void RectU_UnionCoversBothRects()
    {
        RectU union = new RectU(0, 0, 4, 4).Union(new RectU(10, 10, 2, 2));

        Assert.Equal(new RectU(0, 0, 12, 12), union);
    }

    [Fact]
    public void RectU_UnionWithEmptyRectReturnsOtherRect()
    {
        RectU rect = new(4, 5, 6, 7);

        Assert.Equal(rect, rect.Union(RectU.Empty));
        Assert.Equal(rect, RectU.Empty.Union(rect));
    }

    [Fact]
    public void RectU_ContainsRectChecksFullInclusion()
    {
        RectU outer = new(0, 0, 100, 100);

        Assert.True(outer.ContainsRect(new RectU(10, 10, 5, 5)), "Прямоугольник внутри.");
        Assert.True(outer.ContainsRect(outer), "Прямоугольник содержит сам себя.");
        Assert.False(outer.ContainsRect(new RectU(95, 95, 10, 10)), "Прямоугольник выходит за границу.");
        Assert.False(outer.ContainsRect(new RectU(-1, 0, 4, 4)), "Прямоугольник начинается вне.");
    }

    [Fact]
    public void RectU_OffsetShiftsWithoutChangingSize()
    {
        RectU moved = new RectU(10, 10, 4, 4).Offset(3, -2);

        Assert.Equal(new RectU(13, 8, 4, 4), moved);
    }

    [Fact]
    public void RectU_ComparesByValue()
    {
        RectU first = new(1, 2, 3, 4);
        RectU second = new(1, 2, 3, 4);

        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(first, second);
        Assert.True(first != new RectU(1, 2, 3, 5));
    }

    [Fact]
    public void RectU_ToStringIsReadable()
    {
        Assert.Equal("[x=1, y=2, w=3, h=4]", new RectU(1, 2, 3, 4).ToString());
    }
}