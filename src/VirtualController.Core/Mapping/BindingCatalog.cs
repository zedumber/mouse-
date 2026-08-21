using VirtualController.Core.Gamepad;

namespace VirtualController.Core.Mapping;

/// <summary>
/// Qué entradas y salidas se pueden elegir al crear un binding. Vive en el dominio porque es una
/// propiedad del modelo de mapping, no de una pantalla concreta: si mañana se añade un control físico
/// o una salida, aparece en cualquier interfaz sin tocarla.
/// </summary>
public static class BindingCatalog
{
    public static IReadOnlyList<PhysicalInput> AvailableInputs { get; } = BuildInputs();

    public static IReadOnlyList<VirtualOutput> AvailableOutputs { get; } = BuildOutputs();

    private static PhysicalInput[] BuildInputs()
    {
        var inputs = new List<PhysicalInput>();

        foreach (var key in Enum.GetValues<Key>())
        {
            inputs.Add(PhysicalInput.FromKey(key));
        }

        foreach (var button in Enum.GetValues<MouseButton>())
        {
            inputs.Add(PhysicalInput.FromMouseButton(button));
        }

        inputs.Add(PhysicalInput.MouseMovement);
        return inputs.ToArray();
    }

    private static VirtualOutput[] BuildOutputs()
    {
        var outputs = new List<VirtualOutput>();

        foreach (var button in Enum.GetValues<GamepadButton>())
        {
            outputs.Add(new VirtualOutput.DigitalButton(button));
        }

        // Los gatillos se ofrecen a fondo: es lo que se quiere al asignarlos a un botón (click → RT).
        // Valores intermedios siguen siendo posibles editando el JSON.
        foreach (var axis in Enum.GetValues<GamepadAxis>())
        {
            outputs.Add(new VirtualOutput.AnalogValue(axis, 1f));
        }

        foreach (var stick in Enum.GetValues<Stick>())
        {
            foreach (var direction in Enum.GetValues<Direction>())
            {
                outputs.Add(new VirtualOutput.StickDirection(stick, direction));
            }

            outputs.Add(new VirtualOutput.StickVector(stick));
        }

        return outputs.ToArray();
    }

    /// <summary>
    /// Nombre legible de una entrada. Formatear para leer es distinto de formatear para guardar
    /// (eso lo hace la capa de persistencia), así que no comparten implementación a propósito.
    /// </summary>
    public static string Describe(PhysicalInput input) => input.Device switch
    {
        InputDevice.Keyboard => DescribeKey(input.AsKey()),
        InputDevice.Mouse => DescribeMouseButton(input.AsMouseButton()),
        InputDevice.MouseMotion => "Movimiento del mouse",
        _ => input.ToString(),
    };

    public static string Describe(VirtualOutput output) => output switch
    {
        VirtualOutput.DigitalButton button => $"Botón {DescribeButton(button.Button)}",
        VirtualOutput.AnalogValue analog => DescribeAxis(analog.Axis),
        VirtualOutput.StickDirection direction =>
            $"Stick {DescribeStick(direction.Stick)} — {DescribeDirection(direction.Direction)}",
        VirtualOutput.StickVector vector => $"Stick {DescribeStick(vector.Stick)} (movimiento libre)",
        _ => output.ToString() ?? "?",
    };

    private static string DescribeKey(Key key) => key switch
    {
        Key.Space => "Tecla Espacio",
        Key.LeftShift => "Tecla Shift izquierdo",
        Key.LeftControl => "Tecla Ctrl izquierdo",
        Key.LeftAlt => "Tecla Alt izquierdo",
        Key.Escape => "Tecla Escape",
        _ => $"Tecla {key}",
    };

    private static string DescribeMouseButton(MouseButton button) => button switch
    {
        MouseButton.Left => "Click izquierdo",
        MouseButton.Right => "Click derecho",
        MouseButton.Middle => "Click central",
        MouseButton.Extra1 => "Botón lateral 1",
        MouseButton.Extra2 => "Botón lateral 2",
        _ => button.ToString(),
    };

    private static string DescribeButton(GamepadButton button) => button switch
    {
        GamepadButton.LeftShoulder => "LB",
        GamepadButton.RightShoulder => "RB",
        GamepadButton.LeftStick => "LS (pulsar stick izq.)",
        GamepadButton.RightStick => "RS (pulsar stick der.)",
        GamepadButton.DPadUp => "Cruceta arriba",
        GamepadButton.DPadDown => "Cruceta abajo",
        GamepadButton.DPadLeft => "Cruceta izquierda",
        GamepadButton.DPadRight => "Cruceta derecha",
        _ => button.ToString(),
    };

    private static string DescribeAxis(GamepadAxis axis) => axis switch
    {
        GamepadAxis.LeftTrigger => "Gatillo izquierdo (LT)",
        GamepadAxis.RightTrigger => "Gatillo derecho (RT)",
        _ => axis.ToString(),
    };

    private static string DescribeStick(Stick stick) =>
        stick == Stick.Left ? "izquierdo" : "derecho";

    private static string DescribeDirection(Direction direction) => direction switch
    {
        Direction.Up => "arriba",
        Direction.Down => "abajo",
        Direction.Left => "izquierda",
        Direction.Right => "derecha",
        _ => direction.ToString(),
    };
}
