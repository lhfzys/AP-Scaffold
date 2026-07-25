using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 设备连接状态迁移事件参数。
/// </summary>
public sealed class DeviceConnectionTransitionEventArgs : EventArgs
{
    public DeviceConnectionTransitionEventArgs(DeviceConnectionState from, DeviceConnectionState to, string? reason)
    {
        From = from;
        To = to;
        Reason = reason;
        Timestamp = DateTime.Now;
    }

    /// <summary>迁移前状态。</summary>
    public DeviceConnectionState From { get; }

    /// <summary>迁移后状态。</summary>
    public DeviceConnectionState To { get; }

    /// <summary>迁移原因（可选，如 "心跳丢失"、"主动断开"）。</summary>
    public string? Reason { get; }

    /// <summary>迁移发生时间。</summary>
    public DateTime Timestamp { get; }
}

/// <summary>
/// 设备连接状态机（Device Runtime Model 的第一个运行时组件，协议无关）。
/// 只做三件事：查询当前状态、校验并执行迁移、发布状态迁移通知。
/// 不引用任何协议/驱动类型（PLC、串口、相机、MQTT 等均可复用）；
/// 心跳、重连等设备语义由 ConnectionSupervisor 在其上实现。
/// 线程安全：状态读取与迁移均可多线程并发调用。
/// </summary>
public sealed class DeviceConnectionStateMachine
{
    /// <summary>
    /// 合法迁移表。预留状态（Faulted/Disabled）的迁移一并定义，
    /// 避免 DeviceRuntime 落地时返工。
    /// </summary>
    private static readonly IReadOnlyDictionary<DeviceConnectionState, DeviceConnectionState[]> AllowedTransitions =
        new Dictionary<DeviceConnectionState, DeviceConnectionState[]>
        {
            [DeviceConnectionState.Disconnected] = [DeviceConnectionState.Connecting, DeviceConnectionState.Disabled],
            [DeviceConnectionState.Connecting] = [DeviceConnectionState.Connected, DeviceConnectionState.Reconnecting, DeviceConnectionState.Faulted, DeviceConnectionState.Disabled],
            [DeviceConnectionState.Connected] = [DeviceConnectionState.Reconnecting, DeviceConnectionState.Disconnected, DeviceConnectionState.Faulted, DeviceConnectionState.Disabled],
            [DeviceConnectionState.Reconnecting] = [DeviceConnectionState.Connected, DeviceConnectionState.Disconnected, DeviceConnectionState.Faulted, DeviceConnectionState.Disabled],
            [DeviceConnectionState.Faulted] = [DeviceConnectionState.Connecting, DeviceConnectionState.Disabled],
            [DeviceConnectionState.Disabled] = [DeviceConnectionState.Disconnected],
        };

    private readonly object _gate = new();
    private DeviceConnectionState _currentState = DeviceConnectionState.Disconnected;

    /// <summary>当前状态（初始为 <see cref="DeviceConnectionState.Disconnected"/>）。</summary>
    public DeviceConnectionState CurrentState
    {
        get { lock (_gate) return _currentState; }
    }

    /// <summary>
    /// 状态实际发生迁移后触发（在锁外发布，订阅方回调中可安全再次迁移）。
    /// 未来 Alarm / HealthMonitor / Metrics 的挂点。
    /// </summary>
    public event EventHandler<DeviceConnectionTransitionEventArgs>? Transitioned;

    /// <summary>判断从 <paramref name="from"/> 到 <paramref name="to"/> 是否为合法迁移（同态迁移视为非法）。</summary>
    public static bool IsValidTransition(DeviceConnectionState from, DeviceConnectionState to)
    {
        return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    /// <summary>
    /// 尝试迁移到目标状态。非法迁移（含同态）返回 false，不触发事件。
    /// </summary>
    public bool TryTransition(DeviceConnectionState target, string? reason = null)
    {
        DeviceConnectionTransitionEventArgs? args = null;
        lock (_gate)
        {
            if (!IsValidTransition(_currentState, target)) return false;
            args = new DeviceConnectionTransitionEventArgs(_currentState, target, reason);
            _currentState = target;
        }

        Transitioned?.Invoke(this, args);
        return true;
    }

    /// <summary>
    /// 迁移到目标状态，非法迁移（含同态）抛 <see cref="InvalidOperationException"/>。
    /// 用于"迁移失败即编程错误"的运行时组件内部路径。
    /// </summary>
    public void Transition(DeviceConnectionState target, string? reason = null)
    {
        if (!TryTransition(target, reason))
            throw new InvalidOperationException($"非法的设备连接状态迁移: {CurrentState} → {target}");
    }
}
