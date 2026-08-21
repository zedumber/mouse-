using System.Collections.Concurrent;
using System.Diagnostics;
using VirtualController.Core.Gamepad;
using VirtualController.Core.Input;
using VirtualController.Core.Mapping;
using VirtualController.Core.Mouse;
using VirtualController.Core.Profiles;

namespace VirtualController.Core.Engine;

/// <summary>
/// Aloja el consumidor de procesamiento y hace de fachada para la UI. La UI solo habla con esta clase:
/// nunca toca IVirtualGamepad ni el ProcessingLoop directamente (ADR-005).
/// </summary>
public sealed class EmulationService : IDisposable
{
    private readonly IVirtualGamepad _gamepad;
    private readonly IInputSource _inputSource;
    private readonly IInputEventQueue _queue;
    private readonly ProcessingLoop _loop;
    private readonly ConcurrentQueue<ControlCommand> _commands = new();
    private readonly Action<Exception>? _onFailure;

    private CancellationTokenSource? _cancellation;
    private Task? _processingTask;
    private SpinWait _spinner;

    public EmulationService(
        IVirtualGamepad gamepad,
        IInputSource inputSource,
        IInputEventQueue queue,
        Profile profile,
        ProcessingOptions? options = null,
        Action<Exception>? onFailure = null,
        bool metricsEnabled = false,
        ApplicationSettings? settings = null)
    {
        _gamepad = gamepad;
        _inputSource = inputSource;
        _queue = queue;
        _onFailure = onFailure;

        // El atajo de emergencia se traduce aquí a algo que el bucle pueda detectar por sí mismo.
        // Antes se validaba en ApplicationSettings y no se pasaba a nadie: existía en la configuración
        // pero no había ningún código capaz de reconocerlo (requisito 19/20).
        if (settings is not null
            && HotkeyCombination.TryParse(settings.EmergencyStop, out var emergencyHotkey))
        {
            options = (options ?? new ProcessingOptions()) with { EmergencyStopHotkey = emergencyHotkey };
        }

        _loop = new ProcessingLoop(
            gamepad,
            CompiledMapping.Compile(profile.Bindings, profile.ToMappingOptions()),
            new MouseToStickConverter(profile.Mouse),
            new EngineMetrics(metricsEnabled, Stopwatch.Frequency),
            options,
            queue as IInputEventQueueDiagnostics);
    }

    public GamepadSnapshot Snapshot => _loop.Snapshot;

    public MetricsSnapshot Metrics => _loop.Metrics.Snapshot();

    public bool IsRunning => _processingTask is { IsCompleted: false };

    /// <summary>Margen para que el bucle observe la cancelación y termine su iteración en curso.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    public GamepadConnectionResult Start()
    {
        if (IsRunning)
        {
            return GamepadConnectionResult.Connected();
        }

        var connection = _gamepad.Connect();
        if (!connection.IsConnected)
        {
            return connection;
        }

        try
        {
            _inputSource.Start();
        }
        catch
        {
            // Sin este rollback, un fallo al registrar Raw Input dejaba el mando virtual conectado y
            // sin bucle: un dispositivo huérfano que el usuario no podía quitar desde la aplicación.
            _gamepad.Disconnect();
            throw;
        }

        _cancellation = new CancellationTokenSource();
        _commands.Enqueue(new ControlCommand.Start());
        _processingTask = Task.Factory.StartNew(
            () => RunLoop(_cancellation.Token),
            _cancellation.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        return connection;
    }

    public void Stop()
    {
        _cancellation?.Cancel();

        var stoppedCleanly = true;

        try
        {
            // El resultado de Wait NO se puede ignorar: si expira y aun así se liberan los recursos,
            // el bucle sigue vivo usando un backend ya desconectado (NullReferenceException o uso de
            // un handle liberado), y un Start posterior arrancaría un SEGUNDO bucle sobre el mismo
            // estado mutable — justo lo que ADR-005 prohíbe.
            stoppedCleanly = _processingTask?.Wait(ShutdownTimeout) ?? true;
        }
        catch (AggregateException)
        {
            // Ya se reportó vía _onFailure; aquí solo interesa no propagar durante el apagado.
        }

        if (!stoppedCleanly)
        {
            // Se deja todo en pie a propósito: soltar el backend con el bucle todavía usándolo es peor
            // que quedarse en un estado degradado del que el usuario puede salir cerrando la app.
            _onFailure?.Invoke(new TimeoutException(
                "El bucle de procesamiento no respondió a la parada; no se liberaron los recursos para " +
                "evitar usarlos desde un hilo que sigue vivo."));
            return;
        }

        _processingTask = null;
        _cancellation?.Dispose();
        _cancellation = null;

        // La cola de comandos se vacía: si no, un comando de esta sesión se aplicaría al arrancar la
        // siguiente.
        while (_commands.TryDequeue(out _))
        {
        }

        _inputSource.Stop();

        // El estado del bucle se neutraliza explícitamente. Cancelar el token solo detenía el hilo:
        // las teclas retenidas seguían en ActiveInputState y reaparecían intactas en el siguiente
        // Start, con el mando arrancando ya "pulsado".
        _loop.EmergencyStop();

        _gamepad.Disconnect();
    }

    public void ChangeProfile(Profile profile) =>
        _commands.Enqueue(new ControlCommand.ChangeMapping(
            CompiledMapping.Compile(profile.Bindings, profile.ToMappingOptions()),
            profile.Mouse));

    /// <summary>
    /// Aplica ajustes de mouse en caliente, para poder afinar la sensibilidad viendo el efecto al
    /// instante en vez de reiniciar tras editar el JSON.
    /// </summary>
    public void ChangeMouseSettings(MouseSettings settings) =>
        _commands.Enqueue(new ControlCommand.ChangeMouseSettings(settings));

    /// <summary>
    /// Parada de emergencia solicitada desde la UI. Es el camino secundario: el principal es la
    /// combinación de teclas, que el propio bucle reconoce sin depender de que la UI responda.
    /// </summary>
    public void RequestEmergencyStop()
    {
        // Se ejecuta directamente en vez de encolarse: encolarlo dependía de que el bucle siguiera
        // vivo para procesarlo, y quien llama a esto suele detener el bucle acto seguido, con lo que
        // el comando se perdía en una carrera.
        _loop.EmergencyStop();
        Stop();
    }

    private void RunLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                while (_commands.TryDequeue(out var command))
                {
                    _loop.ApplyCommand(command);
                }

                _loop.RunIteration(_queue, Stopwatch.GetTimestamp());
                Wait();
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal.
        }
        catch (Exception ex)
        {
            // Una excepción aquí no puede dejar el mando virtual con teclas pegadas (requisito 20).
            SafeEmergencyStop();
            _onFailure?.Invoke(ex);
        }
    }

    /// <summary>
    /// Tiempo que el consumidor duerme cuando no hay nada que hacer. Windows redondea las esperas a
    /// unos 15 ms, así que pedir menos no serviría de nada; y en reposo esa granularidad no se nota,
    /// porque cualquier input despierta el hilo de inmediato a través de la señal de la cola.
    /// </summary>
    private static readonly TimeSpan IdleWait = TimeSpan.FromMilliseconds(15);

    /// <summary>
    /// Espera híbrida, elegida a partir de mediciones en esta máquina (Fase 8, ADR-005 punto 3):
    ///
    ///   Thread.SpinWait(64)      resolución 0,004 ms   pero 100% de un núcleo, siempre
    ///   Sleep/WaitOne(1 ms)      0% de CPU             pero ~15 ms de resolución real
    ///
    /// Ninguna sirve por sí sola. La salida es que las dos situaciones tienen exigencias distintas:
    ///
    /// - Con algo en marcha (stick fuera del centro, decay convergiendo) hace falta precisión, así
    ///   que se hace un spin adaptativo: SpinWait cede el núcleo por su cuenta si la espera se alarga,
    ///   en vez de quemarlo en seco como el SpinWait(64) fijo.
    /// - En reposo no hay ningún deadline que cumplir, así que se bloquea sobre la señal de la cola:
    ///   cero CPU, y despertar inmediato en cuanto el usuario toca una tecla o mueve el mouse.
    /// </summary>
    private void Wait()
    {
        if (_loop.HasPendingTimeWork)
        {
            _spinner.SpinOnce();
            return;
        }

        _spinner.Reset();
        _queue.WaitForInput(IdleWait);
    }

    private void SafeEmergencyStop()
    {
        try
        {
            _loop.EmergencyStop();
        }
        catch
        {
            // Si incluso la parada de emergencia falla, no hay nada más que podamos hacer aquí y
            // relanzar ocultaría la excepción original que provocó el fallo.
        }
    }

    public void Dispose()
    {
        Stop();
        _inputSource.Dispose();
        _gamepad.Dispose();
    }
}
