using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace AP.Infra.Resilience.Policies;

/// <summary>
/// gRPC 熔断策略 (保护服务端不被过多请求压垮)
/// </summary>
public static class GrpcCircuitBreakerPolicy
{
    public static ResiliencePipeline Create(int failureThreshold, int durationSeconds, ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 0.5, // 50% 失败率触发
                SamplingDuration = TimeSpan.FromSeconds(30), // 采样窗口
                MinimumThroughput = 5, // 最小请求数
                BreakDuration = TimeSpan.FromSeconds(durationSeconds), // 熔断时长
                OnOpened = args =>
                {
                    logger.LogError("gRPC 链路已熔断! 暂停服务 {Duration} 秒. 原因: {Reason}", durationSeconds, args.BreakDuration);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("gRPC 链路已恢复，熔断器关闭。");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogWarning("gRPC 熔断器半开，正在试探性恢复...");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}