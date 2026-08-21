namespace VirtualController.Core.Profiles;

/// <summary>
/// Identidad estable de un perfil, independiente de su nombre visible (ADR-004, punto 2). Renombrar
/// un perfil no cambia su identidad ni el archivo donde vive, y el nombre nunca se usa como ruta.
/// </summary>
public readonly record struct ProfileId(Guid Value)
{
    public static ProfileId New() => new(Guid.NewGuid());

    public static bool TryParse(string? text, out ProfileId id)
    {
        if (Guid.TryParse(text, out var guid))
        {
            id = new ProfileId(guid);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}
