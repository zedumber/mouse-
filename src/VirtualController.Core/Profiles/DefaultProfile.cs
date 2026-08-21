using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;

namespace VirtualController.Core.Profiles;

/// <summary>
/// Mapeo inicial descrito en el brief (sección 1). Nótese que WASD no recibe ningún trato especial:
/// son cuatro bindings normales a direcciones del stick izquierdo (ADR-006).
/// </summary>
public static class DefaultProfile
{
    public static IReadOnlyList<Binding> Bindings { get; } =
    [
        new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Left, Direction.Up)),
        new(PhysicalInput.FromKey(Key.A), new VirtualOutput.StickDirection(Stick.Left, Direction.Left)),
        new(PhysicalInput.FromKey(Key.S), new VirtualOutput.StickDirection(Stick.Left, Direction.Down)),
        new(PhysicalInput.FromKey(Key.D), new VirtualOutput.StickDirection(Stick.Left, Direction.Right)),

        // Movimiento del mouse → stick derecho. Es el único binding cuyo valor no depende de si algo
        // está pulsado, sino del MouseToStickConverter con estado (ADR-006).
        new(PhysicalInput.MouseMovement, new VirtualOutput.StickVector(Stick.Right)),

        new(PhysicalInput.FromMouseButton(MouseButton.Left), new VirtualOutput.AnalogValue(GamepadAxis.RightTrigger, 1f)),
        new(PhysicalInput.FromMouseButton(MouseButton.Right), new VirtualOutput.AnalogValue(GamepadAxis.LeftTrigger, 1f)),

        new(PhysicalInput.FromKey(Key.Space), new VirtualOutput.DigitalButton(GamepadButton.A)),
        new(PhysicalInput.FromKey(Key.R), new VirtualOutput.DigitalButton(GamepadButton.X)),
        new(PhysicalInput.FromKey(Key.E), new VirtualOutput.DigitalButton(GamepadButton.Y)),
        new(PhysicalInput.FromKey(Key.Q), new VirtualOutput.DigitalButton(GamepadButton.LeftShoulder)),
        new(PhysicalInput.FromKey(Key.LeftShift), new VirtualOutput.DigitalButton(GamepadButton.LeftStick)),
    ];

    public static Profile Create(string name = "Default") => new()
    {
        Id = ProfileId.New(),
        Name = name,
        Bindings = Bindings,
    };
}
