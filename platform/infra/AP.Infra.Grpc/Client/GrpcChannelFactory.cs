// ============================================================================
// ❄ 封存代码（Frozen）：本文件属于 Server/Client 分布式模式（gRPC）技术栈。
// 当前项目范围仅 Standalone 单机模式：代码保留、不维护、不验证、不投入改进。
// 解冻需专项评审，详见 docs/EVOLUTION_PLAN.md 0.1 节。
// ============================================================================

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Grpc.Client;

/// <summary>
/// gRPC 通道工厂 (管理长连接)
/// </summary>
public class GrpcChannelFactory : IDisposable
{
    private readonly ILogger<GrpcChannelFactory> _logger;
    private GrpcChannel? _channel;
    private readonly object _lock = new();

    public GrpcChannelFactory(ILogger<GrpcChannelFactory> logger)
    {
        _logger = logger;
    }

    public GrpcChannel GetChannel(string address)
    {
        if (_channel == null || _channel.State == ConnectivityState.Shutdown)
            lock (_lock)
            {
                if (_channel == null || _channel.State == ConnectivityState.Shutdown)
                {
                    _logger.LogInformation("创建新的 gRPC 通道，目标: {Address}", address);

                    var options = new GrpcChannelOptions
                    {
                        MaxReceiveMessageSize = 5 * 1024 * 1024, // 5MB
                        MaxSendMessageSize = 5 * 1024 * 1024,
                        // 配置 KeepAlive 防止连接因空闲被切断
                        HttpHandler = new SocketsHttpHandler
                        {
                            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                            EnableMultipleHttp2Connections = true
                        }
                    };

                    _channel = GrpcChannel.ForAddress(address, options);
                }
            }

        return _channel;
    }

    public void Dispose()
    {
        _channel?.Dispose();
    }
}