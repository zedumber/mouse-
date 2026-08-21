using System.Runtime.InteropServices;

namespace VirtualController.VirtualGamepad.Tests;

/// <summary>
/// Lee el estado del mando tal y como lo ve un juego, a través de XInput. Permite comprobar el efecto
/// real del backend en vez de confiar en lo que el propio backend afirma de sí mismo.
/// </summary>
internal static class XInputProbe
{
    internal readonly record struct ControllerObservation(uint Index, XInputGamepad Gamepad);

    [StructLayout(LayoutKind.Sequential)]
    internal struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    private const uint ErrorSuccess = 0;
    private const uint MaxControllers = 4;

    public static ControllerObservation? WaitForController(
        Func<XInputGamepad, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;

        do
        {
            for (uint i = 0; i < MaxControllers; i++)
            {
                if (XInputGetState(i, out var state) != ErrorSuccess || !predicate(state.Gamepad))
                {
                    continue;
                }

                return new ControllerObservation(i, state.Gamepad);
            }

            Thread.Sleep(10);
        }
        while (Environment.TickCount64 < deadline);

        return null;
    }

    public static XInputGamepad? WaitForState(
        uint index,
        Func<XInputGamepad, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;

        do
        {
            if (XInputGetState(index, out var state) == ErrorSuccess && predicate(state.Gamepad))
            {
                return state.Gamepad;
            }

            Thread.Sleep(10);
        }
        while (Environment.TickCount64 < deadline);

        return null;
    }
}
