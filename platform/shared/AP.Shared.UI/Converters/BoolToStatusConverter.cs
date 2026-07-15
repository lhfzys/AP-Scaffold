using System.Globalization;
using System.Windows.Data;

namespace AP.Shared.UI.Converters;

/// <summary>
/// 布尔值转换为状态文本（成功/失败）
/// </summary>
[ValueConversion(typeof(bool), typeof(string))]
public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? "成功" : "失败";
        }

        return "失败";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
