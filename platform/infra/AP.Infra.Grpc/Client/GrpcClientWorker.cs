using AP.Contracts.Communication.Grpc.AutomationGate;
using AP.Contracts.Hardware.Events;
using AP.Shared.Utilities.Constants;
using AP.Shared.Utilities.Helpers;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Grpc.Client;

/// <summary>
/// 客户端后台工作者 (负责与 Server 保持长连接并接收数据)
/// </summary>
public class GrpcClientWorker : BackgroundService
{
    private readonly ILogger<GrpcClientWorker> _logger;
    private readonly GrpcChannelFactory _channelFactory;
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;

    // 客户端标识
    private readonly string _clientId;
    private readonly string _machineName;
    private readonly string _clientName;

    public GrpcClientWorker(
        ILogger<GrpcClientWorker> logger,
        GrpcChannelFactory channelFactory,
        IConfiguration config,
        IMediator mediator)
    {
        _logger = logger;
        _channelFactory = channelFactory;
        _config = config;
        _mediator = mediator;

        var configId = _config[GlobalConstants.ConfigKeys.ClientId];
        _machineName = Environment.MachineName;
        _clientId = !string.IsNullOrEmpty(configId)
            ? configId
            : $"{_machineName}_{Guid.NewGuid().ToString()[..8]}";

        var configName = _config[GlobalConstants.ConfigKeys.ClientName];
        _clientName = !string.IsNullOrEmpty(configName)
            ? configName
            : _machineName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serverUrl = _config[GlobalConstants.ConfigKeys.GrpcServerUrl];
        if (string.IsNullOrEmpty(serverUrl))
        {
            serverUrl = "http://localhost:5000";
            _logger.LogWarning("未配置 {Key}，使用默认地址: {Url}", GlobalConstants.ConfigKeys.GrpcServerUrl, serverUrl);
        }

        _logger.LogInformation("gRPC Client 启动. ID: {Id}, 目标: {Url}", _clientId, serverUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var channel = _channelFactory.GetChannel(serverUrl);
                var client = new AutomationGate.AutomationGateClient(channel);

                // 1. 建立数据流订阅
                using var call = client.StreamPlcData(new SubscriptionRequest
                {
                    ClientId = _clientId,
                    ClientName = _clientName,
                    Topics = { "All" } // 默认订阅所有
                }, cancellationToken: stoppingToken);

                _logger.LogInformation("已连接到服务器，开始接收数据流...");

                // 2. 循环读取数据流
                await foreach (var data in call.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    // 3. 将远程数据转换为本地事件并发布
                    var realValue = ParseValue(data.ValueJson);
                    var localEvent = new PlcDataChangedEvent(
                        data.DeviceName,
                        data.Tag,
                        realValue,
                        new DateTime(data.Timestamp.Ticks)
                    );

                    await _mediator.Publish(localEvent, stoppingToken);

                    // _logger.LogDebug("收到远程数据: {Tag}={Value}", data.Tag, data.ValueJson);
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                _logger.LogWarning("无法连接到服务器，5秒后重试...");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "gRPC 客户端发生错误");
            }

            // 断线重连等待
            await Task.Delay(5000, stoppingToken);
        }
    }

    private object ParseValue(string json)
    {
        if (long.TryParse(json, out var l)) return l;
        if (double.TryParse(json, out var d)) return d;
        if (bool.TryParse(json, out var b)) return b;
        if (json.StartsWith("\"") && json.EndsWith("\"")) return json.Trim('"');
        return SerializationHelper.FromJson<object>(json) ?? json;
    }
}