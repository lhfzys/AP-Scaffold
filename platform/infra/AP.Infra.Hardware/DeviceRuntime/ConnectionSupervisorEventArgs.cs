namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 一次连接尝试完成事件参数（含结果与耗时）。
/// </summary>
public sealed class ConnectionAttemptedEventArgs : EventArgs
{
    public ConnectionAttemptedEventArgs(ConnectionAttemptResult result, TimeSpan duration)
    {
        Result = result;
        Duration = duration;
    }

    /// <summary>尝试结果。</summary>
    public ConnectionAttemptResult Result { get; }

    /// <summary>尝试耗时（由监督器计时）。</summary>
    public TimeSpan Duration { get; }
}

/// <summary>
/// 监督循环异常退出事件参数（循环将按 <see cref="RestartDelay"/> 延迟后自动重启）。
/// </summary>
public sealed class SupervisorLoopFaultedEventArgs : EventArgs
{
    public SupervisorLoopFaultedEventArgs(Exception exception, TimeSpan restartDelay)
    {
        Exception = exception;
        RestartDelay = restartDelay;
    }

    /// <summary>导致循环退出的异常。</summary>
    public Exception Exception { get; }

    /// <summary>重启延迟。</summary>
    public TimeSpan RestartDelay { get; }
}
