namespace AP.Plugin.Plc.Mitsubishi.Configuration;

/// <summary>
/// 三菱PLC配置
/// </summary>
public class MitsubishiPlcOptions
{
    public const string SectionName = "Plugins:Configuration:AP.Plugin.Plc.Mitsubishi";
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6000;

    /// <summary>
    /// 超时时间 (毫秒)
    /// </summary>
    public int Timeout { get; set; } = 1000;

    /// <summary>
    /// PLC 型号/版本 (对应 IoTClient 的枚举，如 Qna_3E, A_1E)
    /// </summary>
    public string Version { get; set; } = "Qna_3E";

    public string HeartbeatAddress { get; set; } = "D0.0";

    /// <summary>
    /// 心跳探测周期（秒），默认 2。可选键，旧配置不写时行为与之前硬编码一致。
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// 重连失败后的退避间隔（秒），默认 5。可选键。
    /// </summary>
    public int ReconnectBackoffSeconds { get; set; } = 5;

    /// <summary>
    /// 连接监督循环异常退出后的重启延迟（秒），默认 5。可选键。
    /// </summary>
    public int SupervisorRestartDelaySeconds { get; set; } = 5;
}