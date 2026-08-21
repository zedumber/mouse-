using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VirtualController.Core.Diagnostics;
using VirtualController.Core.Engine;
using VirtualController.Core.Gamepad;
using VirtualController.Core.Profiles;

namespace VirtualController.App.ViewModels;

/// <summary>
/// ViewModel de la ventana principal. Deliberadamente libre de tipos de WPF en su superficie pública
/// (ADR-001): no conoce Raw Input ni el backend concreto, solo habla con EmulationService.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly EmulationService _emulation;
    private readonly IProfileRepository _repository;
    private readonly IApplicationSettingsRepository _settingsRepository;
    private readonly IAppLogger _logger;
    private ApplicationSettings _settings;
    private Profile _profile;

    public MainViewModel(
        EmulationService emulation,
        Profile profile,
        string backendName,
        IProfileRepository repository,
        ProfileService profileService,
        IApplicationSettingsRepository settingsRepository,
        ApplicationSettings settings,
        Func<string, bool>? confirmProfileDelete = null,
        IAppLogger? logger = null)
    {
        _emulation = emulation;
        _repository = repository;
        _settingsRepository = settingsRepository;
        _logger = logger ?? NullAppLogger.Instance;
        _settings = settings;
        _profile = profile;
        BackendName = backendName;
        ProfileName = profile.Name;
        BindingEditor = CreateBindingEditor(profile);
        MouseSettings = CreateMouseSettings(profile);
        Calibration = CreateCalibration(MouseSettings);
        Settings = new ApplicationSettingsViewModel(settingsRepository, settings);
        Settings.Saved += OnSettingsSaved;
        ProfileSelector = new ProfileSelectorViewModel(
            profileService,
            profile,
            SwitchProfile,
            confirmProfileDelete);
        LogFile = _logger.CurrentLogFile;
    }

    [ObservableProperty]
    private BindingEditorViewModel _bindingEditor;

    [ObservableProperty]
    private MouseSettingsViewModel _mouseSettings;

    [ObservableProperty]
    private CalibrationWizardViewModel _calibration;

    public ProfileSelectorViewModel ProfileSelector { get; }

    public ApplicationSettingsViewModel Settings { get; }

    public string BackendName { get; }

    public string LogFile { get; }

    [ObservableProperty]
    private string _profileName;

    [ObservableProperty]
    private string _status = "Detenido";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isMouseCaptureEnabled = true;

    // Estado del mando, refrescado a ~60 Hz desde el snapshot inmutable.
    [ObservableProperty]
    private GamepadSnapshot _snapshot = GamepadSnapshot.Neutral;

    [ObservableProperty]
    private string _metricsText = string.Empty;

    public string StartStopLabel => IsRunning ? "STOP EMULATION" : "START EMULATION";

    [RelayCommand]
    private void ToggleEmulation()
    {
        if (IsRunning)
        {
            StopEmulation();
            return;
        }

        ErrorMessage = null;
        var result = _emulation.Start();

        if (!result.IsConnected)
        {
            // Se traduce el resultado tipado a un mensaje accionable en vez de mostrar una excepción.
            ErrorMessage = DescribeFailure(result);
            Status = "No conectado";
            _logger.Warning("emulation-connect-failed", ErrorMessage);
            return;
        }

        IsRunning = true;
        Status = "Emulando";
        _logger.Information("emulation-start", $"Backend: {BackendName}; perfil: {_profile.Name}.");
        OnPropertyChanged(nameof(StartStopLabel));
    }

    public void StopEmulation()
    {
        var wasRunning = IsRunning;
        _emulation.Stop();
        IsRunning = false;
        Status = "Detenido";
        Snapshot = GamepadSnapshot.Neutral;
        if (wasRunning)
        {
            var metrics = _emulation.Metrics;
            _logger.Information(
                "emulation-stop",
                $"Perfil: {_profile.Name}; eventos: {metrics.InputEventsProcessed}; " +
                $"latencia input media/máx: {metrics.AverageInputLatencyMicroseconds:F1}/{metrics.MaximumInputLatencyMicroseconds:F1} us; " +
                $"saturación: {metrics.AimSaturationPercent:F1}%; " +
                $"jitter media/máx: {metrics.AverageSubmitJitterMicroseconds:F1}/{metrics.MaximumSubmitJitterMicroseconds:F1} us.");
        }
        OnPropertyChanged(nameof(StartStopLabel));
    }

    public void ReportConfigurationError(string message)
    {
        ErrorMessage = message;
        _logger.Warning("configuration-error", message);
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        _emulation.RequestEmergencyStop();
        StopEmulation();
        Status = "Parada de emergencia";
        _logger.Warning("emergency-stop", "Se ejecutó la parada de emergencia.");
    }

    [RelayCommand]
    public void ToggleMouseCapture()
    {
        IsMouseCaptureEnabled = !IsMouseCaptureEnabled;
        _emulation.SetMouseCaptureEnabled(IsMouseCaptureEnabled);
        Status = IsMouseCaptureEnabled ? "Mouse activado" : "Mouse pausado";
        _logger.Information("mouse-capture", Status);
    }

    /// <summary>Llamado por la vista a ~60 Hz: la UI marca su propio ritmo, no el motor.</summary>
    public void RefreshFromEngine()
    {
        Snapshot = _emulation.Snapshot;

        var metrics = _emulation.Metrics;
        var text = $"eventos: {metrics.InputEventsProcessed}   envíos: {metrics.GamepadSubmits}   " +
                   $"descartados: {metrics.DroppedInputEvents}   " +
                   $"media: {metrics.AverageProcessingMicroseconds:F1} µs   " +
                   $"máx: {metrics.MaximumProcessingMicroseconds:F1} µs\n" +
                   $"latencia input: {metrics.AverageInputLatencyMicroseconds:F1}/{metrics.MaximumInputLatencyMicroseconds:F1} µs (media/máx)   " +
                   $"saturación aim: {metrics.AimSaturationPercent:F1}%   " +
                   $"jitter envío: {metrics.AverageSubmitJitterMicroseconds:F1}/{metrics.MaximumSubmitJitterMicroseconds:F1} µs";

        // Solo se asigna si cambió: el setter notifica a WPF, y esto corre en cada frame.
        if (text != MetricsText)
        {
            MetricsText = text;
        }
    }

    [RelayCommand]
    private void ResetMetrics()
    {
        _emulation.ResetMetrics();
        MetricsText = string.Empty;
        _logger.Information("metrics-reset", $"Nueva sesión de medición para {_profile.Name}.");
    }

    private void SwitchProfile(Profile profile)
    {
        _profile = profile;
        ProfileName = profile.Name;
        BindingEditor = CreateBindingEditor(profile);
        MouseSettings = CreateMouseSettings(profile);
        Calibration = CreateCalibration(MouseSettings);
        _emulation.ChangeProfile(profile);

        _settings = _settingsRepository.Load() with { SelectedProfileId = profile.Id };
        _settingsRepository.Save(_settings);
        _logger.Information("profile-selected", $"Perfil activo: {profile.Name} ({profile.Id}).");
    }

    private void OnSettingsSaved(ApplicationSettings settings)
    {
        _settings = settings;
        if (VirtualController.Core.Mapping.HotkeyCombination.TryParse(settings.EmergencyStop, out var hotkey))
        {
            _emulation.SetEmergencyHotkey(hotkey);
        }

        _logger.Information("settings-saved", "Hotkeys globales y preferencias de bandeja actualizados.");
    }

    private BindingEditorViewModel CreateBindingEditor(Profile profile) => new(
        new BindingEditor(_repository, profile),
        onSaved: updated =>
        {
            _profile = updated;
            _emulation.ChangeProfile(updated);
        });

    private MouseSettingsViewModel CreateMouseSettings(Profile profile) => new(
        _repository,
        profile,
        onApplied: applied => _emulation.ChangeMouseSettings(applied));

    private CalibrationWizardViewModel CreateCalibration(MouseSettingsViewModel mouseSettings) => new(
        mouseSettings,
        resetMetrics: _emulation.ResetMetrics,
        onCompleted: message => _logger.Information("calibration-saved", $"{message} Perfil: {_profile.Name}."));

    // El nombre del backend viene inyectado, no hardcodeado: el mensaje mentiría si el backend
    // configurado fuese otro (el spike de Fase 3 contempla sustituir ViGEm).
    private string DescribeFailure(GamepadConnectionResult result) => result.Status switch
    {
        GamepadConnectionStatus.BackendNotInstalled =>
            $"No se encontró el driver del mando virtual ({BackendName}). Instálalo y vuelve a intentarlo.",
        GamepadConnectionStatus.BackendVersionIncompatible =>
            "La versión instalada del driver no es compatible. Actualízala.",
        GamepadConnectionStatus.ConnectionFailed =>
            "No se pudo conectar con el driver del mando virtual.",
        GamepadConnectionStatus.DeviceCreationFailed =>
            "El driver respondió pero no se pudo crear el mando virtual.",
        _ => result.Detail ?? "Error desconocido al conectar.",
    };
}
