#region

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
    private readonly MitsubishiClient _client;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger _logger;
    private readonly MitsubishiPlcOptions _options;
    private readonly IMediator _mediator;

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
        if (!Enum.TryParse(_options.Version, true, out MitsubishiVersion version))
            version = MitsubishiVersion.Qna_3E;

        // 初始化 IoTClient
        _client = new MitsubishiClient(version, _options.IpAddress, _options.Port, _options.Timeout);
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
            _logger.LogWarning("首次连接 PLC 失败，看门狗将在后台接管并持续尝试重连。原因: {Msg}", ex.Message);
        }
        finally
        {
            // 2. 无论首次连接成功还是失败，都必须把看门狗跑起来！
            StartWatchdog();
        }
    }

    /// <summary>
    ///     连接与握手逻辑
    /// </summary>
    private async Task ExecuteConnectInternalAsync(CancellationToken ct)
    {
        await _mediator.Publish(new DeviceConnectingEvent(_deviceName), ct);

        try
        {
            await _pipeline.ExecuteAsync(async token =>
            {
                var result = await Task.Run(() => _client.Open(), token);
                if (result.IsSucceed)
                {
                    _logger.LogInformation("✅ 三菱PLC 已连接: {Ip}:{Port}", _options.IpAddress, _options.Port);
                    _currentConnectionState = true;
                    await _mediator.Publish(new DeviceConnectedEvent(_deviceName, DateTime.Now), token);
                }
                else
                {
                    throw new Exception($"连接被拒绝或超时: {result.Err}");
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _currentConnectionState = false;
            _logger.LogError("❌ PLC 建立连接失败: {Msg}", ex.Message);
            await _mediator.Publish(new DeviceConnectionFailedEvent(_deviceName, ex.Message), ct);
            throw; // 向外抛出，让 Polly 重试机制生效
        }
    }

    public async Task DisconnectAsync()
    {
        StopWatchdog();
        _currentConnectionState = false;
        _client.Close();
        _logger.LogInformation("PLC 已断开");
        await _mediator.Publish(new DeviceDisconnectedEvent(
            $"Mitsubishi-Q ({_options.IpAddress})",
            "主动断开",
            DateTime.Now
        ));
    }

    /// <summary>
    ///     核心：7x24小时全自动自愈看门狗
    /// </summary>
    private void StartWatchdog()
    {
        if (_isWatchdogRunning) return;
        _isWatchdogRunning = true;
        _watchdogCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            var hbAddress = string.IsNullOrEmpty(_options.HeartbeatAddress) ? "M0" : _options.HeartbeatAddress;
            _logger.LogInformation("🛡️ PLC 自动自愈看门狗已启动，心跳地址: {Address}", hbAddress);

            // 完美替代 Task.Delay，无时钟漂移，内存更优
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

            while (await timer.WaitForNextTickAsync(_watchdogCts.Token))
            {
                // 【状态一：已断线】-> 尝试重连
                if (!_currentConnectionState)
                {
                    _logger.LogInformation("🔄 [看门狗] PLC处于断开状态，尝试自动重连...");
                    try
                    {
                        // 这里调用连接逻辑，借助 Polly 实现局部重试
                        await ExecuteConnectInternalAsync(_watchdogCts.Token);
                    }
                    catch
                    {
                        // 连不上就静默等待下一次 Timer 触发 (避免刷爆日志和 CPU)
                    }

                    continue;
                }

                // 【状态二：已连接】-> 执行心跳检测
                try
                {
                    var result = _client.ReadInt16(hbAddress);
                    if (!result.IsSucceed)
                    {
                        _logger.LogWarning("⚠️ [看门狗] 心跳读取失败，判定为掉线！原因: {Err}", result.Err);
                        _currentConnectionState = false; // 下一次循环将自动进入重连逻辑
                        await _mediator.Publish(new DeviceDisconnectedEvent(_deviceName, "心跳丢失/网络异常", DateTime.Now));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "⚠️ [看门狗] 发生严重通信异常！");
                    _currentConnectionState = false;
                    await _mediator.Publish(new DeviceDisconnectedEvent(_deviceName, "严重通信异常", DateTime.Now));
                }
            }
        }, _watchdogCts.Token);
    }

    private void StopWatchdog()
    {
        _watchdogCts?.Cancel();
        _watchdogCts?.Dispose();
        _isWatchdogRunning = false;
    }

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_currentConnectionState);
    }

    public async Task<T> ReadAsync<T>(string address, CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            // 根据 T 的类型调用不同的 IoTClient 方法
            dynamic result;

            // 将同步调用包装为 Task
            await Task.Yield(); // 确保异步上下文

            if (typeof(T) == typeof(bool))
                result = _client.ReadBoolean(address);
            else if (typeof(T) == typeof(short))
                result = _client.ReadInt16(address);
            else if (typeof(T) == typeof(ushort))
                result = _client.ReadUInt16(address);
            else if (typeof(T) == typeof(int))
                result = _client.ReadInt32(address);
            else if (typeof(T) == typeof(uint))
                result = _client.ReadUInt32(address);
            else if (typeof(T) == typeof(float))
                result = _client.ReadFloat(address);
            else
                throw new NotSupportedException($"不支持的类型: {typeof(T).Name}");

            if (result.IsSucceed) return (T)result.Value;

            throw new Exception($"读取失败 [{address}]: {result.Err}");
        }, ct);
    }

    public async Task WriteAsync<T>(string address, T value, CancellationToken ct = default)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            dynamic result;
            await Task.Yield();

            if (value is bool b)
                result = _client.Write(address, b);
            else if (value is short s)
                result = _client.Write(address, s);
            else if (value is ushort us)
                result = _client.Write(address, us);
            else if (value is int i)
                result = _client.Write(address, i);
            else if (value is uint ui)
                result = _client.Write(address, ui);
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