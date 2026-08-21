using VirtualController.Core.Gamepad;
using VirtualController.VirtualGamepad.Xbox360;
using Xunit;

namespace VirtualController.VirtualGamepad.Tests;

public class Xbox360ReportConverterTests
{
    [Fact]
    public void Neutral_ConvertsToAllZeroes()
    {
        var report = Xbox360ReportConverter.ToReport(GamepadState.Neutral);

        Assert.Equal(0, report.LeftThumbX);
        Assert.Equal(0, report.LeftThumbY);
        Assert.Equal(0, report.RightThumbX);
        Assert.Equal(0, report.RightThumbY);
        Assert.Equal(0, report.LeftTrigger);
        Assert.Equal(0, report.RightTrigger);
        Assert.Equal(GamepadButtons.None, report.Buttons);
    }

    [Theory]
    [InlineData(1f, short.MaxValue)]
    [InlineData(-1f, short.MinValue)]
    [InlineData(0f, 0)]
    public void ToThumb_ReachesBothExtremesExactly(float normalized, short expected)
    {
        Assert.Equal(expected, Xbox360ReportConverter.ToThumb(normalized));
    }

    [Theory]
    [InlineData(2f, short.MaxValue)]
    [InlineData(-2f, short.MinValue)]
    [InlineData(float.PositiveInfinity, short.MaxValue)]
    public void ToThumb_ClampsOutOfRangeInput_DoesNotOverflow(float normalized, short expected)
    {
        Assert.Equal(expected, Xbox360ReportConverter.ToThumb(normalized));
    }

    [Fact]
    public void ToThumb_DiagonalValue_MapsToProportionalMagnitude()
    {
        // 0.7071 es el valor que produce la normalización diagonal de W+D (ADR-006 / requisito 9).
        var thumb = Xbox360ReportConverter.ToThumb(0.7071f);

        Assert.InRange(thumb, 23100, 23200);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, byte.MaxValue)]
    [InlineData(0.5f, 128)]
    public void ToTrigger_MapsNormalizedRangeToByte(float normalized, byte expected)
    {
        Assert.Equal(expected, Xbox360ReportConverter.ToTrigger(normalized));
    }

    [Theory]
    [InlineData(-1f, 0)]
    [InlineData(2f, byte.MaxValue)]
    public void ToTrigger_ClampsOutOfRangeInput(float normalized, byte expected)
    {
        Assert.Equal(expected, Xbox360ReportConverter.ToTrigger(normalized));
    }

    [Fact]
    public void ToReport_PreservesButtonsAndMapsAllAxes()
    {
        var state = GamepadState.Neutral with
        {
            LeftStickX = 1f,
            LeftStickY = -1f,
            RightStickX = 0f,
            RightStickY = 1f,
            LeftTrigger = 1f,
            RightTrigger = 0f,
            Buttons = GamepadButtons.A | GamepadButtons.Start,
        };

        var report = Xbox360ReportConverter.ToReport(state);

        Assert.Equal(short.MaxValue, report.LeftThumbX);
        Assert.Equal(short.MinValue, report.LeftThumbY);
        Assert.Equal(0, report.RightThumbX);
        Assert.Equal(short.MaxValue, report.RightThumbY);
        Assert.Equal(byte.MaxValue, report.LeftTrigger);
        Assert.Equal(0, report.RightTrigger);
        Assert.Equal(GamepadButtons.A | GamepadButtons.Start, report.Buttons);
    }
}
