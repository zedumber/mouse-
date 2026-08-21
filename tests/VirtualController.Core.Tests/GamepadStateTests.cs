using VirtualController.Core.Gamepad;
using Xunit;

namespace VirtualController.Core.Tests;

public class GamepadStateTests
{
    [Fact]
    public void Neutral_HasNoButtonsPressedAndZeroedAxes()
    {
        var state = GamepadState.Neutral;

        Assert.False(state.IsPressed(GamepadButton.A));
        Assert.Equal(0f, state.LeftStickX);
        Assert.Equal(0f, state.LeftStickY);
        Assert.Equal(0f, state.RightStickX);
        Assert.Equal(0f, state.RightStickY);
        Assert.Equal(0f, state.GetAxis(GamepadAxis.LeftTrigger));
        Assert.Equal(0f, state.GetAxis(GamepadAxis.RightTrigger));
    }

    [Fact]
    public void WithButton_Pressed_SetsOnlyThatButton()
    {
        var state = GamepadState.Neutral.WithButton(GamepadButton.A, pressed: true);

        Assert.True(state.IsPressed(GamepadButton.A));
        Assert.False(state.IsPressed(GamepadButton.B));
    }

    [Fact]
    public void WithButton_Released_ClearsOnlyThatButton()
    {
        var pressed = GamepadState.Neutral
            .WithButton(GamepadButton.A, pressed: true)
            .WithButton(GamepadButton.B, pressed: true);

        var released = pressed.WithButton(GamepadButton.A, pressed: false);

        Assert.False(released.IsPressed(GamepadButton.A));
        Assert.True(released.IsPressed(GamepadButton.B));
    }

    [Fact]
    public void WithAxis_SetsOnlyThatAxis()
    {
        var state = GamepadState.Neutral.WithAxis(GamepadAxis.RightTrigger, 1f);

        Assert.Equal(1f, state.GetAxis(GamepadAxis.RightTrigger));
        Assert.Equal(0f, state.GetAxis(GamepadAxis.LeftTrigger));
    }
}
