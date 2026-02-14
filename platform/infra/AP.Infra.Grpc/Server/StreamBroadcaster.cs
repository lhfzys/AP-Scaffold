using AP.Contracts.Communication.Grpc.AutomationGate;
using AP.Contracts.Communication.Grpc.Common;
using AP.Contracts.Hardware.Events;
using AP.Shared.Utilities.Helpers;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AP.Infra.Grpc.Server;

/// <summary>
/// gRPC 数据广播器 (实现 MediatR 事件处理器)
/// 负责将 PLC 数据变化事件广播给所有连接的 gRPC 客户端
/// </summary>
public class StreamBroadcaster : INotificationHandler<PlcDataChangedEvent>
{
    private readonly ILogger<StreamBroadcaster> _logger;

    // 线程安全的流集合: Key=ClientId, Value=StreamWriter
    private readonly ConcurrentDictionary<string, IServerStreamWriter<GrpcPlcData>> _clients = new();

    public StreamBroadcaster(ILogger<StreamBroadcaster> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 处理 PLC 数据变化事件 (由硬件插件发布)
    /// </summary>
    public async Task Handle(PlcDataChangedEvent notification, CancellationToken cancellationToken)
    {
        if (_clients.IsEmpty) return;

        // 构建 gRPC 消息模型
        var grpcData = new GrpcPlcData
        {
            DeviceName = notification.DeviceName,
            Tag = notification.Address,
            ValueJson = SerializationHelper.ToJson(notification.Value),
            Timestamp = new Timestamp { Ticks = notification.Timestamp.Ticks }
        };

        // 并行推送给所有客户端 (生产级性能优化)
        var tasks = _clients.Select(async kvp =>
        {
            var clientId = kvp.Key;
            var stream = kvp.Value;

            try
            {
                await stream.WriteAsync(grpcData, cancellationToken);
            }
            catch (Exception ex)
            {
                // 如果写入失败(如客户端断开)，记录日志并移除该客户端
                _logger.LogWarning("向客户端 {ClientId} 推送数据失败: {Message}", clientId, ex.Message);
                RemoveClient(clientId);
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 注册客户端流
    /// </summary>
    public void RegisterClient(string clientId, IServerStreamWriter<GrpcPlcData> stream)
    {
        _clients.AddOrUpdate(clientId, stream, (key, oldValue) => stream);
        _logger.LogInformation("客户端已订阅数据流: {ClientId}. 当前在线: {Count}", clientId, _clients.Count);
    }

    /// <summary>
    /// 移除客户端流
    /// </summary>
    public void RemoveClient(string clientId)
    {
        if (_clients.TryRemove(clientId, out _))
            _logger.LogInformation("客户端取消订阅: {ClientId}. 当前在线: {Count}", clientId, _clients.Count);
    }
}