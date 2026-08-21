using VirtualController.Core.Mouse;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class MouseSettingsDraftTests
{
    [Fact]
    public void NewDraft_ProducesValidDefaults()
    {
        var draft = new MouseSettingsDraft();

        Assert.Null(draft.Validate());
        var settings = draft.Build();
        Assert.IsType<LinearCurve>(settings.ResponseCurve);
        Assert.False(settings.SmoothingEnabled);
    }

    [Fact]
    public void RoundTrip_PreservesEverySetting()
    {
        var original = new MouseSettings
        {
            CountsForFullDeflection = 250f,
            SensitivityX = 2.5f,
            SensitivityY = 1.75f,
            InvertX = true,
            InvertY = true,
            DeadzoneInner = 0.12f,
            DeadzoneOuter = 0.93f,
            OutputScale = 1.25f,
            OutputAntiDeadzone = 0.06f,
            MaximumOutput = 0.8f,
            Acceleration = 1.5f,
            SmoothingEnabled = true,
            SmoothingStrength = 0.4f,
            AdaptiveSmoothingEnabled = true,
            AdaptiveSmoothingResponsiveness = 3.5f,
            DecayDelayMilliseconds = 8f,
            AdsEnabled = true,
            AdsSensitivityMultiplier = 0.55f,
            AdsMaximumOutput = 0.7f,
            AdsPrecisionExponent = 1.35f,
            ResponseCurve = new DualZoneCurve(0.4f, 1.7f, 0.8f),
            Decay = new ExponentialDecay(9.5f),
        };

        var rebuilt = new MouseSettingsDraft(original).Build();

        Assert.Equal(250f, rebuilt.CountsForFullDeflection);
        Assert.Equal(2.5f, rebuilt.SensitivityX);
        Assert.Equal(1.75f, rebuilt.SensitivityY);
        Assert.True(rebuilt.InvertX);
        Assert.True(rebuilt.InvertY);
        Assert.Equal(0.12f, rebuilt.DeadzoneInner);
        Assert.Equal(0.93f, rebuilt.DeadzoneOuter);
        Assert.Equal(1.25f, rebuilt.OutputScale);
        Assert.Equal(0.06f, rebuilt.OutputAntiDeadzone);
        Assert.Equal(0.8f, rebuilt.MaximumOutput);
        Assert.Equal(1.5f, rebuilt.Acceleration);
        Assert.True(rebuilt.SmoothingEnabled);
        Assert.Equal(0.4f, rebuilt.SmoothingStrength);
        Assert.True(rebuilt.AdaptiveSmoothingEnabled);
        Assert.Equal(3.5f, rebuilt.AdaptiveSmoothingResponsiveness);
        Assert.Equal(8f, rebuilt.DecayDelayMilliseconds);
        Assert.True(rebuilt.AdsEnabled);
        Assert.Equal(0.55f, rebuilt.AdsSensitivityMultiplier);
        Assert.Equal(0.7f, rebuilt.AdsMaximumOutput);
        Assert.Equal(1.35f, rebuilt.AdsPrecisionExponent);
        var dualZone = Assert.IsType<DualZoneCurve>(rebuilt.ResponseCurve);
        Assert.Equal(0.4f, dualZone.Transition, precision: 4);
        Assert.Equal(1.7f, dualZone.PrecisionExponent, precision: 4);
        Assert.Equal(0.8f, dualZone.TurnExponent, precision: 4);
        Assert.Equal(9.5f, Assert.IsType<ExponentialDecay>(rebuilt.Decay).Rate, precision: 4);
    }

    [Theory]
    [InlineData(ResponseCurveKind.Linear, typeof(LinearCurve))]
    [InlineData(ResponseCurveKind.Power, typeof(PowerCurve))]
    [InlineData(ResponseCurveKind.Exponential, typeof(ExponentialCurve))]
    [InlineData(ResponseCurveKind.DualZone, typeof(DualZoneCurve))]
    public void CurveKind_SelectsTheRightImplementation(ResponseCurveKind kind, Type expected)
    {
        var draft = new MouseSettingsDraft
        {
            CurveKind = kind,
            CurveParameter = MouseSettingsDraft.DefaultParameterFor(kind),
        };

        Assert.IsType(expected, draft.Build().ResponseCurve);
    }

    [Theory]
    [InlineData(DecayKind.Immediate, typeof(ImmediateDecay))]
    [InlineData(DecayKind.Linear, typeof(LinearDecay))]
    [InlineData(DecayKind.Exponential, typeof(ExponentialDecay))]
    public void DecayKind_SelectsTheRightImplementation(DecayKind kind, Type expected)
    {
        var draft = new MouseSettingsDraft
        {
            DecayKind = kind,
            DecaySpeed = MouseSettingsDraft.DefaultSpeedFor(kind),
        };

        Assert.IsType(expected, draft.Build().Decay);
    }

    [Fact]
    public void InvertedDeadzone_ReportsErrorInsteadOfThrowing()
    {
        var draft = new MouseSettingsDraft { DeadzoneInner = 0.9f, DeadzoneOuter = 0.2f };

        var error = draft.Validate();

        Assert.NotNull(error);
        Assert.Contains("Deadzone", error);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void InvalidSensitivity_ReportsError(float sensitivity)
    {
        var draft = new MouseSettingsDraft { SensitivityX = sensitivity };

        Assert.NotNull(draft.Validate());
    }

    [Fact]
    public void MaximumOutputAboveOne_ReportsError()
    {
        var draft = new MouseSettingsDraft { MaximumOutput = 1.5f };

        Assert.NotNull(draft.Validate());
    }

    [Fact]
    public void AntiDeadzoneAtOrAboveMaximum_ReportsError()
    {
        var draft = new MouseSettingsDraft { OutputAntiDeadzone = 0.8f, MaximumOutput = 0.8f };

        Assert.NotNull(draft.Validate());
    }

    [Fact]
    public void ExcessiveExponentialStrength_ReportsError_InsteadOfProducingNaN()
    {
        // Por encima del tope, exp desborda y la curva devolvía NaN, dejando el stick muerto en la
        // deflexión máxima.
        var draft = new MouseSettingsDraft
        {
            CurveKind = ResponseCurveKind.Exponential,
            CurveParameter = 500f,
        };

        Assert.NotNull(draft.Validate());
    }

    [Fact]
    public void ZeroCurveParameter_ReportsError()
    {
        var draft = new MouseSettingsDraft { CurveKind = ResponseCurveKind.Power, CurveParameter = 0f };

        Assert.NotNull(draft.Validate());
    }

    [Fact]
    public void TryBuild_OnInvalidDraft_ReturnsFalseWithReason()
    {
        var draft = new MouseSettingsDraft { MaximumOutput = 0f };

        var ok = draft.TryBuild(out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryBuild_OnValidDraft_ReturnsSettings()
    {
        var draft = new MouseSettingsDraft { SensitivityX = 3f };

        var ok = draft.TryBuild(out var settings, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(3f, settings.SensitivityX);
    }

    [Fact]
    public void DefaultParameters_DifferPerCurveKind()
    {
        // Arrastrar el parámetro entre tipos daría un resultado extraño: un exponente de 2 y una
        // intensidad exponencial de 2 no significan lo mismo.
        Assert.NotEqual(
            MouseSettingsDraft.DefaultParameterFor(ResponseCurveKind.Power),
            MouseSettingsDraft.DefaultParameterFor(ResponseCurveKind.Exponential));
    }

    [Fact]
    public void DefaultsForEveryKind_AreValid()
    {
        foreach (var curve in Enum.GetValues<ResponseCurveKind>())
        {
            foreach (var decay in Enum.GetValues<DecayKind>())
            {
                var draft = new MouseSettingsDraft
                {
                    CurveKind = curve,
                    CurveParameter = MouseSettingsDraft.DefaultParameterFor(curve),
                    DecayKind = decay,
                    DecaySpeed = MouseSettingsDraft.DefaultSpeedFor(decay),
                };

                Assert.Null(draft.Validate());
            }
        }
    }
}
