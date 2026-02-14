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