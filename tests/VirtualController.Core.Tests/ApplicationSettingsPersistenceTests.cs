using VirtualController.Core.Profiles;
using VirtualController.Infrastructure.Persistence;
using Xunit;

namespace VirtualController.Core.Tests;

public sealed class ApplicationSettingsPersistenceTests : IDisposable
{
    private readonly string _root;
    private readonly StoragePaths _paths;
    private readonly JsonApplicationSettingsRepository _repository;

    public ApplicationSettingsPersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vc-set-" + Guid.NewGuid().ToString("N"));
        _paths = new StoragePaths(_root);
        _repository = new JsonApplicationSettingsRepository(_paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_OnFirstRun_ReturnsDefaults_DoesNotThrow()
    {
        var settings = _repository.Load();

        Assert.Equal(ApplicationSettings.DefaultEmergencyStop, settings.EmergencyStop);
        Assert.Null(settings.SelectedProfileId);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEverySetting()
    {
        var id = ProfileId.New();
        var original = new ApplicationSettings
        {
            SelectedProfileId = id,
            EmergencyStop = "Ctrl+Alt+Escape",
            StartStop = "Ctrl+Alt+P",
            ToggleMouseCapture = "Ctrl+Alt+K",
            StartMinimized = true,
            MinimizeToTray = false,
            DiagnosticsEnabled = true,
            PreferredBackend = "hidmaestro",
        };

        _repository.Save(original);
        var loaded = _repository.Load();

        Assert.Equal(id, loaded.SelectedProfileId);
        Assert.Equal("Ctrl+Alt+Escape", loaded.EmergencyStop);
        Assert.Equal("Ctrl+Alt+P", loaded.StartStop);
        Assert.Equal("Ctrl+Alt+K", loaded.ToggleMouseCapture);
        Assert.True(loaded.StartMinimized);
        Assert.False(loaded.MinimizeToTray);
        Assert.True(loaded.DiagnosticsEnabled);
        Assert.Equal("hidmaestro", loaded.PreferredBackend);
    }

    [Fact]
    public void SelectedProfile_SurvivesRestart()
    {
        var id = ProfileId.New();
        _repository.Save(new ApplicationSettings { SelectedProfileId = id });

        // Un repositorio nuevo simula un arranque distinto de la aplicación.
        var afterRestart = new JsonApplicationSettingsRepository(new StoragePaths(_root)).Load();

        Assert.Equal(id, afterRestart.SelectedProfileId);
    }

    [Fact]
    public void Save_UsesAtomicWrite_LeavesNoTempFile()
    {
        _repository.Save(new ApplicationSettings());

        Assert.True(File.Exists(_paths.SettingsFile));
        Assert.Empty(Directory.EnumerateFiles(_paths.SettingsDirectory, "*.tmp"));
    }

    [Fact]
    public void Save_WithEmptyEmergencyStop_IsRejected()
    {
        var invalid = new ApplicationSettings { EmergencyStop = "  " };

        Assert.Throws<ProfileValidationException>(() => _repository.Save(invalid));
    }

    [Fact]
    public void Save_WithUnparseableGlobalHotkey_IsRejected()
    {
        var invalid = new ApplicationSettings { ToggleMouseCapture = "Ctrl+Alt+DefinitelyNotAKey" };

        Assert.Throws<ProfileValidationException>(() => _repository.Save(invalid));
    }

    [Fact]
    public void Load_HandEditedEmptyEmergencyStop_IsRejected()
    {
        // El caso que motiva validar en dominio y no solo en la UI: el usuario edita el JSON a mano.
        Directory.CreateDirectory(_paths.SettingsDirectory);
        File.WriteAllText(_paths.SettingsFile, """
            { "version": 1, "emergencyStop": "" }
            """);

        Assert.Throws<ProfileValidationException>(() => _repository.Load());
    }

    [Fact]
    public void Load_FutureVersion_IsRejected()
    {
        Directory.CreateDirectory(_paths.SettingsDirectory);
        File.WriteAllText(_paths.SettingsFile, """
            { "version": 999 }
            """);

        Assert.Throws<UnsupportedProfileVersionException>(() => _repository.Load());
    }

    [Fact]
    public void Load_UnknownProperty_IsRejected()
    {
        Directory.CreateDirectory(_paths.SettingsDirectory);
        File.WriteAllText(_paths.SettingsFile, """
            { "version": 1, "emergencyStp": "Ctrl+Alt+Escape" }
            """);

        Assert.Throws<ProfileValidationException>(() => _repository.Load());
    }
}
