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
/// Integración end-to-end del flujo input → mapping → gamepad, usando MockVirtualGamepad para no
/// necesitar ningún driver instalado. Cubre el resultado mínimo funcional del brief (sección 40).
/// </summary>
public class ProcessingLoopTests
{
    private const long TicksPerSecond = 1000;

    private static (ProcessingLoop Loop, MockVirtualGamepad Gamepad, ChannelInputEventQueue Queue) Create(
        MouseSettings? mouseSettings = null,
        ProcessingOptions? options = null)
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();

        var mapping = CompiledMapping.Compile(DefaultProfile.Bindings, new MappingOptions(NormalizeDiagonal: true));
        var converter = new MouseToStickConverter(
            mouseSettings ?? new MouseSettings { CountsForFullDeflection = 100f },
            TicksPerSecond);

        var queue = new ChannelInputEventQueue();

        var loop = new ProcessingLoop(
            gamepad,
            mapping,
            converter,
            new EngineMetrics(enabled: true, TicksPerSecond),
            // Submit y snapshot en cada iteración: los tests controlan el reloj manualmente y quieren
            // observar el efecto de cada llamada, no la cadencia limitada de producción.
            (options ?? new ProcessingOptions()) with { SubmitHz = TicksPerSecond, SnapshotHz = TicksPerSecond },
            queue,
            TicksPerSecond);

        loop.ApplyCommand(new ControlCommand.Start());
        return (loop, gamepad, queue);
    }

    private static void PressKey(ChannelInputEventQueue queue, Key key, bool pressed = true) =>
        queue.TryWrite(new InputEvent.Digital(PhysicalInput.FromKey(key), pressed, 0));

    private static void PressMouse(ChannelInputEventQueue queue, MouseButton button, bool pressed = true) =>
        queue.TryWrite(new InputEvent.Digital(PhysicalInput.FromMouseButton(button), pressed, 0));

    [Fact]
    public void PressW_MovesLeftStickUp()
    {
        var (loop, gamepad, queue) = Create();

        PressKey(queue, Key.W);
        loop.RunIteration(queue, now: 1000);

        Assert.Equal(1f, gamepad.LastState.LeftStickY, precision: 4);
        Assert.Equal(0f, gamepad.LastState.LeftStickX, precision: 4);
    }

    [Fact]
    public void PressWAndD_ProducesNormalizedDiagonal()
    {
        var (loop, gamepad, queue) = Create();

        PressKey(queue, Key.W);
        PressKey(queue, Key.D);
        loop.RunIteration(queue, now: 1000);

        Assert.Equal(0.7071f, gamepad.LastState.LeftStickX, precision: 4);
        Assert.Equal(0.7071f, gamepad.LastState.LeftStickY, precision: 4);
    }

    [Fact]
    public void ReleaseKey_ReturnsStickToNeutral_NothingStaysStuck()
    {
        var (loop, gamepad, queue) = Create();

        PressKey(queue, Key.W);
        loop.RunIteration(queue, now: 1000);
        Assert.NotEqual(0f, gamepad.LastState.LeftStickY);

        PressKey(queue, Key.W, pressed: false);
        loop.RunIteration(queue, now: 2000);

        Assert.Equal(0f, gamepad.LastState.LeftStickY);
    }

    [Fact]
    public void LeftMouseButton_PressesRightTrigger()
    {
        var (loop, gamepad, queue) = Create();

        PressMouse(queue, MouseButton.Left);
        loop.RunIteration(queue, now: 1000);

        Assert.Equal(1f, gamepad.LastState.RightTrigger, precision: 4);
        Assert.Equal(0f, gamepad.LastState.LeftTrigger, precision: 4);
    }

    [Fact]
    public void RightMouseButton_PressesLeftTrigger()
    {
        var (loop, gamepad, queue) = Create();

        PressMouse(queue, MouseButton.Right);
        loop.RunIteration(queue, now: 1000);

        Assert.Equal(1f, gamepad.LastState.LeftTrigger, precision: 4);
    }

    [Fact]
    public void AdsTuning_FollowsMappedLeftTrigger_NotAHardcodedPhysicalButton()
    {
        var (loop, gamepad, queue) = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            AdsEnabled = true,
            AdsSensitivityMultiplier = 0.5f,
            AdsPrecisionExponent = 2f,
        });

        // En el perfil por defecto Right Mouse está mapeado a LeftTrigger. El motor activa ADS por
        // la salida virtual, por lo que un remapeo futuro no obliga a cambiar el conversor.
        PressMouse(queue, MouseButton.Right);
        queue.TryWrite(new InputEvent.MouseMove(50, 0, 1000));
        loop.RunIteration(queue, now: 1000);

        Assert.Equal(1f, gamepad.LastState.LeftTrigger, precision: 4);
        Assert.Equal(0.0625f, gamepad.LastState.RightStickX, precision: 4);
    }

    [Fact]
    public void PressSpace_PressesAButton()
    {
        var (loop, gamepad, queue) = Create();

        PressKey(queue, Key.Space);
        loop.RunIteration(queue, now: 1000);

        Assert.True(gamepad.LastState.IsPressed(GamepadButton.A));
    }

    [Fact]
    public void MouseMovement_MovesRightStick()
    {
        var (loop, gamepad, queue) = Create();

        queue.TryWrite(new InputEvent.MouseMove(50, 0, 1000));
        loop.RunIteration(queue, now: 1000);

        Assert.Equal(0.5f, gamepad.LastState.RightStickX, precision: 4);
    }

    [Fact]
    public void MouseMovement_DoesNotAffectLeftStick()
    {
        var (loop, gamepad, queue) = Create();

        queue.TryWrite(new InputEvent.MouseMove(50, 50, 1000));
        loop.RunIteration(queue, now: 1000);

        Assert.Equal(0f, gamepad.LastState.LeftStickX);
        Assert.Equal(0f, gamepad.LastState.LeftStickY);
    }

    [Fact]
    public void DisableMouseCapture_NeutralizesAndIgnoresMovementUntilReenabled()
    {
        var (loop, gamepad, queue) = Create();
        queue.TryWrite(new InputEvent.MouseMove(50, 0, 1000));
        loop.RunIteration(queue, 1000);
        Assert.NotEqual(0f, gamepad.LastState.RightStickX);

        loop.ApplyCommand(new ControlCommand.SetMouseCapture(false));
        queue.TryWrite(new InputEvent.MouseMove(50, 0, 1001));
        loop.RunIteration(queue, 1001);
        Assert.Equal(0f, gamepad.LastState.RightStickX);

        loop.ApplyCommand(new ControlCommand.SetMouseCapture(true));
        queue.TryWrite(new InputEvent.MouseMove(50, 0, 1002));
        loop.RunIteration(queue, 1002);
        Assert.NotEqual(0f, gamepad.LastState.RightStickX);
    }

    [Fact]
    public void MouseStops_StickDecaysWithoutNewInput()
    {
        var (loop, gamepad, queue) = Create(new MouseSettings
        {
            CountsForFullDeflection = 100f,
            Decay = new ExponentialDecay(rate: 10f),
        });

        queue.TryWrite(new InputEvent.MouseMove(100, 0, 1000));
        loop.RunIteration(queue, now: 1000);
        var afterMove = gamepad.LastState.RightStickX;

        // Sin ningún evento nuevo en la cola, el decay debe seguir progresando.
        loop.RunIteration(queue, now: 1200);

        Assert.True(gamepad.LastState.RightStickX < afterMove);
    }

    [Fact]
    public void ReleaseEverything_ReturnsFullyNeutral()
    {
        var (loop, gamepad, queue) = Create();

        PressKey(queue, Key.W);
        PressKey(queue, Key.Space);
        PressMouse(queue, MouseButton.Left);
        queue.TryWrite(new InputEvent.MouseMove(50, 50, 1000));
        loop.RunIteration(queue, now: 1000);

        PressKey(queue, Key.W, pressed: false);
        PressKey(queue, Key.Space, pressed: false);
        PressMouse(queue, MouseButton.Left, pressed: false);
        loop.RunIteration(queue, now: 2000);

        Assert.Equal(GamepadState.Neutral, gamepad.LastState);
    }

    [Fact]
    public void WhileStopped_NothingIsSubmittedToGamepad()
    {
        var (loop, gamepad, queue) = Create();
        loop.ApplyCommand(new ControlCommand.Stop());
        var submitsAfterStop = gamepad.SubmitCount;

        PressKey(queue, Key.W);
        loop.RunIteration(queue, now: 1000);

        Assert.Equal(submitsAfterStop, gamepad.SubmitCount);
    }

    [Fact]
    public void Stop_NeutralizesOutputImmediately()
    {
        var (loop, gamepad, queue) = Create();
        PressKey(queue, Key.W);
        loop.RunIteration(queue, now: 1000);

        loop.ApplyCommand(new ControlCommand.Stop());

        Assert.Equal(GamepadState.Neutral, gamepad.LastState);
    }

    [Fact]
    public void EmergencyStop_ResetsGamepadAndStopsEmulating()
    {
        var (loop, gamepad, queue) = Create();
        PressKey(queue, Key.W);
        PressMouse(queue, MouseButton.Left);
        loop.RunIteration(queue, now: 1000);

        loop.EmergencyStop();

        Assert.False(loop.IsEmulating);
        Assert.Equal(1, gamepad.ResetCount);
        Assert.Equal(GamepadState.Neutral, gamepad.LastState);
    }

    [Fact]
    public void EmergencyStop_ClearsHeldKeys_SoResumingDoesNotReplayThem()
    {
        var (loop, gamepad, queue) = Create();
        PressKey(queue, Key.W);
        loop.RunIteration(queue, now: 1000);

        loop.EmergencyStop();
        loop.ApplyCommand(new ControlCommand.Start());
        loop.RunIteration(queue, now: 2000);

        // W nunca recibió KeyUp, pero la parada de emergencia lo olvidó a propósito.
        Assert.Equal(0f, gamepad.LastState.LeftStickY);
    }

    [Fact]
    public void ChangeMapping_DoesNotLeaveHybridStateBetweenProfiles()
    {
        var (loop, gamepad, queue) = Create();
        PressKey(queue, Key.W);
        loop.RunIteration(queue, now: 1000);
        Assert.NotEqual(0f, gamepad.LastState.LeftStickY);

        // Perfil nuevo donde W no está mapeada a nada.
        var emptyMapping = CompiledMapping.Compile([], new MappingOptions(NormalizeDiagonal: true));
        loop.ApplyCommand(new ControlCommand.ChangeMapping(emptyMapping, new MouseSettings()));
        loop.RunIteration(queue, now: 2000);

        Assert.Equal(GamepadState.Neutral, gamepad.LastState);
    }

    [Fact]
    public void InputDrainBudget_LimitsEventsPerIteration_PreventingStarvation()
    {
        var (loop, _, queue) = Create(options: new ProcessingOptions { InputDrainBudget = 5 });

        for (var i = 0; i < 50; i++)
        {
            PressKey(queue, Key.W, pressed: i % 2 == 0);
        }

        var consumed = loop.RunIteration(queue, now: 1000);

        Assert.Equal(5, consumed);
    }

    [Fact]
    public void RemainingEventsAreConsumedInLaterIterations_NothingIsLost()
    {
        var (loop, _, queue) = Create(options: new ProcessingOptions { InputDrainBudget = 5 });
        for (var i = 0; i < 12; i++)
        {
            PressKey(queue, Key.W, pressed: true);
        }

        var total = loop.RunIteration(queue, now: 1000)
                    + loop.RunIteration(queue, now: 1100)
                    + loop.RunIteration(queue, now: 1200);

        Assert.Equal(12, total);
    }

    [Fact]
    public void Snapshot_IsPublishedForUi()
    {
        var (loop, _, queue) = Create();

        PressKey(queue, Key.W);
        loop.RunIteration(queue, now: 1000);

        var snapshot = loop.Snapshot;
        Assert.True(snapshot.IsEmulating);
        Assert.Equal(1f, snapshot.State.LeftStickY, precision: 4);
    }

    [Fact]
    public void Metrics_CountProcessedEventsAndSubmits()
    {
        var (loop, _, queue) = Create();

        PressKey(queue, Key.W);
        PressKey(queue, Key.Space);
        loop.RunIteration(queue, now: 1000);

        var metrics = loop.Metrics.Snapshot();
        Assert.Equal(2, metrics.InputEventsProcessed);
        Assert.Equal(1, metrics.GamepadSubmits);
    }

    [Fact]
    public void Metrics_MeasureLatencySaturationAndSubmitJitter()
    {
        var (loop, _, queue) = Create();

        queue.TryWrite(new InputEvent.MouseMove(100, 0, 900));
        loop.RunIteration(queue, now: 1000);
        loop.RunIteration(queue, now: 1002);

        var metrics = loop.Metrics.Snapshot();
        Assert.Equal(100_000d, metrics.AverageInputLatencyMicroseconds, precision: 1);
        Assert.Equal(50d, metrics.AimSaturationPercent, precision: 1);
        Assert.Equal(1_000d, metrics.AverageSubmitJitterMicroseconds, precision: 1);
    }

    [Fact]
    public void MetricsDisabled_RecordsNothing()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();
        var loop = new ProcessingLoop(
            gamepad,
            CompiledMapping.Compile(DefaultProfile.Bindings, new MappingOptions(true)),
            new MouseToStickConverter(new MouseSettings(), TicksPerSecond),
            new EngineMetrics(enabled: false, TicksPerSecond),
            ticksPerSecond: TicksPerSecond);
        loop.ApplyCommand(new ControlCommand.Start());
        var queue = new ChannelInputEventQueue();
        PressKey(queue, Key.W);

        loop.RunIteration(queue, now: 1000);

        Assert.Equal(0, loop.Metrics.Snapshot().InputEventsProcessed);
    }
}
