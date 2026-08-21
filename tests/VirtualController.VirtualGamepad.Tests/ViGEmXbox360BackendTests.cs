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
    public void Connect_ReportsAccurateStatusForThisMachine()
    {
        using var backend = new ViGEmXbox360Backend();

        var result = backend.Connect();

        // El test afirma algo en ambos casos, en vez de saltarse silenciosamente cuando el driver
        // está presente: un test que se auto-desactiva da confianza falsa.
        if (ViGEmBusIsInstalled())
        {
            Assert.True(result.IsConnected, $"Con ViGEmBus instalado debería conectar: {result.Detail}");
            Assert.True(backend.IsConnected);
        }
        else
        {
            Assert.Equal(GamepadConnectionStatus.BackendNotInstalled, result.Status);
            Assert.False(backend.IsConnected);
            Assert.NotNull(result.Detail);
        }
    }

    [Fact]
    public void Connect_LeavesDeviceInNeutralState()
    {
        // Regresión: un mando recién creado reportaba valores basura por XInput (~10% de deflexión) y
        // un Submit neutro no lo corregía, así que el juego veía el stick desviado nada más arrancar.
        if (!ViGEmBusIsInstalled())
        {
            return;
        }

        using var backend = new ViGEmXbox360Backend();
        Assert.True(backend.Connect().IsConnected);

        var state = XInputProbe.ReadFirstConnected();

        Assert.NotNull(state);
        Assert.Equal(0, state!.Value.ThumbLX);
        Assert.Equal(0, state.Value.ThumbLY);
        Assert.Equal(0, state.Value.ThumbRX);
        Assert.Equal(0, state.Value.ThumbRY);
        Assert.Equal(0, state.Value.LeftTrigger);
        Assert.Equal(0, state.Value.RightTrigger);
        Assert.Equal(0, state.Value.Buttons);
    }

    [Fact]
    public void Submit_IsVisibleThroughXInput()
    {
        if (!ViGEmBusIsInstalled())
        {
            return;
        }

        using var backend = new ViGEmXbox360Backend();
        Assert.True(backend.Connect().IsConnected);

        backend.Submit(GamepadState.Neutral with { LeftStickY = 1f, RightTrigger = 1f });
        Thread.Sleep(20);

        var state = XInputProbe.ReadFirstConnected();

        Assert.NotNull(state);
        Assert.True(state!.Value.ThumbLY > 30000, $"ThumbLY={state.Value.ThumbLY}");
        Assert.Equal(255, state.Value.RightTrigger);
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
}
