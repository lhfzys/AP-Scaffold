using System.IO.Ports;
using System.Threading.Channels;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.DeviceRuntime;
using AP.Plugin.Scanner.Configuration;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Plugin.Scanner.Services;

/// <summary>
/// 扫码枪扫码服务
/// </summary>
public class SerialPortScannerService : IScannerService, IDevice, IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly IMediator _mediator;
    private readonly ILogger<SerialPortScannerService> _logger;
    private readonly SerialPortOptions _options;
    private readonly string _deviceName;

    // 扫码数据缓冲通道：串口事件（同步）将数据写入通道，后台任务（异步）消费并发布事件
    private Channel<string>? _barcodeChannel;
    private CancellationTokenSource? _processCts;
    private Task? _processTask;

    // --- 连接运行时（Device Runtime Model：状态机 + 监督器，单一事实来源） ---
    private readonly DeviceConnectionStateMachine _stateMachine;
    private readonly ConnectionSupervisor _supervisor;
    private readonly IDisposable _loggerSubscription;

    private readonly object _portLock = new();
    private bool _started;

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
        _deviceName = $"Scanner ({_options.PortName})";

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

        // 连接运行时：参数对齐原重连监控（5 秒周期、每周期一尝试）
        _stateMachine = new DeviceConnectionStateMachine();
        _supervisor = new ConnectionSupervisor(_stateMachine, ConnectPortAsync, ProbePortAsync, new ConnectionSupervisorOptions
        {
            HeartbeatInterval = TimeSpan.FromSeconds(5),
            ReconnectBackoff = TimeSpan.Zero,
        });
        _loggerSubscription = ConnectionSupervisorLogger.Attach(_supervisor, _stateMachine, logger, _deviceName);

        // IDevice 视图初始化：状态机事件转换为契约层 record 转发
        Info = new DeviceInfo($"scanner.{_options.PortName}", "扫码枪", DeviceType.Scanner, "Serial");
        _stateMachine.Transitioned += (_, args) =>
            Transitioned?.Invoke(this, new DeviceConnectionTransition(args.From, args.To, args.Reason, args.Timestamp));
    }

    // --- IDevice 视图（连接状态以状态机为唯一事实来源） ---

    /// <inheritdoc />
    public DeviceInfo Info { get; }

    /// <inheritdoc />
    public DeviceConnectionState State => _stateMachine.CurrentState;

    /// <inheritdoc />
    public event EventHandler<DeviceConnectionTransition>? Transitioned;

    // IScannerService.IsConnected 与 IDevice.State 同源：状态机为唯一事实来源
    public bool IsConnected => _stateMachine.CurrentState == DeviceConnectionState.Connected;

    /// <summary>
    /// 打开扫码枪（A 方案：首开由驱动驱动状态——有文档说明的例外，失败如实抛出由调用方记录；
    /// 首开成功后连接监督器接管心跳/重连）。
    /// </summary>
    public Task OpenAsync()
    {
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

            _started = true;
        }

        _stateMachine.TryTransition(DeviceConnectionState.Connecting, "开始连接");
        try
        {
            TryOpenPort();
        }
        catch
        {
            _stateMachine.TryTransition(DeviceConnectionState.Disconnected, "打开失败");
            throw;
        }

        _stateMachine.TryTransition(DeviceConnectionState.Connected, "打开成功");
        _supervisor.Start();

        return Task.CompletedTask;
    }

    /// <summary>IDevice.ConnectAsync 与 IScannerService.OpenAsync 同语义。</summary>
    Task IDevice.ConnectAsync(CancellationToken ct) => OpenAsync();

    /// <summary>IDevice.DisconnectAsync 与 IScannerService.CloseAsync 同语义。</summary>
    Task IDevice.DisconnectAsync() => CloseAsync();

    /// <summary>
    /// 连接动作（供监督器调用）：先关闭残留句柄再重开
    /// </summary>
    private Task<ConnectionAttemptResult> ConnectPortAsync(CancellationToken ct)
    {
        try
        {
            SafeClosePort(); // 出错重连/拔出后重插：先关闭残留状态
            TryOpenPort();
            return Task.FromResult(ConnectionAttemptResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ConnectionAttemptResult.Fail(ex.Message, ex));
        }
    }

    /// <summary>
    /// 心跳探测动作（供监督器调用）：端口存在 + 句柄打开
    /// </summary>
    private Task<ConnectionAttemptResult> ProbePortAsync(CancellationToken ct)
    {
        // USB 拔出后串口名会从系统中消失（IsOpen 可能仍为 true）
        var portExists = SerialPort.GetPortNames()
            .Any(p => string.Equals(p, _options.PortName, StringComparison.OrdinalIgnoreCase));

        if (!portExists)
            return Task.FromResult(ConnectionAttemptResult.Fail("串口已消失（设备拔出？）"));
        if (!_serialPort.IsOpen)
            return Task.FromResult(ConnectionAttemptResult.Fail("串口句柄已关闭"));
        return Task.FromResult(ConnectionAttemptResult.Ok());
    }

    /// <summary>
    /// 在锁内尝试打开串口（首开与监督器重连共用；失败如实抛出，由调用方处理）
    /// </summary>
    private void TryOpenPort()
    {
        lock (_portLock)
        {
            if (_serialPort.IsOpen) return;
            _serialPort.Open();
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
                _logger.LogWarning(ex, "{Device} 关闭串口时发生异常（设备可能已拔出）", _deviceName);
            }
        }
    }

    /// <summary>
    /// 串口错误事件（USB 拔插、帧错误等）：设备特有信号，由驱动驱动状态迁移，监督器接管重连
    /// </summary>
    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        _stateMachine.TryTransition(
            DeviceConnectionState.Reconnecting,
            $"串口错误: {e.EventType}");
    }

    public async Task CloseAsync()
    {
        _supervisor.Stop();
        _stateMachine.TryTransition(DeviceConnectionState.Disconnected, "主动断开");

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
                    _logger.LogWarning("{Device} 后台处理任务未能在 5 秒内退出，强制取消", _deviceName);
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
            _logger.LogWarning(ex, "{Device} 停止后台任务时发生异常", _deviceName);
        }
        finally
        {
            _processCts?.Dispose();
            _processCts = null;
            _processTask = null;
            _barcodeChannel = null;
        }

        SafeClosePort();
        _started = false;
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
            _logger.LogError(ex, "{Device} 读取扫码数据异常", _deviceName);
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
                    _logger.LogInformation("{Device} 扫码成功: {Barcode}", _deviceName, barcode);

                    await _mediator.Publish(new ScanCompletedEvent(
                        _machineId,
                        _options.PortName,
                        barcode,
                        DateTime.Now
                    ), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Device} 处理扫码数据异常", _deviceName);
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
            _logger.LogError(ex, "{Device} 后台处理任务异常退出", _deviceName);
        }
    }

    public void Dispose()
    {
        try
        {
            _supervisor.Stop();
            _loggerSubscription.Dispose();
            _processCts?.Cancel();
            _barcodeChannel?.Writer.TryComplete();

            if (_serialPort.IsOpen)
                _serialPort.Close();

            _serialPort.Dispose();
            _processCts?.Dispose();
        }
        catch
        {
            // 销毁阶段忽略异常
        }
    }
}
