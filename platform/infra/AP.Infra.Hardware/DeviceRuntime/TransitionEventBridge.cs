using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 状态迁移事件桥（协议与消息框架双无关）。
/// 以声明式映射表把"状态迁移"转换为"设备自定义事件"并发布：
/// 映射（From, To）→ 事件工厂，Attach 时注入发布委托（如 MediatR 的 Publish）。
/// 桥接机制共享、映射表由各设备驱动各自声明——PLC 桥接设备连接事件，
/// 未来相机/MQTT 等设备可桥接自己的事件，互不影响。
/// </summary>
public sealed class TransitionEventBridge
{
    private readonly Dictionary<(DeviceConnectionState From, DeviceConnectionState To), Func<string, string?, object>> _mappings = new();

    /// <summary>
    /// 登记一条映射：发生 From → To 迁移时，用事件工厂生成事件并发布。
    /// 事件工厂参数为（设备名, 迁移原因）。
    /// </summary>
    public TransitionEventBridge Map(
        DeviceConnectionState from,
        DeviceConnectionState to,
        Func<string, string?, object> eventFactory)
    {
        _mappings[(from, to)] = eventFactory ?? throw new ArgumentNullException(nameof(eventFactory));
        return this;
    }

    /// <summary>
    /// 附加到状态机：每次迁移按映射表发布事件；返回的 <see cref="IDisposable"/> 用于退订。
    /// 发布为异步委托，桥内火忘（失败被观察、不影响状态机与其他订阅者）。
    /// </summary>
    public IDisposable Attach(
        DeviceConnectionStateMachine stateMachine,
        Func<object, Task> publish,
        string deviceName)
    {
        ArgumentNullException.ThrowIfNull(stateMachine);
        ArgumentNullException.ThrowIfNull(publish);

        EventHandler<DeviceConnectionTransitionEventArgs> handler = (_, args) =>
        {
            if (!_mappings.TryGetValue((args.From, args.To), out var factory)) return;
            var notification = factory(deviceName, args.Reason);
            _ = publish(notification).ContinueWith(
                t => { _ = t.Exception; }, // 观察异常，避免 UnobservedTaskException
                TaskContinuationOptions.OnlyOnFaulted);
        };

        stateMachine.Transitioned += handler;
        return new Subscription(() => stateMachine.Transitioned -= handler);
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
