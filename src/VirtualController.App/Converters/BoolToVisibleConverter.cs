using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VirtualController.App.Converters;

/// <summary>Muestra un elemento solo cuando la condición es cierta; si no, lo oculta sin dejar hueco.</summary>
public sealed class BoolToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
