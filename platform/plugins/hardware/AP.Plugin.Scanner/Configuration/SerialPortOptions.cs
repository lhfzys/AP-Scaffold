namespace AP.Plugin.Scanner.Configuration;

/// <summary>
/// 扫码枪配置
/// </summary>
public class SerialPortOptions
{
    public const string SectionName = "Plugins:Configuration:AP.Plugin.Scanner";

    /// <summary>是否启用扫码枪（false 时不注册服务/设备、不发起连接，用于无扫码枪的项目）。</summary>
    public bool Enabled { get; set; } = true;

    public string PortName { get; set; } = "COM10";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string NewLine { get; set; } = "\r";
}