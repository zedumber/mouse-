namespace VirtualController.Core.Gamepad;

public readonly record struct GamepadState(
    float LeftStickX,
    float LeftStickY,
    float RightStickX,
    float RightStickY,
    float LeftTrigger,
    float RightTrigger,
    GamepadButtons Buttons)
{
    public static readonly GamepadState Neutral = default;

    public bool IsPressed(GamepadButton button) => (Buttons & ToFlag(button)) != 0;

    public GamepadState WithButton(GamepadButton button, bool pressed) =>
        pressed
            ? this with { Buttons = Buttons | ToFlag(button) }
            : this with { Buttons = Buttons & ~ToFlag(button) };

    public float GetAxis(GamepadAxis axis) => axis switch
    {
        GamepadAxis.LeftTrigger => LeftTrigger,
        GamepadAxis.RightTrigger => RightTrigger,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    public GamepadState WithAxis(GamepadAxis axis, float value) => axis switch
    {
        GamepadAxis.LeftTrigger => this with { LeftTrigger = value },
        GamepadAxis.RightTrigger => this with { RightTrigger = value },
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    private static GamepadButtons ToFlag(GamepadButton button) => button switch
    {
        GamepadButton.A => GamepadButtons.A,
        GamepadButton.B => GamepadButtons.B,
        GamepadButton.X => GamepadButtons.X,
        GamepadButton.Y => GamepadButtons.Y,
        GamepadButton.LeftShoulder => GamepadButtons.LeftShoulder,
        GamepadButton.RightShoulder => GamepadButtons.RightShoulder,
        GamepadButton.LeftStick => GamepadButtons.LeftStick,
        GamepadButton.RightStick => GamepadButtons.RightStick,
        GamepadButton.DPadUp => GamepadButtons.DPadUp,
        GamepadButton.DPadDown => GamepadButtons.DPadDown,
        GamepadButton.DPadLeft => GamepadButtons.DPadLeft,
        GamepadButton.DPadRight => GamepadButtons.DPadRight,
        GamepadButton.Start => GamepadButtons.Start,
        GamepadButton.Back => GamepadButtons.Back,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };
}
