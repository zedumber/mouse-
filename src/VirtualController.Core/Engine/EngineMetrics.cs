namespace VirtualController.Core.Engine;

public readonly record struct MetricsSnapshot(
    long InputEventsProcessed,
    long GamepadSubmits,
    long DroppedInputEvents,
    double AverageProcessingMicroseconds,
    double MaximumProcessingMicroseconds);

/// <summary>
/// Métricas internas opcionales (requisito 7). Cuando están desactivadas, cada operación es una
/// comprobación de un bool y nada más: no debe costar nada tener el código en el hot path.
/// </summary>
public sealed class EngineMetrics
{
    private readonly bool _enabled;
    private readonly double _microsecondsPerTick;

    private long _inputEvents;
    private long _submits;
    private long _dropped;
    private long _iterations;
    private double _totalProcessingTicks;
    private double _maxProcessingTicks;

    public EngineMetrics(bool enabled, long ticksPerSecond)
    {
        _enabled = enabled;
        _microsecondsPerTick = 1_000_000d / ticksPerSecond;
    }

    public bool Enabled => _enabled;

    public void RecordInputEvents(int count)
    {
        if (_enabled)
        {
            _inputEvents += count;
        }
    }

    public void RecordSubmit()
    {
        if (_enabled)
        {
            _submits++;
        }
    }

    public void RecordDropped(long count)
    {
        if (_enabled)
        {
            _dropped += count;
        }
    }

    public void RecordIteration(long elapsedTicks)
    {
        if (!_enabled)
        {
            return;
        }

        _iterations++;
        _totalProcessingTicks += elapsedTicks;

        if (elapsedTicks > _maxProcessingTicks)
        {
            _maxProcessingTicks = elapsedTicks;
        }
    }

    public MetricsSnapshot Snapshot() => new(
        _inputEvents,
        _submits,
        _dropped,
        _iterations == 0 ? 0d : _totalProcessingTicks / _iterations * _microsecondsPerTick,
        _maxProcessingTicks * _microsecondsPerTick);

    public void Reset()
    {
        _inputEvents = 0;
        _submits = 0;
        _dropped = 0;
        _iterations = 0;
        _totalProcessingTicks = 0;
        _maxProcessingTicks = 0;
    }
}
