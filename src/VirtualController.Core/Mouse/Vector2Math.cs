namespace VirtualController.Core.Mouse;

/// <summary>
/// Operaciones sobre vectores 2D compartidas por el motor de mouse y el compositor de sticks.
/// Existe para que el cálculo de magnitud tenga una sola definición: estaba copiado en cinco sitios,
/// que es exactamente el tipo de duplicación que el brief prohíbe.
/// </summary>
public static class Vector2Math
{
    public static float Magnitude(float x, float y) => MathF.Sqrt((x * x) + (y * y));

    /// <summary>
    /// Escala el vector para que su magnitud pase a ser <paramref name="targetMagnitude"/>,
    /// preservando la dirección. Devuelve el vector nulo si la magnitud original es cero.
    /// </summary>
    public static (float X, float Y) WithMagnitude(float x, float y, float currentMagnitude, float targetMagnitude)
    {
        if (currentMagnitude == 0f)
        {
            return (0f, 0f);
        }

        var scale = targetMagnitude / currentMagnitude;
        return (x * scale, y * scale);
    }
}
