using System.Globalization;
using System.Windows.Data;

namespace AP.Shared.UI.Converters;

/// <summary>
/// 文件大小转换器（字节 -> B/KB/MB/GB）
/// </summary>
[ValueConversion(typeof(long), typeof(string))]
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long size) return "0 B";

        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        return size switch
        {
            >= gb => $"{size / (double)gb:F2} GB",
            >= mb => $"{size / (double)mb:F2} MB",
            >= kb => $"{size / (double)kb:F2} KB",
            _ => $"{size} B"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
