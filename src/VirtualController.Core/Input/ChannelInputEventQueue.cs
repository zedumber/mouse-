using System.Threading.Channels;

namespace VirtualController.Core.Input;

public sealed class ChannelInputEventQueue : IInputEventQueue, IInputEventQueueDiagnostics, IDisposable
{
    public const int DefaultCapacity = 4096;

    private readonly Channel<InputEvent> _channel;
    private readonly int _capacity;

    // Señal para despertar al consumidor en cuanto llega input, en vez de que este tenga que sondear.
    // Sin esto, la única forma de reaccionar rápido era no dormir nunca, quemando un núcleo entero.
    private readonly AutoResetEvent _dataAvailable = new(initialState: false);

    private long _dropped;

    public ChannelInputEventQueue(int capacity = DefaultCapacity)
    {
        // Se guarda la capacidad real: comparar contra la constante hacía que el contador de descartes
        // no funcionara con ninguna capacidad distinta de la de por defecto.
        _capacity = capacity;

        // Bounded, no unbounded: si el consumidor se retrasa, es mejor perder los eventos más viejos
        // que acumular cientos de miles y procesar input obsoleto (ADR-002, punto 6).
        _channel = Channel.CreateBounded<InputEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
    }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public bool TryWrite(InputEvent inputEvent)
    {
        // Con DropOldest, TryWrite tiene éxito descartando el evento más antiguo; el contador refleja
        // que hubo pérdida aunque la escritura "funcione", que es lo que importa para diagnóstico.
        if (_channel.Reader.Count >= _capacity)
        {
            Interlocked.Increment(ref _dropped);
        }

        var written = _channel.Writer.TryWrite(inputEvent);

        if (written)
        {
            _dataAvailable.Set();
        }

        return written;
    }

    public bool TryRead(out InputEvent inputEvent) => _channel.Reader.TryRead(out inputEvent!);

    public bool WaitForInput(TimeSpan timeout)
    {
        // Se comprueba antes de bloquear: entre el último TryRead y esta llamada puede haber entrado
        // algo, y dormir entonces añadiría una latencia que no hace falta.
        if (_channel.Reader.Count > 0)
        {
            return true;
        }

        return _dataAvailable.WaitOne(timeout);
    }

    public void Dispose() => _dataAvailable.Dispose();
}
