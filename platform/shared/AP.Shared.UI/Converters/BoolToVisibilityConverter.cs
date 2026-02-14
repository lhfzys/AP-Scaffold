using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AP.Shared.UI.Converters;

/// <summary>
/// 布尔值转可见性 (True -> Visible, False -> Collapsed)
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // 如果 parameter 传了 "Inverse"，则反转逻辑
            if (parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            var isVisible = visibility == Visibility.Visible;
            if (parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true) return !isVisible;
            return isVisible;
        }

        return false;
    }
}