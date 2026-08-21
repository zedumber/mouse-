namespace VirtualController.Core.Profiles;

/// <summary>
/// Errores de aplicación con significado propio. La UI nunca debe recibir IOException/JsonException
/// filtradas desde infraestructura (ADR-004, punto 11).
/// </summary>
public abstract class ProfileException : Exception
{
    protected ProfileException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

public sealed class ProfileValidationException : ProfileException
{
    public ProfileValidationException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

public sealed class ProfileNotFoundException : ProfileException
{
    public ProfileId Id { get; }

    public ProfileNotFoundException(ProfileId id)
        : base($"No existe ningún perfil con id {id}.")
    {
        Id = id;
    }
}

/// <summary>
/// Un perfil de una versión más nueva NO se carga: intentar interpretarlo y luego guardarlo
/// destruiría silenciosamente los datos que no entendemos (ADR-004, punto 6).
/// </summary>
public sealed class UnsupportedProfileVersionException : ProfileException
{
    public int FoundVersion { get; }

    public int SupportedVersion { get; }

    public UnsupportedProfileVersionException(int foundVersion, int supportedVersion)
        : base($"El perfil usa la versión {foundVersion} y esta aplicación soporta hasta la {supportedVersion}. " +
               "Actualiza la aplicación para poder abrirlo.")
    {
        FoundVersion = foundVersion;
        SupportedVersion = supportedVersion;
    }
}

public sealed class ProfileNameConflictException : ProfileException
{
    public ProfileNameConflictException(string name)
        : base($"Ya existe un perfil llamado \"{name}\".")
    {
    }
}

public sealed class ProfileStorageException : ProfileException
{
    public ProfileStorageException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
