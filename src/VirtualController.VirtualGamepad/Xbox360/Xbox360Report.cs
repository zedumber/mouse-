using VirtualController.Core.Gamepad;

namespace VirtualController.VirtualGamepad.Xbox360;

/// <summary>
/// Formato de wire de un mando Xbox 360: sticks como Int16, triggers como byte. Deliberadamente
/// libre de tipos de cualquier SDK de backend (ADR-003, punto 2/7) — así la conversión se testea sin
/// tener el driver instalado, y sirve igual si el backend concreto cambia.
/// </summary>
public readonly record struct Xbox360Report(
    short LeftThumbX,
    short LeftThumbY,
    short RightThumbX,
    short RightThumbY,
    byte LeftTrigger,
    byte RightTrigger,
    GamepadButtons Buttons);

public static class Xbox360ReportConverter
{
    private const short ThumbMax = short.MaxValue;   //  32767
    private const short ThumbMin = short.MinValue;   // -32768
    private const byte TriggerMax = byte.MaxValue;   //    255

    public static Xbox360Report ToReport(in GamepadState state) => new(
        ToThumb(state.LeftStickX),
        ToThumb(state.LeftStickY),
        ToThumb(state.RightStickX),
        ToThumb(state.RightStickY),
        ToTrigger(state.LeftTrigger),
        ToTrigger(state.RightTrigger),
        state.Buttons);

    /// <summary>
    /// El rango de Int16 es asimétrico (-32768..32767), así que -1.0 y +1.0 se escalan con factores
    /// distintos para alcanzar exactamente ambos extremos sin desbordar.
    /// </summary>
    public static short ToThumb(float normalized)
    {
        var clamped = Math.Clamp(normalized, -1f, 1f);

        return clamped < 0f
            ? (short)MathF.Round(clamped * -(float)ThumbMin)
            : (short)MathF.Round(clamped * ThumbMax);
    }

    public static byte ToTrigger(float normalized)
    {
        var clamped = Math.Clamp(normalized, 0f, 1f);
        return (byte)MathF.Round(clamped * TriggerMax);
    }
}
