using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Configuration;
using AP.Infra.Report.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Infra.Report.Services;

/// <summary>
/// 报表定期清理服务
/// 根据保留策略自动清理过期的报表文件和记录
/// </summary>
public class ReportCleanupService : BackgroundService
{
    private readonly IReportRepository _repository;
    private readonly IReportStorage _storage;
    private readonly ReportOptions _options;
    private readonly ILogger<ReportCleanupService> _logger;

    public ReportCleanupService(
        IReportRepository repository,
        IReportStorage storage,
        IOptions<ReportOptions> options,
        ILogger<ReportCleanupService> logger)
    {
        _repository = repository;
        _storage = storage;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Retention.Enabled || !_options.Cleanup.Enabled)
        {
            _logger.LogInformation("报表清理服务已禁用");
            return;
        }

        _logger.LogInformation("报表清理服务已启动，保留天数: {Days}, 检查间隔: {Interval}",
            _options.Retention.Days, _options.Retention.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 等待指定间隔
                await Task.Delay(_options.Retention.CheckInterval, stoppingToken);

                // 执行清理
                await CleanupExpiredReportsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("报表清理服务已停止");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "报表清理任务执行失败");
            }
        }
    }

    /// <summary>
    /// 清理过期报表
    /// </summary>
    private async Task CleanupExpiredReportsAsync(CancellationToken ct)
    {
        var cutoffDate = DateTime.Today.AddDays(-_options.Retention.Days);
        var protectedTypes = _options.Retention.ProtectedTypes;

        _logger.LogInformation("开始清理过期报表，截止日期: {Date}, 受保护类型: {@Types}",
            cutoffDate, protectedTypes);

        // 查询过期记录
        var expiredRecords = await _repository.GetExpiredAsync(cutoffDate, ct);

        // 过滤受保护类型
        var toDelete = expiredRecords
            .Where(r => !protectedTypes.Contains(r.ReportType, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (toDelete.Count == 0)
        {
            _logger.LogInformation("没有需要清理的过期报表");
            return;
        }

        _logger.LogInformation("发现 {Count} 条过期报表记录需要清理", toDelete.Count);

        if (_options.Cleanup.DryRun)
        {
            // 模拟运行模式
            _logger.LogInformation("[模拟运行] 将清理以下报表:");
            foreach (var record in toDelete)
            {
                _logger.LogInformation("  - {Type} {Date}: {Path}",
                    record.ReportType, record.ReportDate, record.FilePath);
            }
            return;
        }

        var deletedCount = 0;
        var failedCount = 0;

        foreach (var record in toDelete)
        {
            try
            {
                // 删除文件
                if (_options.Retention.DeleteFiles && !string.IsNullOrEmpty(record.FilePath))
                {
                    if (_storage.DeleteFile(record.FilePath))
                    {
                        _logger.LogDebug("已删除文件: {Path}", record.FilePath);
                    }
                }

                // 更新记录状态为已清理
                record.Status = ArchiveStatus.Cleaned;
                await _repository.UpdateAsync(record, ct);

                deletedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理报表失败: {Id}, {Path}", record.Id, record.FilePath);
                failedCount++;
            }
        }

        // 清理空目录
        _storage.CleanupEmptyDirectories(_options.Storage.RootPath);

        _logger.LogInformation("报表清理完成，成功: {Deleted}, 失败: {Failed}", deletedCount, failedCount);
    }
}