namespace VirtualController.Core.Profiles;

public interface IApplicationSettingsRepository
{
    /// <summary>
    /// Devuelve los ajustes guardados, o los valores por defecto si aún no existen. Nunca lanza por
    /// "no hay archivo": el primer arranque es un caso normal, no un error.
    /// </summary>
    ApplicationSettings Load();

    void Save(ApplicationSettings settings);
}
