using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Ports;
using AP.Plugin.Scanner.Configuration;

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
        if (!_serialPort.IsOpen)
            try
            {
                _serialPort.Open();
                _logger.LogInformation("扫码枪已连接: {PortName}", _options.PortName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫码枪连接失败: {PortName}", _options.PortName);
                throw;
            }

        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
            _logger.LogInformation("扫码枪已断开");
        }

        return Task.CompletedTask;
    }

    private async void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            // 使用 ReadLine 读取直到换行符
            var rawData = _serialPort.ReadLine();

            // 清理空白字符
            var barcode = rawData.Trim();

            if (string.IsNullOrEmpty(barcode)) return;

            _logger.LogInformation("扫码成功 [{Device}]: {Barcode}", _options.PortName, barcode);

            await _mediator.Publish(new ScanCompletedEvent(
                _machineId,
                _options.PortName,
                barcode,
                DateTime.Now
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取扫码数据异常");
        }
    }

    public void Dispose()
    {
        _serialPort?.Dispose();
    }
}