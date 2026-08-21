using System.Diagnostics;

namespace VirtualController.Core.Mouse;

/// <summary>
/// Convierte el movimiento relativo del mouse (dx/dy) en una posición absoluta de stick (-1..+1).
///
/// Mantiene la separación input-dirigido / tiempo-dirigido de ADR-002/ADR-006: AddDelta solo se llama
/// cuando llega movimiento, Update se llama en cada recomputación aunque no haya llegado nada — y es
/// eso lo que permite que el decay progrese cuando el mouse se detiene.
/// </summary>
public sealed class MouseToStickConverter
{
    /// <summary>Frecuencia de referencia con la que se interpreta SmoothingStrength.</summary>
    private const float ReferenceFramesPerSecond = 60f;

    private readonly MouseSettings _settings;
    private readonly long _ticksPerSecond;

    private float _accumulatedX;
    private float _accumulatedY;
    private float _smoothedX;
    private float _smoothedY;
    private long _lastUpdateTimestamp;
    private long _lastDeltaTimestamp;
    private bool _hasTimestamp;
    private bool _hasDeltaTimestamp;

    public MouseToStickConverter(MouseSettings settings, long? ticksPerSecond = null)
    {
        settings.Validate();
        _settings = settings;
        _ticksPerSecond = ticksPerSecond ?? Stopwatch.Frequency;
    }

    public (float X, float Y) CurrentValue { get; private set; }

    public void AddDelta(int deltaX, int deltaY, long timestamp)
    {
        var scaleX = _settings.SensitivityX / _settings.CountsForFullDeflection;
        var scaleY = _settings.SensitivityY / _settings.CountsForFullDeflection;

        var x = deltaX * scaleX;
        var y = deltaY * scaleY;

        // La aceleración se calcula sobre la VELOCIDAD (counts por segundo), no sobre el tamaño del
        // paquete de Raw Input. Medirla por evento hacía que el mismo gesto físico diera resultados
        // distintos según el polling rate del mouse: 200 counts en un evento amplificaban mucho más
        // que los mismos 200 counts repartidos en veinte eventos de un mouse de 1000 Hz.
        if (_settings.Acceleration > 0f)
        {
            var boost = 1f + (_settings.Acceleration * ComputeSpeedFactor(deltaX, deltaY, timestamp));
            x *= boost;
            y *= boost;
        }

        _lastDeltaTimestamp = timestamp;
        _hasDeltaTimestamp = true;

        if (_settings.InvertX)
        {
            x = -x;
        }

        // El eje Y del mouse crece hacia abajo mientras el stick crece hacia arriba, así que se
        // invierte siempre; InvertY alterna respecto a esa base, no respecto al valor crudo.
        y = _settings.InvertY ? y : -y;

        AccumulateClampedToUnitCircle(x, y);
    }

    /// <summary>
    /// Acota la posición acumulada al CÍRCULO unidad, no al cuadrado. Acotar cada eje por separado
    /// dejaba alcanzar magnitud √2 en diagonal, y entonces había que "descargar" hasta un 41% del
    /// recorrido antes de que la deflexión empezara a bajar — un retardo que dependía de la dirección.
    /// </summary>
    private void AccumulateClampedToUnitCircle(float x, float y)
    {
        var nextX = _accumulatedX + x;
        var nextY = _accumulatedY + y;

        var magnitude = MathF.Sqrt((nextX * nextX) + (nextY * nextY));

        if (magnitude > 1f)
        {
            nextX /= magnitude;
            nextY /= magnitude;
        }

        _accumulatedX = nextX;
        _accumulatedY = nextY;
    }

    public void Update(long now) => Update(now, adsActive: false);

    /// <summary>
    /// Actualiza la salida. ADS se deriva del estado virtual de LeftTrigger, no de una tecla fija,
    /// por lo que sigue funcionando si el usuario remapea el botón de apuntar.
    /// </summary>
    public void Update(long now, bool adsActive)
    {
        var deltaSeconds = ComputeDeltaSeconds(now);
        var decaySeconds = ComputeDecaySeconds(now, deltaSeconds);

        // El decay es puramente temporal: se aplica proporcionalmente al tiempo transcurrido, SIEMPRE.
        // Condicionarlo a "no llegó delta en esta iteración" lo ataba a la frecuencia del bucle: con un
        // bucle de ~100k iteraciones/s y un mouse de 1000 Hz, el 99% de las vueltas no traían delta y
        // el acumulador se vaciaba constantemente, dejando el stick a cero casi siempre.
        if (decaySeconds > 0f)
        {
            var (decayedX, decayedY) = _settings.Decay.Decay(_accumulatedX, _accumulatedY, decaySeconds);
            _accumulatedX = decayedX;
            _accumulatedY = decayedY;
        }

        var (x, y) = ApplySmoothing(_accumulatedX, _accumulatedY, deltaSeconds);
        CurrentValue = ShapeOutput(x, y, adsActive && _settings.AdsEnabled);
    }

    public void Reset()
    {
        _accumulatedX = 0f;
        _accumulatedY = 0f;
        _smoothedX = 0f;
        _smoothedY = 0f;
        _hasTimestamp = false;
        _hasDeltaTimestamp = false;
        CurrentValue = (0f, 0f);
    }

    /// <summary>
    /// Velocidad normalizada del gesto (fracción de deflexión completa por segundo), usada por la
    /// aceleración. Depende del tiempo real entre eventos, no de cómo el driver agrupe los counts.
    /// </summary>
    private float ComputeSpeedFactor(int deltaX, int deltaY, long timestamp)
    {
        if (!_hasDeltaTimestamp)
        {
            return 0f;
        }

        var elapsedTicks = timestamp - _lastDeltaTimestamp;
        if (elapsedTicks <= 0)
        {
            return 0f;
        }

        var seconds = (float)elapsedTicks / _ticksPerSecond;
        var counts = MathF.Sqrt((float)((deltaX * (long)deltaX) + (deltaY * (long)deltaY)));

        return counts / seconds / _settings.CountsForFullDeflection;
    }

    private float ComputeDeltaSeconds(long now)
    {
        // Se usa un flag en vez del centinela 0, porque 0 es un instante perfectamente válido para un
        // reloj monotónico y hacía que el primer intervalo se descartara en silencio.
        if (!_hasTimestamp)
        {
            _lastUpdateTimestamp = now;
            _hasTimestamp = true;
            return 0f;
        }

        var elapsedTicks = now - _lastUpdateTimestamp;
        if (elapsedTicks <= 0)
        {
            return 0f;
        }

        _lastUpdateTimestamp = now;
        return (float)elapsedTicks / _ticksPerSecond;
    }

    private float ComputeDecaySeconds(long now, float updateDeltaSeconds)
    {
        if (updateDeltaSeconds <= 0f || !_hasDeltaTimestamp || _settings.DecayDelayMilliseconds <= 0f)
        {
            return updateDeltaSeconds;
        }

        var elapsedSinceDelta = Math.Max(0f, (float)(now - _lastDeltaTimestamp) / _ticksPerSecond);
        var delaySeconds = _settings.DecayDelayMilliseconds / 1000f;

        // Si el intervalo de Update cruza el final de la retención, solo se decae la parte posterior.
        return Math.Clamp(elapsedSinceDelta - delaySeconds, 0f, updateDeltaSeconds);
    }

    private (float X, float Y) ApplySmoothing(float x, float y, float deltaSeconds)
    {
        if (!_settings.SmoothingEnabled || _settings.SmoothingStrength <= 0f)
        {
            _smoothedX = x;
            _smoothedY = y;
            return (x, y);
        }

        // Filtro exponencial con constante de tiempo, no un "lerp por frame": así el suavizado no
        // depende de la frecuencia de actualización, que ADR-005 deja deliberadamente sin fijar.
        //
        // Si no ha transcurrido tiempo, el filtro NO avanza (alpha = 0). Usar alpha = 1 aquí
        // desactivaría el suavizado en silencio siempre que dos Update compartieran timestamp, algo
        // perfectamente posible a alta frecuencia de actualización.
        if (deltaSeconds <= 0f)
        {
            return (_smoothedX, _smoothedY);
        }

        // El exponente se expresa en "frames de referencia" para que SmoothingStrength tenga el mismo
        // significado intuitivo que en una interfaz a 60 Hz, sin que el resultado dependa realmente de
        // la frecuencia a la que corra el bucle.
        var responsiveness = 1f;
        if (_settings.AdaptiveSmoothingEnabled && _settings.AdaptiveSmoothingResponsiveness > 0f)
        {
            var distance = Vector2Math.Magnitude(x - _smoothedX, y - _smoothedY);
            var normalizedSpeed = distance / deltaSeconds;
            responsiveness += _settings.AdaptiveSmoothingResponsiveness * normalizedSpeed;
        }

        var alpha = 1f - MathF.Pow(
            _settings.SmoothingStrength,
            deltaSeconds * ReferenceFramesPerSecond * responsiveness);

        _smoothedX += (x - _smoothedX) * alpha;
        _smoothedY += (y - _smoothedY) * alpha;

        return (_smoothedX, _smoothedY);
    }

    private (float X, float Y) ShapeOutput(float x, float y, bool adsActive)
    {
        if (adsActive)
        {
            x *= _settings.AdsSensitivityMultiplier;
            y *= _settings.AdsSensitivityMultiplier;
        }

        var (deadzonedX, deadzonedY) = Deadzone.ApplyRadial(x, y, _settings.DeadzoneInner, _settings.DeadzoneOuter);

        var magnitude = Vector2Math.Magnitude(deadzonedX, deadzonedY);
        if (magnitude == 0f)
        {
            return (0f, 0f);
        }

        // La curva se aplica a la magnitud, no a cada eje: hacerlo por eje deformaría la dirección
        // del movimiento en diagonales. La magnitud post-deadzone ya está en [0,1], así que no hace
        // falta acotarla otra vez antes de la curva.
        var curved = Math.Clamp(_settings.ResponseCurve.Apply(magnitude), 0f, 1f);
        if (adsActive)
        {
            curved = MathF.Pow(curved, _settings.AdsPrecisionExponent);
        }

        var maximumOutput = adsActive
            ? Math.Min(_settings.MaximumOutput, _settings.AdsMaximumOutput)
            : _settings.MaximumOutput;

        var limited = Math.Min(curved * _settings.OutputScale, maximumOutput);

        // La anti-deadzone compensa la zona muerta que aplica el juego al stick virtual. Se aplica al
        // final para no deformar la dirección y se reescala hasta el límite en vez de sumarse a él.
        if (_settings.OutputAntiDeadzone > 0f && limited > 0f)
        {
            var normalized = limited / maximumOutput;
            limited = _settings.OutputAntiDeadzone
                + ((maximumOutput - _settings.OutputAntiDeadzone) * normalized);
        }

        return Vector2Math.WithMagnitude(deadzonedX, deadzonedY, magnitude, limited);
    }
}
