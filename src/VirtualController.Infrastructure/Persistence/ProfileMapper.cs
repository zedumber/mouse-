using VirtualController.Core.Mapping;
using VirtualController.Core.Mouse;
using VirtualController.Core.Profiles;

namespace VirtualController.Infrastructure.Persistence;

internal static class ProfileMapper
{
    public static Profile ToDomain(ProfileDto dto)
    {
        var version = Require(dto.Version, "version");

        if (version > Profile.CurrentVersion)
        {
            throw new UnsupportedProfileVersionException(version, Profile.CurrentVersion);
        }

        // Aquí iría la cadena de migraciones secuenciales (V1→V2→...) cuando exista una V2;
        // hoy solo existe la V1, así que no hay nada que migrar (ADR-004, punto 16).

        var idText = Require(dto.Id, "id");
        if (!ProfileId.TryParse(idText, out var id))
        {
            throw new ProfileValidationException($"El id \"{idText}\" no es un GUID válido.");
        }

        var output = Require(dto.Output, "output");
        var controllerTypeText = Require(output.ControllerType, "output.controllerType");

        // Enum.TryParse acepta números arbitrarios ("999" produce un ControllerType inexistente que
        // luego reventaba al resolver el backend), así que hace falta IsDefined además del parseo.
        if (!Enum.TryParse<ControllerType>(controllerTypeText, out var controllerType)
            || !Enum.IsDefined(controllerType))
        {
            throw new ProfileValidationException($"Tipo de mando no soportado: \"{controllerTypeText}\".");
        }

        var profile = new Profile
        {
            Id = id,
            Name = Require(dto.Name, "name"),
            ControllerType = controllerType,
            NormalizeDiagonal = dto.Wasd?.NormalizeDiagonal ?? true,
            Bindings = (dto.Bindings ?? []).Select(ToDomain).ToArray(),
            Mouse = ToDomain(dto.Mouse, version),
        };

        profile.Validate();
        return profile;
    }

    public static ProfileDto ToDto(Profile profile) => new()
    {
        Version = Profile.CurrentVersion,
        Id = profile.Id.ToString(),
        Name = profile.Name,
        Output = new OutputDto { ControllerType = profile.ControllerType.ToString() },
        Wasd = new WasdDto { NormalizeDiagonal = profile.NormalizeDiagonal },
        Bindings = profile.Bindings.Select(ToDto).ToList(),
        Mouse = ToDto(profile.Mouse),
    };

    private static Binding ToDomain(BindingDto dto)
    {
        var input = BindingTextFormat.ParseInput(Require(dto.Input, "bindings[].input"));
        var output = BindingTextFormat.ParseOutput(Require(dto.Output, "bindings[].output"), dto.Value);
        return new Binding(input, output);
    }

    private static BindingDto ToDto(Binding binding) => new()
    {
        Input = BindingTextFormat.FormatInput(binding.Input),
        Output = BindingTextFormat.FormatOutput(binding.Output),
        Value = binding.Output is VirtualOutput.AnalogValue analog ? analog.Value : null,
    };

    private static MouseSettings ToDomain(MouseDto? dto, int profileVersion)
    {
        if (dto is null)
        {
            return new MouseSettings();
        }

        var defaults = new MouseSettings();

        return new MouseSettings
        {
            CountsForFullDeflection = dto.CountsForFullDeflection ?? defaults.CountsForFullDeflection,
            SensitivityX = dto.SensitivityX ?? defaults.SensitivityX,
            SensitivityY = dto.SensitivityY ?? defaults.SensitivityY,
            InvertX = dto.InvertX ?? defaults.InvertX,
            InvertY = dto.InvertY ?? defaults.InvertY,
            DeadzoneInner = dto.Deadzone?.Inner ?? defaults.DeadzoneInner,
            DeadzoneOuter = dto.Deadzone?.Outer ?? defaults.DeadzoneOuter,
            // En V1 MaximumOutput escalaba todo el rango. V2 separa escala y límite; esta migración
            // conserva exactamente la respuesta anterior y deja el nuevo límite en 1.
            OutputScale = profileVersion == 1
                ? dto.OutputScale ?? dto.MaximumOutput ?? defaults.OutputScale
                : dto.OutputScale ?? defaults.OutputScale,
            OutputAntiDeadzone = dto.OutputAntiDeadzone ?? defaults.OutputAntiDeadzone,
            MaximumOutput = profileVersion == 1
                ? defaults.MaximumOutput
                : dto.MaximumOutput ?? defaults.MaximumOutput,
            ResponseCurve = ToDomain(dto.ResponseCurve),
            Acceleration = ToAcceleration(dto.Acceleration),
            SmoothingEnabled = dto.Smoothing?.Enabled ?? defaults.SmoothingEnabled,
            SmoothingStrength = dto.Smoothing?.Strength ?? defaults.SmoothingStrength,
            AdaptiveSmoothingEnabled = dto.Smoothing?.Adaptive ?? defaults.AdaptiveSmoothingEnabled,
            AdaptiveSmoothingResponsiveness = dto.Smoothing?.Responsiveness ?? defaults.AdaptiveSmoothingResponsiveness,
            Decay = ToDomain(dto.Decay),
            DecayDelayMilliseconds = dto.Decay?.DelayMilliseconds ?? defaults.DecayDelayMilliseconds,
            AdsEnabled = dto.Ads?.Enabled ?? defaults.AdsEnabled,
            AdsSensitivityMultiplier = dto.Ads?.SensitivityMultiplier ?? defaults.AdsSensitivityMultiplier,
            AdsMaximumOutput = dto.Ads?.MaximumOutput ?? defaults.AdsMaximumOutput,
            AdsPrecisionExponent = dto.Ads?.PrecisionExponent ?? defaults.AdsPrecisionExponent,
        };
    }

    private static MouseDto ToDto(MouseSettings settings) => new()
    {
        CountsForFullDeflection = settings.CountsForFullDeflection,
        SensitivityX = settings.SensitivityX,
        SensitivityY = settings.SensitivityY,
        InvertX = settings.InvertX,
        InvertY = settings.InvertY,
        Deadzone = new DeadzoneDto { Inner = settings.DeadzoneInner, Outer = settings.DeadzoneOuter },
        OutputScale = settings.OutputScale,
        OutputAntiDeadzone = settings.OutputAntiDeadzone,
        MaximumOutput = settings.MaximumOutput,
        ResponseCurve = ToDto(settings.ResponseCurve),
        Acceleration = new AccelerationDto { Enabled = settings.Acceleration > 0f, Strength = settings.Acceleration },
        Smoothing = new SmoothingDto
        {
            Enabled = settings.SmoothingEnabled,
            Strength = settings.SmoothingStrength,
            Adaptive = settings.AdaptiveSmoothingEnabled,
            Responsiveness = settings.AdaptiveSmoothingResponsiveness,
        },
        Decay = ToDto(settings.Decay, settings.DecayDelayMilliseconds),
        Ads = new AdsDto
        {
            Enabled = settings.AdsEnabled,
            SensitivityMultiplier = settings.AdsSensitivityMultiplier,
            MaximumOutput = settings.AdsMaximumOutput,
            PrecisionExponent = settings.AdsPrecisionExponent,
        },
    };

    private static float ToAcceleration(AccelerationDto? dto)
    {
        if (dto is null)
        {
            return 0f;
        }

        // "enabled: false" gana sobre cualquier strength guardada: desactivar debe desactivar.
        return dto.Enabled == false ? 0f : dto.Strength ?? 0f;
    }

    private static IResponseCurve ToDomain(ResponseCurveDto? dto)
    {
        if (dto?.Type is null)
        {
            return LinearCurve.Instance;
        }

        return dto.Type switch
        {
            "Linear" => LinearCurve.Instance,
            "Power" => new PowerCurve(dto.Exponent ?? 2f),
            "Exponential" => new ExponentialCurve(dto.Strength ?? 3f),
            "DualZone" => new DualZoneCurve(
                dto.Transition ?? MouseSettingsDraft.DefaultDualZoneTransition,
                dto.PrecisionExponent ?? MouseSettingsDraft.DefaultDualZonePrecisionExponent,
                dto.TurnExponent ?? MouseSettingsDraft.DefaultDualZoneTurnExponent),
            "Custom" => BuildCustomCurve(dto.Points),
            _ => throw new ProfileValidationException($"Curva de respuesta desconocida: \"{dto.Type}\"."),
        };
    }

    // Cada rama guarda los parámetros de la curva: emitir solo el discriminador hacía que al recargar
    // el perfil se aplicara el valor por defecto, cambiando en silencio la sensación del mando.
    private static ResponseCurveDto ToDto(IResponseCurve curve) => curve switch
    {
        LinearCurve => new ResponseCurveDto { Type = "Linear" },
        PowerCurve power => new ResponseCurveDto { Type = "Power", Exponent = power.Exponent },
        ExponentialCurve exponential => new ResponseCurveDto { Type = "Exponential", Strength = exponential.Strength },
        DualZoneCurve dualZone => new ResponseCurveDto
        {
            Type = "DualZone",
            Transition = dualZone.Transition,
            PrecisionExponent = dualZone.PrecisionExponent,
            TurnExponent = dualZone.TurnExponent,
        },
        CustomCurve custom => new ResponseCurveDto
        {
            Type = "Custom",
            Points = custom.Points.Select(p => new CurvePointDto { Input = p.Input, Output = p.Output }).ToList(),
        },

        // Degradar una curva desconocida a Linear en silencio perdería la configuración del usuario
        // sin avisar; es preferible fallar y que quien añada una curva nueva actualice también esto.
        _ => throw new ProfileValidationException(
            $"No se sabe persistir la curva de respuesta {curve.GetType().Name}."),
    };

    private static IResponseCurve BuildCustomCurve(List<CurvePointDto>? points)
    {
        if (points is null || points.Count < 2)
        {
            throw new ProfileValidationException("Una curva personalizada necesita al menos 2 puntos.");
        }

        var parsed = points
            .Select(p => (
                Input: Require(p.Input, "responseCurve.points[].input"),
                Output: Require(p.Output, "responseCurve.points[].output")))
            .ToArray();

        return new CustomCurve(parsed);
    }

    private static IDecayStrategy ToDomain(DecayDto? dto)
    {
        if (dto?.Strategy is null)
        {
            return ImmediateDecay.Instance;
        }

        return dto.Strategy switch
        {
            "Immediate" => ImmediateDecay.Instance,
            "Linear" => new LinearDecay(dto.Speed ?? 4f),
            "Exponential" => new ExponentialDecay(dto.Speed ?? 8f),
            _ => throw new ProfileValidationException($"Estrategia de decay desconocida: \"{dto.Strategy}\"."),
        };
    }

    private static DecayDto ToDto(IDecayStrategy decay, float delayMilliseconds) => decay switch
    {
        ImmediateDecay => new DecayDto { Strategy = "Immediate", DelayMilliseconds = delayMilliseconds },
        LinearDecay linear => new DecayDto
        {
            Strategy = "Linear",
            Speed = linear.UnitsPerSecond,
            DelayMilliseconds = delayMilliseconds,
        },
        ExponentialDecay exponential => new DecayDto
        {
            Strategy = "Exponential",
            Speed = exponential.Rate,
            DelayMilliseconds = delayMilliseconds,
        },
        _ => throw new ProfileValidationException(
            $"No se sabe persistir la estrategia de decay {decay.GetType().Name}."),
    };

    // Dos nombres distintos porque C# no permite sobrecargas que difieran solo en el constraint
    // (class vs struct), y necesitamos el mismo mensaje de error para ambos casos.
    private static T Require<T>(T? value, string fieldName)
        where T : class
        => value ?? throw MissingField(fieldName);

    private static T Require<T>(T? value, string fieldName)
        where T : struct
        => value ?? throw MissingField(fieldName);

    private static ProfileValidationException MissingField(string fieldName) => new(
        $"Falta el campo requerido \"{fieldName}\". El archivo puede estar corrupto o incompleto.");
}
