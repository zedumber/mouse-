namespace VirtualController.Core.Profiles;

public sealed record ProfileMetadata(ProfileId Id, string Name);

/// <summary>
/// Persistencia pura, deliberadamente reducida (ADR-004, punto 12): los casos de uso
/// (Create/Rename/Duplicate/Import/Export) viven en ProfileService, no aquí.
/// </summary>
public interface IProfileRepository
{
    IReadOnlyList<ProfileMetadata> List();

    Profile Load(ProfileId id);

    void Save(Profile profile);

    void Delete(ProfileId id);
}
