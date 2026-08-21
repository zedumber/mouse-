using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class BindingResolverTests
{
    private static readonly Binding[] WasdBindings =
    [
        new(PhysicalInput.FromKey(Key.W), new VirtualOutput.StickDirection(Stick.Left, Direction.Up)),
        new(PhysicalInput.FromKey(Key.A), new VirtualOutput.StickDirection(Stick.Left, Direction.Left)),
        new(PhysicalInput.FromKey(Key.S), new VirtualOutput.StickDirection(Stick.Left, Direction.Down)),
        new(PhysicalInput.FromKey(Key.D), new VirtualOutput.StickDirection(Stick.Left, Direction.Right)),
    ];

    [Fact]
    public void W_Alone_MovesLeftStickUp()
    {
        var active = new HashSet<PhysicalInput> { PhysicalInput.FromKey(Key.W) };

        var (x, y) = BindingResolver.ResolveStickDirections(WasdBindings, active, Stick.Left, normalizeDiagonal: true);

        Assert.Equal(0f, x);
        Assert.Equal(1f, y);
    }

    [Fact]
    public void WAndD_ProducesNormalizedDiagonal_NeverMagnitudeGreaterThanOne()
    {
        var active = new HashSet<PhysicalInput> { PhysicalInput.FromKey(Key.W), PhysicalInput.FromKey(Key.D) };

        var (x, y) = BindingResolver.ResolveStickDirections(WasdBindings, active, Stick.Left, normalizeDiagonal: true);

        Assert.Equal(0.7071f, x, precision: 4);
        Assert.Equal(0.7071f, y, precision: 4);
    }

    [Fact]
    public void RedundantBindingsToSameDirection_DoNotDoubleMagnitude()
    {
        var bindingsWithRedundantUp = new List<Binding>(WasdBindings)
        {
            new(PhysicalInput.FromKey(Key.Q), new VirtualOutput.StickDirection(Stick.Left, Direction.Up)),
        };
        var active = new HashSet<PhysicalInput> { PhysicalInput.FromKey(Key.W), PhysicalInput.FromKey(Key.Q) };

        var (x, y) = BindingResolver.ResolveStickDirections(bindingsWithRedundantUp, active, Stick.Left, normalizeDiagonal: true);

        Assert.Equal(0f, x);
        Assert.Equal(1f, y);
    }

    [Fact]
    public void RedundantBindingsToSameButton_ResolveAsOr()
    {
        Binding[] bindings =
        [
            new(PhysicalInput.FromKey(Key.Space), new VirtualOutput.DigitalButton(GamepadButton.A)),
            new(PhysicalInput.FromKey(Key.R), new VirtualOutput.DigitalButton(GamepadButton.A)),
        ];
        var onlyOneActive = new HashSet<PhysicalInput> { PhysicalInput.FromKey(Key.R) };

        var pressed = BindingResolver.ResolveButton(bindings, onlyOneActive, GamepadButton.A);

        Assert.True(pressed);
    }

    [Fact]
    public void Button_NotActive_ResolvesToFalse()
    {
        Binding[] bindings = [new(PhysicalInput.FromKey(Key.Space), new VirtualOutput.DigitalButton(GamepadButton.A))];

        var pressed = BindingResolver.ResolveButton(bindings, new HashSet<PhysicalInput>(), GamepadButton.A);

        Assert.False(pressed);
    }

    [Fact]
    public void MouseLeftButton_Pressed_SetsRightTriggerToBindingValue()
    {
        Binding[] bindings =
        [
            new(PhysicalInput.FromMouseButton(MouseButton.Left), new VirtualOutput.AnalogValue(GamepadAxis.RightTrigger, 1f)),
        ];
        var active = new HashSet<PhysicalInput> { PhysicalInput.FromMouseButton(MouseButton.Left) };

        var value = BindingResolver.ResolveAxis(bindings, active, GamepadAxis.RightTrigger);

        Assert.Equal(1f, value);
    }

    [Fact]
    public void MouseLeftButton_Released_RightTriggerFallsBackToZero()
    {
        Binding[] bindings =
        [
            new(PhysicalInput.FromMouseButton(MouseButton.Left), new VirtualOutput.AnalogValue(GamepadAxis.RightTrigger, 1f)),
        ];

        var value = BindingResolver.ResolveAxis(bindings, new HashSet<PhysicalInput>(), GamepadAxis.RightTrigger);

        Assert.Equal(0f, value);
    }
}
