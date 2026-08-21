using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VirtualController.Core.Mouse;
using VirtualController.Core.Profiles;

namespace VirtualController.App.ViewModels;

/// <summary>
/// Ajustes de mouse editables desde la interfaz (requisito 17). Cada cambio se aplica al motor al
/// instante para poder afinar la sensibilidad viendo el efecto en el visualizador, mientras que
/// guardar en el perfil es un paso aparte y explícito: probar no debería sobrescribir tu configuración.
/// </summary>
public sealed partial class MouseSettingsViewModel : ObservableObject
{
    private readonly IProfileRepository _repository;
    private readonly Action<MouseSettings>? _onApplied;

    private Profile _profile;
    private bool _suspendApply;

    public MouseSettingsViewModel(
        IProfileRepository repository,
        Profile profile,
        Action<MouseSettings>? onApplied = null)
    {
        _repository = repository;
        _profile = profile;
        _onApplied = onApplied;

        LoadFrom(profile.Mouse);
    }

    public IReadOnlyList<ResponseCurveKind> AvailableCurves { get; } = Enum.GetValues<ResponseCurveKind>();

    public IReadOnlyList<DecayKind> AvailableDecays { get; } = Enum.GetValues<DecayKind>();

    [ObservableProperty]
    private float _sensitivityX = 1f;

    [ObservableProperty]
    private float _sensitivityY = 1f;

    [ObservableProperty]
    private float _countsForFullDeflection = 100f;

    [ObservableProperty]
    private bool _invertX;

    [ObservableProperty]
    private bool _invertY;

    [ObservableProperty]
    private float _deadzoneInner;

    [ObservableProperty]
    private float _deadzoneOuter = 1f;

    [ObservableProperty]
    private float _outputScale = 1f;

    [ObservableProperty]
    private float _outputAntiDeadzone;

    [ObservableProperty]
    private float _maximumOutput = 1f;

    [ObservableProperty]
    private float _acceleration;

    [ObservableProperty]
    private bool _smoothingEnabled;

    [ObservableProperty]
    private float _smoothingStrength;

    [ObservableProperty]
    private bool _adaptiveSmoothingEnabled;

    [ObservableProperty]
    private float _adaptiveSmoothingResponsiveness = 2f;

    [ObservableProperty]
    private float _decayDelayMilliseconds;

    [ObservableProperty]
    private bool _adsEnabled;

    [ObservableProperty]
    private float _adsSensitivityMultiplier = 0.65f;

    [ObservableProperty]
    private float _adsMaximumOutput = 0.8f;

    [ObservableProperty]
    private float _adsPrecisionExponent = 1.2f;

    [ObservableProperty]
    private ResponseCurveKind _curveKind = ResponseCurveKind.Linear;

    [ObservableProperty]
    private float _curveParameter = MouseSettingsDraft.DefaultPowerExponent;

    [ObservableProperty]
    private float _curveSecondaryParameter = MouseSettingsDraft.DefaultDualZoneTurnExponent;

    [ObservableProperty]
    private float _curveTransition = MouseSettingsDraft.DefaultDualZoneTransition;

    [ObservableProperty]
    private DecayKind _decayKind = DecayKind.Immediate;

    [ObservableProperty]
    private float _decaySpeed = MouseSettingsDraft.DefaultExponentialDecaySpeed;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>El parámetro de curva solo tiene sentido si la curva lo usa.</summary>
    public bool CurveParameterApplies => CurveKind != ResponseCurveKind.Linear;

    public bool DualZoneParametersApply => CurveKind == ResponseCurveKind.DualZone;

    public bool DecaySpeedApplies => DecayKind != DecayKind.Immediate;

    public string CurveParameterLabel => CurveKind switch
    {
        ResponseCurveKind.Power => "Exponente",
        ResponseCurveKind.Exponential => "Intensidad",
        ResponseCurveKind.DualZone => "Exponente precisión",
        _ => "Parámetro",
    };

    public bool CanSave => HasUnsavedChanges && ValidationError is null;

    [RelayCommand]
    private void Save()
    {
        if (!BuildDraft().TryBuild(out var settings, out var error))
        {
            ValidationError = error;
            return;
        }

        var updated = _profile with { Mouse = settings };

        try
        {
            _repository.Save(updated);
        }
        catch (ProfileException ex)
        {
            ValidationError = ex.Message;
            StatusMessage = null;
            return;
        }

        _profile = updated;
        HasUnsavedChanges = false;
        StatusMessage = "Ajustes guardados en el perfil.";
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>Vuelve a lo guardado en el perfil, descartando lo que se estuviera probando.</summary>
    [RelayCommand]
    private void Revert()
    {
        LoadFrom(_profile.Mouse);
        ApplyLive();

        StatusMessage = "Ajustes restaurados.";
        HasUnsavedChanges = false;
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        LoadFrom(new MouseSettings());
        MarkChangedAndApply();
        StatusMessage = "Valores por defecto cargados (sin guardar).";
    }

    [RelayCommand]
    private void ApplyBalancedAimPreset() => ApplyAimPreset(
        antiDeadzone: 0.05f,
        smoothingStrength: 0.45f,
        responsiveness: 2f,
        decayDelayMs: 8f,
        adsSensitivity: 0.65f,
        adsMaximum: 0.8f,
        adsExponent: 1.2f);

    [RelayCommand]
    private void ApplyPrecisionAimPreset() => ApplyAimPreset(
        antiDeadzone: 0.03f,
        smoothingStrength: 0.6f,
        responsiveness: 1.5f,
        decayDelayMs: 10f,
        adsSensitivity: 0.5f,
        adsMaximum: 0.7f,
        adsExponent: 1.4f);

    [RelayCommand]
    private void ApplyResponsiveAimPreset() => ApplyAimPreset(
        antiDeadzone: 0.06f,
        smoothingStrength: 0.25f,
        responsiveness: 4f,
        decayDelayMs: 4f,
        adsSensitivity: 0.75f,
        adsMaximum: 0.9f,
        adsExponent: 1.1f);

    private void ApplyAimPreset(
        float antiDeadzone,
        float smoothingStrength,
        float responsiveness,
        float decayDelayMs,
        float adsSensitivity,
        float adsMaximum,
        float adsExponent)
    {
        _suspendApply = true;
        CurveKind = ResponseCurveKind.DualZone;
        CurveParameter = MouseSettingsDraft.DefaultDualZonePrecisionExponent;
        CurveSecondaryParameter = MouseSettingsDraft.DefaultDualZoneTurnExponent;
        CurveTransition = MouseSettingsDraft.DefaultDualZoneTransition;
        OutputScale = 1f;
        OutputAntiDeadzone = antiDeadzone;
        MaximumOutput = 1f;
        SmoothingEnabled = true;
        SmoothingStrength = smoothingStrength;
        AdaptiveSmoothingEnabled = true;
        AdaptiveSmoothingResponsiveness = responsiveness;
        DecayDelayMilliseconds = decayDelayMs;
        AdsEnabled = true;
        AdsSensitivityMultiplier = adsSensitivity;
        AdsMaximumOutput = adsMaximum;
        AdsPrecisionExponent = adsExponent;
        _suspendApply = false;

        OnPropertyChanged(nameof(CurveParameterApplies));
        OnPropertyChanged(nameof(DualZoneParametersApply));
        OnPropertyChanged(nameof(CurveParameterLabel));
        MarkChangedAndApply();
        StatusMessage = "Preset cargado. Ajusta la anti-deadzone al valor del juego y guarda cuando esté listo.";
    }

    /// <summary>Aplica de una vez los tres valores obtenidos por el asistente, sin estados intermedios.</summary>
    public void ApplyCalibration(float antiDeadzone, float countsForFullDeflection, float adsSensitivity)
    {
        if (!float.IsFinite(antiDeadzone) || antiDeadzone < 0f || antiDeadzone > 0.3f)
        {
            throw new ArgumentOutOfRangeException(nameof(antiDeadzone));
        }

        if (!float.IsFinite(countsForFullDeflection) || countsForFullDeflection < 20f || countsForFullDeflection > 2000f)
        {
            throw new ArgumentOutOfRangeException(nameof(countsForFullDeflection));
        }

        if (!float.IsFinite(adsSensitivity) || adsSensitivity < 0.2f || adsSensitivity > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(adsSensitivity));
        }

        _suspendApply = true;
        OutputAntiDeadzone = antiDeadzone;
        CountsForFullDeflection = countsForFullDeflection;
        AdsEnabled = true;
        AdsSensitivityMultiplier = adsSensitivity;
        _suspendApply = false;

        MarkChangedAndApply();
        StatusMessage = "Calibración aplicada (pendiente de guardar).";
    }

    private MouseSettingsDraft BuildDraft() => new()
    {
        CountsForFullDeflection = CountsForFullDeflection,
        SensitivityX = SensitivityX,
        SensitivityY = SensitivityY,
        InvertX = InvertX,
        InvertY = InvertY,
        DeadzoneInner = DeadzoneInner,
        DeadzoneOuter = DeadzoneOuter,
        OutputScale = OutputScale,
        OutputAntiDeadzone = OutputAntiDeadzone,
        MaximumOutput = MaximumOutput,
        Acceleration = Acceleration,
        SmoothingEnabled = SmoothingEnabled,
        SmoothingStrength = SmoothingStrength,
        AdaptiveSmoothingEnabled = AdaptiveSmoothingEnabled,
        AdaptiveSmoothingResponsiveness = AdaptiveSmoothingResponsiveness,
        DecayDelayMilliseconds = DecayDelayMilliseconds,
        AdsEnabled = AdsEnabled,
        AdsSensitivityMultiplier = AdsSensitivityMultiplier,
        AdsMaximumOutput = AdsMaximumOutput,
        AdsPrecisionExponent = AdsPrecisionExponent,
        CurveKind = CurveKind,
        CurveParameter = CurveParameter,
        CurveSecondaryParameter = CurveSecondaryParameter,
        CurveTransition = CurveTransition,
        DecayKind = DecayKind,
        DecaySpeed = DecaySpeed,
    };

    private void LoadFrom(MouseSettings settings)
    {
        // Se suspende la aplicación en vivo mientras se cargan los valores: si no, cada asignación
        // dispararía un envío al motor con un estado a medio construir.
        _suspendApply = true;

        var draft = new MouseSettingsDraft(settings);

        CountsForFullDeflection = draft.CountsForFullDeflection;
        SensitivityX = draft.SensitivityX;
        SensitivityY = draft.SensitivityY;
        InvertX = draft.InvertX;
        InvertY = draft.InvertY;
        DeadzoneInner = draft.DeadzoneInner;
        DeadzoneOuter = draft.DeadzoneOuter;
        OutputScale = draft.OutputScale;
        OutputAntiDeadzone = draft.OutputAntiDeadzone;
        MaximumOutput = draft.MaximumOutput;
        Acceleration = draft.Acceleration;
        SmoothingEnabled = draft.SmoothingEnabled;
        SmoothingStrength = draft.SmoothingStrength;
        AdaptiveSmoothingEnabled = draft.AdaptiveSmoothingEnabled;
        AdaptiveSmoothingResponsiveness = draft.AdaptiveSmoothingResponsiveness;
        DecayDelayMilliseconds = draft.DecayDelayMilliseconds;
        AdsEnabled = draft.AdsEnabled;
        AdsSensitivityMultiplier = draft.AdsSensitivityMultiplier;
        AdsMaximumOutput = draft.AdsMaximumOutput;
        AdsPrecisionExponent = draft.AdsPrecisionExponent;
        CurveKind = draft.CurveKind;
        CurveParameter = draft.CurveParameter;
        CurveSecondaryParameter = draft.CurveSecondaryParameter;
        CurveTransition = draft.CurveTransition;
        DecayKind = draft.DecayKind;
        DecaySpeed = draft.DecaySpeed;

        _suspendApply = false;

        ValidationError = null;
        OnPropertyChanged(nameof(CurveParameterApplies));
        OnPropertyChanged(nameof(DualZoneParametersApply));
        OnPropertyChanged(nameof(DecaySpeedApplies));
        OnPropertyChanged(nameof(CurveParameterLabel));
    }

    private void MarkChangedAndApply()
    {
        if (_suspendApply)
        {
            return;
        }

        HasUnsavedChanges = true;
        StatusMessage = null;
        ApplyLive();
    }

    /// <summary>
    /// Envía los ajustes al motor si son válidos. Si no lo son se muestra el motivo y NO se aplica:
    /// el motor sigue con la última configuración que funcionaba, en vez de quedarse sin respuesta
    /// mientras el usuario arrastra un deslizador por una zona inválida.
    /// </summary>
    private void ApplyLive()
    {
        if (BuildDraft().TryBuild(out var settings, out var error))
        {
            ValidationError = null;
            _onApplied?.Invoke(settings);
        }
        else
        {
            ValidationError = error;
        }

        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnSensitivityXChanged(float value) => MarkChangedAndApply();

    partial void OnSensitivityYChanged(float value) => MarkChangedAndApply();

    partial void OnCountsForFullDeflectionChanged(float value) => MarkChangedAndApply();

    partial void OnInvertXChanged(bool value) => MarkChangedAndApply();

    partial void OnInvertYChanged(bool value) => MarkChangedAndApply();

    partial void OnDeadzoneInnerChanged(float value) => MarkChangedAndApply();

    partial void OnDeadzoneOuterChanged(float value) => MarkChangedAndApply();

    partial void OnOutputScaleChanged(float value) => MarkChangedAndApply();

    partial void OnOutputAntiDeadzoneChanged(float value) => MarkChangedAndApply();

    partial void OnMaximumOutputChanged(float value) => MarkChangedAndApply();

    partial void OnAccelerationChanged(float value) => MarkChangedAndApply();

    partial void OnSmoothingEnabledChanged(bool value) => MarkChangedAndApply();

    partial void OnSmoothingStrengthChanged(float value) => MarkChangedAndApply();

    partial void OnAdaptiveSmoothingEnabledChanged(bool value) => MarkChangedAndApply();

    partial void OnAdaptiveSmoothingResponsivenessChanged(float value) => MarkChangedAndApply();

    partial void OnDecayDelayMillisecondsChanged(float value) => MarkChangedAndApply();

    partial void OnAdsEnabledChanged(bool value) => MarkChangedAndApply();

    partial void OnAdsSensitivityMultiplierChanged(float value) => MarkChangedAndApply();

    partial void OnAdsMaximumOutputChanged(float value) => MarkChangedAndApply();

    partial void OnAdsPrecisionExponentChanged(float value) => MarkChangedAndApply();

    partial void OnCurveParameterChanged(float value) => MarkChangedAndApply();

    partial void OnCurveSecondaryParameterChanged(float value) => MarkChangedAndApply();

    partial void OnCurveTransitionChanged(float value) => MarkChangedAndApply();

    partial void OnDecaySpeedChanged(float value) => MarkChangedAndApply();

    partial void OnCurveKindChanged(ResponseCurveKind value)
    {
        // El parámetro se reinicia al cambiar de curva: un exponente de 2 y una intensidad
        // exponencial de 2 no significan lo mismo, así que arrastrarlo daría un resultado inesperado.
        if (!_suspendApply)
        {
            _suspendApply = true;
            CurveParameter = MouseSettingsDraft.DefaultParameterFor(value);
            _suspendApply = false;
        }

        OnPropertyChanged(nameof(CurveParameterApplies));
        OnPropertyChanged(nameof(DualZoneParametersApply));
        OnPropertyChanged(nameof(CurveParameterLabel));
        MarkChangedAndApply();
    }

    partial void OnDecayKindChanged(DecayKind value)
    {
        if (!_suspendApply)
        {
            _suspendApply = true;
            DecaySpeed = MouseSettingsDraft.DefaultSpeedFor(value);
            _suspendApply = false;
        }

        OnPropertyChanged(nameof(DecaySpeedApplies));
        MarkChangedAndApply();
    }

    partial void OnValidationErrorChanged(string? value) => OnPropertyChanged(nameof(CanSave));

    partial void OnHasUnsavedChangesChanged(bool value) => OnPropertyChanged(nameof(CanSave));
}
