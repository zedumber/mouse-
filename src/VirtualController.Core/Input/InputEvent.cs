using VirtualController.Core.Mapping;

namespace VirtualController.Core.Input;

public abstract record InputEvent
{
    /// <summary>
    /// Instante monotónico (Stopwatch.GetTimestamp()) en que la aplicación capturó el evento —
    /// no un timestamp de hardware (ver ADR-002, punto 5).
    /// </summary>
    public sealed record Digital(PhysicalInput Input, bool IsActive, long CaptureTimestamp) : InputEvent;

    public sealed record MouseMove(int DeltaX, int DeltaY, long CaptureTimestamp) : InputEvent;

    private InputEvent()
    {
    }
}
