#region

using System.Threading;
using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.Services;
using AP.Plugin.Plc.Mitsubishi.Configuration;
using IoTClient.Clients.PLC;
using IoTClient.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

#endregion

namespace AP.Plugin.Plc.Mitsubishi.Services;

public class MitsubishiPlcService : IPlcService, IPlcBatchReadWrite
{
    // 客户端实例会在重连时被原子替换，因此不能是 readonly
    private MitsubishiClient _client;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger _logger;
    private readonly MitsubishiPlcOptions _options;
    private readonly IMediator _mediator;
    private readonly MitsubishiVersion _version;

    // --- 看门狗状态 ---
    private bool _isWatchdogRunning;
    private CancellationTokenSource? _watchdogCts;
    private bool _currentConnectionState;
    private readonly string _deviceName;

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
        _mediator = mediator;

        // 解析版本号字符串为枚举
        if (!Enum.TryParse(_options.Version, true, out _version))
            _version = MitsubishiVersion.Qna_3E;

        _deviceName = $"Mitsubishi-Q ({_options.IpAddress}:{_options.Port})";

        // 初始化 IoTClient
        _client = CreateClient();
    }

    /// <summary>
    /// 创建新的 PLC 客户端实例
    /// </summary>
    private MitsubishiClient CreateClient()
    {
        return new MitsubishiClient(_version, _options.IpAddress, _options.Port, _options.Timeout);
    }

    /// <summary>
    /// 安全关闭并释放客户端
    /// </summary>
    private static void SafeCloseClient(MitsubishiClient? client)
    {
        if (client == null) return;
        try
        {
            client.Close();
        }
        catch
        {
            // 关闭时可能已断开或已释放，忽略异常
        }
    }

    /// <summary>
    ///     连接方法（首次连接或人工触发）
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            // 1. 尝试执行一次真正的连接 (受 Polly 策略保护，比如重试5次)
            await ExecuteConnectInternalAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Device} 首次连接失败，看门狗将在后台接管并持续尝试重连", _deviceName);
        }
        finally
        {
            // 2. 无论首次连接成功还是失败，都必须把看门狗跑起来！
            StartWatchdog();
        }
    }

    /// <summary>
    ///     连接与握手逻辑（含硬超时保护）
    /// </summary>
    private async Task ExecuteConnectInternalAsync(CancellationToken ct)
    {
        await _mediator.Publish(new DeviceConnectingEvent(_deviceName), ct);

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

                    _logger.LogInformation("{Device} 已连接: {Ip}:{Port}", _deviceName, _options.IpAddress, _options.Port);
                    _currentConnectionState = true;
                    await _mediator.Publish(new DeviceConnectedEvent(_deviceName, DateTime.Now), token);
                }
                else
                {
                    SafeCloseClient(newClient);
                    throw new Exception($"连接被拒绝或超时: {result.Err}");
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _currentConnectionState = false;
            _logger.LogError(ex, "{Device} 建立连接失败", _deviceName);
            await _mediator.Publish(new DeviceConnectionFailedEvent(_deviceName, ex.Message), ct);
            throw; // 向外抛出，让 Polly 重试机制生效
        }
    }

    public async Task DisconnectAsync()
    {
        StopWatchdog();
        _currentConnectionState = false;
        SafeCloseClient(_client);
        _logger.LogInformation("{Device} 已断开", _deviceName);
        await _mediator.Publish(new DeviceDisconnectedEvent(
            _deviceName,
            "主动断开",
            DateTime.Now
        ));
    }

    /// <summary>
    ///     核心：7x24小时全自动自愈看门狗（含监督者：看门狗循环异常退出会被自动重启）
    /// </summary>
    private void StartWatchdog()
    {
        if (_isWatchdogRunning) return;
        _isWatchdogRunning = true;
        _watchdogCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            var hbAddress = string.IsNullOrEmpty(_options.HeartbeatAddress) ? "M0" : _options.HeartbeatAddress;
            _logger.LogInformation("{Device} 看门狗已启动，心跳地址: {Address}", _deviceName, hbAddress);

            // 监督者循环：看门狗循环异常退出时延迟重启，仅取消时真正退出
            while (!_watchdogCts.IsCancellationRequested)
            {
                try
                {
                    await RunWatchdogLoopAsync(hbAddress, _watchdogCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，退出监督循环
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Device} 看门狗循环异常退出，{RestartDelaySec} 秒后由监督者重启", _deviceName, 5);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), _watchdogCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，退出监督循环
                        break;
                    }
                }
            }

            _logger.LogInformation("{Device} 看门狗已退出", _deviceName);
            _isWatchdogRunning = false;
        }, _watchdogCts.Token);
    }

    /// <summary>
    ///     看门狗扫描循环：断线重连 + 心跳检测（由监督者托管）
    /// </summary>
    private async Task RunWatchdogLoopAsync(string hbAddress, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                // 【状态一：已断线】-> 尝试重连
                if (!_currentConnectionState)
                {
                    _logger.LogDebug("{Device} 处于断开状态，尝试重连", _deviceName);
                    try
                    {
                        await ExecuteConnectInternalAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // 交由监督者处理取消
                    }
                    catch
                    {
                        // 连不上就静默等待下一次 Timer 触发
                        // 增加退避，避免网络不可达时频繁创建连接线程
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    }

                    continue;
                }

                // 【状态二：已连接】-> 执行心跳检测
                try
                {
                    var result = _client.ReadInt16(hbAddress);
                    if (!result.IsSucceed)
                    {
                        _logger.LogWarning("{Device} 心跳读取失败，判定为掉线，原因: {Reason}", _deviceName, result.Err);
                        _currentConnectionState = false;
                        await _mediator.Publish(new DeviceDisconnectedEvent(_deviceName, "心跳丢失/网络异常", DateTime.Now));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 重连替换客户端的瞬间可能读到已释放的旧实例：判定掉线即可，
                    // 重连分支会创建全新客户端，看门狗不再因此退出
                    _logger.LogWarning("{Device} 客户端已释放，判定为掉线并准备重连", _deviceName);
                    _currentConnectionState = false;
                    await _mediator.Publish(new DeviceDisconnectedEvent(_deviceName, "客户端已释放", DateTime.Now));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Device} 看门狗发生严重通信异常", _deviceName);
                    _currentConnectionState = false;
                    await _mediator.Publish(new DeviceDisconnectedEvent(_deviceName, "严重通信异常", DateTime.Now));
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 交由监督者处理取消
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Device} 看门狗循环体发生未预期异常，继续运行", _deviceName);
            }
        }
    }

    private void StopWatchdog()
    {
        if (!_isWatchdogRunning) return;

        try
        {
            _watchdogCts?.Cancel();
            // 给看门狗线程一点时间优雅退出
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Device} 停止看门狗时发生异常", _deviceName);
        }
        finally
        {
            _watchdogCts?.Dispose();
            _watchdogCts = null;
            _isWatchdogRunning = false;
        }
    }

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_currentConnectionState);
    }

    public async Task<T> ReadAsync<T>(string address, CancellationToken ct = default)
    {
        // 捕获当前客户端引用，避免重连替换实例后在同一次读写中混用不同客户端
        var client = _client;

        return await _pipeline.ExecuteAsync(async token =>
        {
            // 根据 T 的类型调用不同的 IoTClient 方法
            dynamic result;

            // 将同步调用包装为 Task
            await Task.Yield(); // 确保异步上下文

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
            else
                throw new NotSupportedException($"不支持的类型: {typeof(T).Name}");

            if (result.IsSucceed) return (T)result.Value;

            throw new Exception($"读取失败 [{address}]: {result.Err}");
        }, ct);
    }

    public async Task WriteAsync<T>(string address, T value, CancellationToken ct = default)
    {
        // 捕获当前客户端引用，避免重连替换实例后在同一次写入中混用不同客户端
        var client = _client;

        await _pipeline.ExecuteAsync(async token =>
        {
            dynamic result;
            await Task.Yield();

            if (value is bool b)
                result = client.Write(address, b);
            else if (value is short s)
                result = client.Write(address, s);
            else if (value is ushort us)
                result = client.Write(address, us);
            else if (value is int i)
                result = client.Write(address, i);
            else if (value is uint ui)
                result = client.Write(address, ui);
            else if (value is float f)
                result = _client.Write(address, f);
            else
                throw new NotSupportedException($"不支持的类型: {typeof(T).Name}");

            if (!result.IsSucceed) throw new Exception($"写入失败 [{address}]: {result.Err}");
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

    public async Task WriteBatchAsync(Dictionary<string, object> data, CancellationToken ct = default)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            foreach (var kvp in data) await WriteAsync(kvp.Key, kvp.Value, token);
        }, ct);
    }
}
