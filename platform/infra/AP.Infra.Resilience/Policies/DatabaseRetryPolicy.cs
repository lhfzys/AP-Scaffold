using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace AP.Infra.Resilience.Policies;

/// <summary>
/// 数据库重试策略构建器
/// </summary>
public static class DatabaseRetryPolicy
{
    public static ResiliencePipeline Create(int retryCount, ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            // 捕获常见的瞬时异常 (此处需根据实际数据库 Provider 调整，这里作为示例捕获所有 Exception)
            // 生产环境建议捕获具体的 DbException 或 TimeoutException
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = retryCount,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential, // 指数退避: 1s, 2s, 4s...
                OnRetry = args =>
                {
                    logger.LogWarning("数据库操作失败，正在重试 ({Retry}/{Max}). 异常: {Message}",
                        args.AttemptNumber, retryCount, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}