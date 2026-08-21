using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VirtualController.Windows.RawInput;

/// <summary>
/// Ventana message-only (HWND_MESSAGE) en un hilo de SO dedicado, con su propio message loop —
/// deliberadamente desacoplada de WPF (ver ADR-001/ADR-002). Único llamante real de
/// RegisterRawInputDevices; RawInputHost es el único punto de entrada público de más alto nivel.
/// </summary>
internal sealed class RawInputWindow : IDisposable
{
    private const int BatchDrainCapacity = 64;
    private const int RawInputAlignment = 8;

    // Nombre único por instancia: si dos instancias coexistieran en el mismo proceso (p. ej. en
    // tests), reutilizar un nombre de clase fijo fallaría con ERROR_CLASS_ALREADY_EXISTS y, peor,
    // enrutaría los mensajes al WndProc de la instancia anterior en vez del de esta.
    private readonly string _className = $"VirtualControllerRawInputWindow_{Guid.NewGuid():N}";

    private readonly WndProc _wndProcDelegate;
    private readonly Action<ReadOnlyMemory<byte>, long> _onRawInput;
    private readonly Action? _onDeviceChange;
    private readonly Action<Exception>? _onError;
    private readonly ManualResetEventSlim _windowReady = new(initialState: false);

    private Thread? _thread;
    private IntPtr _hwnd;
    private IntPtr _hInstance;
    private bool _classRegistered;
    private Exception? _startupException;

    public RawInputWindow(
        Action<ReadOnlyMemory<byte>, long> onRawInput,
        Action? onDeviceChange = null,
        Action<Exception>? onError = null)
    {
        _onRawInput = onRawInput;
        _onDeviceChange = onDeviceChange;
        _onError = onError;
        _wndProcDelegate = WindowProc;
    }

    public void Start()
    {
        if (_thread is not null)
        {
            throw new InvalidOperationException("RawInputWindow ya está iniciado.");
        }

        _windowReady.Reset();
        _startupException = null;

        _thread = new Thread(RunMessageLoop) { IsBackground = true, Name = "RawInputCapture" };
        _thread.Start();

        _windowReady.Wait();

        if (_startupException is { } exception)
        {
            _thread = null;
            throw exception;
        }
    }

    /// <summary>Margen para que el hilo procese el WM_CLOSE antes de darlo por perdido.</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    public void Stop()
    {
        if (_thread is null)
        {
            return;
        }

        var thread = _thread;

        // Se comprueba el retorno: si PostMessage falla (cola saturada, HWND ya destruido), el mensaje
        // se pierde y GetMessage quedaría bloqueado para siempre. Con Join sin timeout eso colgaba el
        // cierre de toda la aplicación, porque Stop se llama desde Dispose en el hilo de UI.
        var posted = _hwnd != IntPtr.Zero
                     && NativeMethods.PostMessage(_hwnd, NativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero);

        if (!posted || !thread.Join(StopTimeout))
        {
            // El hilo es IsBackground, así que no impedirá que el proceso termine. Se reporta para no
            // fingir un apagado limpio que no ocurrió.
            _onError?.Invoke(new TimeoutException(
                "El hilo de captura de Raw Input no respondió al cierre; se abandona sin bloquear la aplicación."));
        }

        _thread = null;
        _hwnd = IntPtr.Zero;
    }

    private void RunMessageLoop()
    {
        try
        {
            try
            {
                CreateWindowAndRegisterDevices();
            }
            catch (Exception ex)
            {
                _startupException = ex;
                _windowReady.Set();
                return;
            }

            _windowReady.Set();

            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            if (_classRegistered)
            {
                NativeMethods.UnregisterClass(_className, _hInstance);
                _classRegistered = false;
            }
        }
    }

    private void CreateWindowAndRegisterDevices()
    {
        _hInstance = NativeMethods.GetModuleHandle(null);

        var wndClass = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = _wndProcDelegate,
            lpszClassName = _className,
            hInstance = _hInstance,
        };

        if (NativeMethods.RegisterClassEx(ref wndClass) == 0)
        {
            throw new InvalidOperationException($"RegisterClassEx falló (error {Marshal.GetLastWin32Error()}).");
        }

        _classRegistered = true;

        _hwnd = NativeMethods.CreateWindowEx(
            0,
            _className,
            null,
            0,
            0,
            0,
            0,
            0,
            new IntPtr(NativeMethods.HwndMessage),
            IntPtr.Zero,
            _hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx falló (error {Marshal.GetLastWin32Error()}).");
        }

        RawInputDevice[] devices =
        [
            new()
            {
                UsagePage = 0x01,
                Usage = 0x02, // mouse
                Flags = NativeMethods.RidevInputSink | NativeMethods.RidevDevNotify,
                Target = _hwnd,
            },
            new()
            {
                UsagePage = 0x01,
                Usage = 0x06, // keyboard
                Flags = NativeMethods.RidevInputSink | NativeMethods.RidevDevNotify,
                Target = _hwnd,
            },
        ];

        if (!NativeMethods.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new InvalidOperationException($"RegisterRawInputDevices falló (error {Marshal.GetLastWin32Error()}).");
        }
    }

    /// <summary>
    /// Este método lo invoca Windows a través de un callback nativo. Una excepción managed que se
    /// propagara por marcos de user32 sería comportamiento indefinido (típicamente, muerte del
    /// proceso), así que aquí se captura todo y se reporta en vez de dejarla escapar.
    /// </summary>
    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case NativeMethods.WmInput:
                    HandleRawInput(lParam);

                    // Para input recibido en primer plano (RIM_INPUT), Microsoft exige llamar a
                    // DefWindowProc para que el sistema pueda hacer su limpieza. Con RIM_INPUTSINK no
                    // hace falta. El código de tipo va en el byte bajo de wParam.
                    return (wParam.ToInt64() & 0xff) == NativeMethods.RimInput
                        ? NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam)
                        : IntPtr.Zero;

                case NativeMethods.WmInputDeviceChange:
                    _onDeviceChange?.Invoke();
                    return IntPtr.Zero;

                case NativeMethods.WmClose:
                    NativeMethods.DestroyWindow(hWnd);
                    return IntPtr.Zero;

                case NativeMethods.WmDestroy:
                    NativeMethods.PostQuitMessage(0);
                    return IntPtr.Zero;

                default:
                    return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }
        catch (Exception ex)
        {
            // Perder un evento de input es preferible a matar el proceso. Se notifica para que la
            // captura no se degrade en silencio, que es el otro fallo posible aquí.
            _onError?.Invoke(ex);
            return IntPtr.Zero;
        }
    }

    private void HandleRawInput(IntPtr hRawInput)
    {
        var captureTimestamp = Stopwatch.GetTimestamp();

        uint size = 0;
        NativeMethods.GetRawInputData(hRawInput, NativeMethods.RidInput, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
        if (size == 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var written = NativeMethods.GetRawInputData(hRawInput, NativeMethods.RidInput, buffer, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
            if (written == unchecked((uint)-1))
            {
                return;
            }

            var managed = new byte[size];
            Marshal.Copy(buffer, managed, 0, (int)size);
            _onRawInput(managed, captureTimestamp);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        DrainPendingRawInput();
    }

    /// <summary>
    /// Con dispositivos de alta frecuencia (mouse de 1000 Hz o más) pueden acumularse varios eventos
    /// entre iteraciones del message loop; GetRawInputBuffer los drena en lote (ADR-002, punto 4).
    /// </summary>
    private void DrainPendingRawInput()
    {
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();

        uint requiredSize = 0;
        if (NativeMethods.GetRawInputBuffer(IntPtr.Zero, ref requiredSize, headerSize) == unchecked((uint)-1)
            || requiredSize == 0)
        {
            return;
        }

        // GetRawInputBuffer necesita holgura: el tamaño que reporta es para UN evento, y en 64-bit
        // los RAWINPUT del lote vienen alineados a 8 bytes.
        var bufferSize = (int)requiredSize * BatchDrainCapacity;
        var buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            while (true)
            {
                var size = (uint)bufferSize;
                var count = NativeMethods.GetRawInputBuffer(buffer, ref size, headerSize);

                if (count == 0 || count == unchecked((uint)-1))
                {
                    return;
                }

                var captureTimestamp = Stopwatch.GetTimestamp();
                var current = buffer;

                for (uint i = 0; i < count; i++)
                {
                    var header = Marshal.PtrToStructure<RawInputHeader>(current);
                    if (header.Size == 0)
                    {
                        return;
                    }

                    var managed = new byte[header.Size];
                    Marshal.Copy(current, managed, 0, (int)header.Size);
                    _onRawInput(managed, captureTimestamp);

                    var stride = Align((int)header.Size, RawInputAlignment);
                    current = IntPtr.Add(current, stride);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);

    public void Dispose()
    {
        Stop();
        _windowReady.Dispose();
    }
}
