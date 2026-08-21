using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VirtualController.Core.Mapping;
using VirtualController.Core.Profiles;

namespace VirtualController.App.ViewModels;

/// <summary>
/// Editor de bindings (requisitos 14 y 17). Toda la lógica de edición vive en <see cref="BindingEditor"/>;
/// aquí solo se traduce a algo que WPF pueda mostrar y se decide qué se le comunica al usuario.
/// </summary>
public sealed partial class BindingEditorViewModel : ObservableObject
{
    private readonly BindingEditor _editor;
    private readonly Action<Profile>? _onSaved;
    private bool _suspendSync;

    public BindingEditorViewModel(BindingEditor editor, Action<Profile>? onSaved = null)
    {
        _editor = editor;
        _onSaved = onSaved;

        Rows = new ObservableCollection<BindingRowViewModel>(
            editor.Bindings.Select(b => new BindingRowViewModel(b, OnRowChanged)));

        Revalidate();
    }

    public ObservableCollection<BindingRowViewModel> Rows { get; }

    public IReadOnlyList<InputOption> AvailableInputs => InputOption.All;

    public IReadOnlyList<OutputOption> AvailableOutputs => OutputOption.All;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>Guardar solo tiene sentido si hay cambios y el conjunto es válido.</summary>
    public bool CanSave => HasUnsavedChanges && ValidationError is null;

    [RelayCommand]
    private void AddBinding()
    {
        // Se añade una fila con la primera opción disponible; el usuario la ajusta con los
        // desplegables. Empezar con algo válido evita tener que representar una fila "vacía".
        var binding = new Binding(InputOption.All[0].Value, OutputOption.All[0].Value);

        _editor.Add(binding);
        Rows.Add(new BindingRowViewModel(binding, OnRowChanged));

        StatusMessage = null;
        Revalidate();
    }

    [RelayCommand]
    private void RemoveBinding(BindingRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var index = Rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        Rows.RemoveAt(index);
        _editor.RemoveAt(index);

        StatusMessage = null;
        Revalidate();
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            var saved = _editor.Save();

            StatusMessage = "Cambios guardados.";
            ValidationError = null;
            HasUnsavedChanges = false;
            OnPropertyChanged(nameof(CanSave));

            // El motor recibe la configuración nueva; aplicarla es responsabilidad de quien nos
            // construyó, no del editor.
            _onSaved?.Invoke(saved);
        }
        catch (ProfileException ex)
        {
            // Los errores de dominio se muestran; el perfil en disco no se ha tocado.
            ValidationError = ex.Message;
            StatusMessage = null;
        }
    }

    [RelayCommand]
    private void Discard()
    {
        _editor.Reset();

        _suspendSync = true;
        Rows.Clear();
        foreach (var binding in _editor.Bindings)
        {
            Rows.Add(new BindingRowViewModel(binding, OnRowChanged));
        }

        _suspendSync = false;

        StatusMessage = "Cambios descartados.";
        Revalidate();
    }

    /// <summary>
    /// Vuelca las filas al editor cuando el usuario cambia un desplegable. Se reconstruye la lista
    /// completa en vez de rastrear qué fila cambió: son unas pocas decenas de elementos y ocurre solo
    /// al interactuar, así que la simplicidad vale más que el ahorro.
    /// </summary>
    private void OnRowChanged()
    {
        if (_suspendSync)
        {
            return;
        }

        for (var i = 0; i < Rows.Count; i++)
        {
            _editor.Replace(i, Rows[i].ToBinding());
        }

        StatusMessage = null;
        Revalidate();
    }

    private void Revalidate()
    {
        ValidationError = _editor.Validate();
        HasUnsavedChanges = _editor.HasUnsavedChanges;
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnValidationErrorChanged(string? value) => OnPropertyChanged(nameof(CanSave));

    partial void OnHasUnsavedChangesChanged(bool value) => OnPropertyChanged(nameof(CanSave));
}
