using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using VirtualController.App.ViewModels;
using VirtualController.Core.Gamepad;
using VirtualController.Core.Profiles;
using Forms = System.Windows.Forms;

namespace VirtualController.App;

public partial class MainWindow : Window
{
    private const double CanvasSize = 140;
    private const double DotSize = 16;

    private readonly MainViewModel _viewModel;
    private ApplicationSettings _settings;
    private nint _windowHandle;
    private GlobalHotkeyManager? _hotkeys;
    private Forms.NotifyIcon? _trayIcon;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel, ApplicationSettings settings)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;
        _viewModel.Settings.Saved += OnSettingsSaved;

        // La UI marca su propio ritmo leyendo el snapshot inmutable; no la despierta el motor
        // (ADR-005, punto 5: las cadencias son independientes).
        CompositionTarget.Rendering += OnRendering;
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;

        Loaded += (_, _) =>
        {
            if (_settings.StartMinimized)
            {
                HideToTray();
            }
        };
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        _viewModel.RefreshFromEngine();
        Render(_viewModel.Snapshot.State);
    }

    private void Render(GamepadState state)
    {
        PlaceDot(LeftStickDot, state.LeftStickX, state.LeftStickY);
        PlaceDot(RightStickDot, state.RightStickX, state.RightStickY);

        LeftStickText.Text = $"X {state.LeftStickX,6:F3}  Y {state.LeftStickY,6:F3}";
        RightStickText.Text = $"X {state.RightStickX,6:F3}  Y {state.RightStickY,6:F3}";

        LeftTriggerBar.Value = state.LeftTrigger;
        RightTriggerBar.Value = state.RightTrigger;

        var pressed = Enum.GetValues<GamepadButton>().Where(state.IsPressed).ToArray();
        ButtonsText.Text = pressed.Length == 0 ? "—" : string.Join("  ", pressed);
    }

    private static void PlaceDot(UIElement dot, float x, float y)
    {
        var center = (CanvasSize - DotSize) / 2;
        var radius = center;

        // El eje Y del stick crece hacia arriba, pero en el canvas crece hacia abajo.
        Canvas.SetLeft(dot, center + (x * radius));
        Canvas.SetTop(dot, center - (y * radius));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _hotkeys?.Dispose();
        _trayIcon?.Dispose();
        _viewModel.Settings.Saved -= OnSettingsSaved;

        // Cerrar la ventana nunca debe dejar el mando virtual conectado con teclas pegadas.
        _viewModel.StopEmulation();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource)PresentationSource.FromVisual(this);
        source.AddHook(WindowHook);
        _windowHandle = source.Handle;

        RegisterGlobalHotkeys();
        CreateTrayIcon();
    }

    private void RegisterGlobalHotkeys()
    {
        _hotkeys?.Dispose();
        _hotkeys = new GlobalHotkeyManager(_windowHandle);

        var errors = new List<string>();
        RegisterHotkey(1, _settings.StartStop, errors);
        RegisterHotkey(2, _settings.ToggleMouseCapture, errors);
        RegisterHotkey(3, _settings.EmergencyStop, errors);

        if (errors.Count > 0)
        {
            _viewModel.ReportConfigurationError(string.Join(Environment.NewLine, errors));
        }
    }

    private void OnSettingsSaved(ApplicationSettings settings)
    {
        _settings = settings;
        RegisterGlobalHotkeys();
    }

    private void RegisterHotkey(int id, string text, List<string> errors)
    {
        if (_hotkeys?.TryRegister(id, text, out var error) == false && error is not null)
        {
            errors.Add(error);
        }
    }

    private nint WindowHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        const int WmHotkey = 0x0312;
        if (message != WmHotkey)
        {
            return 0;
        }

        handled = true;
        switch (wParam.ToInt32())
        {
            case 1:
                _viewModel.ToggleEmulationCommand.Execute(null);
                break;
            case 2:
                _viewModel.ToggleMouseCaptureCommand.Execute(null);
                break;
            case 3:
                _viewModel.EmergencyStopCommand.Execute(null);
                break;
        }

        return 0;
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Mostrar", null, (_, _) => ShowFromTray());
        menu.Items.Add("Iniciar / detener", null, (_, _) => Dispatcher.Invoke(() => _viewModel.ToggleEmulationCommand.Execute(null)));
        menu.Items.Add("Pausar / activar mouse", null, (_, _) => Dispatcher.Invoke(() => _viewModel.ToggleMouseCaptureCommand.Execute(null)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Dispatcher.Invoke(ExitFromTray));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Virtual Controller",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            HideToTray();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_settings.MinimizeToTray && !_allowClose)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = true;
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        _allowClose = true;
        Close();
    }
}
