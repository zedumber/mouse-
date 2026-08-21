using System.Text.Json;
using VirtualController.Core.Profiles;

namespace VirtualController.Infrastructure.Persistence;

public sealed class JsonProfileRepository : IProfileRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

        // Los perfiles se editan a mano, así que deben ser legibles: el encoder por defecto escapa
        // caracteres como '+' a + sin necesidad, al ser un archivo local y no HTML.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly StoragePaths _paths;

    public JsonProfileRepository(StoragePaths paths)
    {
        _paths = paths;
    }

    public IReadOnlyList<ProfileMetadata> List()
    {
        if (!Directory.Exists(_paths.ProfilesDirectory))
        {
            return [];
        }

        var result = new List<ProfileMetadata>();

        foreach (var file in Directory.EnumerateFiles(_paths.ProfilesDirectory, "*.json"))
        {
            // Un perfil corrupto no debe impedir listar los demás: se omite y se seguirá informando
            // del problema cuando alguien intente cargarlo explícitamente.
            if (TryReadMetadata(file, out var metadata))
            {
                result.Add(metadata);
            }
        }

        return result;
    }

    public Profile Load(ProfileId id)
    {
        var path = _paths.ProfileFile(id);

        if (!File.Exists(path))
        {
            throw new ProfileNotFoundException(id);
        }

        var dto = Deserialize(ReadAllText(path), path);
        return ProfileMapper.ToDomain(dto);
    }

    public void Save(Profile profile)
    {
        profile.Validate();

        var dto = ProfileMapper.ToDto(profile);
        var json = JsonSerializer.Serialize(dto, SerializerOptions);

        // La guarda hace el viaje COMPLETO hasta dominio, no solo hasta el DTO: validar únicamente el
        // DTO dejaba pasar perfiles sintácticamente correctos pero imposibles de reconstruir (p. ej.
        // una curva Custom sin puntos), que se escribían en disco y luego rompían todo Load para
        // siempre — incluido el arranque de la aplicación.
        _ = ProfileMapper.ToDomain(Deserialize(json, _paths.ProfileFile(profile.Id)));

        try
        {
            AtomicFileWriter.Write(_paths.ProfileFile(profile.Id), json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProfileStorageException($"No se pudo guardar el perfil \"{profile.Name}\": {ex.Message}", ex);
        }
    }

    public void Delete(ProfileId id)
    {
        var path = _paths.ProfileFile(id);

        if (!File.Exists(path))
        {
            throw new ProfileNotFoundException(id);
        }

        try
        {
            File.Delete(path);

            // El backup también se borra: dejarlo significaba que "borrar un perfil" conservaba una
            // copia íntegra en disco indefinidamente, tanto por espacio como por privacidad.
            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProfileStorageException($"No se pudo eliminar el perfil {id}: {ex.Message}", ex);
        }
    }

    public Profile ReadFromFile(string path)
    {
        var dto = Deserialize(ReadAllText(path), path);
        return ProfileMapper.ToDomain(dto);
    }

    public void WriteToFile(Profile profile, string path)
    {
        profile.Validate();

        var json = JsonSerializer.Serialize(ProfileMapper.ToDto(profile), SerializerOptions);

        try
        {
            AtomicFileWriter.Write(path, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProfileStorageException($"No se pudo exportar el perfil a \"{path}\": {ex.Message}", ex);
        }
    }

    private bool TryReadMetadata(string path, out ProfileMetadata metadata)
    {
        try
        {
            var dto = Deserialize(ReadAllText(path), path);

            if (dto.Name is not null && ProfileId.TryParse(dto.Id, out var id))
            {
                // El id de dentro del JSON debe coincidir con el nombre del archivo, porque Load
                // siempre busca por "<id>.json". Sin esta comprobación, un archivo copiado a mano
                // (p. ej. "Shooter.json") se listaba pero fallaba al abrirlo, y dos archivos con el
                // mismo id producían entradas duplicadas y ediciones que parecían perderse.
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), id.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    metadata = null!;
                    return false;
                }

                metadata = new ProfileMetadata(id, dto.Name);
                return true;
            }
        }
        catch (ProfileException)
        {
            // Ignorado a propósito: ver comentario en List().
        }

        metadata = null!;
        return false;
    }

    private static string ReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProfileStorageException($"No se pudo leer \"{path}\": {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Traduce cualquier fallo de System.Text.Json a un error de aplicación: la UI no debe recibir
    /// JsonException desde infraestructura (ADR-004, punto 11).
    /// </summary>
    private static ProfileDto Deserialize(string json, string path)
    {
        // La versión se comprueba ANTES de vincular al DTO estricto. Si no, un perfil de una versión
        // futura —que por definición trae campos nuevos— fallaba como "propiedad desconocida", es
        // decir se le decía al usuario "tu archivo está corrupto" en vez de "actualiza la aplicación",
        // invitándole a borrar justo los datos que no debe tocar (ADR-004, puntos 6 y 7).
        EnsureVersionIsSupported(json, path);

        try
        {
            return JsonSerializer.Deserialize<ProfileDto>(json, SerializerOptions)
                   ?? throw new ProfileValidationException($"El archivo \"{path}\" está vacío o contiene solo null.");
        }
        catch (JsonException ex)
        {
            throw new ProfileValidationException($"El archivo \"{path}\" no es un perfil válido: {ex.Message}", ex);
        }
    }

    private static void EnsureVersionIsSupported(string json, string path)
    {
        int version;

        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("version", out var element)
                || !element.TryGetInt32(out version))
            {
                return; // La ausencia de version la reporta el mapper con su propio mensaje.
            }
        }
        catch (JsonException ex)
        {
            throw new ProfileValidationException($"El archivo \"{path}\" no es un perfil válido: {ex.Message}", ex);
        }

        if (version > Profile.CurrentVersion)
        {
            throw new UnsupportedProfileVersionException(version, Profile.CurrentVersion);
        }

        if (version < 1)
        {
            throw new ProfileValidationException($"Versión de perfil inválida en \"{path}\": {version}.");
        }
    }
}
