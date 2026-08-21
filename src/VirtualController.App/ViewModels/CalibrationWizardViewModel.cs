using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VirtualController.App.ViewModels;

/// <summary>
/// Guía una calibración reproducible dentro del juego. No intenta leer memoria ni automatizar el
/// juego: el usuario observa cuándo empieza a moverse la cámara y el asistente traduce esa medida al
/// perfil activo.
/// </summary>
public sealed partial class CalibrationWizardViewModel : ObservableObject
{
    private readonly MouseSettingsViewModel _mouseSettings;
    private readonly Action _resetMetrics;
    private readonly Action<string>? _onCompleted;

    public CalibrationWizardViewModel(
        MouseSettingsViewModel mouseSettings,
        Action resetMetrics,
        Action<string>? onCompleted = null)
    {
        _mouseSettings = mouseSettings;
        _resetMetrics = resetMetrics;
        _onCompleted = onCompleted;
        CountsForFullDeflection = mouseSettings.CountsForFullDeflection;
        AdsSensitivityMultiplier = mouseSettings.AdsSensitivityMultiplier;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecommendedAntiDeadzone))]
    private float _observedGameDeadzone = 0.04f;

    [ObservableProperty]
    private float _countsForFullDeflection;

    [ObservableProperty]
    private float _adsSensitivityMultiplier;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    [NotifyPropertyChangedFor(nameof(Instructions))]
    [NotifyPropertyChangedFor(nameof(ShowDeadzoneStep))]
    [NotifyPropertyChangedFor(nameof(ShowNormalSensitivityStep))]
    [NotifyPropertyChangedFor(nameof(ShowAdsStep))]
    [NotifyPropertyChangedFor(nameof(ShowMeasurementStep))]
    [NotifyPropertyChangedFor(nameof(IsStarted))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyAndSaveCommand))]
    private int _currentStep;

    [ObservableProperty]
    private string? _statusMessage;

    public float RecommendedAntiDeadzone => Math.Clamp(ObservedGameDeadzone + 0.01f, 0f, 0.3f);

    public bool IsStarted => CurrentStep > 0;

    public bool ShowDeadzoneStep => CurrentStep == 1;

    public bool ShowNormalSensitivityStep => CurrentStep == 2;

    public bool ShowAdsStep => CurrentStep == 3;

    public bool ShowMeasurementStep => CurrentStep == 4;

    public string StepTitle => CurrentStep switch
    {
        1 => "1/4 · Anti-deadzone",
        2 => "2/4 · Sensibilidad normal",
        3 => "3/4 · Sensibilidad ADS",
        4 => "4/4 · Medición",
        _ => "Calibración por juego",
    };

    public string Instructions => CurrentStep switch
    {
        1 => "Entra en una zona segura. Sube lentamente el valor observado hasta que la cámara apenas empiece a moverse sin saltos.",
        2 => "Haz barridos normales. Menos counts da más velocidad; más counts da más precisión. Busca poder girar sin saturar constantemente.",
        3 => "Apunta con ADS y ajusta el multiplicador. 0,50 es preciso; 0,75 conserva más velocidad.",
        4 => "Reinicia la medición y juega 30 segundos. Comprueba latencia, saturación y jitter; después aplica y guarda.",
        _ => "Aplica el preset Equilibrado y recorre cuatro comprobaciones dentro del juego. Los cambios quedan en el perfil seleccionado.",
    };

    [RelayCommand]
    private void Start()
    {
        _mouseSettings.ApplyBalancedAimPresetCommand.Execute(null);
        CountsForFullDeflection = _mouseSettings.CountsForFullDeflection;
        AdsSensitivityMultiplier = _mouseSettings.AdsSensitivityMultiplier;
        _resetMetrics();
        CurrentStep = 1;
        StatusMessage = "Preset Equilibrado aplicado. Inicia la prueba en el juego.";
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        CurrentStep--;
        StatusMessage = null;
    }

    private bool CanGoPrevious() => CurrentStep > 1;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        ApplyCurrentValues();
        CurrentStep++;
        StatusMessage = CurrentStep == 4
            ? "Valores aplicados. Juega 30 segundos para obtener una muestra estable."
            : "Valor aplicado en vivo; continúa con el siguiente paso.";

        if (CurrentStep == 4)
        {
            _resetMetrics();
        }
    }

    private bool CanGoNext() => CurrentStep is >= 1 and < 4;

    [RelayCommand(CanExecute = nameof(CanApplyAndSave))]
    private void ApplyAndSave()
    {
        ApplyCurrentValues();
        _mouseSettings.SaveCommand.Execute(null);
        StatusMessage = "Calibración aplicada y guardada en el perfil.";
        _onCompleted?.Invoke(StatusMessage);
    }

    private bool CanApplyAndSave() => CurrentStep == 4;

    private void ApplyCurrentValues() => _mouseSettings.ApplyCalibration(
        RecommendedAntiDeadzone,
        CountsForFullDeflection,
        AdsSensitivityMultiplier);
}
