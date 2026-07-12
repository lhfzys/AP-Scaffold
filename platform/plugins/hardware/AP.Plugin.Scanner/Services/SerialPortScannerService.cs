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
    }

    public bool IsConnected => _serialPort.IsOpen;

    public Task OpenAsync()
    {
        if (_serialPort.IsOpen)
            return Task.CompletedTask;

        try
        {
            // 每次打开都创建新的数据通道，避免复用已完成的通道
            _barcodeChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            _serialPort.Open();

            // 启动后台消费者，异步处理扫码事件
            _processCts = new CancellationTokenSource();
            _processTask = Task.Run(() => ProcessBarcodesAsync(_processCts.Token), _processCts.Token);

            _logger.LogInformation("扫码枪已连接: {PortName}", _options.PortName);
        }
        catch (Exception ex)
        {
            _barcodeChannel = null;
            _logger.LogError(ex, "扫码枪连接失败: {PortName}", _options.PortName);
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task CloseAsync()
    {
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
            _serialPort.Close();
            _logger.LogInformation("扫码枪已断开");
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