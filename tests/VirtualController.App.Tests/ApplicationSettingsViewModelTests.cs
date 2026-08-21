using VirtualController.App.ViewModels;
using VirtualController.Core.Profiles;

namespace VirtualController.App.Tests;

public sealed class ApplicationSettingsViewModelTests
{
    [Fact]
    public void Save_PersistsHotkeysAndNotifiesForLiveRegistration()
    {
        var repository = new MemorySettingsRepository(new ApplicationSettings());
        var vm = new ApplicationSettingsViewModel(repository, repository.Load());
        ApplicationSettings? notified = null;
        vm.Saved += settings => notified = settings;

        vm.StartStop = "Ctrl+Alt+P";
        vm.ToggleMouseCapture = "Ctrl+Alt+K";
        vm.EmergencyStop = "Ctrl+Shift+Escape";
        vm.MinimizeToTray = false;
        vm.SaveCommand.Execute(null);

        Assert.Equal("Ctrl+Alt+P", repository.Load().StartStop);
        Assert.Equal("Ctrl+Alt+K", repository.Load().ToggleMouseCapture);
        Assert.False(repository.Load().MinimizeToTray);
        Assert.NotNull(notified);
        Assert.Null(vm.ValidationError);
    }

    [Fact]
    public void InvalidHotkey_IsNotSavedOrNotified()
    {
        var original = new ApplicationSettings();
        var repository = new MemorySettingsRepository(original);
        var vm = new ApplicationSettingsViewModel(repository, original);
        var notifications = 0;
        vm.Saved += _ => notifications++;
        vm.StartStop = "Ctrl+NotAKey";

        vm.SaveCommand.Execute(null);

        Assert.Equal(original.StartStop, repository.Load().StartStop);
        Assert.Equal(0, notifications);
        Assert.NotNull(vm.ValidationError);
    }

    private sealed class MemorySettingsRepository(ApplicationSettings initial) : IApplicationSettingsRepository
    {
        private ApplicationSettings _settings = initial;

        public ApplicationSettings Load() => _settings;

        public void Save(ApplicationSettings settings)
        {
            settings.Validate();
            _settings = settings;
        }
    }
}
