#region

using System.Collections.Concurrent;
using System.Threading.Channels;
using AP.Contracts.Communication.Grpc.AutomationGate;
using AP.Contracts.Communication.Grpc.Common;
using AP.Contracts.Hardware.Events;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Infra.Grpc.Server;

/// <summary>
///     gRPC 数据广播器 (实现 MediatR 事件处理器)
///     使用 System.Threading.Channels 实现背压控制，保护 MediatR 主线程
/// </summary>
public class StreamBroadcaster : INotificationHandler<PlcDataChangedEvent>
{
    private readonly ILogger<StreamBroadcaster> _logger;

    // 线程安全的字典：Key = ClientId, Value = 专属的缓冲通道 (Channel)
    private readonly ConcurrentDictionary<string, Channel<GrpcPlcData>> _clientChannels = new();

    // 用于安全取消和回收各个客户端的后台推送任务
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _clientCts = new();

    public StreamBroadcaster(ILogger<StreamBroadcaster> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     处理 PLC 数据变化事件 (生产者)
    /// </summary>
    public Task Handle(PlcDataChangedEvent notification, CancellationToken cancellationToken)
    {
        if (_clientChannels.IsEmpty) return Task.CompletedTask;

        var grpcData = new GrpcPlcData
        {
            DeviceName = notification.DeviceName,
            Tag = notification.Address,
            ValueJson = notification.Value.ToString(), // 后续我们会优化这里，不再依赖JSON
            Timestamp = new Timestamp { Ticks = notification.Timestamp.Ticks }
        };

        foreach (var kvp in _clientChannels)
            // TryWrite 是非阻塞的。如果缓冲队列满了，DropOldest 策略会自动丢弃旧数据，保证最新数据进入
            kvp.Value.Writer.TryWrite(grpcData);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     注册客户端流
    /// </summary>
    public void RegisterClient(string clientId, IServerStreamWriter<GrpcPlcData> stream)
    {
        // 创建有界通道 (BoundedChannel)，容量设为 1000（可调）
        // 关键背压策略：如果客户端网络卡顿导致消费慢，队列塞满时丢弃最老的数据 (DropOldest)
        var channel = Channel.CreateBounded<GrpcPlcData>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true, // 每个客户端一个专属读取器，性能更优
            SingleWriter = false
        });

        var cts = new CancellationTokenSource();

        _clientChannels.AddOrUpdate(clientId, channel, (_, __) => channel);
        _clientCts.AddOrUpdate(clientId, cts, (_, __) => cts);

        _logger.LogInformation("客户端已订阅数据流: {ClientId}. 当前在线: {Count}", clientId, _clientChannels.Count);

        // 启动专属的后台消费者任务，独立处理该客户端的网络 I/O
        _ = Task.Run(() => ConsumeAndSendAsync(clientId, stream, channel.Reader, cts.Token));
    }

    /// <summary>
    ///     移除客户端流 (清理资源)
    /// </summary>
    public void RemoveClient(string clientId)
    {
        if (_clientCts.TryRemove(clientId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (_clientChannels.TryRemove(clientId, out var channel)) channel.Writer.TryComplete();

        _logger.LogInformation("客户端取消订阅: {ClientId}. 当前在线: {Count}", clientId, _clientChannels.Count);
    }

    /// <summary>
    ///     后台网络推送任务 (消费者)
    /// </summary>
    private async Task ConsumeAndSendAsync(string clientId, IServerStreamWriter<GrpcPlcData> stream,
        ChannelReader<GrpcPlcData> reader, CancellationToken ct)
    {
        try
        {
            // WaitToReadAsync 机制极为高效，没有数据时让出线程，有数据时唤醒
            await foreach (var data in reader.ReadAllAsync(ct))
                // 这里才发生真正的网络 I/O 等待
                await stream.WriteAsync(data, ct);
        }
        catch (OperationCanceledException)
        {
            // 正常断开
        }
        catch (Exception ex)
        {
            _logger.LogWarning("向客户端 {ClientId} 推送数据失败: {Message}", clientId, ex.Message);
            RemoveClient(clientId);
        }
    }
}