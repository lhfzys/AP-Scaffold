using AP.Contracts.Communication.Grpc.AutomationGate;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Grpc.Server;

/// <summary>
/// 自动化网关服务实现 (继承自 Proto 生成的基类)
/// </summary>
public class GrpcGateService : AutomationGate.AutomationGateBase
{
    private readonly ILogger<GrpcGateService> _logger;
    private readonly StreamBroadcaster _broadcaster;

    public GrpcGateService(ILogger<GrpcGateService> logger, StreamBroadcaster broadcaster)
    {
        _logger = logger;
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// 处理心跳请求
    /// </summary>
    public override Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        // _logger.LogDebug("收到心跳: {ClientId}", request.ClientId); // 生产环境可减少日志量
        return Task.FromResult(new HeartbeatResponse
        {
            IsAlive = true,
            ServerTime = DateTime.UtcNow.Ticks
        });
    }

    /// <summary>
    /// 处理数据流订阅 (Server Streaming)
    /// </summary>
    public override async Task StreamPlcData(
        SubscriptionRequest request,
        IServerStreamWriter<GrpcPlcData> responseStream,
        ServerCallContext context)
    {
        var clientId = request.ClientId;
        var clientName = request.ClientName;

        _logger.LogInformation("收到订阅请求. ID: {Id}, 名称: {Name}, 关注: {Topics}",
            clientId, clientName, string.Join(",", request.Topics));
        // 1. 注册到广播器
        _broadcaster.RegisterClient(clientId, responseStream);

        try
        {
            // 2. 保持连接开启，直到客户端取消或发生异常
            // 只要不返回，流就会一直保持打开状态
            while (!context.CancellationToken.IsCancellationRequested)
                // 发送心跳包或空包保活 (可选)
                // 这里我们简单地每秒检查一次取消状态
                await Task.Delay(1000, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("客户端连接已取消: {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据流异常: {ClientId}", clientId);
        }
        finally
        {
            // 3. 清理资源
            _broadcaster.RemoveClient(clientId);
        }
    }
}