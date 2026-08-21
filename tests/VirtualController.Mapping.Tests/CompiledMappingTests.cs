using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class CompiledMappingTests
{
    private static readonly Binding[] WasdBindings =
    [
        new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Left, Direction.Up)),
        new(PhysicalInput.FromKey(Key.A), new VirtualOutput.StickDirection(Stick.Left, Direction.Left)),
        new(PhysicalInput.FromKey(Key.S), new VirtualOutput.StickDirection(Stick.Left, Direction.Down)),
        new(PhysicalInput.FromKey(Key.D), new VirtualOutput.StickDirection(Stick.Left, Direction.Right)),
    ];

    [Fact]
    public void Compile_WithValidBindings_ResolvesSameAsMappingEngine()
    {
        var compiled = CompiledMapping.Compile(WasdBindings, new MappingOptions(NormalizeDiagonal: true));
        var active = new HashSet<PhysicalInput> { PhysicalInput.FromKey(Key.W), PhysicalInput.FromKey(Key.D) };

        var state = compiled.Resolve(active);

        Assert.Equal(0.7071f, state.LeftStickX, precision: 4);
        Assert.Equal(0.7071f, state.LeftStickY, precision: 4);
    }

    [Fact]
    public void Compile_WithConflictingStickOutputs_ThrowsAtCompileTime()
    {
        Binding[] conflicting =
        [
            new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Right, Direction.Up)),
            new(PhysicalInput.FromMouseButton(MouseButton.Left), new VirtualOutput.StickVector(Stick.Right)),
        ];

        Assert.Throws<MappingConflictException>(() =>
            CompiledMapping.Compile(conflicting, new MappingOptions(NormalizeDiagonal: true)));
    }

    [Fact]
    public void Resolve_WithNoActiveInput_ProducesNeutralState()
    {
        var compiled = CompiledMapping.Compile(WasdBindings, new MappingOptions(NormalizeDiagonal: true));

        var state = compiled.Resolve(new HashSet<PhysicalInput>());

        Assert.Equal(GamepadState.Neutral, state);
    }
}
