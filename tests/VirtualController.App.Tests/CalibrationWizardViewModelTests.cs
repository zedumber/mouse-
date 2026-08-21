using VirtualController.App.ViewModels;
using VirtualController.Core.Mouse;
using VirtualController.Core.Profiles;
using VirtualController.TestUtilities;

namespace VirtualController.App.Tests;

public sealed class CalibrationWizardViewModelTests
{
    private static (CalibrationWizardViewModel Wizard, MouseSettingsViewModel Mouse, InMemoryProfileRepository Repository, Profile Profile, List<MouseSettings> Applied, List<string> Completed) Create()
    {
        var repository = new InMemoryProfileRepository();
        var profile = DefaultProfile.Create("Juego");
        repository.Save(profile);
        var applied = new List<MouseSettings>();
        var completed = new List<string>();
        var mouse = new MouseSettingsViewModel(repository, profile, applied.Add);
        var wizard = new CalibrationWizardViewModel(mouse, () => { }, completed.Add);
        return (wizard, mouse, repository, profile, applied, completed);
    }

    [Fact]
    public void Start_AppliesBalancedPresetAndOpensDeadzoneStep()
    {
        var (wizard, _, _, _, applied, _) = Create();

        wizard.StartCommand.Execute(null);

        Assert.Equal(1, wizard.CurrentStep);
        Assert.True(wizard.ShowDeadzoneStep);
        Assert.Single(applied);
        Assert.IsType<DualZoneCurve>(applied[0].ResponseCurve);
    }

    [Fact]
    public void RecommendedAntiDeadzone_AddsSmallMarginAndClamps()
    {
        var (wizard, _, _, _, _, _) = Create();

        wizard.ObservedGameDeadzone = 0.06f;
        Assert.Equal(0.07f, wizard.RecommendedAntiDeadzone, precision: 4);

        wizard.ObservedGameDeadzone = 0.30f;
        Assert.Equal(0.30f, wizard.RecommendedAntiDeadzone, precision: 4);
    }

    [Fact]
    public void Finish_AppliesAllValuesAndPersistsProfile()
    {
        var (wizard, _, repository, profile, _, completed) = Create();
        wizard.StartCommand.Execute(null);
        wizard.ObservedGameDeadzone = 0.05f;
        wizard.CountsForFullDeflection = 180f;
        wizard.AdsSensitivityMultiplier = 0.58f;
        wizard.NextCommand.Execute(null);
        wizard.NextCommand.Execute(null);
        wizard.NextCommand.Execute(null);

        wizard.ApplyAndSaveCommand.Execute(null);

        var saved = repository.Load(profile.Id).Mouse;
        Assert.Equal(0.06f, saved.OutputAntiDeadzone, precision: 4);
        Assert.Equal(180f, saved.CountsForFullDeflection);
        Assert.Equal(0.58f, saved.AdsSensitivityMultiplier, precision: 4);
        Assert.Single(completed);
    }
}
