#region

using AP.Contracts.Hardware.Events;
using AP.Contracts.Hardware.PrismEvents;
using MediatR;

#endregion

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
///     全局硬件事件桥接器 (在 Host 层将 MediatR 事件转发到 UI 线程)
/// </summary>
public class MediatRToPrismBridge :
    INotificationHandler<PlcDataChangedEvent>,
    INotificationHandler<ScanCompletedEvent>,
    INotificationHandler<DeviceDisconnectedEvent>
{
    private readonly IEventAggregator _eventAggregator;

    public MediatRToPrismBridge(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    public Task Handle(PlcDataChangedEvent notification, CancellationToken cancellationToken)
    {
        _eventAggregator.GetEvent<PrismPlcDataChangedEvent>().Publish(notification);
        return Task.CompletedTask;
    }

    public Task Handle(ScanCompletedEvent notification, CancellationToken cancellationToken)
    {
        _eventAggregator.GetEvent<PrismScanCompletedEvent>().Publish(notification);
        return Task.CompletedTask;
    }

    public Task Handle(DeviceDisconnectedEvent notification, CancellationToken cancellationToken)
    {
        _eventAggregator.GetEvent<PrismDeviceDisconnectedEvent>().Publish(notification);
        return Task.CompletedTask;
    }
}