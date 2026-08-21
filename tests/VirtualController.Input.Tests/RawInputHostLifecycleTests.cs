using VirtualController.Core.Input;
using VirtualController.Windows.RawInput;
using Xunit;

namespace VirtualController.Input.Tests;

/// <summary>
/// A diferencia de RawInputParserTests, estos tests SÍ ejercitan Win32 real (RegisterClassEx,
/// CreateWindowEx con HWND_MESSAGE, RegisterRawInputDevices). No verifican captura de hardware
/// real (eso requiere verificación manual — ver PROJECT_STATUS.md), pero sí que el ciclo de vida
/// completo no falla por un error de interop/marshaling.
/// </summary>
public class RawInputHostLifecycleTests
{
    [Fact]
    public void StartThenStop_RegistersRealWin32WindowAndDevices_DoesNotThrow()
    {
        var queue = new ChannelInputEventQueue();
        using var host = new RawInputHost(queue);

        host.Start();
        host.Stop();
    }

    [Fact]
    public void StartWhileAlreadyStarted_Throws()
    {
        var queue = new ChannelInputEventQueue();
        using var host = new RawInputHost(queue);
        host.Start();

        try
        {
            Assert.Throws<InvalidOperationException>(() => host.Start());
        }
        finally
        {
            host.Stop();
        }
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var queue = new ChannelInputEventQueue();
        using var host = new RawInputHost(queue);

        host.Stop();
    }
}
