using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Report.Services;

/// <summary>
/// 报表数据库初始化服务（兼容入口）。
/// 表结构由 DatabaseSchemaMigrator 的 ReportSchemaContributor 统一创建。
/// </summary>
public class ReportDatabaseInitializer : IHostedService
{
    private readonly ILogger<ReportDatabaseInitializer> _logger;

    public ReportDatabaseInitializer(ILogger<ReportDatabaseInitializer> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 表结构由 DatabaseSchemaMigrator 的 ReportSchemaContributor 统一创建；
        // 这里仅保留启动日志/兼容入口，不再单独 SyncStructure。
        _logger.LogInformation("报表归档表 (report_archives) 由数据库迁移器管理");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}