using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VirtualController.App.ViewModels;
using VirtualController.Core.Gamepad;
using VirtualController.Core.Profiles;

namespace VirtualController.App;

public partial class MainWindow : Window
{
    private const double CanvasSize = 140;
    private const double DotSize = 16;

    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        // La UI marca su propio ritmo leyendo el snapshot inmutable; no la despierta el motor
        // (ADR-005, punto 5: las cadencias son independientes).
        CompositionTarget.Rendering += OnRendering;
        Closed += OnClosed;
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

        // Cerrar la ventana nunca debe dejar el mando virtual conectado con teclas pegadas.
        _viewModel.StopEmulation();
    }
}
