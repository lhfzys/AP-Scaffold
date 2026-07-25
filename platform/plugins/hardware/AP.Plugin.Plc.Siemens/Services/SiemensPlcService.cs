using System.Threading;
using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.DeviceRuntime;
using IoTClient.Clients.PLC;
using IoTClient.Common.Enums;
using IoTClient.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace AP.Plugin.Plc.Siemens.Services;

/// <summary>
/// 西门子 PLC 服务实现（基于 IoTClient.SiemensClient）。
/// </summary>
public class SiemensPlcService : IPlcService, IPlcBatchReadWrite
{
    private SiemensClient _client;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger _logger;
    private readonly PlcOptions _options;
    private readonly SiemensVersion _version;
    private readonly string _deviceName;
    private readonly string _heartbeatAddress;

    // --- 连接运行时（Device Runtime Model：状态机 + 监督器，单一事实来源） ---
    private readonly DeviceConnectionStateMachine _stateMachine;
    private readonly ConnectionSupervisor _supervisor;
    private readonly IDisposable _loggerSubscription;
    private readonly IDisposable _bridgeSubscription;

    public PlcServiceFeatures SupportedFeatures =>
        PlcServiceFeatures.BasicReadWrite |
        PlcServiceFeatures.BatchReadWrite |
        PlcServiceFeatures.AutoReconnect;

    public SiemensPlcService(
        IOptions<PlcOptions> options,
        ResiliencePipeline pipeline,
        ILogger<SiemensPlcService> logger,
        IMediator mediator)
    {
        _options = options.Value;
        _pipeline = pipeline;
        _logger = logger;

        if (!Enum.TryParse(_options.Model, true, out _version))
            _version = SiemensVersion.S7_1200;

        _deviceName = $"Siemens-S7 ({_options.IpAddress}:{_options.Port})";
        _heartbeatAddress = string.IsNullOrEmpty(_options.HeartbeatAddress) ? "DB1.0.0" : _options.HeartbeatAddress;
        _client = CreateClient();

        // 连接运行时：状态全部交给 ConnectionSupervisor 驱动（唯一事实来源）
        _stateMachine = new DeviceConnectionStateMachine();
        _supervisor = new ConnectionSupervisor(_stateMachine, ExecuteConnectAsync, ProbeAsync);
        _loggerSubscription = ConnectionSupervisorLogger.Attach(_supervisor, _stateMachine, logger, _deviceName);
        _bridgeSubscription = CreateEventBridge().Attach(_stateMachine, n => mediator.Publish(n), _deviceName);
    }

    /// <summary>
    /// 状态迁移 → MediatR 事件映射表（桥接属于驱动，Supervisor 不认识 MediatR）。
    /// 沿迁移边触发，重连重试期间不重复发事件。
    /// </summary>
    private static TransitionEventBridge CreateEventBridge()
    {
        return new TransitionEventBridge()
            .Map(DeviceConnectionState.Disconnected, DeviceConnectionState.Connecting,
                (device, _) => new DeviceConnectingEvent(device))
            .Map(DeviceConnectionState.Connecting, DeviceConnectionState.Connected,
                (device, _) => new DeviceConnectedEvent(device, DateTime.Now))
            .Map(DeviceConnectionState.Reconnecting, DeviceConnectionState.Connected,
                (device, _) => new DeviceConnectedEvent(device, DateTime.Now))
            .Map(DeviceConnectionState.Connecting, DeviceConnectionState.Reconnecting,
                (device, reason) => new DeviceConnectionFailedEvent(device, reason ?? "连接失败"))
            .Map(DeviceConnectionState.Connected, DeviceConnectionState.Reconnecting,
                (device, reason) => new DeviceDisconnectedEvent(device, reason ?? "心跳丢失/网络异常", DateTime.Now))
            .Map(DeviceConnectionState.Connected, DeviceConnectionState.Disconnected,
                (device, reason) => new DeviceDisconnectedEvent(device, reason ?? "主动断开", DateTime.Now))
            .Map(DeviceConnectionState.Reconnecting, DeviceConnectionState.Disconnected,
                (device, reason) => new DeviceDisconnectedEvent(device, reason ?? "主动断开", DateTime.Now))
            .Map(DeviceConnectionState.Connecting, DeviceConnectionState.Disconnected,
                (device, reason) => new DeviceDisconnectedEvent(device, reason ?? "主动断开", DateTime.Now));
    }

    private SiemensClient CreateClient()
    {
        return new SiemensClient(_version, _options.IpAddress, _options.Port);
    }

    private static void SafeCloseClient(SiemensClient? client)
    {
        if (client == null) return;
        try { client.Close(); }
        catch { /* 关闭时可能已断开，忽略 */ }
    }

    /// <summary>
    ///     连接方法（首次连接或人工触发）：启动连接监督器，
    ///     Connecting/Connected/Reconnecting 状态全部由监督器驱动。
    /// </summary>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("{Device} 连接监督已启动，心跳地址: {Address}", _deviceName, _heartbeatAddress);
        _supervisor.Start();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     连接动作（供监督器调用，含硬超时保护与 Polly 重试）
    /// </summary>
    private async Task<ConnectionAttemptResult> ExecuteConnectAsync(CancellationToken ct)
    {
        try
        {
            await _pipeline.ExecuteAsync(async token =>
            {
                var connectTimeout = TimeSpan.FromSeconds(_options.Timeout > 0 ? _options.Timeout / 1000.0 * 3 : 10);
                var newClient = CreateClient();

                using var attemptCts = new CancellationTokenSource(connectTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, attemptCts.Token);

                var openTask = Task.Factory.StartNew<dynamic>(() =>
                {
                    try { return newClient.Open(); }
                    catch (Exception ex) { return new { IsSucceed = false, Err = ex.Message }; }
                }, linkedCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                var delayTask = Task.Delay(connectTimeout, linkedCts.Token);
                var completedTask = await Task.WhenAny(openTask, delayTask);

                if (completedTask == delayTask)
                {
                    _ = openTask.ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            _logger.LogDebug(t.Exception, "{Device} 连接尝试在超时后抛出异常，已忽略", _deviceName);
                    }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

                    SafeCloseClient(newClient);
                    throw new TimeoutException($"PLC 连接超时 ({connectTimeout.TotalSeconds:F0}s): {_options.IpAddress}:{_options.Port}");
                }

                var result = await openTask;

                if (result.IsSucceed)
                {
                    var oldClient = Interlocked.Exchange(ref _client, newClient);
                    SafeCloseClient(oldClient);
                }
                else
                {
                    SafeCloseClient(newClient);
                    throw new Exception($"连接被拒绝或超时: {result.Err}");
                }
            }, ct);

            return ConnectionAttemptResult.Ok();
        }
        catch (OperationCanceledException)
        {
            throw; // 取消语义原样上传，由监督器处理
        }
        catch (Exception ex)
        {
            return ConnectionAttemptResult.Fail(ex.Message, ex);
        }
    }

    /// <summary>
    ///     心跳探测动作（供监督器调用）
    /// </summary>
    private Task<ConnectionAttemptResult> ProbeAsync(CancellationToken ct)
    {
        try
        {
            var result = _client.ReadBoolean(_heartbeatAddress);
            return Task.FromResult(result.IsSucceed
                ? ConnectionAttemptResult.Ok()
                : ConnectionAttemptResult.Fail(result.Err));
        }
        catch (ObjectDisposedException)
        {
            // 重连替换客户端的瞬间可能读到已释放的旧实例：判定掉线即可
            return Task.FromResult(ConnectionAttemptResult.Fail("客户端已释放"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ConnectionAttemptResult.Fail(ex.Message, ex));
        }
    }

    public Task DisconnectAsync()
    {
        _supervisor.Stop();
        // 桥接按 *→Disconnected 映射发布 DeviceDisconnectedEvent
        _stateMachine.TryTransition(DeviceConnectionState.Disconnected, "主动断开");
        SafeCloseClient(_client);
        return Task.CompletedTask;
    }

    public Task<bool> IsConnectedAsync()
    {
        // 单一事实来源：连接状态只从状态机读取
        return Task.FromResult(_stateMachine.CurrentState == DeviceConnectionState.Connected);
    }

    public async Task<T> ReadAsync<T>(string address, CancellationToken ct = default)
    {
        var client = _client;
        return await _pipeline.ExecuteAsync(async token =>
        {
            await Task.Yield();
            dynamic result;

            if (typeof(T) == typeof(bool))
                result = client.ReadBoolean(address);
            else if (typeof(T) == typeof(short))
                result = client.ReadInt16(address);
            else if (typeof(T) == typeof(ushort))
                result = client.ReadUInt16(address);
            else if (typeof(T) == typeof(int))
                result = client.ReadInt32(address);
            else if (typeof(T) == typeof(uint))
                result = client.ReadUInt32(address);
            else if (typeof(T) == typeof(float))
                result = client.ReadFloat(address);
            else if (typeof(T) == typeof(string))
                result = client.ReadString(address);
            else
                throw new NotSupportedException($"不支持的类型: {typeof(T).Name}");

            if (result.IsSucceed) return (T)result.Value;
            throw new Exception($"读取失败 [{address}]: {result.Err}");
        }, ct);
    }

    public async Task WriteAsync<T>(string address, T value, CancellationToken ct = default)
    {
        var client = _client;
        await _pipeline.ExecuteAsync(async token =>
        {
            await Task.Yield();
            dynamic result;

            switch (value)
            {
                case bool b: result = client.Write(address, b); break;
                case short s: result = client.Write(address, s); break;
                case ushort us: result = client.Write(address, us); break;
                case int i: result = client.Write(address, i); break;
                case uint ui: result = client.Write(address, ui); break;
                case float f: result = client.Write(address, f); break;
                case double d: result = client.Write(address, (float)d); break;
                case string s: result = client.Write(address, s); break;
                default:
                    throw new NotSupportedException($"不支持的类型: {typeof(T).Name}");
            }

            if (!result.IsSucceed) throw new Exception($"写入失败 [{address}]: {result.Err}");
        }, ct);
    }

    public async Task<Dictionary<string, object>> ReadBatchAsync(string[] addresses, CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await Task.Yield();
            var request = new Dictionary<string, DataTypeEnum>();
            foreach (var address in addresses)
                request[address] = DataTypeEnum.Int16; // 默认按 short 读取，业务层可按需扩展

            var response = _client.BatchRead(request);
            if (!response.IsSucceed)
                throw new Exception($"批量读取失败: {response.Err}");

            return response.Value ?? new Dictionary<string, object>();
        }, ct);
    }

    public async Task WriteBatchAsync(Dictionary<string, object> data, CancellationToken ct = default)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            await Task.Yield();
            var request = new Dictionary<string, object>(data);
            var response = _client.BatchWrite(request);
            if (!response.IsSucceed)
                throw new Exception($"批量写入失败: {response.Err}");
        }, ct);
    }
}
