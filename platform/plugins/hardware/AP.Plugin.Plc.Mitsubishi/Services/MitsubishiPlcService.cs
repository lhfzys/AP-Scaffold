#region

using System.Threading;
using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.DeviceRuntime;
using AP.Plugin.Plc.Mitsubishi.Addressing;
using AP.Plugin.Plc.Mitsubishi.Configuration;
using IoTClient.Clients.PLC;
using IoTClient.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

#endregion

namespace AP.Plugin.Plc.Mitsubishi.Services;

public class MitsubishiPlcService : IPlcService, IPlcBatchReadWrite, IDevice, IPlcTypedBatchRead
{
    // 客户端实例会在重连时被原子替换，因此不能是 readonly
    private MitsubishiClient _client;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger _logger;
    private readonly MitsubishiPlcOptions _options;
    private readonly MitsubishiVersion _version;
    private readonly string _deviceName;
    private readonly string _heartbeatAddress;

    // --- 连接运行时（Device Runtime Model：状态机 + 监督器，单一事实来源） ---
    private readonly DeviceConnectionStateMachine _stateMachine;
    private readonly ConnectionSupervisor _supervisor;
    private readonly IDisposable _loggerSubscription;
    private readonly IDisposable _bridgeSubscription;

    // --- IDevice 视图（Device Runtime Model；连接状态以状态机为唯一事实来源） ---
    /// <inheritdoc />
    public DeviceInfo Info { get; }

    /// <inheritdoc />
    public DeviceConnectionState State => _stateMachine.CurrentState;

    /// <inheritdoc />
    public event EventHandler<DeviceConnectionTransition>? Transitioned;

    // 声明能力：支持基础读写 + 批量读写 + 自动重连
    public PlcServiceFeatures SupportedFeatures =>
        PlcServiceFeatures.BasicReadWrite |
        PlcServiceFeatures.BatchReadWrite |
        PlcServiceFeatures.AutoReconnect;

    public MitsubishiPlcService(
        IOptions<MitsubishiPlcOptions> options,
        ResiliencePipeline pipeline,
        ILogger<MitsubishiPlcService> logger,
        IMediator mediator)
    {
        _options = options.Value;
        _pipeline = pipeline;
        _logger = logger;

        // 解析版本号字符串为枚举
        if (!Enum.TryParse(_options.Version, true, out _version))
            _version = MitsubishiVersion.Qna_3E;

        _deviceName = $"Mitsubishi-Q ({_options.IpAddress}:{_options.Port})";
        _heartbeatAddress = string.IsNullOrEmpty(_options.HeartbeatAddress) ? "M0" : _options.HeartbeatAddress;

        // 初始化 IoTClient
        _client = CreateClient();

        // 连接运行时：状态全部交给 ConnectionSupervisor 驱动（唯一事实来源）
        _stateMachine = new DeviceConnectionStateMachine();
        _supervisor = new ConnectionSupervisor(_stateMachine, ExecuteConnectAsync, ProbeAsync, new ConnectionSupervisorOptions
        {
            HeartbeatInterval = TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds),
            ReconnectBackoff = TimeSpan.FromSeconds(_options.ReconnectBackoffSeconds),
            SupervisorRestartDelay = TimeSpan.FromSeconds(_options.SupervisorRestartDelaySeconds),
        });
        _loggerSubscription = ConnectionSupervisorLogger.Attach(_supervisor, _stateMachine, logger, _deviceName);
        _bridgeSubscription = CreateEventBridge().Attach(_stateMachine, n => mediator.Publish(n), _deviceName);

        // IDevice 视图初始化：状态机事件转换为契约层 record 转发
        Info = new DeviceInfo("plc.main", "三菱 PLC", DeviceType.Plc, "Mitsubishi");
        _stateMachine.Transitioned += (_, args) =>
            Transitioned?.Invoke(this, new DeviceConnectionTransition(args.From, args.To, args.Reason, args.Timestamp));
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

    /// <summary>
    /// 创建新的 PLC 客户端实例
    /// </summary>
    private MitsubishiClient CreateClient()
    {
        return new MitsubishiClient(_version, _options.IpAddress, _options.Port, _options.Timeout);
    }

    /// <summary>
    /// 安全关闭并释放客户端（有界等待：Close 为无界同步调用，曾阻塞关闭流程；
    /// 超过 2 秒放弃等待直接继续——被放弃的线程仅做关闭动作，放弃是安全的）
    /// </summary>
    private static void SafeCloseClient(MitsubishiClient? client)
    {
        if (client == null) return;
        try
        {
            Task.Run(() => client.Close()).Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 关闭时可能已断开或已释放，忽略异常
        }
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
                // 硬超时保护：防止 IoTClient.Open() 内部不响应 CancellationToken 导致永久阻塞
                var connectTimeout = TimeSpan.FromSeconds(_options.Timeout > 0 ? _options.Timeout / 1000.0 * 3 : 10);

                // 每次连接尝试都使用新的客户端实例，避免旧客户端的残留状态或阻塞线程影响新连接
                var newClient = CreateClient();

                using var attemptCts = new CancellationTokenSource(connectTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, attemptCts.Token);

                // 使用 LongRunning 在专用线程上执行同步的 Open()，避免占用线程池线程
                var openTask = Task.Factory.StartNew<dynamic>(() =>
                {
                    try
                    {
                        return newClient.Open();
                    }
                    catch (Exception ex)
                    {
                        return new { IsSucceed = false, Err = ex.Message };
                    }
                }, linkedCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                var delayTask = Task.Delay(connectTimeout, linkedCts.Token);
                var completedTask = await Task.WhenAny(openTask, delayTask);

                if (completedTask == delayTask)
                {
                    // 超时：丢弃本次尝试的客户端，不等待 Open() 返回，避免线程长期被占用
                    // 注册一个延续来观察可能发生的异常，防止触发 UnobservedTaskException
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
                    // 原子替换当前客户端，确保读写操作不会拿到已关闭的实例
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
            var result = _client.ReadInt16(_heartbeatAddress);
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
        // 地址预检 + 规范化：非法地址抛 MitsubishiAddressException（ArgumentException 子类），合法地址统一为标准表示
        var normalized = McAddress.Parse(address).Normalized;

        // 捕获当前客户端引用，避免重连替换实例后在同一次读写中混用不同客户端
        var client = _client;

        return await _pipeline.ExecuteAsync(async token =>
        {
            // 根据 T 的类型调用不同的 IoTClient 方法
            dynamic result;

            // 将同步调用包装为 Task
            await Task.Yield(); // 确保异步上下文

            if (typeof(T) == typeof(bool))
                result = client.ReadBoolean(normalized);
            else if (typeof(T) == typeof(short))
                result = client.ReadInt16(normalized);
            else if (typeof(T) == typeof(ushort))
                result = client.ReadUInt16(normalized);
            else if (typeof(T) == typeof(int))
                result = client.ReadInt32(normalized);
            else if (typeof(T) == typeof(uint))
                result = client.ReadUInt32(normalized);
            else if (typeof(T) == typeof(float))
                result = client.ReadFloat(normalized);
            else
                throw new NotSupportedException($"不支持的类型: {typeof(T).Name}");

            if (result.IsSucceed) return (T)result.Value;

            throw new Exception($"读取失败 [{normalized}]: {result.Err}");
        }, ct);
    }

    public async Task WriteAsync<T>(string address, T value, CancellationToken ct = default)
    {
        // 地址预检 + 规范化：非法地址抛 MitsubishiAddressException（ArgumentException 子类）
        var normalized = McAddress.Parse(address).Normalized;

        // 捕获当前客户端引用，避免重连替换实例后在同一次写入中混用不同客户端
        var client = _client;

        await _pipeline.ExecuteAsync(async token =>
        {
            dynamic result;
            await Task.Yield();

            if (value is bool b)
                result = client.Write(normalized, b);
            else if (value is short s)
                result = client.Write(normalized, s);
            else if (value is ushort us)
                result = client.Write(normalized, us);
            else if (value is int i)
                result = client.Write(normalized, i);
            else if (value is uint ui)
                result = client.Write(normalized, ui);
            else if (value is float f)
                result = client.Write(normalized, f);
            else
                throw new NotSupportedException($"不支持的类型: {typeof(T).Name}");

            if (!result.IsSucceed) throw new Exception($"写入失败 [{normalized}]: {result.Err}");
        }, ct);
    }

    // --- 批量读写 (IoTClient 支持) ---

    public async Task<Dictionary<string, object>> ReadBatchAsync(string[] addresses, CancellationToken ct = default)
    {
        // 注意：IoTClient 的批量读取通常要求地址连续，这里如果是散乱地址，
        // 建议循环读取，或者根据业务逻辑优化。
        // 为了演示简单，这里采用循环读取（但在 Polly 管道内，保证整体可靠性）

        return await _pipeline.ExecuteAsync(async token =>
        {
            var result = new Dictionary<string, object>();
            foreach (var addr in addresses)
            {
                // 默认按 short 读取，实际业务可能需要元数据指定类型
                var val = await ReadAsync<short>(addr, token);
                result[addr] = val;
            }

            return result;
        }, ct);
    }

    /// <summary>
    /// 带类型批量读取（三菱为循环逐条按类型读，对外与真批量同一契约）。
    /// 整批中任一条失败即抛出（与驱动单读语义一致），由调用方决定降级策略。
    /// </summary>
    public async Task<Dictionary<string, object>> ReadBatchAsync(IReadOnlyList<BatchReadItem> items, CancellationToken ct = default)
    {
        var result = new Dictionary<string, object>();
        foreach (var item in items)
        {
            var normalized = McAddress.Parse(item.Address).Normalized;
            result[normalized] = await ReadByTypeAsync(normalized, item.DataType, ct);
        }
        return result;
    }

    /// <summary>按 TagDataType 分发单点读取（internal 供测试）。</summary>
    internal async Task<object> ReadByTypeAsync(string address, TagDataType type, CancellationToken ct) => type switch
    {
        TagDataType.Bool => await ReadAsync<bool>(address, ct),
        TagDataType.Int16 => await ReadAsync<short>(address, ct),
        TagDataType.UInt16 => await ReadAsync<ushort>(address, ct),
        TagDataType.Int32 => await ReadAsync<int>(address, ct),
        TagDataType.UInt32 => await ReadAsync<uint>(address, ct),
        TagDataType.Float => await ReadAsync<float>(address, ct),
        _ => throw new NotSupportedException($"三菱驱动暂不支持类型: {type}"),
    };

    public async Task WriteBatchAsync(Dictionary<string, object> data, CancellationToken ct = default)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            foreach (var kvp in data) await WriteAsync(kvp.Key, kvp.Value, token);
        }, ct);
    }
}
