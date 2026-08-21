using VirtualController.Core.Gamepad;

namespace VirtualController.Core.Mapping;

public abstract record VirtualOutput
{
    public sealed record DigitalButton(GamepadButton Button) : VirtualOutput;

    public sealed record AnalogValue(GamepadAxis Axis, float Value) : VirtualOutput;

    public sealed record StickDirection(Stick Stick, Direction Direction) : VirtualOutput;

    public sealed record StickVector(Stick Stick) : VirtualOutput;

    private VirtualOutput()
    {
    }
}
