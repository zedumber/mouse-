using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using VirtualController.Core.Gamepad;

// El SDK de ViGEm también expone un tipo llamado IVirtualGamepad; el alias evita la ambigüedad y deja
// explícito que implementamos NUESTRO puerto de dominio, no el suyo.
using IVirtualGamepad = VirtualController.Core.Gamepad.IVirtualGamepad;

namespace VirtualController.VirtualGamepad.Xbox360;

/// <summary>
/// Backend legacy basado en ViGEmBus (ADR-003). ViGEmBus está EOL desde 2023 y su cliente NuGet no se
/// actualiza desde entonces: esta clase es el ÚNICO punto de la solución que toca ese SDK, para que el
/// spike de Fase 3 pueda sustituirlo (p. ej. por HIDMaestro) sin tocar ninguna otra capa.
///
/// No es thread-safe, igual que ViGEmClient: solo el consumidor de procesamiento puede invocarlo
/// (ADR-003 punto 4 / ADR-005).
/// </summary>
public sealed class ViGEmXbox360Backend : IVirtualGamepad
{
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;

    public bool IsConnected { get; private set; }

    public GamepadConnectionResult Connect()
    {
        if (IsConnected)
        {
            return GamepadConnectionResult.Connected();
        }

        try
        {
            _client = new ViGEmClient();
        }
        catch (VigemBusNotFoundException ex)
        {
            return GamepadConnectionResult.Failed(
                GamepadConnectionStatus.BackendNotInstalled,
                $"El driver ViGEmBus no está instalado: {ex.Message}");
        }
        catch (VigemBusVersionMismatchException ex)
        {
            return GamepadConnectionResult.Failed(
                GamepadConnectionStatus.BackendVersionIncompatible,
                $"Versión de ViGEmBus incompatible con este cliente: {ex.Message}");
        }
        catch (VigemBusAccessFailedException ex)
        {
            return GamepadConnectionResult.Failed(
                GamepadConnectionStatus.ConnectionFailed,
                $"No se pudo acceder a ViGEmBus: {ex.Message}");
        }

        try
        {
            _controller = _client.CreateXbox360Controller();
            _controller.AutoSubmitReport = false;
            _controller.Connect();
        }
        catch (Exception ex)
        {
            DisposeBackendResources();
            return GamepadConnectionResult.Failed(
                GamepadConnectionStatus.DeviceCreationFailed,
                $"No se pudo crear el mando Xbox 360 virtual: {ex.Message}");
        }

        IsConnected = true;
        ForceNeutralInitialState();

        return GamepadConnectionResult.Connected();
    }

    /// <summary>
    /// Deja el dispositivo en un estado neutro observable desde XInput.
    ///
    /// Verificado midiendo con XInput: un mando recién creado reporta valores basura (p. ej.
    /// LX=-3356, LY=-1869, cerca de un 10% de deflexión), y un Submit neutro NO lo corrige, porque
    /// coincide con el estado interno que el cliente considera ya vigente y el report no llega a
    /// transmitirse. Sin esto, el juego ve el stick desviado nada más arrancar la emulación: el
    /// personaje camina o gira solo hasta que el usuario mueve algo.
    ///
    /// Se envía primero un estado deliberadamente distinto para garantizar una transmisión real, y
    /// justo después el neutro. Es un rodeo impuesto por el comportamiento del backend, no por el
    /// diseño: queda aquí encerrado, sin que ninguna otra capa tenga que saberlo.
    /// </summary>
    private void ForceNeutralInitialState()
    {
        Submit(GamepadState.Neutral with { LeftStickX = 1f });
        Submit(GamepadState.Neutral);
    }

    public void Disconnect()
    {
        if (!IsConnected)
        {
            return;
        }

        try
        {
            _controller?.Disconnect();
        }
        catch (Exception ex) when (IsExpectedDisconnectFailure(ex))
        {
            // El objetivo de Disconnect es acabar desconectado; si el dispositivo ya no estaba
            // enchufado o el bus se está cerrando, ya estamos en el estado deseado. Liberar recursos
            // igualmente es lo que impide dejar un mando virtual huérfano (requisito 20).
        }

        DisposeBackendResources();
        IsConnected = false;
    }

    public void Submit(in GamepadState state)
    {
        if (!IsConnected || _controller is null)
        {
            return;
        }

        var report = Xbox360ReportConverter.ToReport(state);

        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, report.LeftThumbX);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, report.LeftThumbY);
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, report.RightThumbX);
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, report.RightThumbY);

        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, report.LeftTrigger);
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, report.RightTrigger);

        SetButtons(report.Buttons);

        _controller.SubmitReport();
    }

    public void Reset()
    {
        if (IsConnected)
        {
            Submit(GamepadState.Neutral);
        }
    }

    /// <summary>
    /// El SDK de ViGEm no tiene una clase base común para sus excepciones (las 17 derivan directamente
    /// de Exception), así que no se puede filtrar por un tipo padre y hay que enumerarlas.
    /// </summary>
    private static bool IsExpectedDisconnectFailure(Exception ex) => ex
        is VigemTargetNotPluggedInException
        or VigemTargetUninitializedException
        or VigemIsDisposingException
        or VigemBusInvalidHandleException
        or VigemRemovalFailedException;

    private void SetButtons(GamepadButtons buttons)
    {
        SetButton(Xbox360Button.A, buttons, GamepadButtons.A);
        SetButton(Xbox360Button.B, buttons, GamepadButtons.B);
        SetButton(Xbox360Button.X, buttons, GamepadButtons.X);
        SetButton(Xbox360Button.Y, buttons, GamepadButtons.Y);
        SetButton(Xbox360Button.LeftShoulder, buttons, GamepadButtons.LeftShoulder);
        SetButton(Xbox360Button.RightShoulder, buttons, GamepadButtons.RightShoulder);
        SetButton(Xbox360Button.LeftThumb, buttons, GamepadButtons.LeftStick);
        SetButton(Xbox360Button.RightThumb, buttons, GamepadButtons.RightStick);
        SetButton(Xbox360Button.Up, buttons, GamepadButtons.DPadUp);
        SetButton(Xbox360Button.Down, buttons, GamepadButtons.DPadDown);
        SetButton(Xbox360Button.Left, buttons, GamepadButtons.DPadLeft);
        SetButton(Xbox360Button.Right, buttons, GamepadButtons.DPadRight);
        SetButton(Xbox360Button.Start, buttons, GamepadButtons.Start);
        SetButton(Xbox360Button.Back, buttons, GamepadButtons.Back);
    }

    private void SetButton(Xbox360Button target, GamepadButtons buttons, GamepadButtons flag) =>
        _controller!.SetButtonState(target, (buttons & flag) != 0);

    private void DisposeBackendResources()
    {
        _controller = null;
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        Disconnect();
        DisposeBackendResources();
    }
}
