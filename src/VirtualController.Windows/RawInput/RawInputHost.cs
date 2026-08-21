using VirtualController.Core.Input;

namespace VirtualController.Windows.RawInput;

/// <summary>
/// Único componente autorizado a llamar RegisterRawInputDevices en todo el proceso (ADR-002, punto 3).
/// Implementa el puerto IInputSource definido en Core; el resto de la aplicación depende solo de esa
/// interfaz, nunca de esta clase ni de tipos Win32.
/// </summary>
public sealed class RawInputHost : IInputSource
{
    private readonly IInputEventQueue _queue;
    private readonly RawInputWindow _window;

    public RawInputHost(IInputEventQueue queue, Action<Exception>? onError = null)
    {
        _queue = queue;
        _window = new RawInputWindow(OnRawInput, onDeviceChange: null, onError);
    }

    public void Start() => _window.Start();

    public void Stop() => _window.Stop();

    private void OnRawInput(ReadOnlyMemory<byte> rawInput, long captureTimestamp)
    {
        foreach (var inputEvent in RawInputParser.Parse(rawInput.Span, captureTimestamp))
        {
            _queue.TryWrite(inputEvent);
        }
    }

    public void Dispose() => _window.Dispose();
}
