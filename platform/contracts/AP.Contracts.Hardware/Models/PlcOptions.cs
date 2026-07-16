namespace AP.Contracts.Hardware.Models;

/// <summary>
/// PLC 统一配置选项。
/// 配置节: "Plc"
/// </summary>
public class PlcOptions
{
    public const string SectionName = "Plc";

    /// <summary>
    /// 驱动类型，例如 Mitsubishi / Siemens / Omron。
    /// </summary>
    public string DriverType { get; set; } = "Mitsubishi";

    /// <summary>
    /// PLC IP 地址。
    /// </summary>
    public string IpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// PLC 端口。
    /// </summary>
    public int Port { get; set; } = 102;

    /// <summary>
    /// 超时时间（毫秒）。
    /// </summary>
    public int Timeout { get; set; } = 1000;

    /// <summary>
    /// PLC 型号/版本，具体取值由驱动决定。
    /// 例如西门子: S7_200 / S7_300 / S7_400 / S7_1200 / S7_1500 / S7_200Smart。
    /// 例如三菱: Qna_3E / A_1E。
    /// </summary>
    public string Model { get; set; } = "S7_1200";

    /// <summary>
    /// 心跳检测地址。
    /// </summary>
    public string HeartbeatAddress { get; set; } = "DB1.0.0";
}
