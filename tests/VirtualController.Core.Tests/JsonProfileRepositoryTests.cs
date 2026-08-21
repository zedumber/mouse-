using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;
using VirtualController.Core.Mouse;
using VirtualController.Core.Profiles;
using VirtualController.Infrastructure.Persistence;
using Xunit;

namespace VirtualController.Core.Tests;

/// <summary>
/// Usa un directorio temporal propio para no tocar los datos reales del usuario en %LOCALAPPDATA%.
/// </summary>
public sealed class JsonProfileRepositoryTests : IDisposable
{
    private readonly string _root;
    private readonly StoragePaths _paths;
    private readonly JsonProfileRepository _repository;

    public JsonProfileRepositoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vc-tests-" + Guid.NewGuid().ToString("N"));
        _paths = new StoragePaths(_root);
        _repository = new JsonProfileRepository(_paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllSettings()
    {
        var original = DefaultProfile.Create("Shooter") with
        {
            NormalizeDiagonal = false,
            Mouse = new MouseSettings
            {
                SensitivityX = 2.5f,
                SensitivityY = 1.5f,
                InvertY = true,
                DeadzoneInner = 0.1f,
                DeadzoneOuter = 0.95f,
                OutputScale = 1.2f,
                OutputAntiDeadzone = 0.05f,
                MaximumOutput = 0.9f,
                Acceleration = 1.5f,
                SmoothingEnabled = true,
                SmoothingStrength = 0.3f,
                AdaptiveSmoothingEnabled = true,
                AdaptiveSmoothingResponsiveness = 2.5f,
                DecayDelayMilliseconds = 8f,
                AdsEnabled = true,
                AdsSensitivityMultiplier = 0.6f,
                AdsMaximumOutput = 0.75f,
                AdsPrecisionExponent = 1.3f,
                ResponseCurve = new DualZoneCurve(0.45f, 1.6f, 0.75f),
            },
        };

        _repository.Save(original);
        var loaded = _repository.Load(original.Id);

        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal("Shooter", loaded.Name);
        Assert.False(loaded.NormalizeDiagonal);
        Assert.Equal(2.5f, loaded.Mouse.SensitivityX);
        Assert.Equal(1.5f, loaded.Mouse.SensitivityY);
        Assert.True(loaded.Mouse.InvertY);
        Assert.Equal(0.1f, loaded.Mouse.DeadzoneInner);
        Assert.Equal(0.95f, loaded.Mouse.DeadzoneOuter);
        Assert.Equal(1.2f, loaded.Mouse.OutputScale);
        Assert.Equal(0.05f, loaded.Mouse.OutputAntiDeadzone);
        Assert.Equal(0.9f, loaded.Mouse.MaximumOutput);
        Assert.Equal(1.5f, loaded.Mouse.Acceleration);
        Assert.True(loaded.Mouse.SmoothingEnabled);
        Assert.Equal(0.3f, loaded.Mouse.SmoothingStrength);
        Assert.True(loaded.Mouse.AdaptiveSmoothingEnabled);
        Assert.Equal(2.5f, loaded.Mouse.AdaptiveSmoothingResponsiveness);
        Assert.Equal(8f, loaded.Mouse.DecayDelayMilliseconds);
        Assert.True(loaded.Mouse.AdsEnabled);
        Assert.Equal(0.6f, loaded.Mouse.AdsSensitivityMultiplier);
        Assert.Equal(0.75f, loaded.Mouse.AdsMaximumOutput);
        Assert.Equal(1.3f, loaded.Mouse.AdsPrecisionExponent);
        Assert.IsType<DualZoneCurve>(loaded.Mouse.ResponseCurve);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsBindings()
    {
        var profile = DefaultProfile.Create("Bindings");

        _repository.Save(profile);
        var loaded = _repository.Load(profile.Id);

        Assert.Equal(profile.Bindings.Count, loaded.Bindings.Count);

        // W → stick izquierdo arriba, y click izquierdo → gatillo derecho a fondo.
        Assert.Contains(loaded.Bindings, b =>
            b.Input == PhysicalInput.FromKey(Key.W)
            && b.Output is VirtualOutput.StickDirection { Stick: Stick.Left, Direction: Direction.Up });

        Assert.Contains(loaded.Bindings, b =>
            b.Input == PhysicalInput.FromMouseButton(MouseButton.Left)
            && b.Output is VirtualOutput.AnalogValue { Axis: GamepadAxis.RightTrigger, Value: 1f });
    }

    [Fact]
    public void FileIsNamedByProfileId_NotByVisibleName()
    {
        var profile = DefaultProfile.Create("Nombre Con Espacios");

        _repository.Save(profile);

        Assert.True(File.Exists(_paths.ProfileFile(profile.Id)));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(_paths.ProfilesDirectory),
            f => Path.GetFileName(f).Contains("Nombre"));
    }

    [Fact]
    public void Load_MissingProfile_ThrowsProfileNotFound()
    {
        Assert.Throws<ProfileNotFoundException>(() => _repository.Load(ProfileId.New()));
    }

    [Fact]
    public void Load_CorruptJson_ThrowsProfileValidation_NotJsonException()
    {
        var id = ProfileId.New();
        WriteRaw(id, "{ this is not valid json ");

        var ex = Assert.Throws<ProfileValidationException>(() => _repository.Load(id));
        Assert.Contains("no es un perfil válido", ex.Message);
    }

    [Fact]
    public void Load_MissingRequiredVersion_Throws()
    {
        var id = ProfileId.New();
        WriteRaw(id, $$"""
            { "id": "{{id}}", "name": "Sin version", "output": { "controllerType": "Xbox360" } }
            """);

        var ex = Assert.Throws<ProfileValidationException>(() => _repository.Load(id));
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public void Load_MissingRequiredControllerType_Throws()
    {
        var id = ProfileId.New();
        WriteRaw(id, $$"""
            { "version": 1, "id": "{{id}}", "name": "Sin tipo", "output": { } }
            """);

        Assert.Throws<ProfileValidationException>(() => _repository.Load(id));
    }

    [Fact]
    public void Load_UnknownProperty_Throws_InsteadOfSilentlyIgnoring()
    {
        var id = ProfileId.New();
        WriteRaw(id, $$"""
            {
              "version": 1,
              "id": "{{id}}",
              "name": "Typo",
              "output": { "controllerType": "Xbox360" },
              "mouse": { "sensitivtyX": 2.4 }
            }
            """);

        // Un typo debe fallar de forma visible, no cargar con el valor por defecto (ADR-004 punto 7).
        Assert.Throws<ProfileValidationException>(() => _repository.Load(id));
    }

    [Fact]
    public void Load_FutureVersion_ThrowsUnsupportedVersion_DoesNotSilentlyDowngrade()
    {
        var id = ProfileId.New();
        WriteRaw(id, $$"""
            {
              "version": 999,
              "id": "{{id}}",
              "name": "Del futuro",
              "output": { "controllerType": "Xbox360" }
            }
            """);

        var ex = Assert.Throws<UnsupportedProfileVersionException>(() => _repository.Load(id));
        Assert.Equal(999, ex.FoundVersion);
        Assert.Equal(Profile.CurrentVersion, ex.SupportedVersion);
    }

    [Fact]
    public void Load_UnknownCurveType_Throws()
    {
        var id = ProfileId.New();
        WriteRaw(id, $$"""
            {
              "version": 1,
              "id": "{{id}}",
              "name": "Curva rara",
              "output": { "controllerType": "Xbox360" },
              "mouse": { "responseCurve": { "type": "Telepatica" } }
            }
            """);

        Assert.Throws<ProfileValidationException>(() => _repository.Load(id));
    }

    [Fact]
    public void Load_MinimalValidProfile_UsesDocumentedDefaults()
    {
        var id = ProfileId.New();
        WriteRaw(id, $$"""
            {
              "version": 1,
              "id": "{{id}}",
              "name": "Minimo",
              "output": { "controllerType": "Xbox360" }
            }
            """);

        var loaded = _repository.Load(id);

        Assert.True(loaded.NormalizeDiagonal);
        Assert.Empty(loaded.Bindings);
        Assert.False(loaded.Mouse.SmoothingEnabled);
        Assert.IsType<LinearCurve>(loaded.Mouse.ResponseCurve);
        Assert.IsType<ImmediateDecay>(loaded.Mouse.Decay);
    }

    [Fact]
    public void Load_V1Profile_MigratesOldMaximumOutputToScaleWithoutChangingResponse()
    {
        var id = ProfileId.New();
        WriteRaw(id, $$"""
            {
              "version": 1,
              "id": "{{id}}",
              "name": "Legacy",
              "output": { "controllerType": "Xbox360" },
              "mouse": { "maximumOutput": 0.65 }
            }
            """);

        var loaded = _repository.Load(id);

        Assert.Equal(0.65f, loaded.Mouse.OutputScale, precision: 4);
        Assert.Equal(1f, loaded.Mouse.MaximumOutput, precision: 4);
    }

    [Fact]
    public void List_ReturnsSavedProfiles()
    {
        _repository.Save(DefaultProfile.Create("Uno"));
        _repository.Save(DefaultProfile.Create("Dos"));

        var listed = _repository.List();

        Assert.Equal(2, listed.Count);
        Assert.Contains(listed, p => p.Name == "Uno");
        Assert.Contains(listed, p => p.Name == "Dos");
    }

    [Fact]
    public void List_SkipsCorruptFiles_WithoutFailingEntirely()
    {
        _repository.Save(DefaultProfile.Create("Bueno"));
        WriteRaw(ProfileId.New(), "{ corrupto ");

        var listed = _repository.List();

        Assert.Single(listed);
        Assert.Equal("Bueno", listed[0].Name);
    }

    [Fact]
    public void List_OnMissingDirectory_ReturnsEmpty_DoesNotThrow()
    {
        Assert.Empty(_repository.List());
    }

    [Fact]
    public void Save_OverExistingProfile_KeepsBackupOfPrevious()
    {
        var profile = DefaultProfile.Create("Original");
        _repository.Save(profile);

        _repository.Save(profile with { Name = "Modificado" });

        Assert.Equal("Modificado", _repository.Load(profile.Id).Name);
        Assert.True(File.Exists(_paths.ProfileFile(profile.Id) + ".bak"));
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        _repository.Save(DefaultProfile.Create("Limpio"));

        Assert.Empty(Directory.EnumerateFiles(_paths.ProfilesDirectory, "*.tmp"));
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        var profile = DefaultProfile.Create("Borrable");
        _repository.Save(profile);

        _repository.Delete(profile.Id);

        Assert.Throws<ProfileNotFoundException>(() => _repository.Load(profile.Id));
    }

    [Fact]
    public void Delete_MissingProfile_ThrowsProfileNotFound()
    {
        Assert.Throws<ProfileNotFoundException>(() => _repository.Delete(ProfileId.New()));
    }

    [Fact]
    public void ExportThenImportFile_RoundTrips()
    {
        var profile = DefaultProfile.Create("Exportable");
        var exportPath = Path.Combine(_root, "export", "Exportable.json");

        _repository.WriteToFile(profile, exportPath);
        var reimported = _repository.ReadFromFile(exportPath);

        Assert.Equal(profile.Name, reimported.Name);
        Assert.Equal(profile.Bindings.Count, reimported.Bindings.Count);
    }

    [Fact]
    public void Save_ProfileWithConflictingStickBindings_IsRejected()
    {
        var invalid = DefaultProfile.Create("Conflictivo") with
        {
            Bindings =
            [
                new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Right, Direction.Up)),
                new(PhysicalInput.FromMouseButton(MouseButton.Left), new VirtualOutput.StickVector(Stick.Right)),
            ],
        };

        Assert.Throws<ProfileValidationException>(() => _repository.Save(invalid));
    }

    private void WriteRaw(ProfileId id, string json)
    {
        Directory.CreateDirectory(_paths.ProfilesDirectory);
        File.WriteAllText(_paths.ProfileFile(id), json);
    }
}
