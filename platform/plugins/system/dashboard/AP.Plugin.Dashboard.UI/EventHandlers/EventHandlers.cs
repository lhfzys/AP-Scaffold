using AP.Contracts.Hardware.Events;
using AP.Plugin.Dashboard.UI.Messaging;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using System.Windows.Media;

namespace AP.Plugin.Dashboard.UI.EventHandlers;

/// <summary>
/// 连接状态中转站：MediatR -> WeakReferenceMessenger
/// </summary>
public class DashboardConnectionHandler :
    INotificationHandler<DeviceConnectingEvent>,
    INotificationHandler<DeviceConnectedEvent>,
    INotificationHandler<DeviceConnectionFailedEvent>,
    INotificationHandler<DeviceDisconnectedEvent>
{
    // 1. 处理 "正在连接"
    public Task Handle(DeviceConnectingEvent notification, CancellationToken ct)
    {
        // 广播给所有 ViewModel
        WeakReferenceMessenger.Default.Send(new DeviceStatusMessage(
            false,
            "正在连接...",
            Brushes.Orange
        ));
        return Task.CompletedTask;
    }

    // 2. 处理 "连接成功"
    public Task Handle(DeviceConnectedEvent notification, CancellationToken ct)
    {
        WeakReferenceMessenger.Default.Send(new DeviceStatusMessage(
            true,
            $"已连接 ({notification.DeviceName})",
            Brushes.LimeGreen
        ));
        return Task.CompletedTask;
    }

    // 3. 处理 "连接失败"
    public Task Handle(DeviceConnectionFailedEvent notification, CancellationToken ct)
    {
        WeakReferenceMessenger.Default.Send(new DeviceStatusMessage(
            false,
            "连接失败 (点击重试)",
            Brushes.Red
        ));
        return Task.CompletedTask;
    }

    // 4. 处理 "断开连接"
    public Task Handle(DeviceDisconnectedEvent notification, CancellationToken ct)
    {
        WeakReferenceMessenger.Default.Send(new DeviceStatusMessage(
            false,
            "已断开",
            Brushes.Gray
        ));
        return Task.CompletedTask;
    }
}