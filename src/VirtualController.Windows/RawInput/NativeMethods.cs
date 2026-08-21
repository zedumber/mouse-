using System.Runtime.InteropServices;

namespace VirtualController.Windows.RawInput;

internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WndClassEx
{
    public uint cbSize;
    public uint style;
    public WndProc lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    public string? lpszMenuName;
    public string lpszClassName;
    public IntPtr hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Msg
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public int ptX;
    public int ptY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RawInputDevice
{
    public ushort UsagePage;
    public ushort Usage;
    public uint Flags;
    public IntPtr Target;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RawInputHeader
{
    public uint Type;
    public uint Size;
    public IntPtr Device;
    public IntPtr WParam;
}

[StructLayout(LayoutKind.Explicit)]
internal struct RawMouse
{
    [FieldOffset(0)] public ushort Flags;
    [FieldOffset(4)] public ushort ButtonFlags;
    [FieldOffset(6)] public ushort ButtonData;
    [FieldOffset(8)] public uint RawButtons;
    [FieldOffset(12)] public int LastX;
    [FieldOffset(16)] public int LastY;
    [FieldOffset(20)] public uint ExtraInformation;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RawKeyboard
{
    public ushort MakeCode;
    public ushort Flags;
    public ushort Reserved;
    public ushort VKey;
    public uint Message;
    public uint ExtraInformation;
}

internal static class NativeMethods
{
    public const int HwndMessage = -3;

    public const uint WmInput = 0x00FF;
    public const uint WmInputDeviceChange = 0x00FE;
    public const uint WmClose = 0x0010;
    public const uint WmDestroy = 0x0002;
    public const uint WmQuit = 0x0012;

    public const uint RidevInputSink = 0x00000100;
    public const uint RidevDevNotify = 0x00002000;
    public const uint RidevRemove = 0x00000001;

    public const uint RimTypeMouse = 0;
    public const uint RimTypeKeyboard = 1;

    public const uint RidInput = 0x10000003;
    public const uint RidHeader = 0x10000005;

    public const ushort RiKeyBreak = 1;

    /// <summary>RAWMOUSE.usFlags: los valores de posición son absolutos, no relativos.</summary>
    public const ushort MouseMoveAbsolute = 0x01;

    /// <summary>wParam de WM_INPUT: el input llegó con la aplicación en primer plano.</summary>
    public const int RimInput = 0;

    public const ushort RiMouseLeftButtonDown = 0x0001;
    public const ushort RiMouseLeftButtonUp = 0x0002;
    public const ushort RiMouseRightButtonDown = 0x0004;
    public const ushort RiMouseRightButtonUp = 0x0008;
    public const ushort RiMouseMiddleButtonDown = 0x0010;
    public const ushort RiMouseMiddleButtonUp = 0x0020;
    public const ushort RiMouseButton4Down = 0x0040;
    public const ushort RiMouseButton4Up = 0x0080;
    public const ushort RiMouseButton5Down = 0x0100;
    public const ushort RiMouseButton5Up = 0x0200;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterRawInputDevices(RawInputDevice[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetRawInputBuffer(IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
}
