using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VirtualController.Core.Profiles;

namespace VirtualController.App.ViewModels;

public sealed partial class ApplicationSettingsViewModel : ObservableObject
{
    private readonly IApplicationSettingsRepository _repository;

    public ApplicationSettingsViewModel(IApplicationSettingsRepository repository, ApplicationSettings settings)
    {
        _repository = repository;
        StartStop = settings.StartStop;
        ToggleMouseCapture = settings.ToggleMouseCapture;
        EmergencyStop = settings.EmergencyStop;
        StartMinimized = settings.StartMinimized;
        MinimizeToTray = settings.MinimizeToTray;
    }

    public event Action<ApplicationSettings>? Saved;

    [ObservableProperty]
    private string _startStop = string.Empty;

    [ObservableProperty]
    private string _toggleMouseCapture = string.Empty;

    [ObservableProperty]
    private string _emergencyStop = string.Empty;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private void Save()
    {
        try
        {
            // Recargar conserva el perfil seleccionado y opciones que esta pantalla no edita.
            var updated = _repository.Load() with
            {
                StartStop = StartStop.Trim(),
                ToggleMouseCapture = ToggleMouseCapture.Trim(),
                EmergencyStop = EmergencyStop.Trim(),
                StartMinimized = StartMinimized,
                MinimizeToTray = MinimizeToTray,
            };

            updated.Validate();
            _repository.Save(updated);
            ValidationError = null;
            StatusMessage = "Configuración guardada y hotkeys registrados.";
            Saved?.Invoke(updated);
        }
        catch (ProfileException ex)
        {
            ValidationError = ex.Message;
            StatusMessage = null;
        }
    }
}
