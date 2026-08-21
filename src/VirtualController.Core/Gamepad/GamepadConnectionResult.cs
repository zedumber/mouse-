namespace VirtualController.Core.Gamepad;

public enum GamepadConnectionStatus
{
    Connected,

    /// <summary>El backend (driver/servicio) no está presente en el sistema.</summary>
    BackendNotInstalled,

    /// <summary>El backend está presente pero su versión no es compatible con este cliente.</summary>
    BackendVersionIncompatible,

    /// <summary>El backend está presente pero la conexión con él falló.</summary>
    ConnectionFailed,

    /// <summary>Conectado al backend, pero la creación del dispositivo virtual falló.</summary>
    DeviceCreationFailed,
}

/// <summary>
/// Resultado tipado de un intento de conexión (ADR-003, punto 6). La capacidad de distinguir el motivo
/// del fallo existe desde Fase 3 para logging/diagnóstico; la UX de first-run que guía al usuario a
/// instalar el backend se construye encima en Fase 9.
/// </summary>
public readonly record struct GamepadConnectionResult(GamepadConnectionStatus Status, string? Detail = null)
{
    public bool IsConnected => Status == GamepadConnectionStatus.Connected;

    public static GamepadConnectionResult Connected() => new(GamepadConnectionStatus.Connected);

    public static GamepadConnectionResult Failed(GamepadConnectionStatus status, string detail) =>
        status == GamepadConnectionStatus.Connected
            ? throw new ArgumentException("Un fallo no puede tener estado Connected.", nameof(status))
            : new GamepadConnectionResult(status, detail);
}
