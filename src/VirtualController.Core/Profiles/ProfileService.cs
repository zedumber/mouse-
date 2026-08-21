namespace VirtualController.Core.Profiles;

/// <summary>
/// Casos de uso sobre perfiles (ADR-004, punto 12). Depende solo de IProfileRepository, así que es
/// completamente testeable sin tocar el sistema de archivos.
/// </summary>
public sealed class ProfileService
{
    private readonly IProfileRepository _repository;

    public ProfileService(IProfileRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<ProfileMetadata> List() => _repository.List();

    public Profile Load(ProfileId id) => _repository.Load(id);

    public Profile Create(string name, IReadOnlyList<Mapping.Binding>? bindings = null)
    {
        EnsureNameIsAvailable(name);

        var profile = new Profile
        {
            Id = ProfileId.New(),
            Name = name.Trim(),
            Bindings = bindings ?? DefaultProfile.Bindings,
        };

        profile.Validate();
        _repository.Save(profile);
        return profile;
    }

    public Profile Rename(ProfileId id, string newName)
    {
        var profile = _repository.Load(id);
        EnsureNameIsAvailable(newName, exclude: id);

        // Solo cambia el nombre visible: el id y el archivo permanecen estables (ADR-004, punto 2).
        var renamed = profile with { Name = newName.Trim() };
        renamed.Validate();
        _repository.Save(renamed);
        return renamed;
    }

    public Profile Duplicate(ProfileId id, string? newName = null)
    {
        var source = _repository.Load(id);
        var name = string.IsNullOrWhiteSpace(newName) ? BuildCopyName(source.Name) : newName.Trim();
        EnsureNameIsAvailable(name);

        var copy = source with { Id = ProfileId.New(), Name = name };
        copy.Validate();
        _repository.Save(copy);
        return copy;
    }

    public void Delete(ProfileId id) => _repository.Delete(id);

    /// <summary>
    /// Importar siempre genera un ProfileId nuevo y un nombre libre: nunca sobrescribe un perfil
    /// existente ni reutiliza la identidad del archivo de origen (ADR-004, puntos 9 y 10).
    /// </summary>
    public Profile Import(Profile imported)
    {
        imported.Validate();

        var name = FindAvailableName(imported.Name);
        var local = imported with { Id = ProfileId.New(), Name = name };

        local.Validate();
        _repository.Save(local);
        return local;
    }

    private void EnsureNameIsAvailable(string name, ProfileId? exclude = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ProfileValidationException("El nombre del perfil no puede estar vacío.");
        }

        var trimmed = name.Trim();

        foreach (var existing in _repository.List())
        {
            if (exclude is { } excluded && existing.Id == excluded)
            {
                continue;
            }

            if (string.Equals(existing.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProfileNameConflictException(trimmed);
            }
        }
    }

    private string FindAvailableName(string desired)
    {
        var baseName = string.IsNullOrWhiteSpace(desired) ? "Imported" : desired.Trim();
        var candidate = baseName;
        var suffix = 2;

        while (NameIsTaken(candidate))
        {
            candidate = $"{baseName} ({suffix++})";
        }

        return candidate;
    }

    private bool NameIsTaken(string name) =>
        _repository.List().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private string BuildCopyName(string sourceName) => FindAvailableName($"{sourceName} (copia)");
}
