using AP.Contracts.Hardware.DeviceRuntime;
using MediatR;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 统一设备状态事件发布器：订阅指定设备的 Transitioned，
/// 把迁移转换为统一的 <see cref="DeviceStateChangedEvent"/> 经 MediatR 发布。
/// 由宿主在设备注册时对每个设备 Attach（一处接线，新设备自动覆盖）。
/// </summary>
public sealed class DeviceStateEventPublisher
{
    private readonly IMediator _mediator;

    public DeviceStateEventPublisher(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// 附加到设备：每次状态迁移发布统一事件；返回的 <see cref="IDisposable"/> 用于退订。
    /// 发布为火忘（失败被观察、不影响设备与其他订阅者）。
    /// </summary>
    public IDisposable Attach(IDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        EventHandler<DeviceConnectionTransition> handler = (_, transition) =>
        {
            _ = _mediator.Publish(new DeviceStateChangedEvent(device.Info, transition))
                .ContinueWith(
                    t => { _ = t.Exception; }, // 观察异常，避免 UnobservedTaskException
                    TaskContinuationOptions.OnlyOnFaulted);
        };

        device.Transitioned += handler;
        return new Subscription(() => device.Transitioned -= handler);
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
