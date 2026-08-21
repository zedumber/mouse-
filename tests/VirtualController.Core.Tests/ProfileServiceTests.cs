using VirtualController.Core.Profiles;
using VirtualController.TestUtilities;
using Xunit;

namespace VirtualController.Core.Tests;

public class ProfileServiceTests
{
    private static (ProfileService Service, InMemoryProfileRepository Repository) CreateService()
    {
        var repository = new InMemoryProfileRepository();
        return (new ProfileService(repository), repository);
    }

    [Fact]
    public void Create_PersistsProfileWithDefaultBindings()
    {
        var (service, repository) = CreateService();

        var profile = service.Create("Nuevo");

        Assert.Equal("Nuevo", profile.Name);
        Assert.NotEmpty(profile.Bindings);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public void Create_TrimsName()
    {
        var (service, _) = CreateService();

        var profile = service.Create("   Espacios   ");

        Assert.Equal("Espacios", profile.Name);
    }

    [Fact]
    public void Create_WithDuplicateName_ThrowsNameConflict()
    {
        var (service, _) = CreateService();
        service.Create("Shooter");

        Assert.Throws<ProfileNameConflictException>(() => service.Create("Shooter"));
    }

    [Fact]
    public void Create_NameConflictIsCaseInsensitive()
    {
        var (service, _) = CreateService();
        service.Create("Shooter");

        Assert.Throws<ProfileNameConflictException>(() => service.Create("SHOOTER"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsValidation(string name)
    {
        var (service, _) = CreateService();

        Assert.Throws<ProfileValidationException>(() => service.Create(name));
    }

    [Fact]
    public void Rename_ChangesNameButKeepsIdStable()
    {
        var (service, _) = CreateService();
        var original = service.Create("Antiguo");

        var renamed = service.Rename(original.Id, "Nuevo");

        Assert.Equal(original.Id, renamed.Id);
        Assert.Equal("Nuevo", renamed.Name);
    }

    [Fact]
    public void Rename_ToOwnName_IsAllowed()
    {
        var (service, _) = CreateService();
        var profile = service.Create("Igual");

        var renamed = service.Rename(profile.Id, "Igual");

        Assert.Equal("Igual", renamed.Name);
    }

    [Fact]
    public void Rename_ToExistingOtherName_ThrowsNameConflict()
    {
        var (service, _) = CreateService();
        service.Create("Ocupado");
        var other = service.Create("Libre");

        Assert.Throws<ProfileNameConflictException>(() => service.Rename(other.Id, "Ocupado"));
    }

    [Fact]
    public void Duplicate_CreatesNewIdAndPreservesSettings()
    {
        var (service, _) = CreateService();
        var original = service.Create("Base");

        var copy = service.Duplicate(original.Id);

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(original.Bindings.Count, copy.Bindings.Count);
        Assert.Contains("copia", copy.Name);
    }

    [Fact]
    public void Duplicate_Twice_ProducesDistinctNames()
    {
        var (service, _) = CreateService();
        var original = service.Create("Base");

        var first = service.Duplicate(original.Id);
        var second = service.Duplicate(original.Id);

        Assert.NotEqual(first.Name, second.Name);
    }

    [Fact]
    public void Import_AlwaysGeneratesNewLocalId()
    {
        var (service, _) = CreateService();
        var external = DefaultProfile.Create("Importado");

        var imported = service.Import(external);

        Assert.NotEqual(external.Id, imported.Id);
    }

    [Fact]
    public void Import_WithNameCollision_DoesNotOverwriteExistingProfile()
    {
        var (service, _) = CreateService();
        var existing = service.Create("Shooter");
        var external = DefaultProfile.Create("Shooter");

        var imported = service.Import(external);

        Assert.NotEqual(existing.Id, imported.Id);
        Assert.NotEqual("Shooter", imported.Name);
        Assert.Equal(2, service.List().Count);
        Assert.Equal("Shooter", service.Load(existing.Id).Name);
    }

    [Fact]
    public void Delete_RemovesProfileFromList()
    {
        var (service, _) = CreateService();
        var profile = service.Create("Temporal");

        service.Delete(profile.Id);

        Assert.Empty(service.List());
    }

    [Fact]
    public void Load_AfterCreate_ReturnsSameProfile()
    {
        var (service, _) = CreateService();
        var created = service.Create("Recuperable");

        var loaded = service.Load(created.Id);

        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal(created.Name, loaded.Name);
    }
}
