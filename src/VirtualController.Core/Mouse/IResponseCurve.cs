namespace VirtualController.Core.Mouse;

/// <summary>
/// Curva de respuesta aplicada a una magnitud normalizada (0..1). Se mantiene como abstracción porque
/// es una de las decisiones que el brief pide poder sustituir sin tocar el resto (requisito 11).
/// </summary>
public interface IResponseCurve
{
    float Apply(float normalizedInput);
}

public sealed class LinearCurve : IResponseCurve
{
    public static readonly LinearCurve Instance = new();

    public float Apply(float normalizedInput) => normalizedInput;
}

/// <summary>output = |input|^exponent — exponente &gt; 1 da más precisión en movimientos pequeños.</summary>
public sealed class PowerCurve : IResponseCurve
{
    // Público para que la persistencia pueda guardarlo: cuando era un campo privado, el perfil se
    // serializaba solo con el tipo y al recargar volvía al exponente por defecto en silencio.
    public float Exponent { get; }

    public PowerCurve(float exponent)
    {
        if (exponent <= 0f || !float.IsFinite(exponent))
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), exponent, "El exponente debe ser finito y mayor que 0.");
        }

        Exponent = exponent;
    }

    public float Apply(float normalizedInput) => MathF.Pow(normalizedInput, Exponent);
}

/// <summary>
/// Curva exponencial normalizada para que Apply(0) == 0 y Apply(1) == 1 en cualquier "strength";
/// sin esa normalización, cambiar el parámetro alteraría también la salida máxima.
/// </summary>
public sealed class ExponentialCurve : IResponseCurve
{
    /// <summary>
    /// Tope del rango admitido. Por encima de ~88 el `exp` desborda a infinito y `Apply(1)` devolvía
    /// NaN, que se propagaba hasta dejar el stick muerto justo en la deflexión máxima. Muy por debajo
    /// de ese límite la curva ya es inservible en la práctica (con 20, el 70% del recorrido es casi
    /// cero), así que el tope se fija donde la curva sigue teniendo sentido.
    /// </summary>
    public const float MaxStrength = 20f;

    public float Strength { get; }

    public ExponentialCurve(float strength)
    {
        if (strength <= 0f || !float.IsFinite(strength) || strength > MaxStrength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(strength),
                strength,
                $"La intensidad debe ser finita y estar en (0, {MaxStrength}].");
        }

        Strength = strength;
    }

    public float Apply(float normalizedInput) =>
        (MathF.Exp(Strength * normalizedInput) - 1f) / (MathF.Exp(Strength) - 1f);
}

/// <summary>
/// Curva continua de dos zonas. La primera favorece la precisión cerca del centro y la segunda
/// permite alcanzar el giro rápido sin sacrificar los micromovimientos. Ambas mitades se normalizan
/// alrededor de <see cref="Transition"/>, por lo que no existe un salto al cruzar de una a otra.
/// </summary>
public sealed class DualZoneCurve : IResponseCurve
{
    public float Transition { get; }

    public float PrecisionExponent { get; }

    public float TurnExponent { get; }

    public DualZoneCurve(float transition, float precisionExponent, float turnExponent)
    {
        if (!float.IsFinite(transition) || transition <= 0f || transition >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(transition), transition, "La transición debe estar en (0, 1).");
        }

        if (!float.IsFinite(precisionExponent) || precisionExponent <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(precisionExponent), precisionExponent, "El exponente de precisión debe ser mayor que 0.");
        }

        if (!float.IsFinite(turnExponent) || turnExponent <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(turnExponent), turnExponent, "El exponente de giro debe ser mayor que 0.");
        }

        Transition = transition;
        PrecisionExponent = precisionExponent;
        TurnExponent = turnExponent;
    }

    public float Apply(float normalizedInput)
    {
        var input = Math.Clamp(normalizedInput, 0f, 1f);

        if (input <= Transition)
        {
            return Transition * MathF.Pow(input / Transition, PrecisionExponent);
        }

        var upperInput = (input - Transition) / (1f - Transition);
        return Transition + ((1f - Transition) * MathF.Pow(upperInput, TurnExponent));
    }
}

/// <summary>
/// Curva definida por puntos de control, con interpolación lineal entre ellos. Permite al usuario
/// dibujar una respuesta que ninguna fórmula cerrada describe.
/// </summary>
public sealed class CustomCurve : IResponseCurve
{
    private readonly (float Input, float Output)[] _points;

    public CustomCurve(IReadOnlyCollection<(float Input, float Output)> points)
    {
        if (points.Count < 2)
        {
            throw new ArgumentException("Una curva personalizada necesita al menos 2 puntos.", nameof(points));
        }

        // Sin esta validación, un punto NaN o fuera de rango llegaba intacto hasta el mando virtual,
        // y una curva cuyo primer punto no arranque en 0 reintroduce el salto brusco al salir de la
        // zona muerta que la deadzone existe precisamente para evitar.
        foreach (var (input, output) in points)
        {
            if (!float.IsFinite(input) || input < 0f || input > 1f)
            {
                throw new ArgumentException($"Entrada de curva fuera de [0,1] o no finita: {input}.", nameof(points));
            }

            if (!float.IsFinite(output) || output < 0f || output > 1f)
            {
                throw new ArgumentException($"Salida de curva fuera de [0,1] o no finita: {output}.", nameof(points));
            }
        }

        _points = points.OrderBy(p => p.Input).ToArray();
    }

    /// <summary>Puntos ordenados por entrada. Expuestos para poder persistir la curva sin perderla.</summary>
    public IReadOnlyList<(float Input, float Output)> Points => _points;

    public float Apply(float normalizedInput)
    {
        var input = Math.Clamp(normalizedInput, 0f, 1f);

        if (input <= _points[0].Input)
        {
            return _points[0].Output;
        }

        if (input >= _points[^1].Input)
        {
            return _points[^1].Output;
        }

        for (var i = 0; i < _points.Length - 1; i++)
        {
            var (startInput, startOutput) = _points[i];
            var (endInput, endOutput) = _points[i + 1];

            if (input > endInput)
            {
                continue;
            }

            var span = endInput - startInput;
            if (span <= 0f)
            {
                return endOutput;
            }

            var t = (input - startInput) / span;
            return startOutput + ((endOutput - startOutput) * t);
        }

        return _points[^1].Output;
    }
}
