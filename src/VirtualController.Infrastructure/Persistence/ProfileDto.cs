using System.Text.Json.Serialization;

namespace VirtualController.Infrastructure.Persistence;

/// <summary>
/// Representación persistida, separada del dominio a propósito (ADR-004, punto 4 del diseño original):
/// el JSON se deserializa primero a este DTO, se valida, y solo entonces se construye el objeto de
/// dominio. `JsonUnmappedMemberHandling.Disallow` hace que un typo como "sensitivtyX" falle con un
/// mensaje claro en vez de cargar en silencio con el valor por defecto (ADR-004, punto 7).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ProfileDto
{
    // Requeridos: su ausencia indica un archivo corrupto, no un perfil mínimo (ADR-004, punto 5).
    public int? Version { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public OutputDto? Output { get; set; }

    // Opcionales con valor por defecto documentado.
    public WasdDto? Wasd { get; set; }

    public List<BindingDto>? Bindings { get; set; }

    public MouseDto? Mouse { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OutputDto
{
    public string? ControllerType { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WasdDto
{
    public bool? NormalizeDiagonal { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BindingDto
{
    public string? Input { get; set; }

    public string? Output { get; set; }

    /// <summary>Solo para salidas analógicas (triggers): el valor que aplica al activarse.</summary>
    public float? Value { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class MouseDto
{
    public float? CountsForFullDeflection { get; set; }

    public float? SensitivityX { get; set; }

    public float? SensitivityY { get; set; }

    public bool? InvertX { get; set; }

    public bool? InvertY { get; set; }

    public DeadzoneDto? Deadzone { get; set; }

    public float? OutputScale { get; set; }

    public float? OutputAntiDeadzone { get; set; }

    public float? MaximumOutput { get; set; }

    public ResponseCurveDto? ResponseCurve { get; set; }

    public AccelerationDto? Acceleration { get; set; }

    public SmoothingDto? Smoothing { get; set; }

    public DecayDto? Decay { get; set; }

    public AdsDto? Ads { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DeadzoneDto
{
    public float? Inner { get; set; }

    public float? Outer { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ResponseCurveDto
{
    public string? Type { get; set; }

    public float? Exponent { get; set; }

    public float? Strength { get; set; }

    public float? Transition { get; set; }

    public float? PrecisionExponent { get; set; }

    public float? TurnExponent { get; set; }

    public List<CurvePointDto>? Points { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CurvePointDto
{
    public float? Input { get; set; }

    public float? Output { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AccelerationDto
{
    public bool? Enabled { get; set; }

    public float? Strength { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SmoothingDto
{
    public bool? Enabled { get; set; }

    public float? Strength { get; set; }

    public bool? Adaptive { get; set; }

    public float? Responsiveness { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DecayDto
{
    public string? Strategy { get; set; }

    public float? Speed { get; set; }

    public float? DelayMilliseconds { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AdsDto
{
    public bool? Enabled { get; set; }

    public float? SensitivityMultiplier { get; set; }

    public float? MaximumOutput { get; set; }

    public float? PrecisionExponent { get; set; }
}
