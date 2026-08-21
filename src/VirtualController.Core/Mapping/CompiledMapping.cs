namespace VirtualController.Core.Mapping;

using VirtualController.Core.Gamepad;

public sealed class CompiledMapping
{
    private readonly IReadOnlyList<Binding> _bindings;
    private readonly MappingOptions _options;

    private CompiledMapping(IReadOnlyList<Binding> bindings, MappingOptions options, Stick? mouseStick)
    {
        _bindings = bindings;
        _options = options;
        MouseStick = mouseStick;
    }

    /// <summary>
    /// Stick alimentado por el movimiento del mouse, resuelto al compilar para no tener que buscarlo
    /// en cada iteración. Null si el perfil no mapea el mouse a ningún stick.
    /// </summary>
    public Stick? MouseStick { get; }

    public static CompiledMapping Compile(IReadOnlyCollection<Binding> bindings, MappingOptions options)
    {
        MappingValidator.EnsureNoStickConflicts(bindings);

        var mouseStick = bindings
            .Select(b => b.Output)
            .OfType<VirtualOutput.StickVector>()
            .Select(v => (Stick?)v.Stick)
            .FirstOrDefault();

        return new CompiledMapping(bindings.ToArray(), options, mouseStick);
    }

    public GamepadState Resolve(IReadOnlySet<PhysicalInput> activeInputs) =>
        MappingEngine.Resolve(_bindings, activeInputs, _options);
}
