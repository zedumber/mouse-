using VirtualController.Core.Mapping;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class StickComposerTests
{
    [Fact]
    public void Up_Alone_ProducesForward()
    {
        var (x, y) = StickComposer.Compose(new HashSet<Direction> { Direction.Up }, normalizeDiagonal: true);

        Assert.Equal(0f, x);
        Assert.Equal(1f, y);
    }

    [Fact]
    public void Right_Alone_ProducesRight()
    {
        var (x, y) = StickComposer.Compose(new HashSet<Direction> { Direction.Right }, normalizeDiagonal: true);

        Assert.Equal(1f, x);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void UpAndRight_WithNormalization_ProducesUnitDiagonal_NeverOne()
    {
        var (x, y) = StickComposer.Compose(new HashSet<Direction> { Direction.Up, Direction.Right }, normalizeDiagonal: true);

        Assert.Equal(0.7071f, x, precision: 4);
        Assert.Equal(0.7071f, y, precision: 4);
        Assert.NotEqual(1f, x);
        Assert.NotEqual(1f, y);
    }

    [Fact]
    public void UpAndRight_WithoutNormalization_ProducesFullMagnitudeOnBothAxes()
    {
        var (x, y) = StickComposer.Compose(new HashSet<Direction> { Direction.Up, Direction.Right }, normalizeDiagonal: false);

        Assert.Equal(1f, x);
        Assert.Equal(1f, y);
    }

    [Fact]
    public void UpAndDown_CancelOutToZero()
    {
        var (x, y) = StickComposer.Compose(new HashSet<Direction> { Direction.Up, Direction.Down }, normalizeDiagonal: true);

        Assert.Equal(0f, x);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void NoDirections_ProducesNeutral()
    {
        var (x, y) = StickComposer.Compose(new HashSet<Direction>(), normalizeDiagonal: true);

        Assert.Equal(0f, x);
        Assert.Equal(0f, y);
    }
}
