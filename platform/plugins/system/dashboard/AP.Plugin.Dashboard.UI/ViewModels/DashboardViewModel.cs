using AP.Contracts.Hardware.Commands;
using AP.Contracts.Hardware.Events;
using AP.Plugin.Dashboard.UI.Messaging;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Media;

namespace AP.Plugin.Dashboard.UI.ViewModels;

public partial class DashboardViewModel : ViewModelBase,
    IRecipient<PlcDataMessage>,
    IRecipient<DeviceStatusMessage>
{
    private readonly ICustomDialogService _dialogService;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly IMediator _mediator;

    [ObservableProperty] private bool _isPlcConnected;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string _connectionStatusText = "等待连接...";
    [ObservableProperty] private Brush _statusColor = Brushes.Gray;

    // --- PLC 实时数据 ---
    [ObservableProperty] private string _lastPlcDevice = "-";
    [ObservableProperty] private string _lastPlcAddress = "-";
    [ObservableProperty] private string _lastPlcValue = "-";
    [ObservableProperty] private DateTime _lastPlcTime;

    public DashboardViewModel(ICustomDialogService dialogService,
        ILogger<DashboardViewModel> logger, IMediator mediator)
    {
        Title = "生产线实时监控";
        _dialogService = dialogService;
        _logger = logger;
        _mediator = mediator;

        WeakReferenceMessenger.Default.RegisterAll(this);
        _logger.LogInformation("Dashboard 初始化完成");
    }


    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ManualConnect()
    {
        _logger.LogInformation("用户触发手动连接");
        if (IsConnecting) return;
        _logger.LogInformation("发送连接命令到 MediatR");
        await _mediator.Send(new ConnectDeviceCommand("Mitsubishi-Q"));
    }

    private bool CanConnect()
    {
        return !IsConnecting && !IsPlcConnected;
    }

    partial void OnIsConnectingChanged(bool value)
    {
        ManualConnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPlcConnectedChanged(bool value)
    {
        ManualConnectCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task TestDialog()
    {
        var result = await _dialogService.ShowConfirmAsync("测试弹窗功能正常吗？");
        if (result) await _dialogService.ShowAlertAsync("功能正常！");
    }

    public void Receive(DeviceStatusMessage message)
    {
        // 因为 Messenger 可能从后台线程发来，保险起见切回 UI 线程
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsPlcConnected = message.Value; // Value 是 bool 连接状态
            ConnectionStatusText = message.StatusText;
            StatusColor = message.StatusColor;

            // 如果正在连接，禁用按钮逻辑等...
            IsConnecting = message.StatusColor == Brushes.Orange;
        });
    }

    // 处理接收到的 PLC 数据
    public void Receive(PlcDataMessage message)
    {
        var data = message.Value;
        if (!IsPlcConnected)
        {
            IsPlcConnected = true;
            StatusColor = Brushes.LimeGreen;
            ConnectionStatusText = "数据接收中...";
        }

        LastPlcDevice = data.DeviceName;
        LastPlcAddress = data.Address;
        LastPlcValue = data.Value?.ToString() ?? "null";
        LastPlcTime = data.Timestamp;
    }

    public override void Destroy()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.Destroy();
    }
}