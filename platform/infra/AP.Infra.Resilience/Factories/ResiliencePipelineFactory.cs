using AP.Infra.Resilience.Configuration;
using AP.Infra.Resilience.Policies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;

namespace AP.Infra.Resilience.Factories;

/// <summary>
/// 韧性管道工厂 (提供预定义的策略管道)
/// </summary>
public class ResiliencePipelineFactory
{
    private readonly ResiliencePipelineRegistry<string> _registry;

    // 定义 Pipeline Key 常量，供外部调用
    public static class Keys
    {
        public const string Database = "Database-Retry";
        public const string Plc = "PLC-Retry";
        public const string Grpc = "Grpc-CircuitBreaker";
    }

    public ResiliencePipelineFactory(
        ResiliencePipelineRegistry<string> registry,
        IOptions<ResilienceOptions> options,
        ILoggerFactory loggerFactory)
    {
        _registry = registry;
        var config = options.Value;
        var logger = loggerFactory.CreateLogger("Resilience");

        // 动态注册策略
        // 1. 数据库策略
        _registry.TryAddBuilder(Keys.Database,
            (builder, context) =>
            {
                builder.AddPipeline(DatabaseRetryPolicy.Create(config.DatabaseRetryCount, logger));
            });

        // 2. PLC 策略
        _registry.TryAddBuilder(Keys.Plc,
            (builder, context) => { builder.AddPipeline(PlcRetryPolicy.Create(config.PlcRetryCount, logger)); });

        // 3. gRPC 策略
        _registry.TryAddBuilder(Keys.Grpc, (builder, context) =>
        {
            builder.AddPipeline(GrpcCircuitBreakerPolicy.Create(
                config.GrpcCircuitBreakerThreshold,
                config.CircuitBreakerDurationSeconds,
                logger));
        });
    }

    /// <summary>
    /// 获取指定名称的韧性管道
    /// </summary>
    public ResiliencePipeline GetPipeline(string key)
    {
        return _registry.GetPipeline(key);
    }
}