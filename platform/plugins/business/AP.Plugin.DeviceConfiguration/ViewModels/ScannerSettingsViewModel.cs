#region

using System.IO.Ports;
using System.Windows;
using AP.Plugin.DeviceConfiguration.Models;
using AP.Shared.UI.Base;
using AP.Shared.Utilities.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using Microsoft.Extensions.Options;

#endregion

namespace AP.Plugin.DeviceConfiguration.ViewModels;

public partial class ScannerSettingsViewModel : ViewModelBase
{
    private readonly IMediator _mediator;
    [ObservableProperty] private string _portName;
    [ObservableProperty] private int _baudRate;

    public ScannerSettingsViewModel(IOptions<ScannerConfigModel> options, IMediator mediator)
    {
        _mediator = mediator;

        // 1. 初始化时，从配置文件中读取当前值
        var currentOptions = options.Value;
        _portName = currentOptions.PortName ?? "COM1";
        _baudRate = currentOptions.BaudRate > 0 ? currentOptions.BaudRate : 9600;
    }

    [RelayCommand]
    private async Task ApplySettingsAsync()
    {
        // 2. 将用户在 UI 上修改的值封装为 Options 对象
        var newOptions = new ScannerConfigModel
        {
            PortName = this.PortName,
            BaudRate = this.BaudRate,
            DataBits = 8,
            StopBits = StopBits.One,
            Parity = Parity.None
        };

        // 3. 核心：持久化到 appsettings.json
        // 注意：这里的 SectionName 必须和 appsettings.json 中的节点层级完全一致！
        ConfigurationHelper.UpdateAppSetting("Plugins:Scanner:SerialPort", newOptions);

        // 4. 发送通知或指令给底层服务，让其重启或重连
        // (视您的底层实现而定，最简单的做法是提示用户重启软件，或者抛出一个重连 Command)
        // await _mediator.Send(new ReconnectScannerCommand(newOptions));

        MessageBox.Show("扫码枪配置已保存！部分配置可能需要重启软件或重新连接设备后生效。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}