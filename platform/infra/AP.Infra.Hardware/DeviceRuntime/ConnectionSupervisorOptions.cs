namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 连接监督器参数。默认值与既有 PLC 看门狗的硬编码参数一致（行为不变承诺），
/// T1.7 将把默认值接入配置。
/// </summary>
public sealed class ConnectionSupervisorOptions
{
    /// <summary>心跳探测周期（默认 2 秒）。</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>重连失败后的退避间隔（默认 5 秒）。</summary>
    public TimeSpan ReconnectBackoff { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>监督循环异常退出后的重启延迟（默认 5 秒）。</summary>
    public TimeSpan SupervisorRestartDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>停止时等待循环退出的超时（默认 3 秒）。</summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
