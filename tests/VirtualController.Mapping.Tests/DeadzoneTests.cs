using VirtualController.Core.Mouse;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class DeadzoneTests
{
    [Fact]
    public void BelowInnerDeadzone_ReturnsZero()
    {
        Assert.Equal(0f, Deadzone.ApplyToMagnitude(0.05f, inner: 0.1f, outer: 1f));
    }

    [Fact]
    public void AtOrAboveOuterDeadzone_SaturatesToOne()
    {
        Assert.Equal(1f, Deadzone.ApplyToMagnitude(0.9f, inner: 0f, outer: 0.9f));
        Assert.Equal(1f, Deadzone.ApplyToMagnitude(1f, inner: 0f, outer: 0.9f));
    }

    [Fact]
    public void JustAboveInner_StartsNearZero_NoSuddenJump()
    {
        var atBoundary = Deadzone.ApplyToMagnitude(0.1f, inner: 0.1f, outer: 1f);
        var justAbove = Deadzone.ApplyToMagnitude(0.11f, inner: 0.1f, outer: 1f);

        Assert.Equal(0f, atBoundary);
        Assert.InRange(justAbove, 0f, 0.05f);
    }

    [Fact]
    public void Midpoint_RescalesRemainingRange()
    {
        // Con inner=0.2 y outer=1.0, la magnitud 0.6 está a mitad del rango útil.
        var result = Deadzone.ApplyToMagnitude(0.6f, inner: 0.2f, outer: 1f);

        Assert.Equal(0.5f, result, precision: 4);
    }

    [Fact]
    public void NoDeadzone_IsIdentity()
    {
        Assert.Equal(0.42f, Deadzone.ApplyToMagnitude(0.42f, inner: 0f, outer: 1f), precision: 4);
    }

    [Theory]
    [InlineData(-0.1f, 1f)]
    [InlineData(0f, 1.5f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.8f, 0.2f)]
    public void InvalidBounds_Throw(float inner, float outer)
    {
        Assert.Throws<ArgumentException>(() => Deadzone.ApplyToMagnitude(0.5f, inner, outer));
    }

    [Fact]
    public void Radial_PreservesDirection()
    {
        var (x, y) = Deadzone.ApplyRadial(0.5f, 0.5f, inner: 0.2f, outer: 1f);

        // La dirección diagonal (x == y) debe conservarse tras reescalar la magnitud.
        Assert.Equal(x, y, precision: 5);
        Assert.True(x > 0f);
    }

    [Fact]
    public void Radial_InsideDeadzone_ReturnsZeroVector()
    {
        var (x, y) = Deadzone.ApplyRadial(0.05f, 0.05f, inner: 0.2f, outer: 1f);

        Assert.Equal(0f, x);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void Radial_ZeroVector_DoesNotDivideByZero()
    {
        var (x, y) = Deadzone.ApplyRadial(0f, 0f, inner: 0.1f, outer: 1f);

        Assert.Equal(0f, x);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void Radial_DiagonalAtFullDeflection_DoesNotExceedUnitMagnitude()
    {
        var (x, y) = Deadzone.ApplyRadial(1f, 1f, inner: 0f, outer: 1f);

        var magnitude = MathF.Sqrt((x * x) + (y * y));
        Assert.True(magnitude <= 1.0001f, $"La magnitud {magnitude} supera 1.");
    }
}
