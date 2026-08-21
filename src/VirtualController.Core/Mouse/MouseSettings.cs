namespace VirtualController.Core.Mouse;

/// <summary>
/// Ajustes del conversor mouse → stick. El smoothing viene desactivado por defecto (requisito 10) y
/// no se aplica ningún suavizado implícito: cualquier filtrado tiene que ser una decisión explícita
/// del usuario, porque añade latencia.
/// </summary>
public sealed record MouseSettings
{
    /// <summary>Píxeles de movimiento que equivalen a deflexión completa del stick antes de curvas.</summary>
    public float CountsForFullDeflection { get; init; } = 100f;

    public float SensitivityX { get; init; } = 1f;
    public float SensitivityY { get; init; } = 1f;

    public bool InvertX { get; init; }
    public bool InvertY { get; init; }

    public float DeadzoneInner { get; init; }
    public float DeadzoneOuter { get; init; } = 1f;

    /// <summary>Escala lineal aplicada después de la curva, antes del límite de salida.</summary>
    public float OutputScale { get; init; } = 1f;

    /// <summary>Valor mínimo emitido al existir movimiento, para superar la deadzone del juego.</summary>
    public float OutputAntiDeadzone { get; init; }

    /// <summary>Límite real de la magnitud de salida; no altera la pendiente de la zona baja.</summary>
    public float MaximumOutput { get; init; } = 1f;

    public IResponseCurve ResponseCurve { get; init; } = LinearCurve.Instance;

    /// <summary>0 = sin aceleración. Amplifica movimientos rápidos de forma proporcional a la velocidad.</summary>
    public float Acceleration { get; init; }

    public bool SmoothingEnabled { get; init; }

    /// <summary>0 = sin efecto, cercano a 1 = muy suavizado (y por tanto más latente).</summary>
    public float SmoothingStrength { get; init; }

    /// <summary>
    /// Reduce automáticamente el suavizado en movimientos rápidos. Conserva estabilidad en
    /// micromovimientos sin imponer la misma latencia durante un giro.
    /// </summary>
    public bool AdaptiveSmoothingEnabled { get; init; }

    /// <summary>Cuánto responde el filtro adaptativo a la velocidad; 0 equivale al filtro fijo.</summary>
    public float AdaptiveSmoothingResponsiveness { get; init; } = 2f;

    /// <summary>Espera antes de iniciar el retorno al centro, expresada en milisegundos.</summary>
    public float DecayDelayMilliseconds { get; init; }

    /// <summary>Activa un ajuste de precisión cuando el mapping mantiene pulsado LeftTrigger.</summary>
    public bool AdsEnabled { get; init; }

    public float AdsSensitivityMultiplier { get; init; } = 0.65f;

    public float AdsMaximumOutput { get; init; } = 0.8f;

    /// <summary>Exponente adicional en ADS; valores mayores que 1 refinan la zona central.</summary>
    public float AdsPrecisionExponent { get; init; } = 1.2f;

    public IDecayStrategy Decay { get; init; } = ImmediateDecay.Instance;

    public void Validate()
    {
        if (CountsForFullDeflection <= 0f || !float.IsFinite(CountsForFullDeflection))
        {
            throw new ArgumentException($"{nameof(CountsForFullDeflection)} debe ser finito y mayor que 0.");
        }

        if (!float.IsFinite(SensitivityX) || SensitivityX <= 0f || !float.IsFinite(SensitivityY) || SensitivityY <= 0f)
        {
            throw new ArgumentException("Las sensibilidades deben ser finitas y mayores que 0.");
        }

        if (DeadzoneInner < 0f || DeadzoneOuter > 1f || DeadzoneInner >= DeadzoneOuter)
        {
            throw new ArgumentException($"Deadzone inválida: se requiere 0 <= inner < outer <= 1 (inner={DeadzoneInner}, outer={DeadzoneOuter}).");
        }

        if (MaximumOutput <= 0f || MaximumOutput > 1f)
        {
            throw new ArgumentException($"{nameof(MaximumOutput)} debe estar en (0, 1] (actual: {MaximumOutput}).");
        }

        if (!float.IsFinite(OutputScale) || OutputScale <= 0f || OutputScale > 4f)
        {
            throw new ArgumentException($"{nameof(OutputScale)} debe estar en (0, 4] (actual: {OutputScale}).");
        }

        if (!float.IsFinite(OutputAntiDeadzone) || OutputAntiDeadzone < 0f || OutputAntiDeadzone >= MaximumOutput)
        {
            throw new ArgumentException($"{nameof(OutputAntiDeadzone)} debe estar en [0, MaximumOutput) (actual: {OutputAntiDeadzone}).");
        }

        if (Acceleration < 0f || !float.IsFinite(Acceleration))
        {
            throw new ArgumentException($"{nameof(Acceleration)} debe ser finita y >= 0.");
        }

        if (SmoothingStrength < 0f || SmoothingStrength >= 1f)
        {
            throw new ArgumentException($"{nameof(SmoothingStrength)} debe estar en [0, 1) (actual: {SmoothingStrength}).");
        }


        if (!float.IsFinite(AdaptiveSmoothingResponsiveness) || AdaptiveSmoothingResponsiveness < 0f || AdaptiveSmoothingResponsiveness > 20f)
        {
            throw new ArgumentException($"{nameof(AdaptiveSmoothingResponsiveness)} debe estar en [0, 20] (actual: {AdaptiveSmoothingResponsiveness}).");
        }

        if (!float.IsFinite(DecayDelayMilliseconds) || DecayDelayMilliseconds < 0f || DecayDelayMilliseconds > 100f)
        {
            throw new ArgumentException($"{nameof(DecayDelayMilliseconds)} debe estar en [0, 100] (actual: {DecayDelayMilliseconds}).");
        }

        if (!float.IsFinite(AdsSensitivityMultiplier) || AdsSensitivityMultiplier <= 0f || AdsSensitivityMultiplier > 2f)
        {
            throw new ArgumentException($"{nameof(AdsSensitivityMultiplier)} debe estar en (0, 2] (actual: {AdsSensitivityMultiplier}).");
        }

        if (!float.IsFinite(AdsMaximumOutput) || AdsMaximumOutput <= 0f || AdsMaximumOutput > 1f)
        {
            throw new ArgumentException($"{nameof(AdsMaximumOutput)} debe estar en (0, 1] (actual: {AdsMaximumOutput}).");
        }

        if (AdsEnabled && OutputAntiDeadzone >= Math.Min(MaximumOutput, AdsMaximumOutput))
        {
            throw new ArgumentException(
                $"{nameof(OutputAntiDeadzone)} debe ser menor que el límite de salida ADS cuando ADS está activo.");
        }

        if (!float.IsFinite(AdsPrecisionExponent) || AdsPrecisionExponent <= 0f || AdsPrecisionExponent > 8f)
        {
            throw new ArgumentException($"{nameof(AdsPrecisionExponent)} debe estar en (0, 8] (actual: {AdsPrecisionExponent}).");
        }
    }
}
