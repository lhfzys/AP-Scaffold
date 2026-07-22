using AP.Infra.Resilience.Factories;
using AP.Infra.Resilience.Policies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace AP.Infra.Resilience.Configuration;

/// <summary>
/// 将所有内容注册到 DI 容器中
/// </summary>
public static class ResilienceServiceExtensions
{
    /// <summary>
    /// 注册平台韧性服务
    /// </summary>
    public static IServiceCollection AddPlatformResilience(this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. 绑定配置
        services.Configure<ResilienceOptions>(configuration.GetSection(ResilienceOptions.SectionName));

        // 2. 注册 Polly Registry，并直接登记平台管道（注册表自描述，不依赖工厂的解析时机）
        services.AddResiliencePipelineRegistry<string>();
        services.AddResiliencePipeline<string>(ResiliencePipelineFactory.Keys.Database, (builder, context) =>
        {
            var (options, logger) = Resolve(context.ServiceProvider);
            builder.AddPipeline(DatabaseRetryPolicy.Create(options.DatabaseRetryCount, logger));
        });
        services.AddResiliencePipeline<string>(ResiliencePipelineFactory.Keys.Plc, (builder, context) =>
        {
            var (options, logger) = Resolve(context.ServiceProvider);
            builder.AddPipeline(PlcRetryPolicy.Create(options.PlcRetryCount, logger));
        });
        services.AddResiliencePipeline<string>(ResiliencePipelineFactory.Keys.Grpc, (builder, context) =>
        {
            var (options, logger) = Resolve(context.ServiceProvider);
            builder.AddPipeline(GrpcCircuitBreakerPolicy.Create(
                options.GrpcCircuitBreakerThreshold,
                options.CircuitBreakerDurationSeconds,
                logger));
        });

        // 3. 注册自定义工厂（PLC 插件等通过 GetPipeline 便捷取管道；
        //    其构造器内的 TryAddBuilder 与上面的注册幂等共存）
        services.AddSingleton<ResiliencePipelineFactory>();

        return services;
    }

    private static (ResilienceOptions Options, ILogger Logger) Resolve(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Resilience");
        return (options, logger);
    }
}