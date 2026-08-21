using VirtualController.App.ViewModels;
using VirtualController.Core.Profiles;
using VirtualController.TestUtilities;

namespace VirtualController.App.Tests;

public class ProfileSelectorViewModelTests
{
    [Fact]
    public void SelectingProfile_LoadsItAndNotifiesConsumer()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var first = service.Create("Primero");
        var second = service.Create("Segundo");
        Profile? selected = null;
        var vm = new ProfileSelectorViewModel(service, first, profile => selected = profile);

        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == second.Id);

        Assert.Equal(second.Id, selected!.Id);
        Assert.Equal("Segundo", vm.NameDraft);
    }

    [Fact]
    public void CreateDuplicateAndRename_RefreshCollectionAndSelectResult()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var initial = service.Create("Base");
        var vm = new ProfileSelectorViewModel(service, initial, _ => { });

        vm.NameDraft = "Juego";
        vm.CreateCommand.Execute(null);
        Assert.Equal("Juego", vm.SelectedProfile!.Name);

        vm.DuplicateCommand.Execute(null);
        Assert.Contains("copia", vm.SelectedProfile!.Name);

        vm.NameDraft = "Juego competitivo";
        vm.RenameCommand.Execute(null);
        Assert.Equal("Juego competitivo", vm.SelectedProfile!.Name);
        Assert.Equal(3, vm.Profiles.Count);
    }

    [Fact]
    public void Delete_RequiresConfirmationAndNeverDeletesLastProfile()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var first = service.Create("Uno");
        _ = service.Create("Dos");
        var allowDelete = false;
        var vm = new ProfileSelectorViewModel(service, first, _ => { }, _ => allowDelete);

        vm.DeleteCommand.Execute(null);
        Assert.Equal(2, vm.Profiles.Count);

        allowDelete = true;
        vm.DeleteCommand.Execute(null);
        Assert.Single(vm.Profiles);
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void DuplicateName_ShowsDomainErrorWithoutChangingProfiles()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var first = service.Create("Uno");
        _ = service.Create("Dos");
        var vm = new ProfileSelectorViewModel(service, first, _ => { });

        vm.NameDraft = "Dos";
        vm.RenameCommand.Execute(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Equal(2, vm.Profiles.Count);
    }
}
