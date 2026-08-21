namespace VirtualController.Core.Profiles;

/// <summary>
/// Configuración global, separada de Profile a propósito (ADR-004, punto 15): si la combinación de
/// parada de emergencia cambiara al cambiar de perfil, el usuario no podría confiar en ella. El
/// backend preferido también vive aquí, no en el perfil.
/// </summary>
public sealed record ApplicationSettings
{
    public const int CurrentVersion = 1;

    public const string DefaultEmergencyStop = "Ctrl+Alt+Escape";

    public ProfileId? SelectedProfileId { get; init; }

    public string EmergencyStop { get; init; } = DefaultEmergencyStop;

    public string StartStop { get; init; } = "Ctrl+Alt+S";

    public string ToggleMouseCapture { get; init; } = "Ctrl+Alt+M";

    public bool StartMinimized { get; init; }

    public bool MinimizeToTray { get; init; } = true;

    public bool DiagnosticsEnabled { get; init; }

    /// <summary>Identificador del backend preferido, nunca un tipo concreto: Core no resuelve implementaciones.</summary>
    public string PreferredBackend { get; init; } = "vigem";

    /// <summary>
    /// La protección del atajo de emergencia se valida aquí, en el dominio, y no solo en la UI: un
    /// usuario que edite el JSON a mano eludiría cualquier comprobación que viviera únicamente en WPF
    /// (ADR-004, punto 14).
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EmergencyStop))
        {
            throw new ProfileValidationException(
                "La combinación de parada de emergencia no puede estar vacía: es el mecanismo que garantiza " +
                "recuperar el control del teclado y el mouse.");
        }

        if (string.IsNullOrWhiteSpace(PreferredBackend))
        {
            throw new ProfileValidationException("El backend preferido no puede estar vacío.");
        }
    }
}
