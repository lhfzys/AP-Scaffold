using AP.Infra.Report.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Infra.Report.Services;

/// <summary>
/// 报表定时归档调度服务
/// 每天在指定时间自动生成前一天的报表
/// </summary>
public class ReportScheduler : BackgroundService
{
    private readonly ReportService _reportService;
    private readonly ReportOptions _options;
    private readonly ILogger<ReportScheduler> _logger;
    private TimeSpan _delay;

    public ReportScheduler(
        ReportService reportService,
        IOptions<ReportOptions> options,
        ILogger<ReportScheduler> logger)
    {
        _reportService = reportService;
        _options = options.Value;
        _logger = logger;

        _delay = CalculateNextRunDelay();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Archive.Enabled)
        {
            _logger.LogInformation("报表定时归档已禁用");
            return;
        }

        _logger.LogInformation("报表定时归档服务已启动，执行时间: {Time}", _options.Archive.Time);
 
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 等待到下次执行时间
                await Task.Delay(_delay, stoppingToken);

                // 执行归档
                await RunArchiveAsync(stoppingToken);

                // 重新计算下次执行延迟（24小时后）
                _delay = TimeSpan.FromDays(1);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("报表定时归档服务已停止");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "报表定时归档执行失败");
                // 出错后1小时后重试
                _delay = TimeSpan.FromHours(1);
            }
        }
    }

    /// <summary>
    /// 执行归档任务
    /// </summary>
    private async Task RunArchiveAsync(CancellationToken ct)
    {
        var yesterday = DateTime.Today.AddDays(-1);

        _logger.LogInformation("开始执行定时归档，日期: {Date}", yesterday);

        var results = await _reportService.GenerateDailyReportsAsync(yesterday, ct);

        foreach (var (type, path) in results)
        {
            if (path.StartsWith("ERROR:"))
            {
                _logger.LogError("报表 {Type} 归档失败: {Error}", type, path);
            }
            else
            {
                _logger.LogInformation("报表 {Type} 归档成功: {Path}", type, path);
            }
        }

        _logger.LogInformation("定时归档完成，共处理 {Count} 个报表", results.Count);
    }

    /// <summary>
    /// 计算到下次执行时间的延迟
    /// </summary>
    private TimeSpan CalculateNextRunDelay()
    {
        if (!TimeSpan.TryParse(_options.Archive.Time, out var timeOfDay))
        {
            _logger.LogWarning("无法解析归档时间配置: {Time}，使用默认值 02:00", _options.Archive.Time);
            timeOfDay = TimeSpan.FromHours(2);
        }

        var now = DateTime.Now;
        var nextRun = now.Date.Add(timeOfDay);

        // 如果今天的执行时间已过，则设置为明天
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }
}