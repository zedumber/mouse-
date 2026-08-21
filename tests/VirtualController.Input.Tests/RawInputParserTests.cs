using System.Runtime.InteropServices;
using VirtualController.Core.Input;
using VirtualController.Core.Mapping;
using VirtualController.Windows.RawInput;
using Xunit;

namespace VirtualController.Input.Tests;

public class RawInputParserTests
{
    [Fact]
    public void MouseMove_ProducesMouseMoveEvent()
    {
        var mouse = new RawMouse { LastX = 5, LastY = -3 };
        var buffer = BuildRawInput(NativeMethods.RimTypeMouse, mouse);

        var events = RawInputParser.Parse(buffer, captureTimestamp: 42);

        var move = Assert.IsType<InputEvent.MouseMove>(Assert.Single(events));
        Assert.Equal(5, move.DeltaX);
        Assert.Equal(-3, move.DeltaY);
        Assert.Equal(42, move.CaptureTimestamp);
    }

    [Fact]
    public void MouseLeftButtonDown_ProducesActiveDigitalEvent()
    {
        var mouse = new RawMouse { ButtonFlags = NativeMethods.RiMouseLeftButtonDown };
        var buffer = BuildRawInput(NativeMethods.RimTypeMouse, mouse);

        var events = RawInputParser.Parse(buffer, captureTimestamp: 1);

        var digital = Assert.IsType<InputEvent.Digital>(Assert.Single(events));
        Assert.Equal(PhysicalInput.FromMouseButton(MouseButton.Left), digital.Input);
        Assert.True(digital.IsActive);
    }

    [Fact]
    public void MouseLeftButtonUp_ProducesInactiveDigitalEvent()
    {
        var mouse = new RawMouse { ButtonFlags = NativeMethods.RiMouseLeftButtonUp };
        var buffer = BuildRawInput(NativeMethods.RimTypeMouse, mouse);

        var events = RawInputParser.Parse(buffer, captureTimestamp: 1);

        var digital = Assert.IsType<InputEvent.Digital>(Assert.Single(events));
        Assert.False(digital.IsActive);
    }

    [Fact]
    public void MouseMoveAndButtonDown_SameBatch_ProducesBothEvents()
    {
        var mouse = new RawMouse { LastX = 10, LastY = 0, ButtonFlags = NativeMethods.RiMouseRightButtonDown };
        var buffer = BuildRawInput(NativeMethods.RimTypeMouse, mouse);

        var events = RawInputParser.Parse(buffer, captureTimestamp: 7);

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e is InputEvent.MouseMove);
        Assert.Contains(events, e => e is InputEvent.Digital d
            && d.Input == PhysicalInput.FromMouseButton(MouseButton.Right)
            && d.IsActive);
    }

    [Fact]
    public void KeyDown_KnownKey_ProducesActiveDigitalEvent()
    {
        var keyboard = new RawKeyboard { VKey = 0x57, Flags = 0 }; // 'W', make
        var buffer = BuildRawInput(NativeMethods.RimTypeKeyboard, keyboard);

        var events = RawInputParser.Parse(buffer, captureTimestamp: 3);

        var digital = Assert.IsType<InputEvent.Digital>(Assert.Single(events));
        Assert.Equal(PhysicalInput.FromKey(Key.W), digital.Input);
        Assert.True(digital.IsActive);
    }

    [Fact]
    public void KeyUp_KnownKey_ProducesInactiveDigitalEvent()
    {
        var keyboard = new RawKeyboard { VKey = 0x57, Flags = NativeMethods.RiKeyBreak };
        var buffer = BuildRawInput(NativeMethods.RimTypeKeyboard, keyboard);

        var events = RawInputParser.Parse(buffer, captureTimestamp: 3);

        var digital = Assert.IsType<InputEvent.Digital>(Assert.Single(events));
        Assert.False(digital.IsActive);
    }

    [Fact]
    public void KeyEvent_UnknownVirtualKey_ProducesNoEvent()
    {
        var keyboard = new RawKeyboard { VKey = 0x1234, Flags = 0 };
        var buffer = BuildRawInput(NativeMethods.RimTypeKeyboard, keyboard);

        var events = RawInputParser.Parse(buffer, captureTimestamp: 3);

        Assert.Empty(events);
    }

    [Fact]
    public void TruncatedBuffer_ProducesNoEvent_DoesNotThrow()
    {
        var events = RawInputParser.Parse(new byte[4], captureTimestamp: 0);

        Assert.Empty(events);
    }

    private static byte[] BuildRawInput<TData>(uint type, TData data)
        where TData : unmanaged
    {
        var header = new RawInputHeader { Type = type, Size = 0, Device = IntPtr.Zero, WParam = IntPtr.Zero };
        var headerBytes = ToBytes(header);
        var dataBytes = ToBytes(data);

        var buffer = new byte[headerBytes.Length + dataBytes.Length];
        headerBytes.CopyTo(buffer, 0);
        dataBytes.CopyTo(buffer, headerBytes.Length);
        return buffer;
    }

    private static byte[] ToBytes<T>(T value)
        where T : unmanaged
    {
        var span = MemoryMarshal.CreateSpan(ref value, 1);
        return MemoryMarshal.AsBytes(span).ToArray();
    }
}
