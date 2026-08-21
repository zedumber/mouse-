using VirtualController.Core.Mapping;
using VirtualController.Core.Mouse;

namespace VirtualController.Core.Profiles;

/// <summary>
/// Tipo de mando que el perfil quiere emular. Deliberadamente NO es el backend técnico: el perfil
/// dice "quiero un Xbox 360", y la aplicación decide con qué backend producirlo (ADR-004, punto 4).
/// Así un perfil sigue siendo válido aunque el backend por defecto cambie.
/// </summary>
public enum ControllerType
{
    Xbox360,
}

public sealed record Profile
{
    public const int CurrentVersion = 2;

    public required ProfileId Id { get; init; }

    public required string Name { get; init; }

    public ControllerType ControllerType { get; init; } = ControllerType.Xbox360;

    public bool NormalizeDiagonal { get; init; } = true;

    public required IReadOnlyList<Binding> Bindings { get; init; }

    public MouseSettings Mouse { get; init; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ProfileValidationException("El nombre del perfil no puede estar vacío.");
        }

        if (Id.Value == Guid.Empty)
        {
            throw new ProfileValidationException("El perfil necesita un identificador válido.");
        }

        // MouseSettings.Validate lanza ArgumentException; sin envolverla, una deadzone inválida en el
        // JSON llegaba cruda hasta la UI (y reventaba el arranque) en vez de ser un error tipado.
        try
        {
            Mouse.Validate();
        }
        catch (ArgumentException ex)
        {
            throw new ProfileValidationException($"Ajustes de mouse inválidos: {ex.Message}", ex);
        }

        // Un StickVector y un StickDirection sobre el mismo stick es un conflicto que se rechaza en
        // validación, no en tiempo de ejecución (ADR-006, punto 4).
        try
        {
            MappingValidator.EnsureNoStickConflicts(Bindings);
        }
        catch (MappingConflictException ex)
        {
            throw new ProfileValidationException(ex.Message, ex);
        }
    }

    public MappingOptions ToMappingOptions() => new(NormalizeDiagonal);
}
