namespace VirtualController.Core.Mouse;

/// <summary>
/// Devuelve el stick hacia el centro cuando el mouse deja de moverse. El brief pide explícitamente NO
/// asumir cuál estrategia funciona mejor (requisito 13), así que las tres son intercambiables y la
/// elección es configuración, no código.
///
/// Todas son funciones del TIEMPO transcurrido, nunca del número de llamadas: así el comportamiento
/// no cambia con la frecuencia del bucle de procesamiento, que ADR-005 deja deliberadamente abierta.
/// </summary>
public interface IDecayStrategy
{
    (float X, float Y) Decay(float x, float y, float deltaSeconds);
}

/// <summary>
/// Sin inercia perceptible: el stick vuelve al centro casi al instante cuando el mouse se detiene.
///
/// "Inmediato" es deliberadamente una constante de tiempo muy corta y no un salto a cero, porque un
/// salto a cero no es una tasa: dependía de cada cuánto se llamara Update, de modo que con un bucle
/// rápido vaciaba el acumulador entre eventos de mouse y el stick nunca llegaba a moverse.
/// </summary>
public sealed class ImmediateDecay : IDecayStrategy
{
    /// <summary>Cae por debajo del 1% de la deflexión en ~35 ms: imperceptible como inercia.</summary>
    public const float Rate = 130f;

    public static readonly ImmediateDecay Instance = new();

    private readonly ExponentialDecay _decay = new(Rate);

    public (float X, float Y) Decay(float x, float y, float deltaSeconds) =>
        _decay.Decay(x, y, deltaSeconds);
}

/// <summary>
/// Resta una cantidad constante por segundo. El decaimiento se aplica sobre la magnitud del vector
/// (no por eje) para que la dirección no rote mientras el stick vuelve al centro.
/// </summary>
public sealed class LinearDecay : IDecayStrategy
{
    /// <summary>Expuesto para poder persistirlo: como campo privado se perdía al guardar el perfil.</summary>
    public float UnitsPerSecond { get; }

    public LinearDecay(float unitsPerSecond)
    {
        if (unitsPerSecond <= 0f || !float.IsFinite(unitsPerSecond))
        {
            throw new ArgumentOutOfRangeException(nameof(unitsPerSecond), unitsPerSecond, "La velocidad debe ser finita y mayor que 0.");
        }

        UnitsPerSecond = unitsPerSecond;
    }

    public (float X, float Y) Decay(float x, float y, float deltaSeconds)
    {
        var magnitude = Vector2Math.Magnitude(x, y);
        if (magnitude == 0f)
        {
            return (0f, 0f);
        }

        var reduced = magnitude - (UnitsPerSecond * deltaSeconds);
        return reduced <= 0f
            ? (0f, 0f)
            : Vector2Math.WithMagnitude(x, y, magnitude, reduced);
    }
}

/// <summary>
/// Decaimiento proporcional al valor actual: rápido al principio y suave al final. Usa una constante
/// de tiempo independiente del framerate, así que el comportamiento no cambia con la frecuencia de
/// actualización (importante dado que ADR-005 deja esa cadencia sin fijar).
/// </summary>
public sealed class ExponentialDecay : IDecayStrategy
{
    /// <summary>Expuesto para poder persistirlo: como campo privado se perdía al guardar el perfil.</summary>
    public float Rate { get; }

    public ExponentialDecay(float rate)
    {
        if (rate <= 0f || !float.IsFinite(rate))
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "La tasa debe ser finita y mayor que 0.");
        }

        Rate = rate;
    }

    public (float X, float Y) Decay(float x, float y, float deltaSeconds)
    {
        var factor = MathF.Exp(-Rate * deltaSeconds);
        return (x * factor, y * factor);
    }
}
