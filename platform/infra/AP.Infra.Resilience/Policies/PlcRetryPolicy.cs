using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace AP.Infra.Resilience.Policies;

/// <summary>
/// PLC 通信重试策略。
///
/// 仅用于连接/读取/探测；**写操作禁止使用本策略**：
/// 通信超时不等于 PLC 未执行，重试可能重复触发启动位、配方号、清零命令等非幂等写入。
/// </summary>
public static class PlcRetryPolicy
{
    public static ResiliencePipeline Create(int retryCount, ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(), // 捕获通信异常
                MaxRetryAttempts = retryCount,
                Delay = TimeSpan.FromMilliseconds(500), // PLC 通常要求快速重试
                BackoffType = DelayBackoffType.Constant, // 固定间隔
                OnRetry = args =>
                {
                    logger.LogWarning("PLC 通信失败，正在重试 ({Retry}/{Max})...", args.AttemptNumber, retryCount);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}