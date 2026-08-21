namespace VirtualController.Core.Mapping;

public static class MappingValidator
{
    public static void EnsureNoStickConflicts(IReadOnlyCollection<Binding> bindings)
    {
        var sticksWithVector = new HashSet<Stick>();
        var sticksWithDirection = new HashSet<Stick>();

        foreach (var binding in bindings)
        {
            switch (binding.Output)
            {
                case VirtualOutput.StickVector vector:
                    sticksWithVector.Add(vector.Stick);
                    break;
                case VirtualOutput.StickDirection direction:
                    sticksWithDirection.Add(direction.Stick);
                    break;
            }
        }

        sticksWithVector.IntersectWith(sticksWithDirection);

        if (sticksWithVector.Count > 0)
        {
            throw new MappingConflictException(sticksWithVector.ToArray());
        }
    }
}
