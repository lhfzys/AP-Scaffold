using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Entities;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Report.Services;

/// <summary>
/// 报表归档记录仓储实现（基于 FreeSql）
/// </summary>
public class ReportRepository : IReportRepository
{
    private readonly IFreeSql _freeSql;
    private readonly ILogger<ReportRepository> _logger;

    public ReportRepository(IFreeSql freeSql, ILogger<ReportRepository> logger)
    {
        _freeSql = freeSql;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAsync(ReportArchive archive, CancellationToken ct = default)
    {
        await _freeSql.Insert(archive).ExecuteAffrowsAsync(ct);
        _logger.LogDebug("添加归档记录: {Id}, 类型: {Type}, 日期: {Date}",
            archive.Id, archive.ReportType, archive.ReportDate);
    }

    /// <inheritdoc />
    public async Task<ReportArchive?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _freeSql.Select<ReportArchive>()
            .Where(a => a.Id == id)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<ReportArchive?> GetByDateAndTypeAsync(DateTime date, string reportType, CancellationToken ct = default)
    {
        return await _freeSql.Select<ReportArchive>()
            .Where(a => a.ReportDate == date && a.ReportType == reportType)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<ReportArchive>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        return await _freeSql.Select<ReportArchive>()
            .Where(a => a.ReportDate >= startDate && a.ReportDate <= endDate)
            .OrderByDescending(a => a.ReportDate)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<ReportArchive>> GetExpiredAsync(DateTime cutoffDate, CancellationToken ct = default)
    {
        return await _freeSql.Select<ReportArchive>()
            .Where(a => a.ReportDate < cutoffDate && a.Status != ArchiveStatus.Cleaned)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ReportArchive archive, CancellationToken ct = default)
    {
        await _freeSql.Update<ReportArchive>()
            .SetSource(archive)
            .ExecuteAffrowsAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _freeSql.Delete<ReportArchive>()
            .Where(a => a.Id == id)
            .ExecuteAffrowsAsync(ct);
        _logger.LogDebug("删除归档记录: {Id}", id);
    }

    /// <inheritdoc />
    public async Task BatchDeleteAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        await _freeSql.Delete<ReportArchive>()
            .Where(a => idList.Contains(a.Id))
            .ExecuteAffrowsAsync(ct);
        _logger.LogDebug("批量删除归档记录: {Count} 条", idList.Count);
    }
}