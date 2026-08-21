using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;
using VirtualController.Core.Profiles;

namespace VirtualController.Infrastructure.Persistence;

/// <summary>
/// Formato textual de bindings en JSON ("Keyboard.W" → "Stick.Left.Up"), legible y editable a mano.
/// Es un detalle de persistencia: el dominio nunca interpreta estas cadenas en caliente, se compilan
/// una vez (ADR-006, punto 8).
/// </summary>
internal static class BindingTextFormat
{
    public static string FormatInput(PhysicalInput input) => input.ToString();

    public static PhysicalInput ParseInput(string text)
    {
        var parts = text.Split('.', 2);
        if (parts.Length != 2)
        {
            throw new ProfileValidationException($"Input con formato inválido: \"{text}\". Se esperaba \"Keyboard.X\" o \"Mouse.X\".");
        }

        return parts[0] switch
        {
            "Mouse" when parts[1] == "Movement" => PhysicalInput.MouseMovement,
            "Keyboard" when TryParseEnum<Key>(parts[1], out var key) => PhysicalInput.FromKey(key),
            "Mouse" when TryParseEnum<MouseButton>(parts[1], out var button) => PhysicalInput.FromMouseButton(button),
            "Keyboard" => throw new ProfileValidationException($"Tecla desconocida: \"{parts[1]}\"."),
            "Mouse" => throw new ProfileValidationException($"Botón de mouse desconocido: \"{parts[1]}\"."),
            _ => throw new ProfileValidationException($"Dispositivo desconocido: \"{parts[0]}\"."),
        };
    }

    /// <summary>
    /// Parseo estricto: Enum.TryParse por sí solo acepta valores numéricos y listas separadas por
    /// comas, de modo que "Keyboard.42" se aceptaba y producía un binding muerto que nunca coincidía
    /// con nada y del que el usuario no recibía ningún aviso.
    /// </summary>
    private static bool TryParseEnum<TEnum>(string text, out TEnum value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse(text, ignoreCase: true, out value) && Enum.IsDefined(value);
    }

    public static string FormatOutput(VirtualOutput output) => output switch
    {
        VirtualOutput.DigitalButton button => $"Gamepad.{button.Button}",
        VirtualOutput.AnalogValue analog => $"Gamepad.{analog.Axis}",
        VirtualOutput.StickDirection direction => $"Stick.{direction.Stick}.{direction.Direction}",
        VirtualOutput.StickVector vector => $"Stick.{vector.Stick}.Vector",
        _ => throw new ProfileValidationException($"Tipo de salida no soportado: {output.GetType().Name}."),
    };

    public static VirtualOutput ParseOutput(string text, float? value)
    {
        var parts = text.Split('.');

        return parts switch
        {
            ["Gamepad", var name] => ParseGamepadOutput(name, value, text),
            ["Stick", var stick, "Vector"] => new VirtualOutput.StickVector(ParseStick(stick, text)),
            ["Stick", var stick, var direction] => new VirtualOutput.StickDirection(
                ParseStick(stick, text),
                ParseDirection(direction, text)),
            _ => throw new ProfileValidationException($"Salida con formato inválido: \"{text}\"."),
        };
    }

    private static VirtualOutput ParseGamepadOutput(string name, float? value, string original)
    {
        if (TryParseEnum<GamepadAxis>(name, out var axis))
        {
            // Un trigger sin valor explícito se interpreta como "totalmente pulsado", que es el caso
            // habitual (click de mouse → gatillo a fondo).
            return new VirtualOutput.AnalogValue(axis, value ?? 1f);
        }

        if (TryParseEnum<GamepadButton>(name, out var button))
        {
            return new VirtualOutput.DigitalButton(button);
        }

        throw new ProfileValidationException($"Salida de gamepad desconocida: \"{original}\".");
    }

    private static Stick ParseStick(string text, string original) =>
        TryParseEnum<Stick>(text, out var stick)
            ? stick
            : throw new ProfileValidationException($"Stick desconocido en \"{original}\".");

    private static Direction ParseDirection(string text, string original) =>
        TryParseEnum<Direction>(text, out var direction)
            ? direction
            : throw new ProfileValidationException($"Dirección desconocida en \"{original}\".");
}
