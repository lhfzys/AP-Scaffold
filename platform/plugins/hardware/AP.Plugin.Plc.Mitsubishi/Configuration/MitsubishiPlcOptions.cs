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
}