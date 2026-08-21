using VirtualController.Core.Mouse;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class DecayStrategyTests
{
    [Fact]
    public void Immediate_ReturnsToCenterInAFewMilliseconds()
    {
        // "Inmediato" es una constante de tiempo muy corta, no un salto a cero: un salto a cero no es
        // una tasa y dependía de la frecuencia con que se llamara Update (ver ImmediateDecay).
        var (x, _) = ImmediateDecay.Instance.Decay(1f, 0f, deltaSeconds: 0.05f);

        Assert.InRange(x, 0f, 0.01f);
    }

    [Fact]
    public void Immediate_IsFramerateIndependent()
    {
        var (single, _) = ImmediateDecay.Instance.Decay(1f, 0f, deltaSeconds: 0.01f);

        var value = 1f;
        for (var i = 0; i < 10; i++)
        {
            (value, _) = ImmediateDecay.Instance.Decay(value, 0f, deltaSeconds: 0.001f);
        }

        Assert.Equal(single, value, precision: 4);
    }

    [Fact]
    public void Immediate_DoesNotCollapseWithinASingleFastIteration()
    {
        // Este es el caso que rompía el mouse: con un bucle rápido, entre dos eventos de mouse pasan
        // microsegundos, y el acumulador no debe vaciarse en ese intervalo.
        var (x, _) = ImmediateDecay.Instance.Decay(1f, 0f, deltaSeconds: 0.00001f);

        Assert.True(x > 0.99f, $"El acumulador se vació en una sola iteración rápida (x={x}).");
    }

    [Fact]
    public void Linear_SubtractsConstantAmountPerSecond()
    {
        var decay = new LinearDecay(unitsPerSecond: 2f);

        // 1.0 de magnitud menos 2.0/s * 0.25s = 0.5
        var (x, _) = decay.Decay(1f, 0f, deltaSeconds: 0.25f);

        Assert.Equal(0.5f, x, precision: 4);
    }

    [Fact]
    public void Linear_ClampsAtZero_DoesNotOvershootIntoNegative()
    {
        var decay = new LinearDecay(unitsPerSecond: 2f);

        var (x, y) = decay.Decay(0.1f, 0f, deltaSeconds: 5f);

        Assert.Equal(0f, x);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void Linear_PreservesDirectionWhileDecaying()
    {
        var decay = new LinearDecay(unitsPerSecond: 0.5f);

        var (x, y) = decay.Decay(0.6f, 0.6f, deltaSeconds: 0.1f);

        Assert.Equal(x, y, precision: 5);
    }

    [Fact]
    public void Exponential_ReducesProportionally_NeverReachesExactlyZero()
    {
        var decay = new ExponentialDecay(rate: 8f);

        var (x, _) = decay.Decay(1f, 0f, deltaSeconds: 0.1f);

        Assert.True(x < 1f);
        Assert.True(x > 0f);
    }

    [Fact]
    public void Exponential_IsFramerateIndependent()
    {
        var decay = new ExponentialDecay(rate: 5f);

        // Un paso de 0.1s debe equivaler a diez pasos de 0.01s.
        var (single, _) = decay.Decay(1f, 0f, deltaSeconds: 0.1f);

        var value = 1f;
        for (var i = 0; i < 10; i++)
        {
            (value, _) = decay.Decay(value, 0f, deltaSeconds: 0.01f);
        }

        Assert.Equal(single, value, precision: 5);
    }

    [Fact]
    public void Exponential_PreservesDirection()
    {
        var decay = new ExponentialDecay(rate: 4f);

        var (x, y) = decay.Decay(0.8f, -0.4f, deltaSeconds: 0.05f);

        Assert.Equal(-2f, x / y, precision: 4);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void InvalidRates_Throw(float rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinearDecay(rate));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialDecay(rate));
    }
}
