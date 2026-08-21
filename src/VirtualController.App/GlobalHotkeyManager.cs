using System.Runtime.InteropServices;
using VirtualController.Core.Mapping;

namespace VirtualController.App;

/// <summary>Registra combinaciones globales sin instalar hooks de teclado.</summary>
internal sealed class GlobalHotkeyManager : IDisposable
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    private readonly nint _windowHandle;
    private readonly HashSet<int> _registeredIds = [];

    public GlobalHotkeyManager(nint windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public bool TryRegister(int id, string text, out string? error)
    {
        error = null;
        if (!HotkeyCombination.TryParse(text, out var combination)
            || !TryConvert(combination, out var modifiers, out var virtualKey))
        {
            error = $"Atajo global no válido: {text}.";
            return false;
        }

        if (!RegisterHotKey(_windowHandle, id, modifiers | ModNoRepeat, virtualKey))
        {
            error = $"No se pudo registrar {text}; puede estar siendo usado por otra aplicación.";
            return false;
        }

        _registeredIds.Add(id);
        return true;
    }

    private static bool TryConvert(HotkeyCombination combination, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        foreach (var key in combination.Keys)
        {
            switch (key)
            {
                case Key.LeftControl:
                    modifiers |= ModControl;
                    break;
                case Key.LeftAlt:
                    modifiers |= ModAlt;
                    break;
                case Key.LeftShift:
                    modifiers |= ModShift;
                    break;
                default:
                    if (virtualKey != 0)
                    {
                        return false;
                    }

                    var name = key.ToString();
                    virtualKey = name.Length == 1 && name[0] is >= 'A' and <= 'Z'
                        ? (uint)name[0]
                        : key switch
                        {
                            Key.Space => 0x20,
                            Key.Escape => 0x1B,
                            _ => 0,
                        };
                    break;
            }
        }

        return virtualKey != 0;
    }

    public void Dispose()
    {
        foreach (var id in _registeredIds)
        {
            _ = UnregisterHotKey(_windowHandle, id);
        }

        _registeredIds.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
