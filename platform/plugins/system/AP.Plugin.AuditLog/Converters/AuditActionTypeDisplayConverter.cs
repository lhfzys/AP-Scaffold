using System.Globalization;
using System.Windows.Data;
using AP.Contracts.Security.Audit;
using AP.Plugin.AuditLog.Models;

namespace AP.Plugin.AuditLog.Converters;

/// <summary>
/// 审计操作类型枚举 → 中文显示（列表"操作类型"列）。
/// </summary>
public sealed class AuditActionTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is AuditActionType actionType
            ? AuditActionTypeDisplay.Of(actionType)
            : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
