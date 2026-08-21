using VirtualController.Core.Gamepad;

namespace VirtualController.TestUtilities;

public sealed class MockVirtualGamepad : IVirtualGamepad
{
    public bool IsConnected { get; private set; }

    public int ConnectCount { get; private set; }

    public int DisconnectCount { get; private set; }

    public int SubmitCount { get; private set; }

    public int ResetCount { get; private set; }

    public GamepadState LastState { get; private set; } = GamepadState.Neutral;

    /// <summary>
    /// Permite a los tests forzar un fallo de conexión concreto sin necesitar un backend real.
    /// </summary>
    public GamepadConnectionResult NextConnectResult { get; set; } = GamepadConnectionResult.Connected();

    public GamepadConnectionResult Connect()
    {
        ConnectCount++;

        if (!NextConnectResult.IsConnected)
        {
            return NextConnectResult;
        }

        IsConnected = true;
        return NextConnectResult;
    }

    public void Disconnect()
    {
        IsConnected = false;
        DisconnectCount++;
    }

    public void Submit(in GamepadState state)
    {
        LastState = state;
        SubmitCount++;
    }

    public void Reset()
    {
        LastState = GamepadState.Neutral;
        ResetCount++;
    }

    public void Dispose()
    {
        if (IsConnected)
        {
            Disconnect();
        }
    }
}
