using VirtualController.Core.Profiles;

namespace VirtualController.TestUtilities;

/// <summary>
/// Repositorio en memoria para testear los casos de uso de ProfileService sin tocar disco.
/// </summary>
public sealed class InMemoryProfileRepository : IProfileRepository
{
    private readonly Dictionary<ProfileId, Profile> _profiles = new();

    public int SaveCount { get; private set; }

    public IReadOnlyList<ProfileMetadata> List() =>
        _profiles.Values.Select(p => new ProfileMetadata(p.Id, p.Name)).ToArray();

    public Profile Load(ProfileId id) =>
        _profiles.TryGetValue(id, out var profile) ? profile : throw new ProfileNotFoundException(id);

    public void Save(Profile profile)
    {
        profile.Validate();
        _profiles[profile.Id] = profile;
        SaveCount++;
    }

    public void Delete(ProfileId id)
    {
        if (!_profiles.Remove(id))
        {
            throw new ProfileNotFoundException(id);
        }
    }
}
