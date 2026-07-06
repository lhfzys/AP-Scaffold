using AP.Infra.Report.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Report.Services;

/// <summary>
/// 报表数据库初始化服务
/// 启动时自动创建报表归档表，防止数据库未初始化导致运行时崩溃
/// </summary>
public class ReportDatabaseInitializer : IHostedService
{
    private readonly IFreeSql _freeSql;
    private readonly ILogger<ReportDatabaseInitializer> _logger;

    public ReportDatabaseInitializer(IFreeSql freeSql, ILogger<ReportDatabaseInitializer> logger)
    {
        _freeSql = freeSql;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 自动同步表结构（如果表不存在则创建）
            _freeSql.CodeFirst.SyncStructure<ReportArchive>();
            _logger.LogInformation("报表归档表 (report_archives) 初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报表归档表初始化失败，报表功能可能不可用");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}