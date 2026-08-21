namespace VirtualController.Core.Mapping;

public enum InputDevice
{
    Keyboard,
    Mouse,

    /// <summary>
    /// Movimiento relativo del mouse. Es un dispositivo lógico aparte de Mouse (los botones) porque su
    /// semántica es distinta: no tiene estado pulsado/soltado, produce deltas continuos.
    /// </summary>
    MouseMotion,
}

public readonly record struct PhysicalInput
{
    public InputDevice Device { get; }

    private int Code { get; }

    private PhysicalInput(InputDevice device, int code)
    {
        Device = device;
        Code = code;
    }

    public static PhysicalInput FromKey(Key key) => new(InputDevice.Keyboard, (int)key);

    public static PhysicalInput FromMouseButton(MouseButton button) => new(InputDevice.Mouse, (int)button);

    public static PhysicalInput MouseMovement { get; } = new(InputDevice.MouseMotion, 0);

    public Key AsKey() => Device == InputDevice.Keyboard
        ? (Key)Code
        : throw new InvalidOperationException($"{this} no es una tecla de teclado.");

    public MouseButton AsMouseButton() => Device == InputDevice.Mouse
        ? (MouseButton)Code
        : throw new InvalidOperationException($"{this} no es un botón de mouse.");

    public override string ToString() => Device switch
    {
        InputDevice.Keyboard => $"Keyboard.{(Key)Code}",
        InputDevice.Mouse => $"Mouse.{(MouseButton)Code}",
        InputDevice.MouseMotion => "Mouse.Movement",
        _ => throw new ArgumentOutOfRangeException()
    };
}
