namespace AP.Plugin.Scanner.Configuration;

/// <summary>
/// 扫码枪配置
/// </summary>
public class SerialPortOptions
{
    public const string SectionName = "Plugins:Configuration:AP.Plugin.Scanner";

    public string PortName { get; set; } = "COM10";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string NewLine { get; set; } = "\r";
}