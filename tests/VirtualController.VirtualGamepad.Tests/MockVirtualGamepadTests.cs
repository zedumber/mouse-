using VirtualController.Core.Gamepad;
using VirtualController.TestUtilities;
using Xunit;

namespace VirtualController.VirtualGamepad.Tests;

public class MockVirtualGamepadTests
{
    [Fact]
    public void Connect_SetsIsConnectedAndIncrementsCount()
    {
        var gamepad = new MockVirtualGamepad();

        var result = gamepad.Connect();

        Assert.True(result.IsConnected);
        Assert.True(gamepad.IsConnected);
        Assert.Equal(1, gamepad.ConnectCount);
    }

    [Fact]
    public void Connect_WhenBackendUnavailable_ReturnsTypedFailureAndStaysDisconnected()
    {
        var gamepad = new MockVirtualGamepad
        {
            NextConnectResult = GamepadConnectionResult.Failed(
                GamepadConnectionStatus.BackendNotInstalled,
                "driver ausente"),
        };

        var result = gamepad.Connect();

        Assert.False(result.IsConnected);
        Assert.Equal(GamepadConnectionStatus.BackendNotInstalled, result.Status);
        Assert.False(gamepad.IsConnected);
    }

    [Fact]
    public void Disconnect_ClearsIsConnectedAndIncrementsCount()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();

        gamepad.Disconnect();

        Assert.False(gamepad.IsConnected);
        Assert.Equal(1, gamepad.DisconnectCount);
    }

    [Fact]
    public void Submit_StoresLastStateAndIncrementsCount()
    {
        var gamepad = new MockVirtualGamepad();
        var state = GamepadState.Neutral.WithButton(GamepadButton.A, pressed: true);

        gamepad.Submit(state);

        Assert.Equal(state, gamepad.LastState);
        Assert.Equal(1, gamepad.SubmitCount);
    }

    [Fact]
    public void Reset_RestoresNeutralStateAndIncrementsCount()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Submit(GamepadState.Neutral.WithButton(GamepadButton.A, pressed: true));

        gamepad.Reset();

        Assert.Equal(GamepadState.Neutral, gamepad.LastState);
        Assert.Equal(1, gamepad.ResetCount);
    }

    [Fact]
    public void Dispose_WhileConnected_Disconnects()
    {
        var gamepad = new MockVirtualGamepad();
        gamepad.Connect();

        gamepad.Dispose();

        Assert.False(gamepad.IsConnected);
        Assert.Equal(1, gamepad.DisconnectCount);
    }
}
