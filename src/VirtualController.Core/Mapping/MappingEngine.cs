using VirtualController.Core.Gamepad;

namespace VirtualController.Core.Mapping;

public readonly record struct MappingOptions(bool NormalizeDiagonal);

public static class MappingEngine
{
    private static readonly GamepadButton[] AllButtons = Enum.GetValues<GamepadButton>();
    private static readonly GamepadAxis[] AllAxes = Enum.GetValues<GamepadAxis>();

    public static GamepadState Resolve(
        IReadOnlyCollection<Binding> bindings,
        IReadOnlySet<PhysicalInput> activeInputs,
        MappingOptions options)
    {
        var (leftX, leftY) = BindingResolver.ResolveStickDirections(bindings, activeInputs, Stick.Left, options.NormalizeDiagonal);
        var (rightX, rightY) = BindingResolver.ResolveStickDirections(bindings, activeInputs, Stick.Right, options.NormalizeDiagonal);

        var state = GamepadState.Neutral with
        {
            LeftStickX = leftX,
            LeftStickY = leftY,
            RightStickX = rightX,
            RightStickY = rightY
        };

        foreach (var button in AllButtons)
        {
            state = state.WithButton(button, BindingResolver.ResolveButton(bindings, activeInputs, button));
        }

        foreach (var axis in AllAxes)
        {
            state = state.WithAxis(axis, BindingResolver.ResolveAxis(bindings, activeInputs, axis));
        }

        // StickVector (movimiento del mouse) todavía no se resuelve aquí: depende de
        // MouseToStickConverter, que se incorpora en un incremento posterior de la Fase 1 (ADR-006 punto 7).
        return state;
    }
}
