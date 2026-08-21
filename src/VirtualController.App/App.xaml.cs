using System.Windows;
using VirtualController.App.Composition;
using VirtualController.App.ViewModels;
using VirtualController.Core.Diagnostics;
using VirtualController.Core.Engine;
using VirtualController.Core.Input;
using VirtualController.Core.Profiles;
using VirtualController.Infrastructure.Diagnostics;
using VirtualController.Infrastructure.Persistence;
using VirtualController.Windows.RawInput;

namespace VirtualController.App;

public partial class App : System.Windows.Application
{
    private EmulationService? _emulation;
    private IAppLogger _logger = NullAppLogger.Instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Una excepción no controlada nunca debe dejar el mando virtual con teclas pegadas ni el
        // cursor capturado (requisito 20).
        DispatcherUnhandledException += (_, args) =>
        {
            _logger.Error("unhandled-ui-exception", "La UI produjo una excepción no controlada.", args.Exception);
            ShutdownEmulationSafely();
            System.Windows.MessageBox.Show(
                $"Se produjo un error inesperado y la emulación se detuvo:\n\n{args.Exception.Message}",
                "Virtual Controller",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            args.Handled = true;
        };

        var paths = new StoragePaths();
        _logger = CreateLogger(paths);
        _logger.Information("application-start", "Virtual Controller iniciado.");
        var repository = new JsonProfileRepository(paths);
        var profileService = new ProfileService(repository);
        var settingsRepository = new JsonApplicationSettingsRepository(paths);

        var settings = settingsRepository.Load();
        var profile = LoadOrCreateSelectedProfile(profileService, settingsRepository, settings);

        var gamepad = BackendResolver.Resolve(profile.ControllerType, settings.PreferredBackend);
        var queue = new ChannelInputEventQueue();

        void ReportError(Exception ex)
        {
            _logger.Error("emulation-failure", "El motor de entrada se detuvo.", ex);
            Dispatcher.Invoke(() => System.Windows.MessageBox.Show(
                $"El motor de entrada se detuvo por un error:\n\n{ex.Message}",
                "Virtual Controller",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error));
        }

        var inputSource = new RawInputHost(queue, ReportError);

        _emulation = new EmulationService(
            gamepad,
            inputSource,
            queue,
            profile,
            options: null,
            onFailure: ReportError,
            metricsEnabled: true,
            settings: settings);

        var viewModel = new MainViewModel(
            _emulation,
            profile,
            BackendResolver.DescribeBackend(settings.PreferredBackend),
            repository,
            profileService,
            settingsRepository,
            settings,
            name => System.Windows.MessageBox.Show(
                $"¿Eliminar el perfil \"{name}\"?",
                "Virtual Controller",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes,
            _logger);

        new MainWindow(viewModel, settings).Show();
    }

    /// <summary>
    /// Carga el perfil que el usuario seleccionó, no uno arbitrario. Antes se usaba el primero de la
    /// enumeración del sistema de archivos, cuyo orden no está especificado: con varios perfiles, la
    /// app podía abrir uno distinto en cada arranque.
    /// </summary>
    private static Profile LoadOrCreateSelectedProfile(
        ProfileService profileService,
        IApplicationSettingsRepository settingsRepository,
        ApplicationSettings settings)
    {
        var existing = profileService.List();

        if (existing.Count == 0)
        {
            var created = profileService.Create("Default");
            settingsRepository.Save(settings with { SelectedProfileId = created.Id });
            return created;
        }

        if (settings.SelectedProfileId is { } selected && existing.Any(p => p.Id == selected))
        {
            return profileService.Load(selected);
        }

        // El perfil seleccionado ya no existe (lo borraron fuera de la app): se cae al primero
        // disponible y se recuerda esa elección para que el siguiente arranque sea estable.
        var fallback = profileService.Load(existing[0].Id);
        settingsRepository.Save(settings with { SelectedProfileId = fallback.Id });
        return fallback;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger.Information("application-stop", "Virtual Controller finalizado.");
        ShutdownEmulationSafely();
        base.OnExit(e);
    }

    private static IAppLogger CreateLogger(StoragePaths paths)
    {
        try
        {
            return new JsonFileLogger(paths);
        }
        catch (System.IO.IOException)
        {
            return NullAppLogger.Instance;
        }
        catch (UnauthorizedAccessException)
        {
            return NullAppLogger.Instance;
        }
    }

    private void ShutdownEmulationSafely()
    {
        try
        {
            _emulation?.Dispose();
        }
        catch
        {
            // Ya estamos cerrando: relanzar aquí solo ocultaría el error original.
        }
        finally
        {
            _emulation = null;
        }
    }
}
