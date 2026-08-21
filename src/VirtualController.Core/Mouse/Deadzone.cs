namespace VirtualController.Core.Mouse;

/// <summary>
/// Única implementación de deadzone de todo el sistema (requisito 12: nunca duplicar esta lógica).
/// La variante vectorial delega en la escalar para que no existan dos fórmulas que puedan divergir.
/// </summary>
public static class Deadzone
{
    /// <summary>
    /// Aplica deadzone interior y exterior reescalando el rango restante a 0..1, de forma que no
    /// exista un salto brusco justo al salir de la zona muerta.
    /// </summary>
    public static float ApplyToMagnitude(float magnitude, float inner, float outer)
    {
        if (inner < 0f || outer > 1f || inner >= outer)
        {
            throw new ArgumentException($"Deadzone inválida: inner={inner}, outer={outer}. Se requiere 0 <= inner < outer <= 1.");
        }

        var clamped = Math.Clamp(magnitude, 0f, 1f);

        if (clamped <= inner)
        {
            return 0f;
        }

        if (clamped >= outer)
        {
            return 1f;
        }

        return (clamped - inner) / (outer - inner);
    }

    /// <summary>
    /// Deadzone radial: preserva la dirección del vector y solo reescala su magnitud. Aplicarla por
    /// eje de forma independiente deformaría las diagonales (produciría una zona muerta cuadrada).
    /// </summary>
    public static (float X, float Y) ApplyRadial(float x, float y, float inner, float outer)
    {
        var magnitude = Vector2Math.Magnitude(x, y);

        if (magnitude == 0f)
        {
            return (0f, 0f);
        }

        return Vector2Math.WithMagnitude(x, y, magnitude, ApplyToMagnitude(magnitude, inner, outer));
    }
}
