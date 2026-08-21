using VirtualController.Core.Mapping;

namespace VirtualController.Core.Profiles;

/// <summary>
/// Edición de los bindings de un perfil (requisitos 14 y 17). Trabaja sobre una copia en memoria y
/// solo toca el repositorio al confirmar, de modo que cancelar no deja el perfil a medias ni un
/// conflicto transitorio impide seguir editando.
/// </summary>
public sealed class BindingEditor
{
    private readonly IProfileRepository _repository;
    private readonly List<Binding> _bindings;

    public BindingEditor(IProfileRepository repository, Profile profile)
    {
        _repository = repository;
        Profile = profile;
        _bindings = profile.Bindings.ToList();
    }

    public Profile Profile { get; private set; }

    public IReadOnlyList<Binding> Bindings => _bindings;

    public bool HasUnsavedChanges { get; private set; }

    public void Add(Binding binding)
    {
        _bindings.Add(binding);
        HasUnsavedChanges = true;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _bindings.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Índice de binding fuera de rango.");
        }

        _bindings.RemoveAt(index);
        HasUnsavedChanges = true;
    }

    public void Replace(int index, Binding binding)
    {
        if (index < 0 || index >= _bindings.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Índice de binding fuera de rango.");
        }

        _bindings[index] = binding;
        HasUnsavedChanges = true;
    }

    public void Reset()
    {
        _bindings.Clear();
        _bindings.AddRange(Profile.Bindings);
        HasUnsavedChanges = false;
    }

    /// <summary>
    /// Comprueba si el conjunto actual es válido sin escribir nada, para poder avisar mientras se
    /// edita en vez de solo al intentar guardar.
    /// </summary>
    public string? Validate()
    {
        try
        {
            MappingValidator.EnsureNoStickConflicts(_bindings);
            return null;
        }
        catch (MappingConflictException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Persiste los cambios y devuelve el perfil actualizado. Lanza <see cref="ProfileValidationException"/>
    /// si la combinación no es válida, sin haber tocado el disco.
    /// </summary>
    public Profile Save()
    {
        var updated = Profile with { Bindings = _bindings.ToArray() };

        // Valida antes de escribir: si el conjunto es inválido, el perfil en disco sigue siendo el
        // último que funcionaba.
        updated.Validate();

        _repository.Save(updated);

        Profile = updated;
        HasUnsavedChanges = false;
        return updated;
    }
}
