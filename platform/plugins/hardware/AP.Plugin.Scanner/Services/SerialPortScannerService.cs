using System.IO.Ports;
using System.Threading.Channels;
using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.Services;
using AP.Plugin.Scanner.Configuration;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Plugin.Scanner.Services;

/// <summary>
/// 扫码枪扫码服务
/// </summary>
public class SerialPortScannerService : IScannerService, IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly IMediator _mediator;
    private readonly ILogger<SerialPortScannerService> _logger;
    private readonly SerialPortOptions _options;

    // 扫码数据缓冲通道：串口事件（同步）将数据写入通道，后台任务（异步）消费并发布事件
    private Channel<string>? _barcodeChannel;
    private CancellationTokenSource? _processCts;
    private Task? _processTask;

    // --- 断线重连状态 ---
    private readonly object _portLock = new();
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private volatile bool _needsReconnect;
    private volatile bool _stopping;
    private bool _started;

    // 重连监控周期（检测到串口错误或设备消失后，按此节奏尝试重开）
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(5);

    // 缓存 MachineId
    private readonly string _machineId;
    private const string MachineIdConfigKey = "AppConfiguration:MachineId";

    public SerialPortScannerService(
        IOptions<SerialPortOptions> options,
        IConfiguration configuration,
        IMediator mediator,
        ILogger<SerialPortScannerService> logger)
    {
        _options = options.Value;
        _mediator = mediator;
        _logger = logger;

        _machineId = configuration[MachineIdConfigKey] ?? "Unknown-Machine";

        _serialPort = new SerialPort
        {
            PortName = _options.PortName,
            BaudRate = _options.BaudRate,
            DataBits = _options.DataBits,
            Parity = Enum.Parse<Parity>(_options.Parity),
            StopBits = Enum.Parse<StopBits>(_options.StopBits),
            NewLine = _options.NewLine
        };

        _serialPort.DataReceived += OnDataReceived;
        _serialPort.ErrorReceived += OnErrorReceived;
    }

    public bool IsConnected => _serialPort.IsOpen;

    public Task OpenAsync()
    {
        _stopping = false;

        // 数据通道与后台消费者只启动一次（重连不重建，避免丢数据与重复消费）
        if (!_started)
        {
            _barcodeChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            _processCts = new CancellationTokenSource();
            _processTask = Task.Run(() => ProcessBarcodesAsync(_processCts.Token), _processCts.Token);

            StartReconnectMonitor();
            _started = true;
        }

        // 打开失败如实抛出（由调用方记录）；重连监控会在后台持续重试
        TryOpenPort();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 在锁内尝试打开串口（重连监控与外部调用共用）
    /// </summary>
    private void TryOpenPort()
    {
        lock (_portLock)
        {
            if (_serialPort.IsOpen) return;

            try
            {
                _serialPort.Open();
                _needsReconnect = false;
                _logger.LogInformation("扫码枪已连接: {PortName}", _options.PortName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫码枪连接失败: {PortName}（重连监控将持续重试）", _options.PortName);
                throw;
            }
        }
    }

    /// <summary>
    /// 在锁内安全关闭串口（忽略异常）
    /// </summary>
    private void SafeClosePort()
    {
        lock (_portLock)
        {
            try
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "关闭扫码枪串口时发生异常（设备可能已拔出）");
            }
        }
    }

    /// <summary>
    /// 串口错误事件（USB 拔插、帧错误等）：标记需要重连，由监控循环处理
    /// </summary>
    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        _logger.LogWarning("扫码枪串口错误 [{PortName}]: {EventType}，将自动重连", _options.PortName, e.EventType);
        _needsReconnect = true;
    }

    /// <summary>
    /// 断线重连监控：周期检查串口健康，设备消失时关闭残留句柄，设备恢复/出错后自动重开
    /// </summary>
    private void StartReconnectMonitor()
    {
        _monitorCts = new CancellationTokenSource();
        _monitorTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(ReconnectInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(_monitorCts.Token))
                {
                    try
                    {
                        EnsurePortConnected();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "扫码枪重连监控异常");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }, _monitorCts.Token);
    }

    private void EnsurePortConnected()
    {
        if (_stopping) return;

        // USB 拔出后串口名会从系统中消失（IsOpen 可能仍为 true）
        var portExists = SerialPort.GetPortNames()
            .Any(p => string.Equals(p, _options.PortName, StringComparison.OrdinalIgnoreCase));

        if (!portExists)
        {
            if (_serialPort.IsOpen)
            {
                _logger.LogWarning("扫码枪串口 {PortName} 已消失（设备拔出？），关闭并等待重新插入", _options.PortName);
                SafeClosePort();
            }
            return; // 等待设备重新插入
        }

        if (_serialPort.IsOpen && !_needsReconnect) return;

        // 出错重连：先关闭残留状态再重开
        if (_serialPort.IsOpen)
        {
            _logger.LogWarning("扫码枪串口 {PortName} 发生错误，执行重连", _options.PortName);
            SafeClosePort();
        }

        try
        {
            TryOpenPort();
            _logger.LogInformation("✅ 扫码枪已自动重连: {PortName}", _options.PortName);
        }
        catch
        {
            // 重开失败，等待下一周期重试（TryOpenPort 内已记录原因）
        }
    }

    public async Task CloseAsync()
    {
        _stopping = true;
        StopReconnectMonitor();

        try
        {
            // 标记通道完成，让后台任务处理完剩余数据后自然退出
            _barcodeChannel?.Writer.TryComplete();

            // 等待后台任务优雅退出
            if (_processTask != null)
            {
                try
                {
                    await _processTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("扫码枪后台处理任务未能在5秒内退出，强制取消");
                    _processCts?.Cancel();
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "停止扫码枪后台任务时发生异常");
        }
        finally
        {
            _processCts?.Dispose();
            _processCts = null;
            _processTask = null;
            _barcodeChannel = null;
        }

        if (_serialPort.IsOpen)
        {
            SafeClosePort();
            _logger.LogInformation("扫码枪已断开");
        }

        _started = false;
    }

    /// <summary>
    /// 停止断线重连监控（等待监控任务退出）
    /// </summary>
    private void StopReconnectMonitor()
    {
        try
        {
            _monitorCts?.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "停止扫码枪重连监控时发生异常");
        }
        finally
        {
            _monitorCts?.Dispose();
            _monitorCts = null;
            _monitorTask = null;
        }
    }

    /// <summary>
    /// 串口数据接收事件处理器（同步）
    /// 只做一件事：读取条码并写入通道，避免在事件线程中执行异步操作
    /// </summary>
    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            // 使用 ReadLine 读取直到换行符
            var rawData = _serialPort.ReadLine();

            // 清理空白字符
            var barcode = rawData.Trim();

            if (string.IsNullOrEmpty(barcode)) return;

            // 将条码写入通道，不阻塞串口事件线程
            _barcodeChannel?.Writer.TryWrite(barcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取扫码数据异常");
        }
    }

    /// <summary>
    /// 后台消费者：从通道读取条码并发布 MediatR 事件
    /// </summary>
    private async Task ProcessBarcodesAsync(CancellationToken ct)
    {
        if (_barcodeChannel == null) return;

        try
        {
            await foreach (var barcode in _barcodeChannel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    _logger.LogInformation("扫码成功 [{Device}]: {Barcode}", _options.PortName, barcode);

                    await _mediator.Publish(new ScanCompletedEvent(
                        _machineId,
                        _options.PortName,
                        barcode,
                        DateTime.Now
                    ), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理扫码数据异常");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (ChannelClosedException)
        {
            // 通道已关闭，正常退出
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫码枪后台处理任务异常退出");
        }
    }

    public void Dispose()
    {
        try
        {
            _stopping = true;
            _monitorCts?.Cancel();
            _processCts?.Cancel();
            _barcodeChannel?.Writer.TryComplete();

            if (_serialPort.IsOpen)
                _serialPort.Close();

            _serialPort.Dispose();
            _processCts?.Dispose();
            _monitorCts?.Dispose();
        }
        catch
        {
            // 销毁阶段忽略异常
        }
    }
}