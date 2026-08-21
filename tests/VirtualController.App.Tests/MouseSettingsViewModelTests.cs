using VirtualController.App.ViewModels;
using VirtualController.Core.Mouse;
using VirtualController.Core.Profiles;
using VirtualController.TestUtilities;

namespace VirtualController.App.Tests;

public class MouseSettingsViewModelTests
{
    private static (MouseSettingsViewModel Vm, InMemoryProfileRepository Repository, Profile Profile, List<MouseSettings> Applied) Create()
    {
        var repository = new InMemoryProfileRepository();
        var profile = DefaultProfile.Create("Ajustes");
        repository.Save(profile);

        var applied = new List<MouseSettings>();
        var vm = new MouseSettingsViewModel(repository, profile, applied.Add);
        return (vm, repository, profile, applied);
    }

    [Fact]
    public void Constructor_LoadsProfileValuesWithoutMarkingChanges()
    {
        var (vm, _, profile, applied) = Create();

        Assert.Equal(profile.Mouse.SensitivityX, vm.SensitivityX);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Empty(applied);
    }

    [Fact]
    public void ChangingSensitivity_AppliesLiveImmediately()
    {
        var (vm, _, _, applied) = Create();

        vm.SensitivityX = 4.5f;

        Assert.Single(applied);
        Assert.Equal(4.5f, applied[0].SensitivityX);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void LiveChanges_AreNotPersistedUntilSave()
    {
        var (vm, repository, profile, _) = Create();

        vm.SensitivityX = 7f;

        // Probar no debe sobrescribir la configuración guardada.
        Assert.Equal(profile.Mouse.SensitivityX, repository.Load(profile.Id).Mouse.SensitivityX);
    }

    [Fact]
    public void Save_PersistsToTheProfile()
    {
        var (vm, repository, profile, _) = Create();
        vm.SensitivityX = 3f;
        vm.DeadzoneInner = 0.15f;

        vm.SaveCommand.Execute(null);

        var saved = repository.Load(profile.Id).Mouse;
        Assert.Equal(3f, saved.SensitivityX);
        Assert.Equal(0.15f, saved.DeadzoneInner);
        Assert.False(vm.HasUnsavedChanges);
        Assert.NotNull(vm.StatusMessage);
    }

    [Fact]
    public void Save_PreservesBindings()
    {
        var (vm, repository, profile, _) = Create();
        vm.SensitivityX = 2f;

        vm.SaveCommand.Execute(null);

        Assert.Equal(profile.Bindings.Count, repository.Load(profile.Id).Bindings.Count);
    }

    [Fact]
    public void InvalidDeadzone_ShowsErrorAndIsNotAppliedToTheEngine()
    {
        var (vm, _, _, applied) = Create();
        applied.Clear();

        // Interior por encima del exterior: mientras el usuario arrastra puede pasar por aquí.
        vm.DeadzoneInner = 0.9f;
        vm.DeadzoneOuter = 0.2f;

        Assert.NotNull(vm.ValidationError);
        Assert.False(vm.CanSave);

        // Lo importante: el motor conserva la última configuración válida en vez de quedarse sin
        // respuesta a mitad de un ajuste.
        Assert.DoesNotContain(applied, s => s.DeadzoneInner > s.DeadzoneOuter);
    }

    [Fact]
    public void FixingAnInvalidValue_ClearsTheErrorAndAppliesAgain()
    {
        var (vm, _, _, applied) = Create();
        vm.DeadzoneInner = 0.9f;
        vm.DeadzoneOuter = 0.2f;
        Assert.NotNull(vm.ValidationError);

        vm.DeadzoneOuter = 1f;

        Assert.Null(vm.ValidationError);
        Assert.True(vm.CanSave);
        Assert.Contains(applied, s => s.DeadzoneInner == 0.9f && s.DeadzoneOuter == 1f);
    }

    [Fact]
    public void Save_WithInvalidValues_DoesNotTouchTheStoredProfile()
    {
        var (vm, repository, profile, _) = Create();
        var original = repository.Load(profile.Id).Mouse.DeadzoneInner;

        vm.DeadzoneInner = 0.9f;
        vm.DeadzoneOuter = 0.2f;
        vm.SaveCommand.Execute(null);

        Assert.Equal(original, repository.Load(profile.Id).Mouse.DeadzoneInner);
    }

    [Fact]
    public void ChangingCurveKind_ResetsTheParameterToASensibleDefault()
    {
        var (vm, _, _, _) = Create();

        vm.CurveKind = ResponseCurveKind.Power;
        var powerDefault = vm.CurveParameter;

        vm.CurveKind = ResponseCurveKind.Exponential;

        // Arrastrar el parámetro entre tipos daría un resultado inesperado: no significan lo mismo.
        Assert.NotEqual(powerDefault, vm.CurveParameter);
        Assert.Null(vm.ValidationError);
    }

    [Fact]
    public void CurveParameter_OnlyAppliesToParametricCurves()
    {
        var (vm, _, _, _) = Create();

        vm.CurveKind = ResponseCurveKind.Linear;
        Assert.False(vm.CurveParameterApplies);

        vm.CurveKind = ResponseCurveKind.Power;
        Assert.True(vm.CurveParameterApplies);
        Assert.Equal("Exponente", vm.CurveParameterLabel);
    }

    [Fact]
    public void DecaySpeed_OnlyAppliesToGradualDecays()
    {
        var (vm, _, _, _) = Create();

        vm.DecayKind = DecayKind.Immediate;
        Assert.False(vm.DecaySpeedApplies);

        vm.DecayKind = DecayKind.Exponential;
        Assert.True(vm.DecaySpeedApplies);
    }

    [Fact]
    public void SelectedCurveAndDecay_SurviveSaveAndReload()
    {
        var (vm, repository, profile, _) = Create();

        vm.CurveKind = ResponseCurveKind.Power;
        vm.CurveParameter = 3.5f;
        vm.DecayKind = DecayKind.Exponential;
        vm.DecaySpeed = 11f;
        vm.SaveCommand.Execute(null);

        var saved = repository.Load(profile.Id).Mouse;
        Assert.Equal(3.5f, Assert.IsType<PowerCurve>(saved.ResponseCurve).Exponent, precision: 4);
        Assert.Equal(11f, Assert.IsType<ExponentialDecay>(saved.Decay).Rate, precision: 4);
    }

    [Fact]
    public void DualZoneAndAimSettings_SurviveSave()
    {
        var (vm, repository, profile, _) = Create();

        vm.CurveKind = ResponseCurveKind.DualZone;
        vm.CurveTransition = 0.4f;
        vm.CurveParameter = 1.7f;
        vm.CurveSecondaryParameter = 0.8f;
        vm.OutputAntiDeadzone = 0.06f;
        vm.AdsEnabled = true;
        vm.AdsSensitivityMultiplier = 0.55f;
        vm.AdsMaximumOutput = 0.7f;
        vm.AdsPrecisionExponent = 1.35f;
        vm.SaveCommand.Execute(null);

        var saved = repository.Load(profile.Id).Mouse;
        var curve = Assert.IsType<DualZoneCurve>(saved.ResponseCurve);
        Assert.Equal(0.4f, curve.Transition, precision: 4);
        Assert.Equal(1.7f, curve.PrecisionExponent, precision: 4);
        Assert.Equal(0.8f, curve.TurnExponent, precision: 4);
        Assert.Equal(0.06f, saved.OutputAntiDeadzone, precision: 4);
        Assert.True(saved.AdsEnabled);
        Assert.Equal(0.55f, saved.AdsSensitivityMultiplier, precision: 4);
        Assert.Equal(0.7f, saved.AdsMaximumOutput, precision: 4);
        Assert.Equal(1.35f, saved.AdsPrecisionExponent, precision: 4);
    }

    [Fact]
    public void BalancedPreset_ConfiguresCompleteAimPipelineAndAppliesOnce()
    {
        var (vm, _, _, applied) = Create();

        vm.ApplyBalancedAimPresetCommand.Execute(null);

        Assert.Single(applied);
        var settings = applied[0];
        Assert.IsType<DualZoneCurve>(settings.ResponseCurve);
        Assert.True(settings.SmoothingEnabled);
        Assert.True(settings.AdaptiveSmoothingEnabled);
        Assert.True(settings.AdsEnabled);
        Assert.True(settings.OutputAntiDeadzone > 0f);
        Assert.True(settings.DecayDelayMilliseconds > 0f);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void Revert_RestoresSavedValuesAndReappliesThem()
    {
        var (vm, _, profile, applied) = Create();
        vm.SensitivityX = 9f;
        applied.Clear();

        vm.RevertCommand.Execute(null);

        Assert.Equal(profile.Mouse.SensitivityX, vm.SensitivityX);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Contains(applied, s => s.SensitivityX == profile.Mouse.SensitivityX);
    }

    [Fact]
    public void ResetToDefaults_LoadsDefaultsWithoutSaving()
    {
        var (vm, repository, profile, _) = Create();
        vm.SensitivityX = 8f;
        vm.SaveCommand.Execute(null);

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal(new MouseSettings().SensitivityX, vm.SensitivityX);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal(8f, repository.Load(profile.Id).Mouse.SensitivityX);
    }

    [Fact]
    public void SmoothingStaysOffByDefault()
    {
        var (vm, _, _, _) = Create();

        Assert.False(vm.SmoothingEnabled);
    }

    [Fact]
    public void EveryCurveAndDecayCombination_IsSelectableWithoutError()
    {
        var (vm, _, _, _) = Create();

        foreach (var curve in vm.AvailableCurves)
        {
            foreach (var decay in vm.AvailableDecays)
            {
                vm.CurveKind = curve;
                vm.DecayKind = decay;

                Assert.Null(vm.ValidationError);
            }
        }
    }
}
