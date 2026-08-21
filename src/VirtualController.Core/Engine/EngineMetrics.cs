using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;

namespace VirtualController.Core.Engine;

public readonly record struct MetricsSnapshot(
    long InputEventsProcessed,
    long GamepadSubmits,
    long DroppedInputEvents,
    double AverageProcessingMicroseconds,
    double MaximumProcessingMicroseconds,
    double AverageInputLatencyMicroseconds,
    double MaximumInputLatencyMicroseconds,
    double AimSaturationPercent,
    double AverageSubmitJitterMicroseconds,
    double MaximumSubmitJitterMicroseconds);

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
    private long _latencySamples;
    private double _totalInputLatencyTicks;
    private double _maxInputLatencyTicks;
    private long _activeAimSamples;
    private long _saturatedAimSamples;
    private long _submitIntervalSamples;
    private long _lastSubmitTimestamp;
    private double _totalSubmitJitterTicks;
    private double _maxSubmitJitterTicks;

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

    public void RecordSubmit(long timestamp, long targetIntervalTicks, in GamepadState state, Stick? aimStick)
    {
        if (!_enabled)
        {
            return;
        }

        _submits++;

        if (_lastSubmitTimestamp != 0)
        {
            var actualInterval = Math.Max(0, timestamp - _lastSubmitTimestamp);
            var jitter = Math.Abs(actualInterval - targetIntervalTicks);
            _submitIntervalSamples++;
            _totalSubmitJitterTicks += jitter;
            _maxSubmitJitterTicks = Math.Max(_maxSubmitJitterTicks, jitter);
        }

        _lastSubmitTimestamp = timestamp;

        var (x, y) = aimStick == Stick.Left
            ? (state.LeftStickX, state.LeftStickY)
            : (state.RightStickX, state.RightStickY);
        var magnitude = MathF.Sqrt((x * x) + (y * y));
        if (magnitude > 0.02f)
        {
            _activeAimSamples++;
            if (magnitude >= 0.98f)
            {
                _saturatedAimSamples++;
            }
        }
    }

    public void RecordInputLatency(long elapsedTicks)
    {
        if (!_enabled || elapsedTicks < 0)
        {
            return;
        }

        _latencySamples++;
        _totalInputLatencyTicks += elapsedTicks;
        _maxInputLatencyTicks = Math.Max(_maxInputLatencyTicks, elapsedTicks);
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
        _maxProcessingTicks * _microsecondsPerTick,
        _latencySamples == 0 ? 0d : _totalInputLatencyTicks / _latencySamples * _microsecondsPerTick,
        _maxInputLatencyTicks * _microsecondsPerTick,
        _activeAimSamples == 0 ? 0d : 100d * _saturatedAimSamples / _activeAimSamples,
        _submitIntervalSamples == 0 ? 0d : _totalSubmitJitterTicks / _submitIntervalSamples * _microsecondsPerTick,
        _maxSubmitJitterTicks * _microsecondsPerTick);

    public void Reset()
    {
        _inputEvents = 0;
        _submits = 0;
        _dropped = 0;
        _iterations = 0;
        _totalProcessingTicks = 0;
        _maxProcessingTicks = 0;
        _latencySamples = 0;
        _totalInputLatencyTicks = 0;
        _maxInputLatencyTicks = 0;
        _activeAimSamples = 0;
        _saturatedAimSamples = 0;
        _submitIntervalSamples = 0;
        _lastSubmitTimestamp = 0;
        _totalSubmitJitterTicks = 0;
        _maxSubmitJitterTicks = 0;
    }
}
