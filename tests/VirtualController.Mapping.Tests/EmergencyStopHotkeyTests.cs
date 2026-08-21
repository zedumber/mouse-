using VirtualController.Core.Engine;
using VirtualController.Core.Gamepad;
using VirtualController.Core.Input;
using VirtualController.Core.Mapping;
using VirtualController.Core.Mouse;
using VirtualController.Core.Profiles;
using VirtualController.TestUtilities;
using Xunit;

namespace VirtualController.Mapping.Tests;

/// <summary>
/// Cubre el requisito 19/20 y ADR-005 puntos 1 y 7: la parada de emergencia debe reconocerse desde el
/// flujo normal de input, sin depender de la UI. Antes no existía ningún test porque tampoco existía
/// el cableado: la combinación se validaba en ApplicationSettings y no llegaba a ningún componente
/// capaz de detectarla, así que el único disparador real era un botón de WPF.
/// </summary>
public class EmergencyStopHotkeyTests
{
    private const long TicksPerSecond = 1000;

    private static (ProcessingLoop Loop, MockVirtualGamepad Gamepad, ChannelInputEventQueue Queue) Create(
        string hotkeyText = "Ctrl+Alt+Escape")
    {
        Assert.True(HotkeyCombination.TryParse(hotkeyText, out var hotkey));

        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();
        var queue = new ChannelInputEventQueue();

        var loop = new ProcessingLoop(
            gamepad,
            CompiledMapping.Compile(DefaultProfile.Bindings, new MappingOptions(NormalizeDiagonal: true)),
            new MouseToStickConverter(new MouseSettings(), TicksPerSecond),
            new EngineMetrics(enabled: false, TicksPerSecond),
            new ProcessingOptions
            {
                EmergencyStopHotkey = hotkey,
                SubmitHz = TicksPerSecond,
                SnapshotHz = TicksPerSecond,
            },
            queue,
            TicksPerSecond);

        loop.ApplyCommand(new ControlCommand.Start());
        return (loop, gamepad, queue);
    }

    private static void Press(ChannelInputEventQueue queue, Key key, bool pressed = true) =>
        queue.TryWrite(new InputEvent.Digital(PhysicalInput.FromKey(key), pressed, 0));

    [Fact]
    public void FullCombination_TriggersEmergencyStop()
    {
        var (loop, gamepad, queue) = Create();

        Press(queue, Key.LeftControl);
        Press(queue, Key.LeftAlt);
        Press(queue, Key.Escape);
        loop.RunIteration(queue, now: 1000);

        Assert.False(loop.IsEmulating);
        Assert.Equal(1, gamepad.ResetCount);
    }

    [Fact]
    public void PartialCombination_DoesNotTrigger()
    {
        var (loop, gamepad, queue) = Create();

        Press(queue, Key.LeftControl);
        Press(queue, Key.LeftAlt);
        loop.RunIteration(queue, now: 1000);

        Assert.True(loop.IsEmulating);
        Assert.Equal(0, gamepad.ResetCount);
    }

    [Fact]
    public void EmergencyStop_NeutralizesGamepadEvenWithKeysHeld()
    {
        var (loop, gamepad, queue) = Create();
        Press(queue, Key.W);
        loop.RunIteration(queue, now: 1000);
        Assert.NotEqual(0f, gamepad.LastState.LeftStickY);

        Press(queue, Key.LeftControl);
        Press(queue, Key.LeftAlt);
        Press(queue, Key.Escape);
        loop.RunIteration(queue, now: 1100);

        Assert.Equal(GamepadState.Neutral, gamepad.LastState);
    }

    [Fact]
    public void AfterEmergencyStop_HeldKeysDoNotReplayOnResume()
    {
        var (loop, gamepad, queue) = Create();
        Press(queue, Key.W);
        loop.RunIteration(queue, now: 1000);

        Press(queue, Key.LeftControl);
        Press(queue, Key.LeftAlt);
        Press(queue, Key.Escape);
        loop.RunIteration(queue, now: 1100);

        // W nunca recibió KeyUp (la captura se detuvo), pero no debe reaparecer al reanudar.
        loop.ApplyCommand(new ControlCommand.Start());
        loop.RunIteration(queue, now: 1200);

        Assert.Equal(0f, gamepad.LastState.LeftStickY);
    }

    [Fact]
    public void WithoutHotkeyConfigured_NothingIsTriggered()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();
        var queue = new ChannelInputEventQueue();
        var loop = new ProcessingLoop(
            gamepad,
            CompiledMapping.Compile(DefaultProfile.Bindings, new MappingOptions(true)),
            new MouseToStickConverter(new MouseSettings(), TicksPerSecond),
            new EngineMetrics(enabled: false, TicksPerSecond),
            new ProcessingOptions { SubmitHz = TicksPerSecond, SnapshotHz = TicksPerSecond },
            queue,
            TicksPerSecond);
        loop.ApplyCommand(new ControlCommand.Start());

        Press(queue, Key.Escape);
        loop.RunIteration(queue, now: 1000);

        Assert.True(loop.IsEmulating);
    }

    [Fact]
    public void ChangeEmergencyHotkey_AppliesWithoutRestartingLoop()
    {
        var (loop, gamepad, queue) = Create();
        Assert.True(HotkeyCombination.TryParse("Ctrl+Alt+M", out var replacement));
        loop.ApplyCommand(new ControlCommand.SetEmergencyHotkey(replacement));

        Press(queue, Key.LeftControl);
        Press(queue, Key.LeftAlt);
        Press(queue, Key.M);
        loop.RunIteration(queue, now: 1000);

        Assert.False(loop.IsEmulating);
        Assert.Equal(1, gamepad.ResetCount);
    }

    [Theory]
    [InlineData("Ctrl+Alt+Escape", 3)]
    [InlineData("ctrl+alt+esc", 3)]
    [InlineData("Control+Alt+Escape", 3)]
    [InlineData("Shift+Escape", 2)]
    [InlineData("Escape", 1)]
    public void TryParse_AcceptsCommonSpellings(string text, int expectedKeyCount)
    {
        Assert.True(HotkeyCombination.TryParse(text, out var combination));
        Assert.Equal(expectedKeyCount, combination.Keys.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Ctrl+Teclanoexiste")]
    public void TryParse_RejectsInvalidText_WithoutThrowing(string? text)
    {
        Assert.False(HotkeyCombination.TryParse(text, out _));
    }

    [Fact]
    public void TryParse_IgnoresDuplicateKeys()
    {
        Assert.True(HotkeyCombination.TryParse("Ctrl+Ctrl+Escape", out var combination));

        Assert.Equal(2, combination.Keys.Count);
    }
}
