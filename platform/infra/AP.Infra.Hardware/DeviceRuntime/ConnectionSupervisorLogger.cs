using AP.Contracts.Hardware.DeviceRuntime;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 连接监督器的日志消费者（可选附件）。
/// 订阅 ConnectionSupervisor 与状态机事件，按 docs/conventions/LOGGING.md 标准模板输出：
/// 状态迁移采用"状态迁移记录法"（变化瞬间记一条），连接尝试失败记 Debug，监督循环异常记 Error。
/// 三个 PLC 驱动（T1.3~T1.5）通过 Attach 获得统一日志，不再各自书写。
/// </summary>
public static class ConnectionSupervisorLogger
{
    /// <summary>
    /// 附加日志消费；返回的 <see cref="IDisposable"/> 用于退订（随监督器生命周期释放）。
    /// </summary>
    public static IDisposable Attach(
        ConnectionSupervisor supervisor,
        DeviceConnectionStateMachine stateMachine,
        ILogger logger,
        string deviceName)
    {
        EventHandler<DeviceConnectionTransitionEventArgs> onTransition = (_, args) =>
            LogTransition(logger, deviceName, args);

        EventHandler<ConnectionAttemptedEventArgs> onAttempted = (_, args) =>
        {
            if (!args.Result.Success)
                logger.LogDebug(
                    args.Result.Exception,
                    "{Device} 连接尝试失败，原因: {Reason}，耗时: {DurationMs}ms",
                    deviceName,
                    args.Result.ErrorReason ?? "未知",
                    (long)args.Duration.TotalMilliseconds);
        };

        EventHandler<SupervisorLoopFaultedEventArgs> onFaulted = (_, args) =>
            logger.LogError(
                args.Exception,
                "{Device} 连接监督循环异常退出，{RestartDelaySec} 秒后重启",
                deviceName,
                args.RestartDelay.TotalSeconds);

        stateMachine.Transitioned += onTransition;
        supervisor.ConnectAttempted += onAttempted;
        supervisor.LoopFaulted += onFaulted;

        return new Subscription(() =>
        {
            stateMachine.Transitioned -= onTransition;
            supervisor.ConnectAttempted -= onAttempted;
            supervisor.LoopFaulted -= onFaulted;
        });
    }

    private static void LogTransition(ILogger logger, string deviceName, DeviceConnectionTransitionEventArgs args)
    {
        switch (args.To)
        {
            case DeviceConnectionState.Connected when args.From == DeviceConnectionState.Reconnecting:
                logger.LogInformation("{Device} 重连成功，已恢复连接", deviceName);
                break;
            case DeviceConnectionState.Connected:
                logger.LogInformation("{Device} 已连接", deviceName);
                break;
            case DeviceConnectionState.Reconnecting when args.From == DeviceConnectionState.Connecting:
                logger.LogWarning("{Device} 连接失败，将自动重连，原因: {Reason}", deviceName, args.Reason ?? "未知");
                break;
            case DeviceConnectionState.Reconnecting:
                logger.LogWarning("{Device} 连接丢失，将自动重连，原因: {Reason}", deviceName, args.Reason ?? "未知");
                break;
            case DeviceConnectionState.Disconnected:
                logger.LogInformation("{Device} 已断开，原因: {Reason}", deviceName, args.Reason ?? "主动断开");
                break;
            case DeviceConnectionState.Faulted:
                logger.LogError("{Device} 进入故障态，原因: {Reason}", deviceName, args.Reason ?? "未知");
                break;
            case DeviceConnectionState.Disabled:
                logger.LogInformation("{Device} 已停用，原因: {Reason}", deviceName, args.Reason ?? "维护模式");
                break;
            case DeviceConnectionState.Connecting:
                logger.LogDebug("{Device} 正在建立连接", deviceName);
                break;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
