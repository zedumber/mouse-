using System.Runtime.InteropServices;
using VirtualController.Core.Input;
using VirtualController.Core.Mapping;

namespace VirtualController.Windows.RawInput;

/// <summary>
/// Traduce el contenido crudo de un RAWINPUT (tal como lo entrega GetRawInputData) a InputEvent de
/// dominio. Función pura, sin dependencia de hardware real — se testea con buffers sintéticos
/// (ver VirtualController.Input.Tests).
/// </summary>
internal static class RawInputParser
{
    private static readonly int HeaderSize = Marshal.SizeOf<RawInputHeader>();

    public static IReadOnlyList<InputEvent> Parse(ReadOnlySpan<byte> rawInput, long captureTimestamp)
    {
        if (rawInput.Length < HeaderSize)
        {
            return [];
        }

        var header = MemoryMarshal.Read<RawInputHeader>(rawInput);
        var data = rawInput[HeaderSize..];

        return header.Type switch
        {
            NativeMethods.RimTypeMouse when data.Length >= Marshal.SizeOf<RawMouse>() =>
                ParseMouse(MemoryMarshal.Read<RawMouse>(data), captureTimestamp),
            NativeMethods.RimTypeKeyboard when data.Length >= Marshal.SizeOf<RawKeyboard>() =>
                ParseKeyboard(MemoryMarshal.Read<RawKeyboard>(data), captureTimestamp),
            _ => [],
        };
    }

    private static IReadOnlyList<InputEvent> ParseMouse(RawMouse mouse, long timestamp)
    {
        var events = new List<InputEvent>();

        // Con MOUSE_MOVE_ABSOLUTE, LastX/LastY NO son deltas: son coordenadas normalizadas 0..65535.
        // Ocurre en RDP, máquinas virtuales, streaming remoto, tabletas y pantallas táctiles. Tratarlas
        // como deltas mandaba al stick un movimiento de decenas de miles de counts y lo dejaba clavado
        // al máximo de forma permanente. Se descartan: este proyecto necesita movimiento relativo.
        var isAbsolute = (mouse.Flags & NativeMethods.MouseMoveAbsolute) != 0;

        if (!isAbsolute && (mouse.LastX != 0 || mouse.LastY != 0))
        {
            events.Add(new InputEvent.MouseMove(mouse.LastX, mouse.LastY, timestamp));
        }

        AddButtonEvent(events, mouse.ButtonFlags, NativeMethods.RiMouseLeftButtonDown, NativeMethods.RiMouseLeftButtonUp, MouseButton.Left, timestamp);
        AddButtonEvent(events, mouse.ButtonFlags, NativeMethods.RiMouseRightButtonDown, NativeMethods.RiMouseRightButtonUp, MouseButton.Right, timestamp);
        AddButtonEvent(events, mouse.ButtonFlags, NativeMethods.RiMouseMiddleButtonDown, NativeMethods.RiMouseMiddleButtonUp, MouseButton.Middle, timestamp);
        AddButtonEvent(events, mouse.ButtonFlags, NativeMethods.RiMouseButton4Down, NativeMethods.RiMouseButton4Up, MouseButton.Extra1, timestamp);
        AddButtonEvent(events, mouse.ButtonFlags, NativeMethods.RiMouseButton5Down, NativeMethods.RiMouseButton5Up, MouseButton.Extra2, timestamp);

        return events;
    }

    private static void AddButtonEvent(
        List<InputEvent> events,
        ushort buttonFlags,
        ushort downFlag,
        ushort upFlag,
        MouseButton button,
        long timestamp)
    {
        if ((buttonFlags & downFlag) != 0)
        {
            events.Add(new InputEvent.Digital(PhysicalInput.FromMouseButton(button), IsActive: true, timestamp));
        }
        else if ((buttonFlags & upFlag) != 0)
        {
            events.Add(new InputEvent.Digital(PhysicalInput.FromMouseButton(button), IsActive: false, timestamp));
        }
    }

    private static IReadOnlyList<InputEvent> ParseKeyboard(RawKeyboard keyboard, long timestamp)
    {
        var key = MapVirtualKey(keyboard.VKey);
        if (key is null)
        {
            return [];
        }

        var isActive = (keyboard.Flags & NativeMethods.RiKeyBreak) == 0;
        return [new InputEvent.Digital(PhysicalInput.FromKey(key.Value), isActive, timestamp)];
    }

    // Nota: para Shift/Ctrl/Alt, RAWKEYBOARD.VKey a veces reporta la tecla virtual genérica
    // (p. ej. VK_SHIFT = 0x10) en vez de la específica izquierda/derecha (VK_LSHIFT = 0xA0);
    // distinguir left/right de forma fiable requiere inspeccionar MakeCode + el flag RI_KEY_E0.
    // Como Key solo tiene "LeftShift" (sin "RightShift" todavía), aceptamos ambos valores conocidos
    // sin necesitar esa disambiguación por ahora.
    private static Key? MapVirtualKey(ushort virtualKey) => virtualKey switch
    {
        0x57 => Key.W,
        0x41 => Key.A,
        0x53 => Key.S,
        0x44 => Key.D,
        0x20 => Key.Space,
        0x51 => Key.Q,
        0x45 => Key.E,
        0x52 => Key.R,
        0x10 or 0xA0 => Key.LeftShift,
        0x1B => Key.Escape,
        _ => null,
    };
}
