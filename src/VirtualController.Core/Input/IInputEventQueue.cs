namespace VirtualController.Core.Input;

/// <summary>
/// Cruce de hilos entre la captura (productor) y el consumidor de procesamiento (ver ADR-002/ADR-005).
/// La implementación inicial usa Channel&lt;T&gt; bounded; no se promete lock-free ni allocation-free
/// (ADR-002, punto 6) — si el profiling de Fase 8 lo justifica, se sustituye sin tocar consumidores.
/// </summary>
public interface IInputEventQueue
{
    bool TryWrite(InputEvent inputEvent);

    /// <summary>
    /// Lectura no bloqueante: el consumidor drena con un presupuesto acotado por iteración, así que
    /// necesita poder preguntar "¿hay algo ahora?" sin esperar (ADR-005, punto 4).
    /// </summary>
    bool TryRead(out InputEvent inputEvent);

    /// <summary>
    /// Bloquea hasta que llegue input o expire el tiempo indicado. Devuelve true si despertó por
    /// input. Es lo que permite que el consumidor duerma sin coste de CPU cuando no hay nada que
    /// hacer, y aun así reaccione de inmediato en cuanto el usuario toca algo (ADR-005, punto 3).
    /// </summary>
    bool WaitForInput(TimeSpan timeout);
}

/// <summary>
/// Información de diagnóstico opcional. Separada de IInputEventQueue para que una implementación
/// mínima no esté obligada a llevar contadores que quizá no necesite.
/// </summary>
public interface IInputEventQueueDiagnostics
{
    long DroppedCount { get; }
}
