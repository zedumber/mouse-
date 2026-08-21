namespace VirtualController.Core.Input;

/// <summary>
/// Puerto del dominio para una fuente de input físico en vivo (teclado/mouse). La implementación
/// concreta para Windows (Raw Input) vive en VirtualController.Windows; Core no conoce Win32 (ver ADR-001/ADR-002).
/// </summary>
public interface IInputSource : IDisposable
{
    void Start();

    void Stop();
}
