using AP.Infra.Logging.Enrichers;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace AP.Infra.Logging.Configuration;

/// <summary>
/// Serilog 全局配置中心
/// </summary>
public static class SerilogConfiguration
{
    public static void Configure(IConfiguration configuration)
    {
        // 基础日志配置
        var loggerConfiguration = new LoggerConfiguration()
            // 1. 设置最小日志级别 (默认 Information，可被 appsettings 覆盖)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)

            // 2. 加载自定义增强器
            .Enrich.FromLogContext()
            .Enrich.With(new MachineNameEnricher())
            .Enrich.With(new ThreadIdEnricher())
            .Enrich.WithProcessId() // 使用官方包补充进程ID

            // 3. 读取配置文件 (appsettings.json 中的 Serilog 节点)
            .ReadFrom.Configuration(configuration)

            // 4. 配置输出目标 (Sinks)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                "logs/log-.txt", // 每天生成一个文件: log-20231027.txt
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31, // 保留31天
                fileSizeLimitBytes: 10 * 1024 * 1024, // 单个文件最大 10MB
                rollOnFileSizeLimit: true,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [PID:{ProcessId}] [TID:{ThreadId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            );

        // 创建全局 Logger
        Log.Logger = loggerConfiguration.CreateLogger();
    }
}