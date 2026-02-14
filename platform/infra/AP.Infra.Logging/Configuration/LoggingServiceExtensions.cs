using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AP.Infra.Logging.Configuration;

/// <summary>
/// 日志服务依赖注入扩展
/// </summary>
public static class LoggingServiceExtensions
{
    /// <summary>
    /// 注册平台级日志服务 (Serilog)
    /// </summary>
    public static IServiceCollection AddPlatformLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. 初始化 Serilog 静态配置
        SerilogConfiguration.Configure(configuration);

        // 2. 将 Serilog 注册为 Microsoft.Extensions.Logging 的实现
        services.AddLogging(builder =>
        {
            builder.ClearProviders(); // 清除默认提供程序 (如 Console, Debug)
            builder.AddSerilog(dispose: true);
        });

        return services;
    }
}