using VirtualController.Core.Mapping;
using VirtualController.Core.Mouse;

namespace VirtualController.Core.Engine;

/// <summary>
/// Comandos originados en la UI. Se aplican en la frontera de una iteración, nunca a mitad de un
/// cálculo de mapping, para que no exista un frame que mezcle dos configuraciones (ADR-005, punto 6).
///
/// La parada de emergencia NO viaja por aquí: llega por el flujo normal de input para no depender de
/// que la UI responda (ADR-005, punto 1).
/// </summary>
public abstract record ControlCommand
{
    public sealed record Start : ControlCommand;

    public sealed record Stop : ControlCommand;

    public sealed record ChangeMapping(CompiledMapping Mapping, MouseSettings MouseSettings) : ControlCommand;

    /// <summary>
    /// Cambia solo los ajustes de mouse. Separado de ChangeMapping a propósito: cambiar de perfil
    /// tiene que olvidar las teclas pulsadas para no mezclar dos configuraciones, pero mover un
    /// deslizador de sensibilidad mientras juegas no debería soltarte las teclas que tienes apretadas.
    /// </summary>
    public sealed record ChangeMouseSettings(MouseSettings MouseSettings) : ControlCommand;

    private ControlCommand()
    {
    }
}
