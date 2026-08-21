using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly Profile _profile;

    public MainViewModel(
        EmulationService emulation,
        Profile profile,
        string backendName,
        BindingEditorViewModel bindingEditor,
        MouseSettingsViewModel mouseSettings)
    {
        _emulation = emulation;
        _profile = profile;
        BackendName = backendName;
        ProfileName = profile.Name;
        BindingEditor = bindingEditor;
        MouseSettings = mouseSettings;
    }

    public BindingEditorViewModel BindingEditor { get; }

    public MouseSettingsViewModel MouseSettings { get; }

    public string BackendName { get; }

    public string ProfileName { get; }

    [ObservableProperty]
    private string _status = "Detenido";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string? _errorMessage;

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
            return;
        }

        IsRunning = true;
        Status = "Emulando";
        OnPropertyChanged(nameof(StartStopLabel));
    }

    public void StopEmulation()
    {
        _emulation.Stop();
        IsRunning = false;
        Status = "Detenido";
        Snapshot = GamepadSnapshot.Neutral;
        OnPropertyChanged(nameof(StartStopLabel));
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        _emulation.RequestEmergencyStop();
        StopEmulation();
        Status = "Parada de emergencia";
    }

    /// <summary>Llamado por la vista a ~60 Hz: la UI marca su propio ritmo, no el motor.</summary>
    public void RefreshFromEngine()
    {
        Snapshot = _emulation.Snapshot;

        var metrics = _emulation.Metrics;
        var text = $"eventos: {metrics.InputEventsProcessed}   envíos: {metrics.GamepadSubmits}   " +
                   $"descartados: {metrics.DroppedInputEvents}   " +
                   $"media: {metrics.AverageProcessingMicroseconds:F1} µs   " +
                   $"máx: {metrics.MaximumProcessingMicroseconds:F1} µs";

        // Solo se asigna si cambió: el setter notifica a WPF, y esto corre en cada frame.
        if (text != MetricsText)
        {
            MetricsText = text;
        }
    }

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
