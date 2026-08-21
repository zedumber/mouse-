namespace VirtualController.Core.Gamepad;

public interface IVirtualGamepad : IDisposable
{
    bool IsConnected { get; }

    /// <summary>
    /// Devuelve un resultado tipado en vez de lanzar una excepción genérica, para poder distinguir
    /// "backend no instalado" de "error al crear el dispositivo" (ADR-003, punto 6).
    /// </summary>
    GamepadConnectionResult Connect();

    void Disconnect();

    void Submit(in GamepadState state);

    void Reset();
}
