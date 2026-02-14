using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AP.Shared.UI.Converters;

/// <summary>
/// bool 取反转 Visibility
/// True -> Collapsed
/// False -> Visible
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue) return boolValue ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Collapsed;
    }
}