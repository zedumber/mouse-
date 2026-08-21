namespace VirtualController.Core.Mouse;

public enum ResponseCurveKind
{
    Linear,
    Power,
    Exponential,
    DualZone,
}

public enum DecayKind
{
    Immediate,
    Linear,
    Exponential,
}

/// <summary>
/// Borrador mutable de los ajustes de mouse, pensado para que una interfaz pueda editarlos valor a
/// valor. <see cref="MouseSettings"/> es inmutable y se valida al construirse, así que editarlo
/// directamente desde deslizadores obligaría a atrapar excepciones en cada cambio; aquí en cambio se
/// puede preguntar si el estado actual es válido y avisar mientras se edita.
///
/// La curva y el decay se representan como "tipo + parámetro" porque es como los ve el usuario, y
/// porque un desplegable no puede seleccionar una instancia de <see cref="IResponseCurve"/>.
/// </summary>
public sealed class MouseSettingsDraft
{
    public const float DefaultPowerExponent = 2f;
    public const float DefaultExponentialStrength = 3f;
    public const float DefaultDualZoneTransition = 0.45f;
    public const float DefaultDualZonePrecisionExponent = 1.6f;
    public const float DefaultDualZoneTurnExponent = 0.75f;
    public const float DefaultLinearDecaySpeed = 4f;
    public const float DefaultExponentialDecaySpeed = 8f;

    public MouseSettingsDraft()
    {
    }

    public MouseSettingsDraft(MouseSettings settings)
    {
        CountsForFullDeflection = settings.CountsForFullDeflection;
        SensitivityX = settings.SensitivityX;
        SensitivityY = settings.SensitivityY;
        InvertX = settings.InvertX;
        InvertY = settings.InvertY;
        DeadzoneInner = settings.DeadzoneInner;
        DeadzoneOuter = settings.DeadzoneOuter;
        MaximumOutput = settings.MaximumOutput;
        OutputScale = settings.OutputScale;
        OutputAntiDeadzone = settings.OutputAntiDeadzone;
        Acceleration = settings.Acceleration;
        SmoothingEnabled = settings.SmoothingEnabled;
        SmoothingStrength = settings.SmoothingStrength;
        AdaptiveSmoothingEnabled = settings.AdaptiveSmoothingEnabled;
        AdaptiveSmoothingResponsiveness = settings.AdaptiveSmoothingResponsiveness;
        DecayDelayMilliseconds = settings.DecayDelayMilliseconds;
        AdsEnabled = settings.AdsEnabled;
        AdsSensitivityMultiplier = settings.AdsSensitivityMultiplier;
        AdsMaximumOutput = settings.AdsMaximumOutput;
        AdsPrecisionExponent = settings.AdsPrecisionExponent;

        switch (settings.ResponseCurve)
        {
            case PowerCurve power:
                CurveKind = ResponseCurveKind.Power;
                CurveParameter = power.Exponent;
                break;
            case ExponentialCurve exponential:
                CurveKind = ResponseCurveKind.Exponential;
                CurveParameter = exponential.Strength;
                break;
            case DualZoneCurve dualZone:
                CurveKind = ResponseCurveKind.DualZone;
                CurveParameter = dualZone.PrecisionExponent;
                CurveSecondaryParameter = dualZone.TurnExponent;
                CurveTransition = dualZone.Transition;
                break;
            default:
                CurveKind = ResponseCurveKind.Linear;
                break;
        }

        switch (settings.Decay)
        {
            case LinearDecay linear:
                DecayKind = DecayKind.Linear;
                DecaySpeed = linear.UnitsPerSecond;
                break;
            case ExponentialDecay exponential:
                DecayKind = DecayKind.Exponential;
                DecaySpeed = exponential.Rate;
                break;
            default:
                DecayKind = DecayKind.Immediate;
                break;
        }
    }

    public float CountsForFullDeflection { get; set; } = 100f;

    public float SensitivityX { get; set; } = 1f;

    public float SensitivityY { get; set; } = 1f;

    public bool InvertX { get; set; }

    public bool InvertY { get; set; }

    public float DeadzoneInner { get; set; }

    public float DeadzoneOuter { get; set; } = 1f;

    public float MaximumOutput { get; set; } = 1f;

    public float OutputScale { get; set; } = 1f;

    public float OutputAntiDeadzone { get; set; }

    public float Acceleration { get; set; }

    public bool SmoothingEnabled { get; set; }

    public float SmoothingStrength { get; set; }

    public bool AdaptiveSmoothingEnabled { get; set; }

    public float AdaptiveSmoothingResponsiveness { get; set; } = 2f;

    public float DecayDelayMilliseconds { get; set; }

    public bool AdsEnabled { get; set; }

    public float AdsSensitivityMultiplier { get; set; } = 0.65f;

    public float AdsMaximumOutput { get; set; } = 0.8f;

    public float AdsPrecisionExponent { get; set; } = 1.2f;

    public ResponseCurveKind CurveKind { get; set; } = ResponseCurveKind.Linear;

    /// <summary>Exponente para Power, intensidad para Exponential. Se ignora en Linear.</summary>
    public float CurveParameter { get; set; } = DefaultPowerExponent;

    public float CurveSecondaryParameter { get; set; } = DefaultDualZoneTurnExponent;

    public float CurveTransition { get; set; } = DefaultDualZoneTransition;

    public DecayKind DecayKind { get; set; } = DecayKind.Immediate;

    /// <summary>Unidades por segundo para Linear, tasa para Exponential. Se ignora en Immediate.</summary>
    public float DecaySpeed { get; set; } = DefaultExponentialDecaySpeed;

    /// <summary>
    /// Devuelve el motivo por el que los ajustes actuales no son válidos, o null si lo son. Permite
    /// avisar mientras se edita sin lanzar excepciones a cada pulsación.
    /// </summary>
    public string? Validate()
    {
        try
        {
            _ = Build();
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ex.Message;
        }
    }

    public bool TryBuild(out MouseSettings settings, out string? error)
    {
        try
        {
            settings = Build();
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            settings = new MouseSettings();
            error = ex.Message;
            return false;
        }
    }

    public MouseSettings Build()
    {
        var settings = new MouseSettings
        {
            CountsForFullDeflection = CountsForFullDeflection,
            SensitivityX = SensitivityX,
            SensitivityY = SensitivityY,
            InvertX = InvertX,
            InvertY = InvertY,
            DeadzoneInner = DeadzoneInner,
            DeadzoneOuter = DeadzoneOuter,
            MaximumOutput = MaximumOutput,
            OutputScale = OutputScale,
            OutputAntiDeadzone = OutputAntiDeadzone,
            Acceleration = Acceleration,
            SmoothingEnabled = SmoothingEnabled,
            SmoothingStrength = SmoothingStrength,
            AdaptiveSmoothingEnabled = AdaptiveSmoothingEnabled,
            AdaptiveSmoothingResponsiveness = AdaptiveSmoothingResponsiveness,
            DecayDelayMilliseconds = DecayDelayMilliseconds,
            AdsEnabled = AdsEnabled,
            AdsSensitivityMultiplier = AdsSensitivityMultiplier,
            AdsMaximumOutput = AdsMaximumOutput,
            AdsPrecisionExponent = AdsPrecisionExponent,
            ResponseCurve = BuildCurve(),
            Decay = BuildDecay(),
        };

        settings.Validate();
        return settings;
    }

    private IResponseCurve BuildCurve() => CurveKind switch
    {
        ResponseCurveKind.Power => new PowerCurve(CurveParameter),
        ResponseCurveKind.Exponential => new ExponentialCurve(CurveParameter),
        ResponseCurveKind.DualZone => new DualZoneCurve(CurveTransition, CurveParameter, CurveSecondaryParameter),
        _ => LinearCurve.Instance,
    };

    private IDecayStrategy BuildDecay() => DecayKind switch
    {
        DecayKind.Linear => new LinearDecay(DecaySpeed),
        DecayKind.Exponential => new ExponentialDecay(DecaySpeed),
        _ => ImmediateDecay.Instance,
    };

    /// <summary>
    /// Valor por defecto sensato al cambiar de tipo de curva: el parámetro de una curva Power no
    /// significa lo mismo que el de una Exponential, así que arrastrarlo daría un resultado extraño.
    /// </summary>
    public static float DefaultParameterFor(ResponseCurveKind kind) => kind switch
    {
        ResponseCurveKind.Power => DefaultPowerExponent,
        ResponseCurveKind.Exponential => DefaultExponentialStrength,
        ResponseCurveKind.DualZone => DefaultDualZonePrecisionExponent,
        _ => 0f,
    };

    public static float DefaultSpeedFor(DecayKind kind) => kind switch
    {
        DecayKind.Linear => DefaultLinearDecaySpeed,
        DecayKind.Exponential => DefaultExponentialDecaySpeed,
        _ => 0f,
    };
}
