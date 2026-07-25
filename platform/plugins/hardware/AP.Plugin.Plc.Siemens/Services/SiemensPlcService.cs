using System.Threading;
using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
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
    private readonly IMediator _mediator;
    private readonly SiemensVersion _version;

    private bool _isWatchdogRunning;
    private CancellationTokenSource? _watchdogCts;
    private bool _currentConnectionState;
    private readonly string _deviceName;

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
        _mediator = mediator;

        if (!Enum.TryParse(_options.Model, true, out _version))
            _version = SiemensVersion.S7_1200;

        _deviceName = $"Siemens-S7 ({_options.IpAddress}:{_options.Port})";
        _client = CreateClient();
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

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            await ExecuteConnectInternalAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Device} 首次连接失败，看门狗将在后台接管并持续尝试重连", _deviceName);
        }
        finally
        {
            StartWatchdog();
        }
    }

    private async Task ExecuteConnectInternalAsync(CancellationToken ct)
    {
        await _mediator.Publish(new DeviceConnectingEvent(_deviceName), ct);

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
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        StopWatchdog();
        _currentConnectionState = false;
        SafeCloseClient(_client);
        _logger.LogInformation("{Device} 已断开", _deviceName);
        await _mediator.Publish(new DeviceDisconnectedEvent(_deviceName, "主动断开", DateTime.Now));
    }

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_currentConnectionState);
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
            var hbAddress = string.IsNullOrEmpty(_options.HeartbeatAddress) ? "DB1.0.0" : _options.HeartbeatAddress;
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
                    var result = _client.ReadBoolean(hbAddress);
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
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "{Device} 停止看门狗时发生异常", _deviceName); }
        finally
        {
            _watchdogCts?.Dispose();
            _watchdogCts = null;
            _isWatchdogRunning = false;
        }
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
