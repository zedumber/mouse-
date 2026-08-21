using VirtualController.Core.Profiles;
using Xunit;

namespace VirtualController.Core.Tests;

public class ApplicationSettingsTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        new ApplicationSettings().Validate();
    }

    [Fact]
    public void Defaults_HaveEmergencyStopConfigured()
    {
        Assert.False(string.IsNullOrWhiteSpace(new ApplicationSettings().EmergencyStop));
    }

    [Fact]
    public void Defaults_HaveSmoothingRelatedDiagnosticsDisabled()
    {
        Assert.False(new ApplicationSettings().DiagnosticsEnabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyEmergencyStop_IsRejectedByDomain_NotOnlyByUi(string? value)
    {
        var settings = new ApplicationSettings { EmergencyStop = value! };

        var ex = Assert.Throws<ProfileValidationException>(settings.Validate);
        Assert.Contains("emergencia", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPreferredBackend_IsRejected(string value)
    {
        var settings = new ApplicationSettings { PreferredBackend = value };

        Assert.Throws<ProfileValidationException>(settings.Validate);
    }

    [Fact]
    public void PreferredBackendIsAnIdentifier_NotAConcreteType()
    {
        // Core no debe conocer clases concretas de backend: solo un identificador de configuración
        // que el composition root resuelve (ADR-003 punto 3 / ADR-004 punto 4).
        Assert.IsType<string>(new ApplicationSettings().PreferredBackend);
    }

    [Fact]
    public void SelectedProfileId_IsOptional()
    {
        var settings = new ApplicationSettings { SelectedProfileId = null };

        settings.Validate();
        Assert.Null(settings.SelectedProfileId);
    }
}
