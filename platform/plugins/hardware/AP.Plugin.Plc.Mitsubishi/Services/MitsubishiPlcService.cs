using AP.Contracts.Core.Models;
using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Commands;
using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.Services;
using AP.Plugin.Plc.Mitsubishi.Configuration;
using IoTClient.Clients.PLC;
using IoTClient.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace AP.Plugin.Plc.Mitsubishi.Services;

public class MitsubishiPlcService : IPlcService, IPlcBatchReadWrite
{
    private readonly MitsubishiClient _client;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger _logger;
    private readonly MitsubishiPlcOptions _options;
    private readonly IMediator _mediator;

    // --- 心跳控制状态 ---
    private bool _isHeartbeatRunning;
    private CancellationTokenSource? _heartbeatCts;
    private bool _currentConnectionState;

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

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _mediator.Publish(new DeviceConnectingEvent($"Mitsubishi-Q ({_options.IpAddress})"), ct);

        try
        {
            // 使用 Polly 策略保护连接过程
            await _pipeline.ExecuteAsync(async token =>
            {
                var result = await Task.Run(() => _client.Open(), token);

                if (result.IsSucceed)
                {
                    _logger.LogInformation("三菱PLC 已连接: {Ip}:{Port}", _options.IpAddress, _options.Port);
                    _currentConnectionState = true;
                    await _mediator.Publish(new DeviceConnectedEvent(
                        $"Mitsubishi-Q ({_options.IpAddress})",
                        DateTime.Now
                    ), token);
                    StartHeartbeat();
                }
                else
                {
                    throw new Exception($"连接超时/被拒绝: {result.Err}");
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC 连接最终失败");
            await _mediator.Publish(new DeviceConnectionFailedEvent(
                $"Mitsubishi-Q ({_options.IpAddress})",
                ex.Message
            ), ct);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        StopHeartbeat();
        _currentConnectionState = false;
        _client.Close();
        _logger.LogInformation("PLC 已断开");
        await _mediator.Publish(new DeviceDisconnectedEvent(
            $"Mitsubishi-Q ({_options.IpAddress})",
            "主动断开",
            DateTime.Now
        ));
    }

    private void StartHeartbeat()
    {
        if (_isHeartbeatRunning) return;

        var hbAddress = string.IsNullOrEmpty(_options.HeartbeatAddress) ? "M0" : _options.HeartbeatAddress;

        _isHeartbeatRunning = true;
        _heartbeatCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("PLC 心跳监测已启动，监听地址: {Address}", hbAddress);

                while (!_heartbeatCts.Token.IsCancellationRequested)
                {
                    // 每 2 秒检查一次
                    await Task.Delay(2000, _heartbeatCts.Token);

                    if (!_currentConnectionState) break;

                    try
                    {
                        // 尝试读取一个 bool 值作为心跳验证
                        var result = _client.ReadInt16(hbAddress);
                        _logger.LogInformation("PLC 心跳监测: {Address}", result);
                        if (!result.IsSucceed)
                        {
                            // 读取失败，说明掉线了！
                            _logger.LogWarning("❌ 心跳检测失败，PLC连接已丢失！原因: {Err}", result.Err);

                            _currentConnectionState = false;
                            _isHeartbeatRunning = false;

                            // 广播断开事件，界面将瞬间变红，手动重连按钮亮起！
                            await _mediator.Publish(new DeviceDisconnectedEvent(
                                $"Mitsubishi-Q ({_options.IpAddress})",
                                "心跳丢失/网络异常",
                                DateTime.Now
                            ));
                            break;
                        }
                    }
                    catch
                    {
                        // 捕获严重异常(断网/电源切断)
                        _currentConnectionState = false;
                        _isHeartbeatRunning = false;
                        await _mediator.Publish(new DeviceDisconnectedEvent(
                            $"Mitsubishi-Q ({_options.IpAddress})",
                            "网络通信异常退出",
                            DateTime.Now
                        ));
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 忽略这个异常，不做处理
            }
            catch (Exception ex)
            {
                _logger.LogWarning("心跳线程异常退出: {Message}", ex.Message);
            }
            finally
            {
                _isHeartbeatRunning = false;
            }
        }, _heartbeatCts.Token);
    }

    private void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
        _isHeartbeatRunning = false;
    }

    public async Task<bool> IsConnectedAsync()
    {
        return _currentConnectionState;
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