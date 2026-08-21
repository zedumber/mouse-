using System.ServiceProcess;
using VirtualController.Core.Gamepad;
using VirtualController.VirtualGamepad.Xbox360;
using Xunit;

namespace VirtualController.VirtualGamepad.Tests;

/// <summary>
/// Estos tests verifican el comportamiento del backend en una máquina SIN ViGEmBus instalado — que es
/// el caso que más importa no romper: el usuario que abre la app por primera vez (ADR-003, punto 6).
/// La verificación con el driver realmente instalado (joy.cpl, XInput) forma parte del spike manual
/// de Fase 3 y no puede automatizarse aquí.
/// </summary>
public class ViGEmXbox360BackendTests
{
    /// <summary>
    /// Detecta el driver sin crear un mando virtual: comprobar el servicio evita el efecto secundario
    /// de enchufar y desenchufar un dispositivo real solo para decidir si un test aplica.
    /// </summary>
    private static bool ViGEmBusIsInstalled()
    {
        using var service = new ServiceController("ViGEmBus");
        try
        {
            return service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    [Fact]
    public void Connect_WhenDriverIsMissing_ReturnsActionableStatus()
    {
        if (ViGEmBusIsInstalled())
        {
            return;
        }

        using var backend = new ViGEmXbox360Backend();

        var result = backend.Connect();

        Assert.Equal(GamepadConnectionStatus.BackendNotInstalled, result.Status);
        Assert.False(backend.IsConnected);
        Assert.NotNull(result.Detail);
    }

    [Fact]
    public void ConnectSubmitAndReset_AreVisibleThroughTheSameXInputDevice()
    {
        // XInput puede conservar durante un instante el slot del mando creado por un test anterior y
        // reutilizar ese mismo índice. Se identifica nuestro dispositivo con un estado marcador antes
        // de comprobar el neutro; "el primer mando" o "un slot nuevo" producen falsos negativos.
        if (!ViGEmBusIsInstalled())
        {
            return;
        }

        using var backend = new ViGEmXbox360Backend();
        Assert.True(backend.Connect().IsConnected);

        backend.Submit(GamepadState.Neutral with
        {
            LeftStickX = 0.5f,
            LeftStickY = 1f,
            RightTrigger = 0.75f,
        });
        var identified = XInputProbe.WaitForController(
            gamepad => gamepad.ThumbLX is > 16000 and < 17000
                && gamepad.ThumbLY > 30000
                && gamepad.RightTrigger is >= 190 and <= 192,
            TimeSpan.FromSeconds(2));

        Assert.NotNull(identified);
        Assert.True(identified!.Value.Gamepad.ThumbLY > 30000);
        Assert.Equal(191, identified.Value.Gamepad.RightTrigger);

        backend.Reset();
        var state = XInputProbe.WaitForState(
            identified.Value.Index,
            IsNeutral,
            TimeSpan.FromSeconds(2));

        Assert.NotNull(state);
        Assert.True(IsNeutral(state!.Value));
    }

    [Fact]
    public void Submit_WhileDisconnected_IsIgnored_DoesNotThrow()
    {
        using var backend = new ViGEmXbox360Backend();

        backend.Submit(GamepadState.Neutral.WithButton(GamepadButton.A, pressed: true));
    }

    [Fact]
    public void Reset_WhileDisconnected_IsIgnored_DoesNotThrow()
    {
        using var backend = new ViGEmXbox360Backend();

        backend.Reset();
    }

    [Fact]
    public void Disconnect_WithoutConnect_DoesNotThrow()
    {
        using var backend = new ViGEmXbox360Backend();

        backend.Disconnect();
    }

    [Fact]
    public void Dispose_WithoutConnect_DoesNotThrow()
    {
        var backend = new ViGEmXbox360Backend();

        backend.Dispose();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var backend = new ViGEmXbox360Backend();

        backend.Dispose();
        backend.Dispose();
    }

    private static bool IsNeutral(XInputProbe.XInputGamepad state) =>
        state.ThumbLX == 0
        && state.ThumbLY == 0
        && state.ThumbRX == 0
        && state.ThumbRY == 0
        && state.LeftTrigger == 0
        && state.RightTrigger == 0
        && state.Buttons == 0;
}
