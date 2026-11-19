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

        // 启动先补一次：跨天/停机后不必等到下一个 02:00；已成功的日期会自动跳过。
        try
        {
            await RunArchiveAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("报表定时归档服务已停止");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动补档失败，等待下次定时执行");
        }

        _delay = CalculateNextRunDelay();

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
        var days = Math.Clamp(_options.Archive.CatchUpDays, 1, 31);
        _logger.LogInformation("开始执行定时归档，补档天数: {Days}", days);

        for (var offset = 1; offset <= days; offset++)
        {
            var date = DateTime.Today.AddDays(-offset);
            _logger.LogInformation("归档日期: {Date}", date);

            var results = await _reportService.GenerateDailyReportsAsync(date, ct);

            foreach (var (type, path) in results)
            {
                if (path.StartsWith("ERROR:", StringComparison.Ordinal))
                {
                    _logger.LogError("报表 {Type} 归档失败（{Date}）: {Error}", type, date, path);
                }
                else
                {
                    _logger.LogInformation("报表 {Type} 归档成功（{Date}）: {Path}", type, date, path);
                }
            }

            _logger.LogInformation("日期 {Date} 归档完成，共处理 {Count} 个报表", date, results.Count);
        }
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