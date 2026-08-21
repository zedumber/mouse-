namespace VirtualController.Core.Mapping;

using VirtualController.Core.Gamepad;

public static class BindingResolver
{
    public static (float X, float Y) ResolveStickDirections(
        IReadOnlyCollection<Binding> bindings,
        IReadOnlySet<PhysicalInput> activeInputs,
        Stick stick,
        bool normalizeDiagonal)
    {
        var activeDirections = new HashSet<Direction>();

        foreach (var binding in bindings)
        {
            if (binding.Output is VirtualOutput.StickDirection direction
                && direction.Stick == stick
                && activeInputs.Contains(binding.Input))
            {
                activeDirections.Add(direction.Direction);
            }
        }

        return StickComposer.Compose(activeDirections, normalizeDiagonal);
    }

    public static bool ResolveButton(
        IReadOnlyCollection<Binding> bindings,
        IReadOnlySet<PhysicalInput> activeInputs,
        GamepadButton button)
    {
        foreach (var binding in bindings)
        {
            if (binding.Output is VirtualOutput.DigitalButton digitalButton
                && digitalButton.Button == button
                && activeInputs.Contains(binding.Input))
            {
                return true;
            }
        }

        return false;
    }

    public static float ResolveAxis(
        IReadOnlyCollection<Binding> bindings,
        IReadOnlySet<PhysicalInput> activeInputs,
        GamepadAxis axis)
    {
        foreach (var binding in bindings)
        {
            if (binding.Output is VirtualOutput.AnalogValue analogValue
                && analogValue.Axis == axis
                && activeInputs.Contains(binding.Input))
            {
                return analogValue.Value;
            }
        }

        return 0f;
    }
}
