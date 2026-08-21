namespace VirtualController.Core.Gamepad;

[Flags]
public enum GamepadButtons : ushort
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LeftShoulder = 1 << 4,
    RightShoulder = 1 << 5,
    LeftStick = 1 << 6,
    RightStick = 1 << 7,
    DPadUp = 1 << 8,
    DPadDown = 1 << 9,
    DPadLeft = 1 << 10,
    DPadRight = 1 << 11,
    Start = 1 << 12,
    Back = 1 << 13
}
