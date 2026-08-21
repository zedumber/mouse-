using System.Diagnostics;
using VirtualController.Core.Gamepad;
using VirtualController.Core.Input;
using VirtualController.Core.Mapping;
using VirtualController.Core.Mouse;

namespace VirtualController.Core.Engine;

public sealed record ProcessingOptions
{
    /// <summary>
    /// Máximo de eventos de input consumidos por iteración. Existe para garantizar que un flujo
    /// continuo (un mouse de alta frecuencia) no pueda impedir indefinidamente el trabajo dirigido por
    /// tiempo ni los comandos de control (ADR-005, punto 4). El valor exacto es objeto de Fase 8.
    /// </summary>
    public int InputDrainBudget { get; init; } = 256;

    /// <summary>
    /// Cadencia máxima de envío al mando virtual. Las cuatro cadencias del sistema (captura, drenado,
    /// Submit y snapshot de UI) son independientes (ADR-005, punto 5): sin este límite, el bucle
    /// enviaba un report por iteración, decenas de miles por segundo, muy por encima de lo que
    /// cualquier consumidor de XInput lee.
    /// </summary>
    public double SubmitHz { get; init; } = 250d;

    /// <summary>Cadencia de publicación del snapshot hacia la UI; la UI no necesita más de ~60 Hz.</summary>
    public double SnapshotHz { get; init; } = 60d;

    /// <summary>
    /// Combinación que dispara la parada de emergencia, reconocida desde el flujo normal de input y por
    /// tanto independiente de que la UI responda (ADR-005, puntos 1 y 7).
    /// </summary>
    public HotkeyCombination? EmergencyStopHotkey { get; init; }
}

/// <summary>
/// Único propietario del estado mutable del controller y único componente autorizado a invocar
/// IVirtualGamepad (ADR-005, principio central). No es thread-safe por diseño: se ejecuta desde un
/// solo consumidor lógico.
///
/// Esta clase contiene el cuerpo de una iteración, no el bucle infinito ni la política de espera: eso
/// permite testearla determinísticamente sin depender del tiempo real (ADR-005, punto 3 deja la
/// primitiva de espera sin fijar hasta tener mediciones).
/// </summary>
public sealed class ProcessingLoop
{
    private readonly IVirtualGamepad _gamepad;
    private readonly ProcessingOptions _options;
    private readonly ActiveInputState _activeInputs = new();

    private readonly IInputEventQueueDiagnostics? _queueDiagnostics;
    private readonly long _submitIntervalTicks;
    private readonly long _snapshotIntervalTicks;

    private CompiledMapping _mapping;
    private MouseToStickConverter _mouseConverter;
    private GamepadState _state = GamepadState.Neutral;
    private GamepadSnapshot _snapshot = GamepadSnapshot.Neutral;
    private long _nextSubmitDeadline;
    private long _nextSnapshotDeadline;
    private long _droppedEvents;

    public ProcessingLoop(
        IVirtualGamepad gamepad,
        CompiledMapping mapping,
        MouseToStickConverter mouseConverter,
        EngineMetrics metrics,
        ProcessingOptions? options = null,
        IInputEventQueueDiagnostics? queueDiagnostics = null,
        long? ticksPerSecond = null)
    {
        _gamepad = gamepad;
        _mapping = mapping;
        _mouseConverter = mouseConverter;
        Metrics = metrics;
        _options = options ?? new ProcessingOptions();

        // Se resuelve una vez, no por iteración: leer el contador de la cola en cada ciclo generaba
        // ping-pong de caché con el hilo de captura.
        _queueDiagnostics = queueDiagnostics;

        var ticks = ticksPerSecond ?? Stopwatch.Frequency;
        _submitIntervalTicks = (long)(ticks / _options.SubmitHz);
        _snapshotIntervalTicks = (long)(ticks / _options.SnapshotHz);
    }

    public EngineMetrics Metrics { get; }

    public bool IsEmulating { get; private set; }

    /// <summary>Último snapshot publicado hacia la UI. Inmutable, así que leerlo siempre es consistente.</summary>
    public GamepadSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>
    /// Indica si queda trabajo dirigido por tiempo: el estado no está en reposo, así que el decay
    /// todavía tiene que hacerlo converger y hay que seguir enviando al mando.
    ///
    /// Es lo que permite al bucle distinguir "hay algo pasando, conviene ir fino" de "todo quieto,
    /// puedo dormir sin que nadie lo note", en vez de girar siempre al máximo por si acaso.
    /// </summary>
    public bool HasPendingTimeWork =>
        IsEmulating && (_state != GamepadState.Neutral || _mouseConverter.CurrentValue != (0f, 0f));

    public void ApplyCommand(ControlCommand command)
    {
        switch (command)
        {
            case ControlCommand.Start:
                IsEmulating = true;
                break;

            case ControlCommand.Stop:
                IsEmulating = false;
                NeutralizeOutput();
                break;

            case ControlCommand.ChangeMapping change:
                ChangeMapping(change);
                break;

            case ControlCommand.ChangeMouseSettings change:
                ChangeMouseSettings(change.MouseSettings);
                break;
        }
    }

    /// <summary>
    /// Sustituye el conversor conservando las teclas pulsadas. El stick del mouse vuelve al centro
    /// porque el estado acumulado ya no es interpretable con los ajustes nuevos (una sensibilidad
    /// distinta cambia lo que significa esa posición), pero lo que el usuario tenga apretado sigue
    /// vigente: no debe soltarse por tocar un deslizador.
    /// </summary>
    private void ChangeMouseSettings(MouseSettings settings) =>
        _mouseConverter = new MouseToStickConverter(settings);

    /// <summary>
    /// Cambiar de perfil neutraliza primero la salida y resetea el estado con memoria: si no, un botón
    /// que quedó activo con el perfil anterior podría quedarse pegado, o el stick conservar una
    /// posición derivada de ajustes que ya no aplican (ADR-005, punto 6).
    /// </summary>
    private void ChangeMapping(ControlCommand.ChangeMapping change)
    {
        NeutralizeOutput();
        _activeInputs.Clear();

        _mapping = change.Mapping;
        _mouseConverter = new MouseToStickConverter(change.MouseSettings);
    }

    /// <summary>
    /// Parada de emergencia (requisito 20). La invoca el propio consumidor al detectar la combinación
    /// en el flujo de input, sin pasar por la UI, para que siga funcionando aunque la UI esté colgada.
    /// </summary>
    public void EmergencyStop()
    {
        IsEmulating = false;
        _activeInputs.Clear();
        _mouseConverter.Reset();
        _state = GamepadState.Neutral;
        PublishSnapshot(0, 0);

        _gamepad.Reset();
    }

    /// <summary>
    /// Cuerpo de una iteración: drena input con presupuesto, avanza el trabajo dirigido por tiempo,
    /// recompone el estado completo y publica. Devuelve cuántos eventos consumió.
    /// </summary>
    public int RunIteration(IInputEventQueue queue, long now)
    {
        var iterationStart = Stopwatch.GetTimestamp();

        var consumed = DrainInputWithinBudget(queue, now);

        // La combinación de emergencia se evalúa aquí, sobre el estado físico recién drenado: es lo que
        // la hace independiente de la UI (ADR-005, puntos 1 y 7).
        if (_options.EmergencyStopHotkey?.IsFullyPressed(_activeInputs) == true)
        {
            EmergencyStop();
            Metrics.RecordIteration(Stopwatch.GetTimestamp() - iterationStart);
            return consumed;
        }

        _state = _mapping.Resolve(_activeInputs.Active);

        // Update se llama siempre, no solo cuando llegó movimiento: es lo que permite que el decay
        // progrese cuando el mouse se detiene (ADR-006, punto 7). El modo ADS se obtiene del gatillo
        // virtual ya resuelto, de modo que no depende de que apuntar esté asignado a una tecla fija.
        _mouseConverter.Update(now, _state.LeftTrigger >= 0.5f);
        ApplyMouseVectorToState();

        // Submit y snapshot tienen sus propias cadencias: enviar un report por iteración saturaba el
        // driver y generaba basura de GC en el camino más sensible a jitter (ADR-005, punto 5).
        if (IsEmulating && HasReachedDeadline(now, ref _nextSubmitDeadline, _submitIntervalTicks))
        {
            _gamepad.Submit(_state);
            Metrics.RecordSubmit();
        }

        if (HasReachedDeadline(now, ref _nextSnapshotDeadline, _snapshotIntervalTicks))
        {
            PublishSnapshot(consumed, _droppedEvents);
        }

        Metrics.RecordIteration(Stopwatch.GetTimestamp() - iterationStart);
        return consumed;
    }

    /// <summary>
    /// Devuelve true cuando toca ejecutar el trabajo asociado al deadline, y lo reprograma. Si el
    /// bucle se retrasa mucho, el deadline se reancla a "ahora" en vez de acumular deuda y disparar
    /// una ráfaga de ejecuciones para compensar.
    /// </summary>
    private static bool HasReachedDeadline(long now, ref long nextDeadline, long intervalTicks)
    {
        if (now < nextDeadline)
        {
            return false;
        }

        var scheduled = nextDeadline + intervalTicks;
        nextDeadline = scheduled <= now ? now + intervalTicks : scheduled;
        return true;
    }

    private int DrainInputWithinBudget(IInputEventQueue queue, long now)
    {
        var consumed = 0;

        while (consumed < _options.InputDrainBudget && queue.TryRead(out var inputEvent))
        {
            Handle(inputEvent, now);
            consumed++;
        }

        Metrics.RecordInputEvents(consumed);

        // El contador de descartes lo mantiene la cola; la métrica que la UI muestra debe reflejar ese
        // valor real, no un contador paralelo que nadie alimenta.
        if (_queueDiagnostics is { } diagnostics)
        {
            var dropped = diagnostics.DroppedCount;
            Metrics.RecordDropped(dropped - _droppedEvents);
            _droppedEvents = dropped;
        }

        return consumed;
    }

    private void Handle(InputEvent inputEvent, long now)
    {
        switch (inputEvent)
        {
            case InputEvent.Digital digital:
                _activeInputs.SetActive(digital.Input, digital.IsActive);
                break;

            case InputEvent.MouseMove move:
                _mouseConverter.AddDelta(move.DeltaX, move.DeltaY, move.CaptureTimestamp);
                break;
        }
    }

    /// <summary>
    /// El mouse produce un vector para el stick que tenga asignado un StickVector. Se aplica después
    /// del mapping porque su valor no viene de inputs activos, sino del converter con estado.
    /// </summary>
    private void ApplyMouseVectorToState()
    {
        if (_mapping.MouseStick is not { } stick)
        {
            return;
        }

        var (x, y) = _mouseConverter.CurrentValue;

        _state = stick == Stick.Left
            ? _state with { LeftStickX = x, LeftStickY = y }
            : _state with { RightStickX = x, RightStickY = y };
    }

    private void NeutralizeOutput()
    {
        _state = GamepadState.Neutral;
        _mouseConverter.Reset();
        PublishSnapshot(0, 0);

        if (_gamepad.IsConnected)
        {
            _gamepad.Submit(GamepadState.Neutral);
        }
    }

    private void PublishSnapshot(int consumedEvents, long droppedEvents)
    {
        var snapshot = new GamepadSnapshot(_state, IsEmulating, consumedEvents, droppedEvents);
        Volatile.Write(ref _snapshot, snapshot);
    }
}
