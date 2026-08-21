using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class MappingEngineTests
{
    private static readonly Binding[] Bindings =
    [
        new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Left, Direction.Up)),
        new(PhysicalInput.FromKey(Key.A), new VirtualOutput.StickDirection(Stick.Left, Direction.Left)),
        new(PhysicalInput.FromKey(Key.S), new VirtualOutput.StickDirection(Stick.Left, Direction.Down)),
        new(PhysicalInput.FromKey(Key.D), new VirtualOutput.StickDirection(Stick.Left, Direction.Right)),
        new(PhysicalInput.FromKey(Key.Space), new VirtualOutput.DigitalButton(GamepadButton.A)),
        new(PhysicalInput.FromMouseButton(MouseButton.Left), new VirtualOutput.AnalogValue(GamepadAxis.RightTrigger, 1f)),
    ];

    [Fact]
    public void NoActiveInput_ResolvesToFullyNeutralState()
    {
        var state = MappingEngine.Resolve(Bindings, new HashSet<PhysicalInput>(), new MappingOptions(NormalizeDiagonal: true));

        Assert.Equal(GamepadState.Neutral, state);
    }

    [Fact]
    public void WAndDAndSpace_ResolvesDiagonalStickAndButtonSimultaneously()
    {
        var active = new HashSet<PhysicalInput>
        {
            PhysicalInput.FromKey(Key.W),
            PhysicalInput.FromKey(Key.D),
            PhysicalInput.FromKey(Key.Space),
        };

        var state = MappingEngine.Resolve(Bindings, active, new MappingOptions(NormalizeDiagonal: true));

        Assert.Equal(0.7071f, state.LeftStickX, precision: 4);
        Assert.Equal(0.7071f, state.LeftStickY, precision: 4);
        Assert.True(state.IsPressed(GamepadButton.A));
        Assert.Equal(0f, state.GetAxis(GamepadAxis.RightTrigger));
    }

    [Fact]
    public void ReleasingEverything_ProducesFullyNeutralState_NothingStaysStuck()
    {
        var pressedState = MappingEngine.Resolve(
            Bindings,
            new HashSet<PhysicalInput> { PhysicalInput.FromKey(Key.Space), PhysicalInput.FromMouseButton(MouseButton.Left) },
            new MappingOptions(NormalizeDiagonal: true));
        Assert.True(pressedState.IsPressed(GamepadButton.A));

        var releasedState = MappingEngine.Resolve(Bindings, new HashSet<PhysicalInput>(), new MappingOptions(NormalizeDiagonal: true));

        Assert.Equal(GamepadState.Neutral, releasedState);
    }
}
