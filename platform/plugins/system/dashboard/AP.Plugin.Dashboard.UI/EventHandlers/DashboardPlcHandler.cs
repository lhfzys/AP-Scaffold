using AP.Contracts.Hardware.Events;
using AP.Plugin.Dashboard.UI.Messaging;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;

namespace AP.Plugin.Dashboard.UI.EventHandlers;

/// <summary>
/// 桥接器：接收 MediatR 的 PLC 事件 -> 转发给 MVVM Messenger
/// </summary>
public class DashboardPlcHandler : INotificationHandler<PlcDataChangedEvent>
{
    public Task Handle(PlcDataChangedEvent notification, CancellationToken cancellationToken)
    {
        // 使用弱引用消息发送，不用担心内存泄漏
        WeakReferenceMessenger.Default.Send(new PlcDataMessage(notification));
        return Task.CompletedTask;
    }
}