using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AP.Infra.Grpc.Server.Interceptors;

/// <summary>
/// 记录所有 gRPC 调用的请求和响应时间，用于性能监控
/// </summary>
public class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC 调用失败: {Method}", context.Method);
            throw;
        }
        finally
        {
            sw.Stop();
            // 记录慢调用 (>500ms)
            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("gRPC 慢调用: {Method} 耗时 {Elapsed}ms", context.Method, sw.ElapsedMilliseconds);
        }
    }
}