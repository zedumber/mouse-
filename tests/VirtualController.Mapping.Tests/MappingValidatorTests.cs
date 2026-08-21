using VirtualController.Core.Mapping;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class MappingValidatorTests
{
    [Fact]
    public void StickVectorAndStickDirection_OnSameStick_Throws()
    {
        Binding[] bindings =
        [
            new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Right, Direction.Up)),
            new(PhysicalInput.FromMouseButton(MouseButton.Left), new VirtualOutput.StickVector(Stick.Right)),
        ];

        var exception = Assert.Throws<MappingConflictException>(() => MappingValidator.EnsureNoStickConflicts(bindings));
        Assert.Contains(Stick.Right, exception.ConflictingSticks);
    }

    [Fact]
    public void StickVectorAndStickDirection_OnDifferentSticks_DoesNotThrow()
    {
        Binding[] bindings =
        [
            new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Left, Direction.Up)),
            new(PhysicalInput.FromMouseButton(MouseButton.Left), new VirtualOutput.StickVector(Stick.Right)),
        ];

        MappingValidator.EnsureNoStickConflicts(bindings);
    }

    [Fact]
    public void OnlyStickDirections_DoesNotThrow()
    {
        Binding[] bindings =
        [
            new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Left, Direction.Up)),
            new(PhysicalInput.FromKey(Key.S), new VirtualOutput.StickDirection(Stick.Left, Direction.Down)),
        ];

        MappingValidator.EnsureNoStickConflicts(bindings);
    }
}
