using System.IO.Ports;
using AP.Plugin.DeviceConfiguration.Models;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AP.Plugin.DeviceConfiguration.ViewModels;

/// <summary>
/// 扫码枪配置编辑器 ViewModel
/// </summary>
public partial class ScannerSettingsViewModel : ViewModelBase, ISettingsEditorViewModel
{
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private string _portName = "COM1";
    [ObservableProperty] private int _baudRate = 9600;
    [ObservableProperty] private int _dataBits = 8;
    [ObservableProperty] private string _parity = "None";
    [ObservableProperty] private string _stopBits = "One";
    [ObservableProperty] private string _newLine = "\r";

    public bool RequiresRestart => true;

    public ScannerSettingsViewModel(IOptions<ScannerConfigModel> options)
    {
        LoadFromOptions(options.Value);
    }

    public void LoadFromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(ScannerConfigModel.SectionName);
        var options = section.Get<ScannerConfigModel>() ?? new ScannerConfigModel();
        LoadFromOptions(options);
    }

    private void LoadFromOptions(ScannerConfigModel options)
    {
        Enabled = options.Enabled;
        PortName = options.PortName ?? "COM1";
        BaudRate = options.BaudRate > 0 ? options.BaudRate : 9600;
        DataBits = options.DataBits > 0 ? options.DataBits : 8;
        Parity = options.Parity.ToString();
        StopBits = options.StopBits.ToString();
        NewLine = string.IsNullOrWhiteSpace(options.NewLine) ? "\r" : options.NewLine;
    }

    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(PortName))
            errors.Add("通信串口不能为空");

        if (BaudRate <= 0)
            errors.Add("波特率必须大于 0");

        var validBaudRates = new[] { 9600, 19200, 38400, 57600, 115200 };
        if (!validBaudRates.Contains(BaudRate))
            errors.Add($"波特率 {BaudRate} 不是常用值，请确认");

        if (DataBits is < 5 or > 8)
            errors.Add("数据位必须在 5-8 之间");

        if (!Enum.TryParse<Parity>(Parity, true, out _))
            errors.Add($"校验位 {Parity} 无效");

        if (!Enum.TryParse<StopBits>(StopBits, true, out _))
            errors.Add($"停止位 {StopBits} 无效");

        return errors;
    }

    public object GetConfigurationValue()
    {
        var parity = Enum.TryParse<System.IO.Ports.Parity>(Parity, true, out var p) ? p : System.IO.Ports.Parity.None;
        var stopBits = Enum.TryParse<System.IO.Ports.StopBits>(StopBits, true, out var s) ? s : System.IO.Ports.StopBits.One;

        return new ScannerConfigModel
        {
            Enabled = Enabled,
            PortName = PortName,
            BaudRate = BaudRate,
            DataBits = DataBits,
            Parity = parity,
            StopBits = stopBits,
            NewLine = NewLine
        };
    }
}
