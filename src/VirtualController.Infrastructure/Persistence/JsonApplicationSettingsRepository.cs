using System.Text.Json;
using System.Text.Json.Serialization;
using VirtualController.Core.Profiles;

namespace VirtualController.Infrastructure.Persistence;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ApplicationSettingsDto
{
    public int? Version { get; set; }

    public string? SelectedProfileId { get; set; }

    public string? EmergencyStop { get; set; }

    public string? StartStop { get; set; }

    public string? ToggleMouseCapture { get; set; }

    public bool? StartMinimized { get; set; }

    public bool? MinimizeToTray { get; set; }

    public bool? DiagnosticsEnabled { get; set; }

    public string? PreferredBackend { get; set; }
}

/// <summary>
/// Persistencia de la configuración global (ADR-004, punto 15). Antes de existir, `ApplicationSettings`
/// se construía con valores por defecto en cada arranque: el usuario no podía cambiar el backend, ni el
/// atajo de emergencia, ni recordar qué perfil tenía seleccionado.
/// </summary>
public sealed class JsonApplicationSettingsRepository : IApplicationSettingsRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // Sin esto, "Ctrl+Alt+Escape" se guarda como "Ctrl+Alt+Escape". Es JSON válido, pero
        // estos archivos están pensados para editarse a mano y deben ser legibles. El encoder relajado
        // es apropiado aquí porque el destino es un archivo local, no HTML.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly StoragePaths _paths;

    public JsonApplicationSettingsRepository(StoragePaths paths)
    {
        _paths = paths;
    }

    public ApplicationSettings Load()
    {
        if (!File.Exists(_paths.SettingsFile))
        {
            return new ApplicationSettings();
        }

        string json;
        try
        {
            json = File.ReadAllText(_paths.SettingsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProfileStorageException($"No se pudo leer la configuración: {ex.Message}", ex);
        }

        ApplicationSettingsDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ApplicationSettingsDto>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new ProfileValidationException($"La configuración no es válida: {ex.Message}", ex);
        }

        if (dto is null)
        {
            return new ApplicationSettings();
        }

        if (dto.Version > ApplicationSettings.CurrentVersion)
        {
            throw new UnsupportedProfileVersionException(dto.Version.Value, ApplicationSettings.CurrentVersion);
        }

        var defaults = new ApplicationSettings();

        var settings = new ApplicationSettings
        {
            SelectedProfileId = ProfileId.TryParse(dto.SelectedProfileId, out var id) ? id : null,
            EmergencyStop = dto.EmergencyStop ?? defaults.EmergencyStop,
            StartStop = dto.StartStop ?? defaults.StartStop,
            ToggleMouseCapture = dto.ToggleMouseCapture ?? defaults.ToggleMouseCapture,
            StartMinimized = dto.StartMinimized ?? defaults.StartMinimized,
            MinimizeToTray = dto.MinimizeToTray ?? defaults.MinimizeToTray,
            DiagnosticsEnabled = dto.DiagnosticsEnabled ?? defaults.DiagnosticsEnabled,
            PreferredBackend = dto.PreferredBackend ?? defaults.PreferredBackend,
        };

        settings.Validate();
        return settings;
    }

    public void Save(ApplicationSettings settings)
    {
        settings.Validate();

        var dto = new ApplicationSettingsDto
        {
            Version = ApplicationSettings.CurrentVersion,
            SelectedProfileId = settings.SelectedProfileId?.ToString(),
            EmergencyStop = settings.EmergencyStop,
            StartStop = settings.StartStop,
            ToggleMouseCapture = settings.ToggleMouseCapture,
            StartMinimized = settings.StartMinimized,
            MinimizeToTray = settings.MinimizeToTray,
            DiagnosticsEnabled = settings.DiagnosticsEnabled,
            PreferredBackend = settings.PreferredBackend,
        };

        try
        {
            AtomicFileWriter.Write(_paths.SettingsFile, JsonSerializer.Serialize(dto, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProfileStorageException($"No se pudo guardar la configuración: {ex.Message}", ex);
        }
    }
}
