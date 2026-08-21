using System.Diagnostics;
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
/// Cubre la espera híbrida que sustituyó a Thread.SpinWait(64). Medido en la máquina de desarrollo:
/// el spin fijo consumía el 100% de un núcleo de forma permanente, mientras que cualquier espera con
/// timeout tiene ~15 ms de resolución real en Windows. La solución se apoya en que ambas situaciones
/// tienen exigencias distintas, así que estos tests fijan ese contrato.
/// </summary>
public class WaitStrategyTests
{
    private const long TicksPerSecond = 1000;

    private static ProcessingLoop CreateLoop(MockVirtualGamepad gamepad, ChannelInputEventQueue queue) =>
        new(
            gamepad,
            CompiledMapping.Compile(DefaultProfile.Bindings, new MappingOptions(NormalizeDiagonal: true)),
            new MouseToStickConverter(new MouseSettings { CountsForFullDeflection = 100f }, TicksPerSecond),
            new EngineMetrics(enabled: false, TicksPerSecond),
            new ProcessingOptions { SubmitHz = TicksPerSecond, SnapshotHz = TicksPerSecond },
            queue,
            TicksPerSecond);

    [Fact]
    public void WaitForInput_WithDataAlreadyQueued_ReturnsImmediately()
    {
        using var queue = new ChannelInputEventQueue();
        queue.TryWrite(new InputEvent.Digital(PhysicalInput.FromKey(Key.W), true, 0));

        var sw = Stopwatch.StartNew();
        var gotInput = queue.WaitForInput(TimeSpan.FromSeconds(5));

        Assert.True(gotInput);
        Assert.True(sw.ElapsedMilliseconds < 100, $"No debería haber esperado ({sw.ElapsedMilliseconds} ms).");
    }

    [Fact]
    public void WaitForInput_WakesUpWhenInputArrives()
    {
        using var queue = new ChannelInputEventQueue();

        var producer = new Thread(() =>
        {
            Thread.Sleep(50);
            queue.TryWrite(new InputEvent.Digital(PhysicalInput.FromKey(Key.W), true, 0));
        });

        var sw = Stopwatch.StartNew();
        producer.Start();
        var gotInput = queue.WaitForInput(TimeSpan.FromSeconds(5));
        producer.Join();

        Assert.True(gotInput, "Debería despertar por la señal, no por el timeout.");
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Tardó {sw.ElapsedMilliseconds} ms en despertar.");
    }

    [Fact]
    public void WaitForInput_WithoutInput_ReturnsFalseAfterTimeout()
    {
        using var queue = new ChannelInputEventQueue();

        var gotInput = queue.WaitForInput(TimeSpan.FromMilliseconds(50));

        Assert.False(gotInput);
    }

    [Fact]
    public void HasPendingTimeWork_IsFalseWhenIdle()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();
        using var queue = new ChannelInputEventQueue();
        var loop = CreateLoop(gamepad, queue);
        loop.ApplyCommand(new ControlCommand.Start());

        loop.RunIteration(queue, now: 1000);

        // En reposo el bucle puede dormir: es lo que evita quemar un núcleo con la app abierta y
        // sin jugar, que es la situación más habitual.
        Assert.False(loop.HasPendingTimeWork);
    }

    [Fact]
    public void HasPendingTimeWork_IsTrueWhileStickIsDeflected()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();
        using var queue = new ChannelInputEventQueue();
        var loop = CreateLoop(gamepad, queue);
        loop.ApplyCommand(new ControlCommand.Start());

        queue.TryWrite(new InputEvent.Digital(PhysicalInput.FromKey(Key.W), true, 0));
        loop.RunIteration(queue, now: 1000);

        Assert.True(loop.HasPendingTimeWork);
    }

    [Fact]
    public void HasPendingTimeWork_IsTrueWhileMouseDecayIsConverging()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();
        using var queue = new ChannelInputEventQueue();
        var loop = CreateLoop(gamepad, queue);
        loop.ApplyCommand(new ControlCommand.Start());

        queue.TryWrite(new InputEvent.MouseMove(50, 0, 1000));
        loop.RunIteration(queue, now: 1000);

        // El decay aún tiene que llevar el stick al centro: dormir aquí lo haría a saltos de 15 ms.
        Assert.True(loop.HasPendingTimeWork);
    }

    [Fact]
    public void HasPendingTimeWork_IsFalseWhenNotEmulating()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();
        using var queue = new ChannelInputEventQueue();
        var loop = CreateLoop(gamepad, queue);

        queue.TryWrite(new InputEvent.Digital(PhysicalInput.FromKey(Key.W), true, 0));
        loop.RunIteration(queue, now: 1000);

        Assert.False(loop.HasPendingTimeWork);
    }
}
