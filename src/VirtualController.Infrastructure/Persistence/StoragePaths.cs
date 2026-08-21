namespace VirtualController.Infrastructure.Persistence;

/// <summary>
/// Rutas de almacenamiento (ADR-004, punto 3). Se usa %LOCALAPPDATA% y no una carpeta relativa al
/// ejecutable porque la app instalada puede vivir en Program Files, donde no se puede escribir.
/// El constructor acepta una raíz explícita para que los tests no toquen los datos reales del usuario.
/// </summary>
public sealed class StoragePaths
{
    private const string ApplicationFolderName = "VirtualController";

    public StoragePaths(string? rootDirectory = null)
    {
        Root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationFolderName);

        ProfilesDirectory = Path.Combine(Root, "Profiles");
        SettingsDirectory = Path.Combine(Root, "Settings");
        LogsDirectory = Path.Combine(Root, "Logs");
    }

    public string Root { get; }

    public string ProfilesDirectory { get; }

    public string SettingsDirectory { get; }

    public string LogsDirectory { get; }

    public string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>
    /// El archivo se nombra por ProfileId, nunca por el nombre visible: así renombrar no mueve
    /// archivos y un nombre con caracteres inválidos o reservados no puede romper nada.
    /// </summary>
    public string ProfileFile(Core.Profiles.ProfileId id) =>
        Path.Combine(ProfilesDirectory, $"{id}.json");
}
