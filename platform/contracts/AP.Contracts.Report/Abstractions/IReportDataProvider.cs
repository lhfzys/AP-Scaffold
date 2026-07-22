using AP.Contracts.Report.Models;

namespace AP.Contracts.Report.Abstractions;

/// <summary>
/// 报表数据提供者接口
/// 业务插件实现此接口以提供报表数据
/// </summary>
public interface IReportDataProvider
{
    /// <summary>
    /// 报表类型标识（如 "DeviceRun"）
    /// </summary>
    string ReportType { get; }

    /// <summary>
    /// 报表显示名称（如 "设备运行日报"）
    /// </summary>
    string ReportName { get; }

    /// <summary>
    /// 获取指定日期的报表数据
    /// </summary>
    /// <param name="date">报表日期</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>报表数据</returns>
    Task<ReportData> GetReportDataAsync(DateTime date, CancellationToken ct = default);

    /// <summary>
    /// 获取 Excel 模板路径（可选）
    /// 返回 null 则使用默认模板
    /// </summary>
    string? GetTemplatePath() => null;
}
