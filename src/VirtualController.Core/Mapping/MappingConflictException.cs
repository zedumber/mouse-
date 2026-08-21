namespace VirtualController.Core.Mapping;

public sealed class MappingConflictException : Exception
{
    public IReadOnlyList<Stick> ConflictingSticks { get; }

    public MappingConflictException(IReadOnlyList<Stick> conflictingSticks)
        : base(BuildMessage(conflictingSticks))
    {
        ConflictingSticks = conflictingSticks;
    }

    private static string BuildMessage(IReadOnlyList<Stick> conflictingSticks) =>
        $"Conflicto de mapping: los sticks [{string.Join(", ", conflictingSticks)}] tienen asignado " +
        "simultáneamente un StickVector y uno o más StickDirection (ver ADR-006, punto 4).";
}
