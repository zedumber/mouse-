using VirtualController.Core.Mouse;
using VirtualController.Core.Profiles;
using VirtualController.Infrastructure.Persistence;
using Xunit;

namespace VirtualController.Core.Tests;

/// <summary>
/// Estos tests existían como hueco: el test de round-trip original solo usaba LinearCurve e
/// ImmediateDecay — las dos únicas implementaciones SIN parámetros, es decir las únicas que no podían
/// perder nada al guardarse. Todas las demás perdían su configuración en silencio.
///
/// Comparan comportamiento (Apply/Decay) y no solo el tipo, porque el bug era precisamente que el tipo
/// sobrevivía y el parámetro no.
/// </summary>
public sealed class ProfileRoundTripParametersTests : IDisposable
{
    private readonly string _root;
    private readonly JsonProfileRepository _repository;

    public ProfileRoundTripParametersTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vc-rt-" + Guid.NewGuid().ToString("N"));
        _repository = new JsonProfileRepository(new StoragePaths(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private Profile SaveAndReload(MouseSettings mouse)
    {
        var profile = DefaultProfile.Create("RoundTrip") with { Mouse = mouse };
        _repository.Save(profile);
        return _repository.Load(profile.Id);
    }

    [Fact]
    public void PowerCurve_PreservesExponent()
    {
        var reloaded = SaveAndReload(new MouseSettings { ResponseCurve = new PowerCurve(3.5f) });

        var curve = Assert.IsType<PowerCurve>(reloaded.Mouse.ResponseCurve);
        Assert.Equal(3.5f, curve.Exponent, precision: 4);
        Assert.Equal(MathF.Pow(0.5f, 3.5f), curve.Apply(0.5f), precision: 5);
    }

    [Fact]
    public void ExponentialCurve_PreservesStrength()
    {
        var reloaded = SaveAndReload(new MouseSettings { ResponseCurve = new ExponentialCurve(6f) });

        var curve = Assert.IsType<ExponentialCurve>(reloaded.Mouse.ResponseCurve);
        Assert.Equal(6f, curve.Strength, precision: 4);
    }

    [Fact]
    public void CustomCurve_PreservesItsPoints()
    {
        var points = new[] { (0f, 0f), (0.5f, 0.2f), (1f, 1f) };
        var reloaded = SaveAndReload(new MouseSettings { ResponseCurve = new CustomCurve(points) });

        var curve = Assert.IsType<CustomCurve>(reloaded.Mouse.ResponseCurve);
        Assert.Equal(3, curve.Points.Count);
        Assert.Equal(0.2f, curve.Apply(0.5f), precision: 4);
    }

    [Fact]
    public void CustomCurve_SurvivesSaveAndCanBeLoadedAgain()
    {
        // Antes, guardar una CustomCurve escribía {"type":"Custom"} sin puntos: el archivo pasaba la
        // guarda de Save y a partir de ahí TODO Load fallaba para siempre, incluido el arranque.
        var points = new[] { (0f, 0f), (1f, 1f) };
        var profile = DefaultProfile.Create("Custom") with
        {
            Mouse = new MouseSettings { ResponseCurve = new CustomCurve(points) },
        };

        _repository.Save(profile);
        _repository.Save(_repository.Load(profile.Id));

        Assert.IsType<CustomCurve>(_repository.Load(profile.Id).Mouse.ResponseCurve);
    }

    [Fact]
    public void ExponentialDecay_PreservesRate()
    {
        var reloaded = SaveAndReload(new MouseSettings { Decay = new ExponentialDecay(12f) });

        var decay = Assert.IsType<ExponentialDecay>(reloaded.Mouse.Decay);
        Assert.Equal(12f, decay.Rate, precision: 4);
    }

    [Fact]
    public void LinearDecay_PreservesSpeed()
    {
        var reloaded = SaveAndReload(new MouseSettings { Decay = new LinearDecay(1f) });

        var decay = Assert.IsType<LinearDecay>(reloaded.Mouse.Decay);
        Assert.Equal(1f, decay.UnitsPerSecond, precision: 4);

        // Comparación de comportamiento: con el bug, el rate 1 pasaba a 4 y el stick volvía al centro
        // cuatro veces más rápido, una sensación completamente distinta.
        Assert.Equal(0.5f, decay.Decay(1f, 0f, 0.5f).X, precision: 4);
    }

    [Fact]
    public void RepeatedSaves_DoNotDriftParameters()
    {
        var profile = DefaultProfile.Create("Drift") with
        {
            Mouse = new MouseSettings { ResponseCurve = new PowerCurve(3.5f), Decay = new ExponentialDecay(12f) },
        };

        _repository.Save(profile);
        for (var i = 0; i < 3; i++)
        {
            _repository.Save(_repository.Load(profile.Id));
        }

        var final = _repository.Load(profile.Id);
        Assert.Equal(3.5f, Assert.IsType<PowerCurve>(final.Mouse.ResponseCurve).Exponent, precision: 4);
        Assert.Equal(12f, Assert.IsType<ExponentialDecay>(final.Mouse.Decay).Rate, precision: 4);
    }
}
