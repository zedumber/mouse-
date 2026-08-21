using CommunityToolkit.Mvvm.ComponentModel;
using VirtualController.Core.Mapping;
using DomainBinding = VirtualController.Core.Mapping.Binding;

namespace VirtualController.App.ViewModels;

/// <summary>
/// Una fila del editor. Envuelve las opciones del catálogo para que los desplegables puedan mostrar
/// texto legible sin que la vista tenga que conocer los tipos del dominio.
/// </summary>
public sealed partial class BindingRowViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public BindingRowViewModel(DomainBinding binding, Action onChanged)
    {
        _onChanged = onChanged;
        _input = InputOption.For(binding.Input);
        _output = OutputOption.For(binding.Output);
    }

    [ObservableProperty]
    private InputOption _input;

    [ObservableProperty]
    private OutputOption _output;

    public DomainBinding ToBinding() => new(Input.Value, Output.Value);

    partial void OnInputChanged(InputOption value) => _onChanged();

    partial void OnOutputChanged(OutputOption value) => _onChanged();
}

/// <summary>
/// Opción seleccionable en un desplegable. Se comparan por el valor de dominio para que WPF pueda
/// preseleccionar el elemento correcto aunque la instancia mostrada sea otra.
/// </summary>
public sealed class InputOption : IEquatable<InputOption>
{
    private static readonly Dictionary<PhysicalInput, InputOption> Cache =
        BindingCatalog.AvailableInputs.ToDictionary(i => i, i => new InputOption(i));

    public static IReadOnlyList<InputOption> All { get; } = Cache.Values.ToArray();

    private InputOption(PhysicalInput value)
    {
        Value = value;
        Display = BindingCatalog.Describe(value);
    }

    public PhysicalInput Value { get; }

    public string Display { get; }

    public static InputOption For(PhysicalInput input) =>
        Cache.TryGetValue(input, out var option) ? option : new InputOption(input);

    public bool Equals(InputOption? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as InputOption);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Display;
}

public sealed class OutputOption : IEquatable<OutputOption>
{
    private static readonly OutputOption[] AllOptions =
        BindingCatalog.AvailableOutputs.Select(o => new OutputOption(o)).ToArray();

    public static IReadOnlyList<OutputOption> All => AllOptions;

    private OutputOption(VirtualOutput value)
    {
        Value = value;
        Display = BindingCatalog.Describe(value);
    }

    public VirtualOutput Value { get; }

    public string Display { get; }

    public static OutputOption For(VirtualOutput output) =>
        AllOptions.FirstOrDefault(o => o.Value == output) ?? new OutputOption(output);

    public bool Equals(OutputOption? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as OutputOption);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Display;
}
