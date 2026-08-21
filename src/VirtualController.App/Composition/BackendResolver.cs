using VirtualController.Core.Gamepad;
using VirtualController.Core.Profiles;
using VirtualController.VirtualGamepad.Xbox360;

namespace VirtualController.App.Composition;

/// <summary>
/// Traduce (ControllerType del perfil + PreferredBackend de los ajustes) en una implementación
/// concreta. Vive en el composition root a propósito: Core nunca instancia backends por nombre
/// (ADR-003, punto 3 / ADR-004, punto 4).
/// </summary>
public static class BackendResolver
{
    public const string ViGEm = "vigem";

    public static IVirtualGamepad Resolve(ControllerType controllerType, string preferredBackend) =>
        (controllerType, preferredBackend.ToLowerInvariant()) switch
        {
            (ControllerType.Xbox360, ViGEm) => new ViGEmXbox360Backend(),

            // Cuando el spike de Fase 3 valide HIDMaestro, aquí se añade su caso sin tocar Core ni la UI.
            (ControllerType.Xbox360, var unknown) => throw new NotSupportedException(
                $"Backend \"{unknown}\" no soportado todavía para {controllerType}. Backends disponibles: {ViGEm}."),

            _ => throw new NotSupportedException($"Tipo de mando no soportado: {controllerType}."),
        };

    public static string DescribeBackend(string preferredBackend) => preferredBackend.ToLowerInvariant() switch
    {
        ViGEm => "Xbox 360 (ViGEmBus)",
        _ => preferredBackend,
    };
}
