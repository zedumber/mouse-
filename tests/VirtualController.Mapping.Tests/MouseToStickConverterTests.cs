using VirtualController.Core.Mouse;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class MouseToStickConverterTests
{
    // Reloj sintético: 1000 ticks = 1 segundo, para que los tests no dependan de Stopwatch.Frequency
    // ni del tiempo real transcurrido.
    private const long TicksPerSecond = 1000;

    private static MouseToStickConverter Create(MouseSettings? settings = null) =>
        new(settings ?? new MouseSettings { CountsForFullDeflection = 100f }, TicksPerSecond);

    [Fact]
    public void NoInput_StaysNeutral()
    {
        var converter = Create();

        converter.Update(now: 1000);

        Assert.Equal((0f, 0f), converter.CurrentValue);
    }

    [Fact]
    public void MouseRight_ProducesPositiveX()
    {
        var converter = Create();

        converter.AddDelta(deltaX: 50, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        var (x, y) = converter.CurrentValue;
        Assert.Equal(0.5f, x, precision: 4);
        Assert.Equal(0f, y, precision: 4);
    }

    [Fact]
    public void MouseDown_ProducesNegativeY_BecauseScreenAndStickAxesAreOpposite()
    {
        var converter = Create();

        // En pantalla, dy positivo es "hacia abajo"; en el stick, Y positivo es "arriba".
        converter.AddDelta(deltaX: 0, deltaY: 50, timestamp: 1000);
        converter.Update(now: 1000);

        var (_, y) = converter.CurrentValue;
        Assert.Equal(-0.5f, y, precision: 4);
    }

    [Fact]
    public void FullDeflectionCounts_ReachesMaximum()
    {
        var converter = Create();

        converter.AddDelta(deltaX: 100, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(1f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void ExcessiveMovement_ClampsToUnitRange()
    {
        var converter = Create();

        converter.AddDelta(deltaX: 10_000, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(1f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void Sensitivity_ScalesOutput()
    {
        var converter = Create(new MouseSettings { CountsForFullDeflection = 100f, SensitivityX = 2f });

        converter.AddDelta(deltaX: 25, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(0.5f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void SensitivityXAndY_AreIndependent()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            SensitivityX = 2f,
            SensitivityY = 1f,
        });

        converter.AddDelta(deltaX: 25, deltaY: -25, timestamp: 1000);
        converter.Update(now: 1000);

        var (x, y) = converter.CurrentValue;
        Assert.Equal(0.5f, x, precision: 4);
        Assert.Equal(0.25f, y, precision: 4);
    }

    [Fact]
    public void InvertX_FlipsHorizontalDirection()
    {
        var converter = Create(new MouseSettings { CountsForFullDeflection = 100f, InvertX = true });

        converter.AddDelta(deltaX: 50, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(-0.5f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void InvertY_FlipsVerticalDirection()
    {
        var converter = Create(new MouseSettings { CountsForFullDeflection = 100f, InvertY = true });

        converter.AddDelta(deltaX: 0, deltaY: 50, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(0.5f, converter.CurrentValue.Y, precision: 4);
    }

    [Fact]
    public void MaximumOutput_LimitsDeflection()
    {
        var converter = Create(new MouseSettings { CountsForFullDeflection = 100f, MaximumOutput = 0.5f });

        converter.AddDelta(deltaX: 100, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(0.5f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void OutputScaleAndMaximumOutput_HaveIndependentSemantics()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            OutputScale = 2f,
            MaximumOutput = 0.6f,
        });

        converter.AddDelta(deltaX: 20, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);
        Assert.Equal(0.4f, converter.CurrentValue.X, precision: 4);

        converter.AddDelta(deltaX: 80, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);
        Assert.Equal(0.6f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void OutputAntiDeadzone_OvercomesGameDeadzoneWithoutChangingDirection()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            OutputAntiDeadzone = 0.2f,
        });

        converter.AddDelta(deltaX: 10, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(0.28f, converter.CurrentValue.X, precision: 4);
        Assert.Equal(0f, converter.CurrentValue.Y);
    }

    [Fact]
    public void Deadzone_SuppressesTinyMovements()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            DeadzoneInner = 0.2f,
        });

        converter.AddDelta(deltaX: 5, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(0f, converter.CurrentValue.X);
    }

    [Fact]
    public void PowerCurve_IsAppliedToOutput()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            ResponseCurve = new PowerCurve(2f),
        });

        converter.AddDelta(deltaX: 50, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        // 0.5 elevado al cuadrado = 0.25
        Assert.Equal(0.25f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void Curve_AppliedToMagnitude_PreservesDiagonalDirection()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            ResponseCurve = new PowerCurve(2f),
        });

        converter.AddDelta(deltaX: 40, deltaY: -40, timestamp: 1000);
        converter.Update(now: 1000);

        var (x, y) = converter.CurrentValue;
        Assert.Equal(x, y, precision: 5);
    }

    [Fact]
    public void ImmediateDecay_ReturnsToCenterWhenMouseStops()
    {
        var converter = Create();

        converter.AddDelta(deltaX: 50, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);
        Assert.NotEqual(0f, converter.CurrentValue.X);

        converter.Update(now: 2000);

        Assert.Equal(0f, converter.CurrentValue.X);
    }

    [Fact]
    public void ExponentialDecay_ReturnsGraduallyNotInstantly()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            Decay = new ExponentialDecay(rate: 5f),
        });

        converter.AddDelta(deltaX: 100, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);
        Assert.Equal(1f, converter.CurrentValue.X, precision: 3);

        // 100 ticks = 0.1 s con el reloj sintético.
        converter.Update(now: 1100);
        var afterOneStep = converter.CurrentValue.X;

        Assert.True(afterOneStep < 1f, "Debería haber decaído algo.");
        Assert.True(afterOneStep > 0f, "No debería caer a cero de golpe.");
    }

    [Fact]
    public void DecayDelay_HoldsThenDecaysOnlyElapsedTail()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            Decay = new ExponentialDecay(rate: 10f),
            DecayDelayMilliseconds = 10f,
        });

        converter.AddDelta(deltaX: 50, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);
        converter.Update(now: 1005);
        Assert.Equal(0.5f, converter.CurrentValue.X, precision: 4);

        converter.Update(now: 1015);
        Assert.Equal(0.5f * MathF.Exp(-10f * 0.005f), converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void AdsMode_AppliesSensitivityPrecisionAndOutputLimit()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            AdsEnabled = true,
            AdsSensitivityMultiplier = 0.5f,
            AdsPrecisionExponent = 2f,
            AdsMaximumOutput = 0.8f,
        });

        converter.AddDelta(deltaX: 50, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000, adsActive: true);

        Assert.Equal(0.0625f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void SustainedMovement_ReachesEquilibriumInsteadOfCollapsing()
    {
        // El decay es temporal y corre siempre; un movimiento sostenido debe mantener el stick
        // desviado, con la entrada compensando el decaimiento. Antes, el decay solo corría en las
        // iteraciones sin delta, así que con un bucle rápido el stick se vaciaba entre eventos.
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            Decay = new ExponentialDecay(rate: 5f),
        });

        var now = 1000L;
        for (var i = 0; i < 20; i++)
        {
            converter.AddDelta(deltaX: 10, deltaY: 0, timestamp: now);
            converter.Update(now);
            now += 10; // 10 ms entre eventos: un mouse de 100 Hz.
        }

        Assert.True(
            converter.CurrentValue.X > 0.5f,
            $"Un movimiento sostenido debería mantener el stick desviado, pero vale {converter.CurrentValue.X}.");
    }

    [Fact]
    public void FastLoopBetweenMouseEvents_DoesNotCollapseTheStick()
    {
        // Reproduce el bucle real: muchas iteraciones sin evento entre dos eventos de mouse.
        var converter = Create(new MouseSettings { CountsForFullDeflection = 100f });

        converter.AddDelta(deltaX: 100, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        // 50 iteraciones ocupando 1 ms en total (equivalente a un bucle de ~50 kHz).
        for (var i = 1; i <= 50; i++)
        {
            converter.Update(now: 1000 + (i / 50));
        }

        Assert.True(
            converter.CurrentValue.X > 0.8f,
            $"El stick se vació por iterar rápido sin eventos nuevos (x={converter.CurrentValue.X}).");
    }

    [Fact]
    public void SmoothingDisabledByDefault_OutputFollowsInputImmediately()
    {
        var converter = Create();

        converter.AddDelta(deltaX: 100, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);

        Assert.Equal(1f, converter.CurrentValue.X, precision: 4);
    }

    [Fact]
    public void SmoothingEnabled_LagsBehindInput()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            SmoothingEnabled = true,
            SmoothingStrength = 0.5f,
        });

        converter.AddDelta(deltaX: 100, deltaY: 0, timestamp: 1000);
        converter.Update(now: 1000);
        converter.AddDelta(deltaX: 0, deltaY: 0, timestamp: 1010);
        converter.Update(now: 1010);

        // Con suavizado, la salida aún no ha alcanzado el valor pedido.
        Assert.True(converter.CurrentValue.X < 1f);
        Assert.True(converter.CurrentValue.X > 0f);
    }

    [Fact]
    public void AdaptiveSmoothing_RespondsFasterToLargeMovement()
    {
        var fixedFilter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            SmoothingEnabled = true,
            SmoothingStrength = 0.8f,
            Decay = new ExponentialDecay(0.001f),
        });
        var adaptiveFilter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            SmoothingEnabled = true,
            SmoothingStrength = 0.8f,
            AdaptiveSmoothingEnabled = true,
            AdaptiveSmoothingResponsiveness = 2f,
            Decay = new ExponentialDecay(0.001f),
        });

        fixedFilter.Update(1000);
        adaptiveFilter.Update(1000);
        fixedFilter.AddDelta(100, 0, 1010);
        adaptiveFilter.AddDelta(100, 0, 1010);
        fixedFilter.Update(1010);
        adaptiveFilter.Update(1010);

        Assert.True(adaptiveFilter.CurrentValue.X > fixedFilter.CurrentValue.X);
        Assert.True(adaptiveFilter.CurrentValue.X <= 1f);
    }

    [Fact]
    public void Acceleration_AmplifiesByVelocity_NotByPacketSize()
    {
        // Mismo desplazamiento físico total (200 counts) y mismo tiempo, repartido de dos formas.
        // La aceleración debe dar prácticamente lo mismo: antes se calculaba por evento, así que un
        // mouse de bajo polling rate recibía mucha más amplificación que uno de 1000 Hz.
        // Aceleración moderada para que ninguno de los dos casos sature en 1.0: si ambos saturan, el
        // test pasa sin comparar nada.
        var settings = new MouseSettings { CountsForFullDeflection = 1000f, Acceleration = 0.5f };

        var fewPackets = Create(settings);
        fewPackets.AddDelta(deltaX: 0, deltaY: 0, timestamp: 1000);
        fewPackets.AddDelta(deltaX: 100, deltaY: 0, timestamp: 1050);
        fewPackets.AddDelta(deltaX: 100, deltaY: 0, timestamp: 1100);
        fewPackets.Update(now: 1100);

        var manyPackets = Create(settings);
        manyPackets.AddDelta(deltaX: 0, deltaY: 0, timestamp: 1000);
        for (var i = 1; i <= 20; i++)
        {
            manyPackets.AddDelta(deltaX: 10, deltaY: 0, timestamp: 1000 + (i * 5));
        }
        manyPackets.Update(now: 1100);

        Assert.Equal(fewPackets.CurrentValue.X, manyPackets.CurrentValue.X, precision: 2);
    }

    [Fact]
    public void Acceleration_AmplifiesFastGestureMoreThanSlowOne()
    {
        // Aceleración moderada para que ninguno de los dos casos sature en 1.0: si ambos saturan, el
        // test pasa sin comparar nada.
        var settings = new MouseSettings { CountsForFullDeflection = 1000f, Acceleration = 0.5f };

        // Mismos counts, pero uno recorridos en la quinta parte de tiempo.
        var fast = Create(settings);
        fast.AddDelta(deltaX: 0, deltaY: 0, timestamp: 1000);
        fast.AddDelta(deltaX: 20, deltaY: 0, timestamp: 1010);
        fast.Update(now: 1010);

        var slow = Create(settings);
        slow.AddDelta(deltaX: 0, deltaY: 0, timestamp: 1000);
        slow.AddDelta(deltaX: 20, deltaY: 0, timestamp: 1050);
        slow.Update(now: 1050);

        Assert.True(
            fast.CurrentValue.X > slow.CurrentValue.X,
            $"El gesto rápido ({fast.CurrentValue.X}) debería amplificarse más que el lento ({slow.CurrentValue.X}).");
    }

    [Fact]
    public void Reset_ClearsAccumulatedStateAndOutput()
    {
        var converter = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            Decay = new ExponentialDecay(rate: 1f),
        });
        converter.AddDelta(deltaX: 100, deltaY: 100, timestamp: 1000);
        converter.Update(now: 1000);

        converter.Reset();

        Assert.Equal((0f, 0f), converter.CurrentValue);
    }

    [Fact]
    public void InvalidSettings_ThrowAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => Create(new MouseSettings { DeadzoneInner = 0.9f, DeadzoneOuter = 0.5f }));
        Assert.Throws<ArgumentException>(() => Create(new MouseSettings { MaximumOutput = 0f }));
        Assert.Throws<ArgumentException>(() => Create(new MouseSettings { SensitivityX = 0f }));
        Assert.Throws<ArgumentException>(() => Create(new MouseSettings { SmoothingStrength = 1f }));
        Assert.Throws<ArgumentException>(() => Create(new MouseSettings { OutputAntiDeadzone = 0.5f, MaximumOutput = 0.5f }));
        Assert.Throws<ArgumentException>(() => Create(new MouseSettings { DecayDelayMilliseconds = 101f }));
    }
}
