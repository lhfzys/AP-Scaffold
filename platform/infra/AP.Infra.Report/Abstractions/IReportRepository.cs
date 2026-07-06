using AP.Infra.Report.Entities;

namespace AP.Infra.Report.Abstractions;

/// <summary>
/// 报表归档记录仓储接口
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// 添加归档记录
    /// </summary>
    Task AddAsync(ReportArchive archive, CancellationToken ct = default);

    /// <summary>
    /// 根据ID获取归档记录
    /// </summary>
    Task<ReportArchive?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 查询指定日期和类型的归档记录
    /// </summary>
    Task<ReportArchive?> GetByDateAndTypeAsync(DateTime date, string reportType, CancellationToken ct = default);

    /// <summary>
    /// 查询指定日期范围的所有归档记录
    /// </summary>
    Task<List<ReportArchive>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>
    /// 查询过期的归档记录
    /// </summary>
    Task<List<ReportArchive>> GetExpiredAsync(DateTime cutoffDate, CancellationToken ct = default);

    /// <summary>
    /// 更新归档记录
    /// </summary>
    Task UpdateAsync(ReportArchive archive, CancellationToken ct = default);

    /// <summary>
    /// 删除归档记录
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 批量删除归档记录
    /// </summary>
    Task BatchDeleteAsync(IEnumerable<string> ids, CancellationToken ct = default);
}