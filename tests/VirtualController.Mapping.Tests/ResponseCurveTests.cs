using VirtualController.Core.Mouse;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class ResponseCurveTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void Linear_IsIdentity(float input)
    {
        Assert.Equal(input, LinearCurve.Instance.Apply(input));
    }

    [Fact]
    public void Power_WithExponentOne_MatchesLinear()
    {
        var curve = new PowerCurve(1f);

        Assert.Equal(0.5f, curve.Apply(0.5f), precision: 5);
    }

    [Fact]
    public void Power_WithExponentAboveOne_ReducesSmallInputs()
    {
        var curve = new PowerCurve(2f);

        // Más precisión en movimientos pequeños: la salida queda por debajo de la entrada.
        Assert.Equal(0.25f, curve.Apply(0.5f), precision: 5);
        Assert.True(curve.Apply(0.5f) < 0.5f);
    }

    [Theory]
    [InlineData(2f)]
    [InlineData(0.5f)]
    [InlineData(3f)]
    public void Power_PreservesEndpoints(float exponent)
    {
        var curve = new PowerCurve(exponent);

        Assert.Equal(0f, curve.Apply(0f), precision: 5);
        Assert.Equal(1f, curve.Apply(1f), precision: 5);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void Power_WithInvalidExponent_Throws(float exponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PowerCurve(exponent));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(5f)]
    public void Exponential_IsNormalizedToPreserveEndpoints(float strength)
    {
        var curve = new ExponentialCurve(strength);

        Assert.Equal(0f, curve.Apply(0f), precision: 5);
        Assert.Equal(1f, curve.Apply(1f), precision: 5);
    }

    [Fact]
    public void Exponential_IsMonotonicallyIncreasing()
    {
        var curve = new ExponentialCurve(3f);

        var previous = curve.Apply(0f);
        for (var i = 1; i <= 10; i++)
        {
            var current = curve.Apply(i / 10f);
            Assert.True(current > previous, $"No es monótona en {i / 10f}.");
            previous = current;
        }
    }

    [Fact]
    public void DualZone_IsContinuousAndPreservesEndpoints()
    {
        var curve = new DualZoneCurve(transition: 0.45f, precisionExponent: 1.6f, turnExponent: 0.75f);

        Assert.Equal(0f, curve.Apply(0f), precision: 5);
        Assert.Equal(0.45f, curve.Apply(0.45f), precision: 5);
        Assert.Equal(1f, curve.Apply(1f), precision: 5);
        Assert.True(Math.Abs(curve.Apply(0.4499f) - curve.Apply(0.4501f)) < 0.002f);
    }

    [Fact]
    public void DualZone_GivesPrecisionLowAndFasterTurningHigh()
    {
        var curve = new DualZoneCurve(transition: 0.45f, precisionExponent: 1.6f, turnExponent: 0.75f);

        Assert.True(curve.Apply(0.2f) < 0.2f);
        Assert.True(curve.Apply(0.8f) > 0.8f);
    }

    [Theory]
    [InlineData(0f, 1f, 1f)]
    [InlineData(1f, 1f, 1f)]
    [InlineData(0.5f, 0f, 1f)]
    [InlineData(0.5f, 1f, 0f)]
    public void DualZone_InvalidParametersThrow(float transition, float precision, float turn)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DualZoneCurve(transition, precision, turn));
    }

    [Fact]
    public void Custom_InterpolatesLinearlyBetweenPoints()
    {
        var curve = new CustomCurve([(0f, 0f), (0.5f, 0.1f), (1f, 1f)]);

        Assert.Equal(0.05f, curve.Apply(0.25f), precision: 4);
        Assert.Equal(0.1f, curve.Apply(0.5f), precision: 4);
        Assert.Equal(0.55f, curve.Apply(0.75f), precision: 4);
    }

    [Fact]
    public void Custom_ClampsOutsideDefinedRange()
    {
        var curve = new CustomCurve([(0.2f, 0.3f), (0.8f, 0.9f)]);

        Assert.Equal(0.3f, curve.Apply(0f), precision: 4);
        Assert.Equal(0.9f, curve.Apply(1f), precision: 4);
    }

    [Fact]
    public void Custom_AcceptsUnorderedPoints()
    {
        var curve = new CustomCurve([(1f, 1f), (0f, 0f), (0.5f, 0.5f)]);

        Assert.Equal(0.5f, curve.Apply(0.5f), precision: 4);
    }

    [Fact]
    public void Custom_WithFewerThanTwoPoints_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CustomCurve([(0f, 0f)]));
    }
}
