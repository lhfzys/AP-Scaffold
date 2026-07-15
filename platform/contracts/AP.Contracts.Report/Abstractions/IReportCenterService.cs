using AP.Contracts.Report.Models;

namespace AP.Contracts.Report.Abstractions;

/// <summary>
/// 报表中心服务
/// 为 UI 插件提供报表查询、生成、导出能力
/// </summary>
public interface IReportCenterService
{
    /// <summary>
    /// 获取所有已注册的报表类型
    /// </summary>
    Task<IReadOnlyList<ReportTypeInfo>> GetReportTypesAsync(CancellationToken ct = default);

    /// <summary>
    /// 查询归档记录
    /// </summary>
    Task<IReadOnlyList<ReportArchiveDto>> GetArchivesAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? reportType = null,
        CancellationToken ct = default);

    /// <summary>
    /// 手动生成指定日期的报表
    /// </summary>
    Task<string> GenerateAsync(string reportType, DateTime date, CancellationToken ct = default);

    /// <summary>
    /// 重新生成指定日期的报表
    /// </summary>
    Task<string> RegenerateAsync(string reportType, DateTime date, CancellationToken ct = default);

    /// <summary>
    /// 打开报表文件
    /// </summary>
    Task OpenAsync(string archiveId, CancellationToken ct = default);

    /// <summary>
    /// 将报表文件导出到指定目录
    /// </summary>
    Task<string> ExportAsync(string archiveId, string destinationDirectory, CancellationToken ct = default);
}

/// <summary>
/// 报表类型信息
/// </summary>
public class ReportTypeInfo
{
    public string ReportType { get; set; } = string.Empty;

    public string ReportName { get; set; } = string.Empty;
}
