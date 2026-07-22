using AP.Contracts.Report.Models;

namespace AP.Infra.Report.Abstractions;

/// <summary>
/// Excel 导出接口
/// </summary>
public interface IExcelExporter
{
    /// <summary>
    /// 导出报表数据到 Excel 文件
    /// </summary>
    /// <param name="data">报表数据</param>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="templatePath">模板文件路径（可选）</param>
    /// <param name="ct">取消令牌</param>
    Task ExportAsync(ReportData data, string filePath, string? templatePath = null, CancellationToken ct = default);
}